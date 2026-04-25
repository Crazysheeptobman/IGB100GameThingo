using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GrappleCrosshairIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GrappleGunController grappleController;

    [Header("Single Image Mode")]
    [SerializeField] private Image crosshairImage;
    [SerializeField] private Sprite reachableSprite;
    [SerializeField] private Sprite unreachableSprite;

    [Header("Variant Object Mode")]
    [SerializeField] private Graphic reachableCrosshair;
    [SerializeField] private Graphic unreachableCrosshair;
    [SerializeField] private bool toggleVariantGameObjects;

    private bool wasReachable;
    private bool hasAppliedState;

    private void Awake()
    {
        if (crosshairImage == null)
        {
            crosshairImage = GetComponent<Image>();
        }

        ResolveGrappleController();
        ApplyState(false);
    }

    private void Update()
    {
        ResolveGrappleController();

        bool isReachable = grappleController != null && grappleController.HasReachableGrappleTarget;
        ApplyState(isReachable);
    }

    private void ResolveGrappleController()
    {
        if (grappleController != null && grappleController.isActiveAndEnabled)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        grappleController = FindFirstObjectByType<GrappleGunController>();
#else
        grappleController = FindObjectOfType<GrappleGunController>();
#endif
    }

    private void ApplyState(bool isReachable)
    {
        if (hasAppliedState && wasReachable == isReachable)
        {
            return;
        }

        hasAppliedState = true;
        wasReachable = isReachable;

        if (crosshairImage != null)
        {
            Sprite nextSprite = isReachable ? reachableSprite : unreachableSprite;
            if (nextSprite != null)
            {
                crosshairImage.sprite = nextSprite;
            }
        }

        SetVariantVisible(reachableCrosshair, isReachable);
        SetVariantVisible(unreachableCrosshair, !isReachable);
    }

    private void SetVariantVisible(Graphic variant, bool isVisible)
    {
        if (variant == null)
        {
            return;
        }

        if (toggleVariantGameObjects && variant.gameObject != gameObject)
        {
            variant.gameObject.SetActive(isVisible);
            return;
        }

        variant.enabled = isVisible;
    }
}
