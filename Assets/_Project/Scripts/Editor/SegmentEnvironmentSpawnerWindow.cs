using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SegmentEnvironmentSpawnerWindow : EditorWindow
{
    private const string DefaultEnvironmentPrefabFolder = "Assets/_Project/Environment/Prefabs";
    private const string GeneratedRootName = "Generated Environment";

    [SerializeField] private GameObject targetSegmentPrefab;
    [SerializeField] private DefaultAsset environmentPrefabFolder;
    [SerializeField] private int spawnAmount = 12;
    [SerializeField] private Vector3 colliderPadding;
    [SerializeField] private Vector3 rotationMin = Vector3.zero;
    [SerializeField] private Vector3 rotationMax = new Vector3(0f, 360f, 0f);
    [SerializeField] private Vector2 scaleMultiplierRange = new Vector2(0.85f, 1.25f);
    [SerializeField] private bool useFixedSeed;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private bool showLoadedPrefabs;

    private readonly List<GameObject> environmentPrefabs = new List<GameObject>();
    private Vector2 scrollPosition;
    private string statusMessage = "Choose a segment prefab or select one in the Hierarchy, then generate.";

    [MenuItem("Tools/Level Tools/Segment Environment Spawner")]
    private static void Open()
    {
        GetWindow<SegmentEnvironmentSpawnerWindow>("Segment Spawner");
    }

    private void OnEnable()
    {
        EnsureDefaultFolder();
        RefreshEnvironmentPrefabList();
        AdoptSelectionIfUseful();
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Segment Environment Spawner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Spawns random environment prefabs inside the segment BoxCollider. Redo replaces only the Generated Environment child.", MessageType.Info);

        DrawTargetSection();
        DrawPrefabSourceSection();
        DrawRandomSettingsSection();
        DrawActionSection();
        DrawStatusSection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawTargetSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        targetSegmentPrefab = (GameObject)EditorGUILayout.ObjectField("Segment Prefab Asset", targetSegmentPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (targetSegmentPrefab != null && !IsPrefabAsset(targetSegmentPrefab))
            {
                statusMessage = "The target field only accepts prefab assets from the Project window.";
                targetSegmentPrefab = null;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Project Selection"))
            {
                UseProjectSelection();
            }

            if (GUILayout.Button("Open Target Prefab"))
            {
                OpenTargetPrefab();
            }
        }

        GameObject editableSelection = GetEditableSelectedSegmentRoot();
        string selectedName = editableSelection != null ? editableSelection.name : "None";
        EditorGUILayout.LabelField("Editable Hierarchy Selection", selectedName);
    }

    private void DrawPrefabSourceSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Environment Prefabs", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        environmentPrefabFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefab Folder", environmentPrefabFolder, typeof(DefaultAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            RefreshEnvironmentPrefabList();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Loaded Prefabs", environmentPrefabs.Count.ToString());

            if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
            {
                RefreshEnvironmentPrefabList();
            }
        }

        showLoadedPrefabs = EditorGUILayout.Foldout(showLoadedPrefabs, "Loaded Prefab Names");
        if (showLoadedPrefabs)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                if (environmentPrefabs.Count == 0)
                {
                    EditorGUILayout.LabelField("No prefabs found in this folder.");
                }

                foreach (GameObject prefab in environmentPrefabs)
                {
                    EditorGUILayout.ObjectField(prefab, typeof(GameObject), false);
                }
            }
        }
    }

    private void DrawRandomSettingsSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);

        spawnAmount = Mathf.Max(0, EditorGUILayout.IntField("Spawn Amount", spawnAmount));
        colliderPadding = Vector3.Max(Vector3.zero, EditorGUILayout.Vector3Field("Collider Padding", colliderPadding));
        rotationMin = EditorGUILayout.Vector3Field("Rotation Min", rotationMin);
        rotationMax = EditorGUILayout.Vector3Field("Rotation Max", rotationMax);
        scaleMultiplierRange = EditorGUILayout.Vector2Field("Scale Multiplier Range", scaleMultiplierRange);

        if (scaleMultiplierRange.x < 0.01f)
        {
            scaleMultiplierRange.x = 0.01f;
        }

        if (scaleMultiplierRange.y < 0.01f)
        {
            scaleMultiplierRange.y = 0.01f;
        }

        useFixedSeed = EditorGUILayout.Toggle("Use Fixed Seed", useFixedSeed);
        using (new EditorGUI.DisabledScope(!useFixedSeed))
        {
            fixedSeed = EditorGUILayout.IntField("Fixed Seed", fixedSeed);
        }
    }

    private void DrawActionSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!CanGenerate()))
        {
            if (GUILayout.Button("Generate / Redo Selected Segment", GUILayout.Height(32f)))
            {
                GenerateIntoSelectedSegment();
            }

            if (GUILayout.Button("Generate / Redo Target Prefab Asset", GUILayout.Height(32f)))
            {
                GenerateIntoTargetPrefabAsset();
            }
        }

        if (GUILayout.Button("Keep Current Layout", GUILayout.Height(26f)))
        {
            KeepCurrentLayout();
        }
    }

    private void DrawStatusSection()
    {
        EditorGUILayout.Space();
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }
    }

    private void EnsureDefaultFolder()
    {
        if (environmentPrefabFolder != null)
        {
            return;
        }

        environmentPrefabFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultEnvironmentPrefabFolder);
    }

    private void RefreshEnvironmentPrefabList()
    {
        environmentPrefabs.Clear();

        string folderPath = GetPrefabFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            statusMessage = "Pick a valid Project folder containing environment prefabs.";
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        Array.Sort(prefabGuids, StringComparer.Ordinal);

        foreach (string guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab != null)
            {
                environmentPrefabs.Add(prefab);
            }
        }

        statusMessage = environmentPrefabs.Count > 0
            ? $"Loaded {environmentPrefabs.Count} environment prefab(s) from {folderPath}."
            : $"No prefabs found in {folderPath}.";
    }

    private string GetPrefabFolderPath()
    {
        if (environmentPrefabFolder == null)
        {
            return string.Empty;
        }

        string folderPath = AssetDatabase.GetAssetPath(environmentPrefabFolder);
        return AssetDatabase.IsValidFolder(folderPath) ? folderPath : string.Empty;
    }

    private bool CanGenerate()
    {
        return environmentPrefabs.Count > 0;
    }

    private void UseProjectSelection()
    {
        GameObject selectedPrefab = Selection.activeObject as GameObject;
        if (selectedPrefab == null || !IsPrefabAsset(selectedPrefab))
        {
            statusMessage = "Select a segment prefab asset in the Project window first.";
            return;
        }

        targetSegmentPrefab = selectedPrefab;
        statusMessage = $"Target segment prefab set to {targetSegmentPrefab.name}.";
    }

    private void OpenTargetPrefab()
    {
        if (targetSegmentPrefab == null)
        {
            statusMessage = "Pick a target segment prefab asset first.";
            return;
        }

        AssetDatabase.OpenAsset(targetSegmentPrefab);
        statusMessage = $"Opened {targetSegmentPrefab.name}. You can now use Generate / Redo Selected Segment while viewing it.";
    }

    private void AdoptSelectionIfUseful()
    {
        if (targetSegmentPrefab != null)
        {
            return;
        }

        GameObject selectedPrefab = Selection.activeObject as GameObject;
        if (selectedPrefab != null && IsPrefabAsset(selectedPrefab))
        {
            targetSegmentPrefab = selectedPrefab;
        }
    }

    private void GenerateIntoSelectedSegment()
    {
        GameObject root = GetEditableSelectedSegmentRoot();
        if (root == null)
        {
            statusMessage = "Select a segment object in the Hierarchy or open a segment prefab first.";
            return;
        }

        BoxCollider boundsCollider = FindBoundsCollider(root);
        if (boundsCollider == null)
        {
            statusMessage = $"{root.name} needs a BoxCollider to define the spawn area.";
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Generate Segment Environment");
        int seed = GetSeed();
        int spawned = GenerateIntoRoot(root, boundsCollider, seed, useUndo: true);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.scene);

        statusMessage = $"Generated {spawned} object(s) in {root.name}. Click Keep if you like it, or Generate / Redo Selected Segment to reroll.";
    }

    private void GenerateIntoTargetPrefabAsset()
    {
        if (targetSegmentPrefab == null)
        {
            statusMessage = "Pick a target segment prefab asset first.";
            return;
        }

        if (!IsPrefabAsset(targetSegmentPrefab))
        {
            statusMessage = "Target Segment Prefab Asset must be a prefab from the Project window.";
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(targetSegmentPrefab);
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            BoxCollider boundsCollider = FindBoundsCollider(root);
            if (boundsCollider == null)
            {
                statusMessage = $"{targetSegmentPrefab.name} needs a BoxCollider to define the spawn area.";
                return;
            }

            int seed = GetSeed();
            int spawned = GenerateIntoRoot(root, boundsCollider, seed, useUndo: false);
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            statusMessage = $"Generated and saved {spawned} object(s) into {targetSegmentPrefab.name}. Redo will replace the Generated Environment child.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private int GenerateIntoRoot(GameObject root, BoxCollider boundsCollider, int seed, bool useUndo)
    {
        ClearGeneratedRoot(root, useUndo);

        GameObject generatedRoot = new GameObject(GeneratedRootName);
        if (useUndo)
        {
            Undo.RegisterCreatedObjectUndo(generatedRoot, "Create Generated Environment");
        }

        generatedRoot.transform.SetParent(root.transform, false);
        generatedRoot.transform.localPosition = Vector3.zero;
        generatedRoot.transform.localRotation = Quaternion.identity;
        generatedRoot.transform.localScale = Vector3.one;

        System.Random random = new System.Random(seed);
        int spawned = 0;

        for (int i = 0; i < spawnAmount; i++)
        {
            GameObject sourcePrefab = environmentPrefabs[random.Next(environmentPrefabs.Count)];
            GameObject instance = PrefabUtility.InstantiatePrefab(sourcePrefab, generatedRoot.transform) as GameObject;
            if (instance == null)
            {
                continue;
            }

            if (useUndo)
            {
                Undo.RegisterCreatedObjectUndo(instance, "Spawn Environment Prefab");
            }

            instance.name = sourcePrefab.name;
            Vector3 defaultScale = instance.transform.localScale;

            instance.transform.localPosition = GetRandomLocalPosition(root.transform, boundsCollider, random);
            instance.transform.localRotation = Quaternion.Euler(GetRandomVector(rotationMin, rotationMax, random));
            instance.transform.localScale = defaultScale * GetRandomScaleMultiplier(random);

            spawned++;
        }

        EditorUtility.SetDirty(generatedRoot);
        return spawned;
    }

    private void ClearGeneratedRoot(GameObject root, bool useUndo)
    {
        Transform existing = root.transform.Find(GeneratedRootName);
        if (existing == null)
        {
            return;
        }

        if (useUndo)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
        }
    }

    private Vector3 GetRandomLocalPosition(Transform rootTransform, BoxCollider boundsCollider, System.Random random)
    {
        Vector3 halfSize = boundsCollider.size * 0.5f;
        Vector3 padding = new Vector3(
            Mathf.Min(colliderPadding.x, halfSize.x),
            Mathf.Min(colliderPadding.y, halfSize.y),
            Mathf.Min(colliderPadding.z, halfSize.z));

        Vector3 colliderLocalPosition = boundsCollider.center + new Vector3(
            RandomRange(random, -halfSize.x + padding.x, halfSize.x - padding.x),
            RandomRange(random, -halfSize.y + padding.y, halfSize.y - padding.y),
            RandomRange(random, -halfSize.z + padding.z, halfSize.z - padding.z));

        Vector3 worldPosition = boundsCollider.transform.TransformPoint(colliderLocalPosition);
        return rootTransform.InverseTransformPoint(worldPosition);
    }

    private Vector3 GetRandomVector(Vector3 min, Vector3 max, System.Random random)
    {
        return new Vector3(
            RandomRange(random, Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x)),
            RandomRange(random, Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y)),
            RandomRange(random, Mathf.Min(min.z, max.z), Mathf.Max(min.z, max.z)));
    }

    private float GetRandomScaleMultiplier(System.Random random)
    {
        float min = Mathf.Min(scaleMultiplierRange.x, scaleMultiplierRange.y);
        float max = Mathf.Max(scaleMultiplierRange.x, scaleMultiplierRange.y);

        min = Mathf.Max(0.01f, min);
        max = Mathf.Max(0.01f, max);

        return RandomRange(random, min, max);
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        return min + (float)random.NextDouble() * (max - min);
    }

    private int GetSeed()
    {
        return useFixedSeed ? fixedSeed : Guid.NewGuid().GetHashCode();
    }

    private void KeepCurrentLayout()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.prefabContentsRoot != null)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
            AssetDatabase.SaveAssets();
            statusMessage = $"Kept and saved the current layout in {prefabStage.prefabContentsRoot.name}.";
            return;
        }

        GameObject editableSelection = GetEditableSelectedSegmentRoot();
        if (editableSelection != null)
        {
            EditorUtility.SetDirty(editableSelection);
            EditorSceneManager.MarkSceneDirty(editableSelection.scene);
            statusMessage = $"Kept the current layout on {editableSelection.name}. Save the scene, or apply the prefab instance if you want it written back to the prefab asset.";
            return;
        }

        if (targetSegmentPrefab != null)
        {
            AssetDatabase.SaveAssets();
            statusMessage = $"{targetSegmentPrefab.name} is already saved when using Generate / Redo Target Prefab Asset.";
            return;
        }

        statusMessage = "Nothing to keep yet. Generate a layout first.";
    }

    private GameObject GetEditableSelectedSegmentRoot()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.prefabContentsRoot != null)
        {
            return prefabStage.prefabContentsRoot;
        }

        GameObject selected = Selection.activeGameObject;
        if (selected == null || EditorUtility.IsPersistent(selected))
        {
            return null;
        }

        BoxCollider ownCollider = selected.GetComponent<BoxCollider>();
        if (ownCollider != null)
        {
            return selected;
        }

        BoxCollider parentCollider = selected.GetComponentInParent<BoxCollider>();
        return parentCollider != null ? parentCollider.gameObject : null;
    }

    private static BoxCollider FindBoundsCollider(GameObject root)
    {
        BoxCollider rootCollider = root.GetComponent<BoxCollider>();
        if (rootCollider != null)
        {
            return rootCollider;
        }

        BoxCollider[] childColliders = root.GetComponentsInChildren<BoxCollider>(true);
        foreach (BoxCollider collider in childColliders)
        {
            if (collider.isTrigger)
            {
                return collider;
            }
        }

        return childColliders.Length > 0 ? childColliders[0] : null;
    }

    private static bool IsPrefabAsset(GameObject gameObject)
    {
        return gameObject != null
            && EditorUtility.IsPersistent(gameObject)
            && PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab;
    }
}
