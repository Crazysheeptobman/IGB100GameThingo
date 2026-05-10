using UnityEngine;

[DisallowMultipleComponent]
public class RandomSpaceSpin : MonoBehaviour
{
    [Header("Spin")]
    [SerializeField, Min(0f)] private float minDegreesPerSecond = 3f;
    [SerializeField, Min(0f)] private float maxDegreesPerSecond = 10f;
    [SerializeField] private Space rotationSpace = Space.Self;
    [SerializeField] private bool randomizeOnEnable = true;
    [SerializeField] private bool randomizeStartingRotation;

    private Vector3 spinAxis = Vector3.up;
    private float degreesPerSecond;
    private bool hasSpin;

    private void Awake()
    {
        if (!randomizeOnEnable)
        {
            RandomizeSpin();
        }
    }

    private void OnEnable()
    {
        if (randomizeOnEnable || !hasSpin)
        {
            RandomizeSpin();
        }
    }

    private void Update()
    {
        if (Mathf.Approximately(degreesPerSecond, 0f))
        {
            return;
        }

        transform.Rotate(spinAxis, degreesPerSecond * Time.deltaTime, rotationSpace);
    }

    [ContextMenu("Randomize Spin")]
    public void RandomizeSpin()
    {
        float minSpeed = Mathf.Min(minDegreesPerSecond, maxDegreesPerSecond);
        float maxSpeed = Mathf.Max(minDegreesPerSecond, maxDegreesPerSecond);

        spinAxis = Random.onUnitSphere;
        degreesPerSecond = Random.Range(minSpeed, maxSpeed);

        if (Random.value < 0.5f)
        {
            degreesPerSecond = -degreesPerSecond;
        }

        if (randomizeStartingRotation)
        {
            transform.rotation = Random.rotationUniform;
        }

        hasSpin = true;
    }

    private void OnValidate()
    {
        minDegreesPerSecond = Mathf.Max(0f, minDegreesPerSecond);
        maxDegreesPerSecond = Mathf.Max(0f, maxDegreesPerSecond);
    }
}
