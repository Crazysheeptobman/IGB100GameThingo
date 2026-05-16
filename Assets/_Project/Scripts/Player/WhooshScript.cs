using UnityEngine;
using UnityEngine.UI;

public class WhooshLinesEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParkourPlayerMovement playerMovement;
    [SerializeField] private Image whooshImage;

    [Header("Speed Thresholds")]
    [SerializeField, Min(0f)] private float minSpeed = 12f;
    [SerializeField, Min(0f)] private float maxSpeed = 30f;

    [Header("Opacity")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.75f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)] private float fadeSpeed = 6f;

    private float currentAlpha;

    private void Reset()
    {
        playerMovement = GetComponentInParent<ParkourPlayerMovement>();
        whooshImage = GetComponentInChildren<Image>();
    }

    private void Awake()
    {
        if (playerMovement == null)
            Debug.LogWarning("WhooshLinesEffect: playerMovement is NULL!", this);

        if (whooshImage == null)
        {
            Debug.LogError("WhooshLinesEffect: whooshImage is NULL, disabling script!", this);
            enabled = false;
            return;
        }
        whooshImage.gameObject.SetActive(true);
        SetImageAlpha(0f);
        currentAlpha = 0f;
    }

    private void Update()
    {
        if (playerMovement == null || whooshImage == null)
            return;

        float speed = playerMovement.CurrentVelocity.magnitude;
        float targetAlpha = CalculateTargetAlpha(speed);

        float blend = 1f - Mathf.Exp(-fadeSpeed * Time.deltaTime);
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, blend);

        SetImageAlpha(currentAlpha);
    }

    private float CalculateTargetAlpha(float speed)
    {
        if (maxSpeed <= minSpeed)
            return speed >= minSpeed ? maxAlpha : minAlpha;

        float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        return Mathf.Lerp(minAlpha, maxAlpha, t);
    }

    private void SetImageAlpha(float alpha)
    {
        Color c = whooshImage.color;
        c.a = Mathf.Clamp01(alpha);
        whooshImage.color = c;
    }
}