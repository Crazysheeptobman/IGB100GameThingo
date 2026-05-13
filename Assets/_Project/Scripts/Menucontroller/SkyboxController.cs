using UnityEngine;

public class SkyboxBlendController : MonoBehaviour
{
    public enum TransitionTrigger { Time, Distance }

    [Header("Skyboxes")]
    [Tooltip("Add all your skybox cubemaps here in order")]
    [SerializeField] private Material[] skyboxes;
    [SerializeField] private Material blendMaterial;

    [Header("Trigger")]
    [SerializeField] private TransitionTrigger trigger = TransitionTrigger.Time;
    [SerializeField, Min(1f)] private float timeInterval = 60f;
    [SerializeField, Min(1f)] private float distanceInterval = 200f;
    [SerializeField] private Transform playerTransform;

    [Header("Transition")]
    [SerializeField, Min(0.01f)] private float blendDuration = 3f;
    [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private int currentIndex = 0;
    private int nextIndex = 1;

    private float triggerCounter = 0f;
    private Vector3 lastPlayerPosition;

    private float blendTimer = 0f;
    private bool isBlending = false;

    private static readonly int PropA       = Shader.PropertyToID("_SkyboxA");
    private static readonly int PropB       = Shader.PropertyToID("_SkyboxB");
    private static readonly int PropBlend   = Shader.PropertyToID("_Blend");

    private void Start()
    {
        if (blendMaterial == null)
        {
            Debug.LogError("SkyboxBlendController: no blend material assigned!", this);
            enabled = false;
            return;
        }

        if (skyboxes == null || skyboxes.Length < 2)
        {
            Debug.LogError("SkyboxBlendController: need at least 2 skyboxes!", this);
            enabled = false;
            return;
        }

        if (playerTransform == null)
            playerTransform = FindObjectOfType<ParkourPlayerMovement>()?.transform;

        CopySkyboxToSlot(skyboxes[currentIndex], "A");
        CopySkyboxToSlot(skyboxes[nextIndex], "B");
        blendMaterial.SetFloat(PropBlend, 0f);

        RenderSettings.skybox = blendMaterial;
        DynamicGI.UpdateEnvironment();

        if (playerTransform != null)
            lastPlayerPosition = playerTransform.position;
    }

    private void Update()
    {
        if (isBlending)
        {
            UpdateBlend();
            return;
        }

        AdvanceTriggerCounter();
    }

    private void AdvanceTriggerCounter()
    {
        if (trigger == TransitionTrigger.Time)
        {
            triggerCounter += Time.deltaTime;
            if (triggerCounter >= timeInterval)
            {
                triggerCounter = 0f;
                StartTransition();
            }
        }
        else
        {
            if (playerTransform == null) return;

            Vector3 currentPos = playerTransform.position;
            triggerCounter += Vector3.Distance(currentPos, lastPlayerPosition);
            lastPlayerPosition = currentPos;

            if (triggerCounter >= distanceInterval)
            {
                triggerCounter = 0f;
                StartTransition();
            }
        }
    }

    private void StartTransition()
    {
        CopySkyboxToSlot(skyboxes[currentIndex], "A");
        CopySkyboxToSlot(skyboxes[nextIndex], "B");
        blendMaterial.SetFloat(PropBlend, 0f);

        blendTimer = 0f;
        isBlending = true;
    }

    private void UpdateBlend()
    {
        blendTimer += Time.deltaTime;
        float t = Mathf.Clamp01(blendTimer / blendDuration);
        float curved = blendCurve.Evaluate(t);
        blendMaterial.SetFloat(PropBlend, curved);
        DynamicGI.UpdateEnvironment();

        if (t >= 1f)
        {
            FinishTransition();
        }
    }

    private void FinishTransition()
    {
        isBlending = false;
        blendTimer = 0f;

        currentIndex = nextIndex;
        nextIndex = (nextIndex + 1) % skyboxes.Length;

        CopySkyboxToSlot(skyboxes[currentIndex], "A");
        CopySkyboxToSlot(skyboxes[nextIndex], "B");
        blendMaterial.SetFloat(PropBlend, 0f);
    }

    private void CopySkyboxToSlot(Material skybox, string slot)
    {
        string[] faces = { "Front", "Back", "Left", "Right", "Up", "Down" };
        string[] props = { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex" };

        for (int i = 0; i < faces.Length; i++)
        {
            Texture tex = skybox.GetTexture(props[i]);
            if (tex != null)
                blendMaterial.SetTexture("_" + faces[i] + slot, tex);
        }
    }




}