using UnityEngine;
using UnityEngine.UI;

public class WhooshLinesEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParkourPlayerMovement playerMovement;
    [SerializeField] private Image whooshImage;

    [Header("Speed Thresholds")]
    [Tooltip("Speed at which whoosh lines begin to appear.")]
    [SerializeField, Min(0f)] private float minSpeed = 12f;

    [Tooltip("Speed at which whoosh lines reach maximum opacity.")]
    [SerializeField, Min(0f)] private float maxSpeed = 30f;

    [Header("Opacity")]
    [Tooltip("Alpha when effect is at its weakest (just above minSpeed).")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0f;

    [Tooltip("Alpha when effect is at full intensity (at or above maxSpeed).")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.75f;

    [Header("Smoothing")]
    [Tooltip("How quickly the effect fades in and out. Higher = snappier.")]
    [SerializeField, Min(0.01f)] private float fadeSpeed = 6f;

    private float currentAlpha;

    private void Reset()
    {
        playerMovement = GetComponentInParent<ParkourPlayerMovement>();
        whooshImage = GetComponentInChildren<Image>();
    }

    private void Awake()
    {
        if (whooshImage == null)
        {
            Debug.LogWarning("WhooshLinesEffect: no Image assigned.", this);
            enabled = false;
            return;
        }
        SetImageAlpha(0f);
    }

    private void Update()
    {
        if (playerMovement == null || whooshImage == null)
        {
            return;
        }

        float speed = playerMovement.CurrentVelocity.magnitude;
        float targetAlpha = CalculateTargetAlpha(speed);

        float blend = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, blend);

        SetImageAlpha(currentAlpha);
    }

    private float CalculateTargetAlpha(float speed)
    {
        if (maxSpeed <= minSpeed)
        {
            return speed >= minSpeed ? maxAlpha : minAlpha;
        }

        float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        return alpha;
    }

    private void SetImageAlpha(float alpha)
    {
        Color c = whooshImage.color;
        c.a = Mathf.Clamp01(alpha);
        whooshImage.color = c;
    }
}