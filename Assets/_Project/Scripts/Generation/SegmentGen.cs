using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class SegmentGen : MonoBehaviour
{
    [Serializable]
    private struct SegmentOption
    {
        public GameObject prefab;
        [Min(0f)] public float weight;
    }

    [Header("Tiles")]
    [SerializeField] private SegmentOption[] segments = Array.Empty<SegmentOption>();
    [SerializeField, HideInInspector, FormerlySerializedAs("segment"), FormerlySerializedAs("segmentPrefabs")] private GameObject[] legacySegmentPrefabs = Array.Empty<GameObject>();
    [SerializeField, HideInInspector, FormerlySerializedAs("segmentSelectionWeights")] private float[] legacySegmentSelectionWeights = Array.Empty<float>();

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform segmentParent;

    [Header("Path")]
    [SerializeField] private bool useGeneratorForward = true;
    [SerializeField, Min(0.01f)] private float tileSize = 50f;
    [SerializeField, Min(0f)] private float firstSegmentDistance = 50f;
    [SerializeField] private float spawnHeight;

    [Header("Streaming Distances")]
    [SerializeField, Min(0f)] private float spawnAheadDistance = 220f;
    [SerializeField, Min(0f)] private float despawnBehindDistance = 120f;
    [SerializeField, Min(1)] private int initialSegments = 5;
    [SerializeField] private bool startNearPlayer = true;
    [SerializeField, Min(0f)] private float startBehindPlayerDistance = 60f;

    [Header("Selection Rules")]
    [SerializeField] private bool avoidImmediateRepeats = true;
    [SerializeField, Min(1)] private int maxSequentialRepeats = 1;
    [SerializeField] private bool useDeterministicSeed;
    [SerializeField] private int deterministicSeed = 12345;

    [Header("Performance")]
    [SerializeField] private bool usePooling = true;
    [SerializeField, Min(0)] private int poolPrewarmPerPrefab = 1;
    [SerializeField, Min(1)] private int maxSpawnsPerFrame = 8;
    [SerializeField, Min(1)] private int maxDespawnsPerFrame = 12;
    [SerializeField] private bool runInFixedUpdate;

    [Header("Debug")]
    [SerializeField] private bool drawDebug;

    private readonly List<ActiveSegment> activeSegments = new();
    private readonly Dictionary<int, Queue<GameObject>> pooledSegmentsByPrefab = new();

    private Vector3 pathDirection = Vector3.forward;
    private Vector3 pathOrigin = Vector3.zero;
    private float nextSpawnDistance;
    private int lastPrefabIndex = -1;
    private int lastPrefabRepeatCount;
    private bool isInitialized;
    private System.Random seededRandom;

    private struct ActiveSegment
    {
        public int SegmentOptionIndex;
        public float EndDistance;
        public GameObject Instance;
    }

    private void Awake()
    {
        InitializeGenerator(resetSegments: true);
    }

    private void OnEnable()
    {
        if (!isInitialized)
        {
            InitializeGenerator(resetSegments: true);
        }
    }

    private void Update()
    {
        if (runInFixedUpdate)
        {
            return;
        }

        StreamSegments();
    }

    private void FixedUpdate()
    {
        if (!runInFixedUpdate)
        {
            return;
        }

        StreamSegments();
    }

    [ContextMenu("Regenerate Segments")]
    public void RegenerateSegments()
    {
        InitializeGenerator(resetSegments: true);
    }

    private void InitializeGenerator(bool resetSegments)
    {
        ResolveReferences();
        ValidateSettings();
        MigrateLegacySegmentsIfNeeded();
        ApplyDefaultWeights();

        Vector3 desiredDirection = useGeneratorForward ? transform.forward : Vector3.forward;
        desiredDirection.y = 0f;
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            desiredDirection = Vector3.forward;
        }

        pathDirection = desiredDirection.normalized;
        pathOrigin = transform.position;
        nextSpawnDistance = firstSegmentDistance;
        lastPrefabIndex = -1;
        lastPrefabRepeatCount = 0;
        seededRandom = useDeterministicSeed ? new System.Random(deterministicSeed) : null;
        isInitialized = true;

        if (startNearPlayer && player != null)
        {
            float playerDistance = DistanceAlongPath(player.position);
            float targetStartDistance = Mathf.Max(firstSegmentDistance, playerDistance - startBehindPlayerDistance);
            if (targetStartDistance > firstSegmentDistance)
            {
                int segmentsToSkip = Mathf.FloorToInt((targetStartDistance - firstSegmentDistance) / tileSize);
                nextSpawnDistance = firstSegmentDistance + segmentsToSkip * tileSize;
            }
        }

        if (resetSegments)
        {
            ClearActiveSegments();
            PrewarmPools();
        }

        EnsureInitialSegments();
    }

    private void ResolveReferences()
    {
        if (segmentParent == null)
        {
            segmentParent = transform;
        }

        if (player != null)
        {
            return;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            player = taggedPlayer.transform;
            return;
        }

        ParkourPlayerMovement fallbackPlayer = FindFirstObjectByType<ParkourPlayerMovement>();
        if (fallbackPlayer != null)
        {
            player = fallbackPlayer.transform;
        }
    }

    private void ValidateSettings()
    {
        tileSize = Mathf.Max(0.01f, tileSize);
        firstSegmentDistance = Mathf.Max(0f, firstSegmentDistance);
        initialSegments = Mathf.Max(1, initialSegments);
        startBehindPlayerDistance = Mathf.Max(0f, startBehindPlayerDistance);
        maxSequentialRepeats = Mathf.Max(1, maxSequentialRepeats);
        maxSpawnsPerFrame = Mathf.Max(1, maxSpawnsPerFrame);
        maxDespawnsPerFrame = Mathf.Max(1, maxDespawnsPerFrame);
        poolPrewarmPerPrefab = Mathf.Max(0, poolPrewarmPerPrefab);
    }

    private void MigrateLegacySegmentsIfNeeded()
    {
        if (segments != null && segments.Length > 0)
        {
            return;
        }

        if (legacySegmentPrefabs == null || legacySegmentPrefabs.Length == 0)
        {
            return;
        }

        SegmentOption[] migrated = new SegmentOption[legacySegmentPrefabs.Length];
        for (int i = 0; i < legacySegmentPrefabs.Length; i++)
        {
            float legacyWeight = 1f;
            if (legacySegmentSelectionWeights != null && i < legacySegmentSelectionWeights.Length)
            {
                legacyWeight = legacySegmentSelectionWeights[i];
            }

            migrated[i] = new SegmentOption
            {
                prefab = legacySegmentPrefabs[i],
                weight = legacyWeight > 0f ? legacyWeight : 1f
            };
        }

        segments = migrated;
    }

    private void ApplyDefaultWeights()
    {
        if (segments == null)
        {
            return;
        }

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].weight > 0f)
            {
                continue;
            }

            SegmentOption option = segments[i];
            option.weight = 1f;
            segments[i] = option;
        }
    }

    private void EnsureInitialSegments()
    {
        for (int i = activeSegments.Count; i < initialSegments; i++)
        {
            SpawnNextSegment();
        }
    }

    private void StreamSegments()
    {
        if (!isInitialized)
        {
            InitializeGenerator(resetSegments: false);
        }

        if (player == null)
        {
            ResolveReferences();
            if (player == null)
            {
                return;
            }
        }

        float playerDistance = DistanceAlongPath(player.position);
        float spawnLimitDistance = playerDistance + spawnAheadDistance;

        int spawnedThisFrame = 0;
        while (nextSpawnDistance <= spawnLimitDistance && spawnedThisFrame < maxSpawnsPerFrame)
        {
            SpawnNextSegment();
            spawnedThisFrame++;
        }

        float despawnThresholdDistance = playerDistance - despawnBehindDistance;
        int despawnedThisFrame = 0;
        while (activeSegments.Count > 0 && despawnedThisFrame < maxDespawnsPerFrame)
        {
            ActiveSegment oldest = activeSegments[0];
            if (oldest.EndDistance > despawnThresholdDistance)
            {
                break;
            }

            DespawnOldestSegment();
            despawnedThisFrame++;
        }
    }

    private void SpawnNextSegment()
    {
        int segmentOptionIndex = ChooseNextSegmentIndex();
        if (segmentOptionIndex < 0)
        {
            return;
        }

        GameObject instance = GetSegmentInstance(segmentOptionIndex);
        if (instance == null)
        {
            return;
        }

        float segmentStartDistance = nextSpawnDistance;
        Vector3 spawnPosition = pathOrigin + pathDirection * segmentStartDistance;
        spawnPosition.y = spawnHeight;
        Quaternion spawnRotation = Quaternion.LookRotation(pathDirection, Vector3.up);

        instance.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        instance.transform.SetParent(segmentParent, true);
        instance.SetActive(true);

        ActiveSegment activeSegment = new ActiveSegment
        {
            SegmentOptionIndex = segmentOptionIndex,
            EndDistance = segmentStartDistance + tileSize,
            Instance = instance
        };

        activeSegments.Add(activeSegment);
        nextSpawnDistance += tileSize;
    }

    private int ChooseNextSegmentIndex()
    {
        int segmentCount = segments != null ? segments.Length : 0;
        if (segmentCount == 0)
        {
            Debug.LogWarning("SegmentGen has no tile segments configured.", this);
            return -1;
        }

        int validSegmentCount = 0;
        float totalWeight = 0f;

        for (int i = 0; i < segmentCount; i++)
        {
            if (segments[i].prefab == null)
            {
                continue;
            }

            validSegmentCount++;
            totalWeight += GetSegmentWeight(i);
        }

        if (validSegmentCount == 0 || totalWeight <= 0f)
        {
            Debug.LogWarning("SegmentGen has no valid segment prefabs assigned.", this);
            return -1;
        }

        int selectedIndex = ChooseWeightedSegmentIndex(totalWeight);
        if (selectedIndex < 0)
        {
            return -1;
        }

        if (avoidImmediateRepeats && validSegmentCount > 1 && lastPrefabIndex >= 0 && lastPrefabRepeatCount >= maxSequentialRepeats)
        {
            int safetyCounter = 0;
            while (selectedIndex == lastPrefabIndex && safetyCounter < 20)
            {
                selectedIndex = ChooseWeightedSegmentIndex(totalWeight);
                safetyCounter++;
            }

            if (selectedIndex == lastPrefabIndex)
            {
                selectedIndex = ChooseFallbackSegmentIndex(excludeSegmentIndex: lastPrefabIndex);
            }
        }

        if (selectedIndex == lastPrefabIndex)
        {
            lastPrefabRepeatCount++;
        }
        else
        {
            lastPrefabIndex = selectedIndex;
            lastPrefabRepeatCount = 1;
        }

        return selectedIndex;
    }

    private float GetSegmentWeight(int segmentIndex)
    {
        float weight = segments[segmentIndex].weight;
        return weight > 0f ? weight : 1f;
    }

    private int ChooseWeightedSegmentIndex(float totalWeight)
    {
        if (segments == null || segments.Length == 0 || totalWeight <= 0f)
        {
            return -1;
        }

        float randomValue = NextRandomFloat01() * totalWeight;
        float cumulativeWeight = 0f;
        int fallbackIndex = -1;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].prefab == null)
            {
                continue;
            }

            fallbackIndex = i;
            cumulativeWeight += GetSegmentWeight(i);
            if (randomValue <= cumulativeWeight)
            {
                return i;
            }
        }

        return fallbackIndex;
    }

    private int ChooseFallbackSegmentIndex(int excludeSegmentIndex)
    {
        if (segments == null)
        {
            return -1;
        }

        for (int i = 0; i < segments.Length; i++)
        {
            if (i == excludeSegmentIndex || segments[i].prefab == null)
            {
                continue;
            }

            return i;
        }

        return excludeSegmentIndex;
    }

    private GameObject GetSegmentInstance(int segmentOptionIndex)
    {
        if (segments == null || segmentOptionIndex < 0 || segmentOptionIndex >= segments.Length)
        {
            return null;
        }

        GameObject prefab = segments[segmentOptionIndex].prefab;
        if (prefab == null)
        {
            Debug.LogWarning($"SegmentGen prefab at index {segmentOptionIndex} is null.", this);
            return null;
        }

        if (!usePooling)
        {
            return Instantiate(prefab);
        }

        if (!pooledSegmentsByPrefab.TryGetValue(segmentOptionIndex, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pooledSegmentsByPrefab[segmentOptionIndex] = pool;
        }

        while (pool.Count > 0)
        {
            GameObject pooledSegment = pool.Dequeue();
            if (pooledSegment != null)
            {
                return pooledSegment;
            }
        }

        return Instantiate(prefab);
    }

    private void DespawnOldestSegment()
    {
        ActiveSegment oldest = activeSegments[0];
        activeSegments.RemoveAt(0);

        if (oldest.Instance == null)
        {
            return;
        }

        if (!usePooling)
        {
            Destroy(oldest.Instance);
            return;
        }

        oldest.Instance.SetActive(false);
        oldest.Instance.transform.SetParent(segmentParent, true);

        if (!pooledSegmentsByPrefab.TryGetValue(oldest.SegmentOptionIndex, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            pooledSegmentsByPrefab[oldest.SegmentOptionIndex] = pool;
        }

        pool.Enqueue(oldest.Instance);
    }

    private void ClearActiveSegments()
    {
        for (int i = 0; i < activeSegments.Count; i++)
        {
            ActiveSegment activeSegment = activeSegments[i];
            if (activeSegment.Instance == null)
            {
                continue;
            }

            if (usePooling)
            {
                activeSegment.Instance.SetActive(false);

                if (!pooledSegmentsByPrefab.TryGetValue(activeSegment.SegmentOptionIndex, out Queue<GameObject> pool))
                {
                    pool = new Queue<GameObject>();
                    pooledSegmentsByPrefab[activeSegment.SegmentOptionIndex] = pool;
                }

                pool.Enqueue(activeSegment.Instance);
            }
            else
            {
                Destroy(activeSegment.Instance);
            }
        }

        activeSegments.Clear();
    }

    private void PrewarmPools()
    {
        if (!usePooling || poolPrewarmPerPrefab <= 0 || segments == null)
        {
            return;
        }

        for (int i = 0; i < segments.Length; i++)
        {
            GameObject prefab = segments[i].prefab;
            if (prefab == null)
            {
                continue;
            }

            if (!pooledSegmentsByPrefab.TryGetValue(i, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                pooledSegmentsByPrefab[i] = pool;
            }

            int instancesNeeded = poolPrewarmPerPrefab - pool.Count;
            for (int j = 0; j < instancesNeeded; j++)
            {
                GameObject pooledSegment = Instantiate(prefab);
                pooledSegment.SetActive(false);
                pooledSegment.transform.SetParent(segmentParent, true);
                pool.Enqueue(pooledSegment);
            }
        }
    }

    private float DistanceAlongPath(Vector3 worldPosition)
    {
        return Vector3.Dot(worldPosition - pathOrigin, pathDirection);
    }

    private float NextRandomFloat01()
    {
        return seededRandom != null
            ? (float)seededRandom.NextDouble()
            : UnityEngine.Random.value;
    }

    private void OnValidate()
    {
        tileSize = Mathf.Max(0.01f, tileSize);
        firstSegmentDistance = Mathf.Max(0f, firstSegmentDistance);
        spawnAheadDistance = Mathf.Max(0f, spawnAheadDistance);
        despawnBehindDistance = Mathf.Max(0f, despawnBehindDistance);
        initialSegments = Mathf.Max(1, initialSegments);
        startBehindPlayerDistance = Mathf.Max(0f, startBehindPlayerDistance);
        maxSequentialRepeats = Mathf.Max(1, maxSequentialRepeats);
        maxSpawnsPerFrame = Mathf.Max(1, maxSpawnsPerFrame);
        maxDespawnsPerFrame = Mathf.Max(1, maxDespawnsPerFrame);
        poolPrewarmPerPrefab = Mathf.Max(0, poolPrewarmPerPrefab);
        MigrateLegacySegmentsIfNeeded();
        ApplyDefaultWeights();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug)
        {
            return;
        }

        Vector3 debugDirection = useGeneratorForward ? transform.forward : Vector3.forward;
        debugDirection.y = 0f;
        if (debugDirection.sqrMagnitude < 0.0001f)
        {
            debugDirection = Vector3.forward;
        }

        debugDirection.Normalize();
        Vector3 debugOrigin = transform.position;
        debugOrigin.y = spawnHeight;
        Vector3 firstSpawnPosition = debugOrigin + debugDirection * firstSegmentDistance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(debugOrigin, debugOrigin + debugDirection * (firstSegmentDistance + spawnAheadDistance));
        Gizmos.DrawWireSphere(firstSpawnPosition, 0.45f);

        if (player == null)
        {
            return;
        }

        float playerDistance = Vector3.Dot(player.position - debugOrigin, debugDirection);
        Vector3 despawnMarker = debugOrigin + debugDirection * (playerDistance - despawnBehindDistance);
        Vector3 spawnMarker = debugOrigin + debugDirection * (playerDistance + spawnAheadDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(despawnMarker, 0.35f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnMarker, 0.35f);
    }
}
