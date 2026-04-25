using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class GrappleGunController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform gunPivot;
    [SerializeField] private Transform muzzleTip;
    [SerializeField] private Transform hookVisual;
    [SerializeField] private LineRenderer ropeRenderer;

    [Header("Grapple Cast")]
    [SerializeField] private LayerMask grappleMask = ~0;
    [SerializeField, Min(0f)] private float maxGrappleDistance = 65f;
    [SerializeField, Min(0f)] private float minimumAttachDistance = 1.5f;
    [SerializeField, Range(0.1f, 1f)] private float initialRopeTightness = 0.9f;
    [SerializeField] private bool useCameraAssistAim = true;

    [Header("Swing Joint")]
    [SerializeField, Min(0f)] private float swingSpring = 5.5f;
    [SerializeField, Min(0f)] private float swingDamper = 2.5f;
    [SerializeField, Min(0f)] private float swingMassScale = 4f;

    [Header("Pull-In")]
    [SerializeField] private bool enableReelIn = true;
    [SerializeField, Min(0f)] private float reelInSpeed = 16f;
    [SerializeField, Min(0f)] private float pullAcceleration = 28f;
    [SerializeField, Min(0f)] private float maxPullSpeed = 24f;
    [SerializeField, Min(0f)] private float minimumRopeLength = 1.75f;
    [SerializeField, Min(0f)] private float autoDetachDistance = 1.35f;

    [Header("Input")]
    [SerializeField] private bool holdToMaintainGrapple = true;
    [SerializeField, Min(0f), Tooltip("Seconds a held mouse button keeps trying to start a swing after the first press misses.")]
    private float heldStartRetryDuration = 1f;

    [Header("Rope Visual")]
    [SerializeField] private bool autoCreateRopeRenderer = true;
    [SerializeField, Min(0f)] private float ropeWidth = 0.025f;
    [SerializeField] private Color ropeColor = new Color(0.9f, 0.95f, 1f, 1f);

    [Header("Hook Visual")]
    [SerializeField, Min(0f)] private float hookLaunchSpeed = 85f;
    [SerializeField, Min(0f)] private float hookRetractSpeed = 110f;
    [SerializeField, Min(0f)] private float hookSnapDistance = 0.04f;
    [SerializeField] private bool rotateHookToVelocity = true;
    [SerializeField] private bool orientHookToSurfaceOnLatch = true;
    [SerializeField] private Vector3 hookForwardAxisLocal = Vector3.forward;

    [Header("Audio")]
    [SerializeField] private AudioClip grappleDeployClip;
    [SerializeField] private AudioClip grappleReturnClip;
    [SerializeField] private AudioSource grappleDeploySource;
    [SerializeField] private AudioSource grappleReturnSource;
    [SerializeField, Range(0f, 1f)] private float grappleDeployVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float grappleReturnVolume = 0.75f;
    [SerializeField, Min(0.01f)] private float minimumReturnSoundDuration = 0.05f;

    [Header("Grapple Timer")]
    [SerializeField, Min(0f)] private float grappleDuration = 3f;
    [SerializeField] private Slider grappleTimerSlider;
    [SerializeField] private Image grappleTimerFill;
    [SerializeField] private Color grappleTimerFullColor = new Color(0.175f, 1f, 0f, 1f);
    [SerializeField] private Color grappleTimerEmptyColor = Color.red;
    [SerializeField] private bool hideTimerWhenInactive = true;

    private SpringJoint grappleJoint;
    private Rigidbody grappleConnectedBody;
    private Vector3 grappleConnectedLocalPoint;
    private Vector3 grappleConnectedLocalNormal;
    private Vector3 grapplePoint;
    private Vector3 grappleSurfaceNormal = Vector3.forward;
    private float targetRopeLength;
    private bool isGrappling;
    private bool hasPendingGrapple;
    private bool isPrimaryController = true;
    private GrappleMode activeGrappleMode = GrappleMode.None;
    private GrappleMode pendingGrappleMode = GrappleMode.None;
    private float swingStartRetryTimer;
    private bool wasSwingHeldLastFrame;

    private HookVisualState hookVisualState = HookVisualState.Idle;
    private Vector3 hookHomeLocalPosition;
    private Quaternion hookHomeLocalRotation;
    private bool hasHookHomePose;
    private AudioClip generatedReturnClip;
    private float grappleTimerRemaining;

    public bool IsGrappling => isGrappling;
    public bool IsDirectPulling => false;
    public bool HasReachableGrappleTarget => TryGetReachableGrappleTarget(out _);

    public bool TryGetActiveGrapplePoint(out Vector3 point)
    {
        if (!isGrappling)
        {
            point = default;
            return false;
        }

        RefreshDynamicAnchor();
        point = grapplePoint;
        return true;
    }

    private enum GrappleMode
    {
        None = 0,
        SwingPull = 1
    }

    private enum HookVisualState
    {
        Idle = 0,
        Firing = 1,
        Latched = 2,
        Retracting = 3
    }

    private void Reset()
    {
        gunPivot = transform;
        playerBody = GetComponentInParent<Rigidbody>();
        if (Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (muzzleTip == null)
        {
            Transform foundTip = transform.Find("GunTip");
            if (foundTip == null)
            {
                foundTip = transform.Find("Tip");
            }

            if (foundTip != null)
            {
                muzzleTip = foundTip;
            }
        }

        if (hookVisual == null)
        {
            hookVisual = FindLikelyHookVisual(transform);
        }
    }

    private void Awake()
    {
        if (gunPivot == null)
        {
            gunPivot = transform;
        }

        if (playerBody == null)
        {
            playerBody = GetComponentInParent<Rigidbody>();
        }

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (muzzleTip == null)
        {
            Transform foundTip = gunPivot != null ? gunPivot.Find("GunTip") : null;
            if (foundTip == null && gunPivot != null)
            {
                foundTip = gunPivot.Find("Tip");
            }

            if (foundTip != null)
            {
                muzzleTip = foundTip;
            }
            else
            {
                muzzleTip = gunPivot;
            }
        }

        if (hookVisual == null)
        {
            hookVisual = FindLikelyHookVisual(gunPivot);
            if (hookVisual == null && muzzleTip != null)
            {
                hookVisual = FindLikelyHookVisual(muzzleTip.parent);
            }

            if (hookVisual == null)
            {
                hookVisual = FindLikelyHookVisual(transform);
            }
        }

        CacheHookHomePose();
        ResetHookVisualImmediate();

        EnsureRopeRenderer();
        EnsureAudioSources();
        EnsureGrappleTimerUi();
        SetRopeVisible(false);
        ResetGrappleTimerUi();
        isPrimaryController = IsPrimaryControllerForPlayerBody();
    }

    private void OnDisable()
    {
        EndGrapple(playRetract: false, immediateVisualReset: true);
        StopReturnSfx();
        swingStartRetryTimer = 0f;
        wasSwingHeldLastFrame = false;
    }

    private void OnDestroy()
    {
        ReleaseGeneratedReturnClip();
    }

    private void Update()
    {
        bool shouldHandleInput = IsPrimaryControllerForPlayerBody();
        if (!shouldHandleInput)
        {
            if (isPrimaryController)
            {
                EndGrapple(playRetract: false, immediateVisualReset: true);
            }

            isPrimaryController = false;
            swingStartRetryTimer = 0f;
            wasSwingHeldLastFrame = false;
            return;
        }

        isPrimaryController = true;

        ReadGrappleInput(
            out bool leftPressed,
            out bool leftHeld,
            out _,
            out bool rightPressed,
            out bool rightHeld,
            out _);

        bool swingHeld = leftHeld || rightHeld;
        bool swingPressed = leftPressed || rightPressed || (swingHeld && !wasSwingHeldLastFrame);
        UpdateSwingStartRetry(swingPressed, swingHeld);
        HandleSwingInput(swingPressed, swingHeld);
        UpdateGrappleTimer(Time.deltaTime);
        wasSwingHeldLastFrame = swingHeld;
    }

    private void HandleSwingInput(bool swingPressed, bool swingHeld)
    {
        if (hasPendingGrapple)
        {
            if (holdToMaintainGrapple)
            {
                if (!swingHeld)
                {
                    EndGrapple(playRetract: true, immediateVisualReset: false);
                }
            }
            else if (swingPressed)
            {
                EndGrapple(playRetract: true, immediateVisualReset: false);
            }

            return;
        }

        if (!isGrappling)
        {
            if (ShouldAttemptSwingStart(swingPressed, swingHeld) && TryStartGrapple(GrappleMode.SwingPull))
            {
                swingStartRetryTimer = 0f;
            }

            return;
        }

        if (holdToMaintainGrapple)
        {
            if (!swingHeld)
            {
                EndGrapple(playRetract: true, immediateVisualReset: false);
            }
        }
        else if (swingPressed)
        {
            EndGrapple(playRetract: true, immediateVisualReset: false);
        }
    }

    private void UpdateSwingStartRetry(bool swingPressed, bool swingHeld)
    {
        if (swingPressed)
        {
            swingStartRetryTimer = Mathf.Max(0f, heldStartRetryDuration);
            return;
        }

        if (!swingHeld)
        {
            swingStartRetryTimer = 0f;
            return;
        }

        if (swingStartRetryTimer > 0f)
        {
            swingStartRetryTimer = Mathf.Max(0f, swingStartRetryTimer - Time.deltaTime);
        }
    }

    private bool ShouldAttemptSwingStart(bool swingPressed, bool swingHeld)
    {
        return swingPressed || (swingHeld && swingStartRetryTimer > 0f);
    }

    private void FixedUpdate()
    {
        if (!isPrimaryController || playerBody == null)
        {
            return;
        }

        RefreshDynamicAnchor();

        if (!isGrappling)
        {
            return;
        }

        Vector3 toAnchor = grapplePoint - playerBody.worldCenterOfMass;
        float distanceToAnchor = toAnchor.magnitude;

        if (distanceToAnchor <= autoDetachDistance)
        {
            EndGrapple(playRetract: true, immediateVisualReset: false);
            return;
        }

        if (activeGrappleMode == GrappleMode.SwingPull)
        {
            ApplySwingPullForces(toAnchor, distanceToAnchor);
        }
    }

    private void LateUpdate()
    {
        if (!isPrimaryController || muzzleTip == null)
        {
            return;
        }

        RefreshDynamicAnchor();
        UpdateHookVisual(Time.deltaTime);
        UpdateRopeVisual();
    }

    private bool TryStartGrapple(GrappleMode grappleMode)
    {
        if (playerBody == null || muzzleTip == null)
        {
            return false;
        }

        if (!TryGetReachableGrappleTarget(out RaycastHit hit))
        {
            return false;
        }

        EndGrapple(playRetract: false, immediateVisualReset: false);
        CaptureAnchor(hit);

        hasPendingGrapple = true;
        pendingGrappleMode = grappleMode;
        activeGrappleMode = GrappleMode.None;
        isGrappling = false;
        SetRopeVisible(true);
        PlayDeploySfx();

        if (hookVisual != null)
        {
            hookVisualState = HookVisualState.Firing;
            hookVisual.position = muzzleTip.position;
            Vector3 launchDirection = grapplePoint - hookVisual.position;
            if (rotateHookToVelocity && launchDirection.sqrMagnitude > 0.0001f)
            {
                SetHookFacing(launchDirection.normalized);
            }
        }
        else
        {
            hookVisualState = HookVisualState.Latched;
            ActivatePendingGrapple();
        }

        return true;
    }

    public bool TryGetReachableGrappleTarget(out RaycastHit hit)
    {
        if (playerBody == null || muzzleTip == null)
        {
            hit = default;
            return false;
        }

        if (!TryGetGrappleHitPoint(out hit))
        {
            return false;
        }

        return Vector3.Distance(playerBody.worldCenterOfMass, hit.point) >= minimumAttachDistance;
    }

    private bool TryGetGrappleHitPoint(out RaycastHit hit)
    {
        Vector3 castOrigin = muzzleTip != null ? muzzleTip.position : transform.position;
        Vector3 castDirection = GetFallbackCastDirection();

        if (useCameraAssistAim && playerCamera != null)
        {
            Ray lookRay = new Ray(playerCamera.position, playerCamera.forward);
            Vector3 lookPoint = Physics.Raycast(lookRay, out RaycastHit lookHit, maxGrappleDistance, grappleMask, QueryTriggerInteraction.Ignore)
                ? lookHit.point
                : lookRay.GetPoint(maxGrappleDistance);

            Vector3 assistDirection = lookPoint - castOrigin;
            if (assistDirection.sqrMagnitude > 0.0001f)
            {
                castDirection = assistDirection.normalized;
            }
        }

        if (castDirection.sqrMagnitude < 0.0001f)
        {
            hit = default;
            return false;
        }

        return Physics.Raycast(castOrigin, castDirection, out hit, maxGrappleDistance, grappleMask, QueryTriggerInteraction.Ignore);
    }

    private bool IsPrimaryControllerForPlayerBody()
    {
        if (playerBody == null)
        {
            return true;
        }

        GrappleGunController[] controllers = playerBody.GetComponentsInChildren<GrappleGunController>(true);
        GrappleGunController bestController = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < controllers.Length; i++)
        {
            GrappleGunController controller = controllers[i];
            if (controller == null || !controller.enabled || !controller.gameObject.activeInHierarchy)
            {
                continue;
            }

            int score = controller.GetControllerPriorityScore();
            if (bestController == null || score > bestScore)
            {
                bestController = controller;
                bestScore = score;
            }
        }

        return bestController == null || bestController == this;
    }

    private int GetControllerPriorityScore()
    {
        int score = 0;
        if (useCameraAssistAim)
        {
            score += 2;
        }

        if (gunPivot != null)
        {
            score += 1;
        }

        if (muzzleTip != null && gunPivot != null && muzzleTip != gunPivot)
        {
            score += 4;
        }

        return score;
    }

    private Vector3 GetFallbackCastDirection()
    {
        if (muzzleTip != null)
        {
            return muzzleTip.forward;
        }

        if (gunPivot != null)
        {
            return gunPivot.forward;
        }

        return transform.forward;
    }

    private static Transform FindLikelyHookVisual(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform exact = root.Find("Grapple");
        if (exact != null)
        {
            return exact;
        }

        exact = root.Find("Hook");
        if (exact != null)
        {
            return exact;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("grapple") || lowerName.Contains("hook"))
            {
                return child;
            }
        }

        return null;
    }

    private void ReadGrappleInput(
        out bool leftPressed,
        out bool leftHeld,
        out bool leftReleased,
        out bool rightPressed,
        out bool rightHeld,
        out bool rightReleased)
    {
        leftPressed = false;
        leftHeld = false;
        leftReleased = false;
        rightPressed = false;
        rightHeld = false;
        rightReleased = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            leftPressed |= Mouse.current.leftButton.wasPressedThisFrame;
            leftHeld |= Mouse.current.leftButton.isPressed;
            leftReleased |= Mouse.current.leftButton.wasReleasedThisFrame;
            rightPressed |= Mouse.current.rightButton.wasPressedThisFrame;
            rightHeld |= Mouse.current.rightButton.isPressed;
            rightReleased |= Mouse.current.rightButton.wasReleasedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        leftPressed |= Input.GetButtonDown("Fire1") || Input.GetMouseButtonDown(0);
        leftHeld |= Input.GetButton("Fire1") || Input.GetMouseButton(0);
        leftReleased |= Input.GetButtonUp("Fire1") || Input.GetMouseButtonUp(0);
        rightPressed |= Input.GetButtonDown("Fire2") || Input.GetMouseButtonDown(1);
        rightHeld |= Input.GetButton("Fire2") || Input.GetMouseButton(1);
        rightReleased |= Input.GetButtonUp("Fire2") || Input.GetMouseButtonUp(1);
#endif
    }

    private void CaptureAnchor(RaycastHit hit)
    {
        grapplePoint = hit.point;

        if (hit.normal.sqrMagnitude > 0.0001f)
        {
            grappleSurfaceNormal = hit.normal.normalized;
        }
        else
        {
            Vector3 fallbackNormal = -GetFallbackCastDirection();
            grappleSurfaceNormal = fallbackNormal.sqrMagnitude > 0.0001f ? fallbackNormal.normalized : Vector3.forward;
        }

        if (hit.rigidbody != null && hit.rigidbody != playerBody)
        {
            grappleConnectedBody = hit.rigidbody;
            grappleConnectedLocalPoint = hit.rigidbody.transform.InverseTransformPoint(hit.point);
            grappleConnectedLocalNormal = hit.rigidbody.transform.InverseTransformDirection(grappleSurfaceNormal);
        }
        else
        {
            grappleConnectedBody = null;
            grappleConnectedLocalPoint = Vector3.zero;
            grappleConnectedLocalNormal = Vector3.zero;
        }
    }

    private void RefreshDynamicAnchor()
    {
        if (grappleConnectedBody == null)
        {
            return;
        }

        Transform connectedTransform = grappleConnectedBody.transform;
        grapplePoint = connectedTransform.TransformPoint(grappleConnectedLocalPoint);

        Vector3 normal = connectedTransform.TransformDirection(grappleConnectedLocalNormal);
        if (normal.sqrMagnitude > 0.0001f)
        {
            grappleSurfaceNormal = normal.normalized;
        }
    }

    private void ActivatePendingGrapple()
    {
        if (!hasPendingGrapple)
        {
            return;
        }

        if (playerBody == null)
        {
            EndGrapple(playRetract: true, immediateVisualReset: false);
            return;
        }

        RefreshDynamicAnchor();
        float distance = Vector3.Distance(playerBody.worldCenterOfMass, grapplePoint);
        if (distance < minimumAttachDistance)
        {
            EndGrapple(playRetract: true, immediateVisualReset: false);
            return;
        }

        GrappleMode mode = pendingGrappleMode == GrappleMode.None ? GrappleMode.SwingPull : pendingGrappleMode;
        hasPendingGrapple = false;
        pendingGrappleMode = GrappleMode.None;

        activeGrappleMode = mode;
        isGrappling = true;

        grappleJoint = playerBody.gameObject.AddComponent<SpringJoint>();
        grappleJoint.autoConfigureConnectedAnchor = false;
        grappleJoint.enableCollision = false;
        grappleJoint.spring = swingSpring;
        grappleJoint.damper = swingDamper;
        grappleJoint.massScale = swingMassScale;

        if (grappleConnectedBody != null)
        {
            grappleJoint.connectedBody = grappleConnectedBody;
            grappleJoint.connectedAnchor = grappleConnectedLocalPoint;
        }
        else
        {
            grappleJoint.connectedBody = null;
            grappleJoint.connectedAnchor = grapplePoint;
        }

        targetRopeLength = Mathf.Clamp(distance * initialRopeTightness, minimumRopeLength, distance);
        grappleJoint.maxDistance = targetRopeLength;
        grappleJoint.minDistance = 0f;

        hookVisualState = HookVisualState.Latched;
        if (orientHookToSurfaceOnLatch)
        {
            OrientHookToSurface();
        }

        SetRopeVisible(true);
        StartGrappleTimer();
    }

    private void EndGrapple(bool playRetract, bool immediateVisualReset)
    {
        bool hadActiveHookState = hasPendingGrapple || isGrappling || hookVisualState == HookVisualState.Firing || hookVisualState == HookVisualState.Latched;
        float expectedReturnDuration = GetExpectedHookReturnDuration();

        if (grappleJoint != null)
        {
            Destroy(grappleJoint);
            grappleJoint = null;
        }

        isGrappling = false;
        hasPendingGrapple = false;
        activeGrappleMode = GrappleMode.None;
        pendingGrappleMode = GrappleMode.None;
        grappleConnectedBody = null;
        grappleConnectedLocalPoint = Vector3.zero;
        grappleConnectedLocalNormal = Vector3.zero;
        ResetGrappleTimerUi();

        if (immediateVisualReset || hookVisual == null)
        {
            hookVisualState = HookVisualState.Idle;
            ResetHookVisualImmediate();
            SetRopeVisible(false);
            StopReturnSfx();
            return;
        }

        if (playRetract && hadActiveHookState)
        {
            hookVisualState = HookVisualState.Retracting;
            SetRopeVisible(true);
            PlayReturnSfx(expectedReturnDuration);
            return;
        }

        hookVisualState = HookVisualState.Idle;
        ResetHookVisualImmediate();
        SetRopeVisible(false);
        StopReturnSfx();
    }

    private void UpdateHookVisual(float deltaTime)
    {
        if (hookVisual == null || muzzleTip == null)
        {
            return;
        }

        switch (hookVisualState)
        {
            case HookVisualState.Idle:
            {
                return;
            }
            case HookVisualState.Firing:
            {
                MoveHookTowards(grapplePoint, hookLaunchSpeed, deltaTime, out bool reachedTarget);
                if (reachedTarget)
                {
                    hookVisualState = HookVisualState.Latched;
                    if (orientHookToSurfaceOnLatch)
                    {
                        OrientHookToSurface();
                    }

                    ActivatePendingGrapple();
                }

                break;
            }
            case HookVisualState.Latched:
            {
                hookVisual.position = grapplePoint;
                if (orientHookToSurfaceOnLatch)
                {
                    OrientHookToSurface();
                }

                break;
            }
            case HookVisualState.Retracting:
            {
                MoveHookTowards(muzzleTip.position, hookRetractSpeed, deltaTime, out bool reachedMuzzle);
                if (reachedMuzzle)
                {
                    hookVisualState = HookVisualState.Idle;
                    ResetHookVisualImmediate();
                    SetRopeVisible(false);
                }

                break;
            }
        }
    }

    private void MoveHookTowards(Vector3 targetPosition, float speed, float deltaTime, out bool reachedTarget)
    {
        if (hookVisual == null)
        {
            reachedTarget = true;
            return;
        }

        if (deltaTime <= 0f || speed <= 0f)
        {
            hookVisual.position = targetPosition;
            reachedTarget = true;
            return;
        }

        Vector3 startPosition = hookVisual.position;
        Vector3 endPosition = Vector3.MoveTowards(startPosition, targetPosition, speed * deltaTime);
        hookVisual.position = endPosition;

        Vector3 travelDirection = endPosition - startPosition;
        if (rotateHookToVelocity && travelDirection.sqrMagnitude > 0.000001f)
        {
            SetHookFacing(travelDirection.normalized);
        }

        float snapDistance = Mathf.Max(0.0001f, hookSnapDistance);
        reachedTarget = (endPosition - targetPosition).sqrMagnitude <= snapDistance * snapDistance;
        if (reachedTarget)
        {
            hookVisual.position = targetPosition;
        }
    }

    private void CacheHookHomePose()
    {
        if (hookVisual == null)
        {
            return;
        }

        hookHomeLocalPosition = hookVisual.localPosition;
        hookHomeLocalRotation = hookVisual.localRotation;
        hasHookHomePose = true;
    }

    private void ResetHookVisualImmediate()
    {
        if (hookVisual == null)
        {
            return;
        }

        if (!hasHookHomePose)
        {
            CacheHookHomePose();
        }

        if (hasHookHomePose)
        {
            hookVisual.localPosition = hookHomeLocalPosition;
            hookVisual.localRotation = hookHomeLocalRotation;
            return;
        }

        if (muzzleTip != null)
        {
            hookVisual.position = muzzleTip.position;
            hookVisual.rotation = muzzleTip.rotation;
        }
    }

    private void SetHookFacing(Vector3 worldDirection)
    {
        if (hookVisual == null || worldDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 localForward = hookForwardAxisLocal.sqrMagnitude > 0.0001f ? hookForwardAxisLocal.normalized : Vector3.forward;
        hookVisual.rotation = Quaternion.FromToRotation(localForward, worldDirection.normalized);
    }

    private void OrientHookToSurface()
    {
        if (hookVisual == null)
        {
            return;
        }

        Vector3 intoSurface = -grappleSurfaceNormal;
        if (intoSurface.sqrMagnitude < 0.0001f)
        {
            intoSurface = GetFallbackCastDirection();
        }

        if (intoSurface.sqrMagnitude < 0.0001f)
        {
            return;
        }

        SetHookFacing(intoSurface.normalized);
    }

    private void UpdateRopeVisual()
    {
        if (ropeRenderer == null || muzzleTip == null)
        {
            return;
        }

        bool shouldShowRope = isGrappling || hasPendingGrapple || hookVisualState == HookVisualState.Firing || hookVisualState == HookVisualState.Latched || hookVisualState == HookVisualState.Retracting;
        if (!shouldShowRope)
        {
            SetRopeVisible(false);
            return;
        }

        SetRopeVisible(true);
        ropeRenderer.positionCount = 2;
        ropeRenderer.SetPosition(0, muzzleTip.position);
        ropeRenderer.SetPosition(1, GetRopeEndPoint());
    }

    private Vector3 GetRopeEndPoint()
    {
        if (hookVisual != null && hookVisualState != HookVisualState.Idle)
        {
            return hookVisual.position;
        }

        if (isGrappling || hasPendingGrapple)
        {
            return grapplePoint;
        }

        return muzzleTip != null ? muzzleTip.position : transform.position;
    }

    private void ApplySwingPullForces(Vector3 toAnchor, float distanceToAnchor)
    {
        if (grappleJoint == null)
        {
            EndGrapple(playRetract: true, immediateVisualReset: false);
            return;
        }

        if (enableReelIn && reelInSpeed > 0f)
        {
            targetRopeLength = Mathf.Max(minimumRopeLength, targetRopeLength - reelInSpeed * Time.fixedDeltaTime);
            grappleJoint.maxDistance = targetRopeLength;
            grappleJoint.minDistance = 0f;
        }

        if (pullAcceleration <= 0f || distanceToAnchor < 0.001f)
        {
            return;
        }

        Vector3 pullDirection = toAnchor / distanceToAnchor;
        float currentTowardSpeed = Vector3.Dot(BodyVelocity, pullDirection);
        float speedScale = maxPullSpeed <= 0f
            ? 1f
            : Mathf.Clamp01(1f - (currentTowardSpeed / maxPullSpeed));

        if (speedScale <= 0f)
        {
            return;
        }

        playerBody.AddForce(pullDirection * (pullAcceleration * speedScale), ForceMode.Acceleration);
    }

    private void EnsureGrappleTimerUi()
    {
        if (grappleTimerSlider == null)
        {
            grappleTimerSlider = FindFirstObjectByType<Slider>(FindObjectsInactive.Include);
        }

        if (grappleTimerFill == null && grappleTimerSlider != null && grappleTimerSlider.fillRect != null)
        {
            grappleTimerFill = grappleTimerSlider.fillRect.GetComponent<Image>();
        }

        if (grappleTimerSlider == null)
        {
            return;
        }

        grappleTimerSlider.minValue = 0f;
        grappleTimerSlider.maxValue = 1f;
        grappleTimerSlider.wholeNumbers = false;
        grappleTimerSlider.interactable = false;
    }

    private void StartGrappleTimer()
    {
        if (grappleDuration <= 0f)
        {
            ResetGrappleTimerUi();
            return;
        }

        grappleTimerRemaining = grappleDuration;
        SetGrappleTimerVisible(true);
        UpdateGrappleTimerUi(1f);
    }

    private void UpdateGrappleTimer(float deltaTime)
    {
        if (!isGrappling || grappleDuration <= 0f)
        {
            return;
        }

        grappleTimerRemaining = Mathf.Max(0f, grappleTimerRemaining - deltaTime);
        UpdateGrappleTimerUi(grappleTimerRemaining / grappleDuration);

        if (grappleTimerRemaining <= 0f)
        {
            EndGrapple(playRetract: true, immediateVisualReset: false);
        }
    }

    private void ResetGrappleTimerUi()
    {
        grappleTimerRemaining = 0f;
        UpdateGrappleTimerUi(0f);
        SetGrappleTimerVisible(false);
    }

    private void UpdateGrappleTimerUi(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);

        if (grappleTimerSlider != null)
        {
            grappleTimerSlider.SetValueWithoutNotify(normalizedTime);
        }

        if (grappleTimerFill != null)
        {
            grappleTimerFill.color = Color.Lerp(grappleTimerEmptyColor, grappleTimerFullColor, normalizedTime);
        }
    }

    private void SetGrappleTimerVisible(bool visible)
    {
        if (!hideTimerWhenInactive && !visible)
        {
            visible = true;
        }

        if (grappleTimerSlider != null)
        {
            grappleTimerSlider.gameObject.SetActive(visible);
        }
    }

    private void EnsureRopeRenderer()
    {
        if (ropeRenderer == null)
        {
            ropeRenderer = GetComponent<LineRenderer>();
        }

        if (ropeRenderer == null && autoCreateRopeRenderer)
        {
            ropeRenderer = gameObject.AddComponent<LineRenderer>();
        }

        if (ropeRenderer == null)
        {
            return;
        }

        ropeRenderer.useWorldSpace = true;
        ropeRenderer.textureMode = LineTextureMode.Stretch;
        ropeRenderer.alignment = LineAlignment.View;
        ropeRenderer.startWidth = ropeWidth;
        ropeRenderer.endWidth = ropeWidth;
        ropeRenderer.startColor = ropeColor;
        ropeRenderer.endColor = ropeColor;
        ropeRenderer.positionCount = 0;

        if (ropeRenderer.material == null)
        {
            Shader defaultLineShader = Shader.Find("Sprites/Default");
            if (defaultLineShader != null)
            {
                ropeRenderer.material = new Material(defaultLineShader);
            }
        }
    }

    private void SetRopeVisible(bool visible)
    {
        if (ropeRenderer == null)
        {
            return;
        }

        ropeRenderer.enabled = visible;
        ropeRenderer.positionCount = visible ? 2 : 0;
    }

    private void EnsureAudioSources()
    {
        EnsureAudioSource(ref grappleDeploySource);
        EnsureAudioSource(ref grappleReturnSource);
    }

    private void EnsureAudioSource(ref AudioSource source)
    {
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.pitch = 1f;
    }

    private void PlayDeploySfx()
    {
        if (grappleDeployClip == null)
        {
            return;
        }

        EnsureAudioSources();
        grappleDeploySource.PlayOneShot(grappleDeployClip, grappleDeployVolume);
    }

    private void PlayReturnSfx(float targetDuration)
    {
        if (grappleReturnClip == null)
        {
            return;
        }

        EnsureAudioSources();
        grappleReturnSource.Stop();
        grappleReturnSource.clip = null;
        ReleaseGeneratedReturnClip();

        AudioClip clipToPlay = grappleReturnClip;
        if (targetDuration > 0f)
        {
            clipToPlay = AudioTimeStretchUtility.CreateStretchedClip(
                grappleReturnClip,
                targetDuration,
                $"{grappleReturnClip.name}_Return_{targetDuration:0.000}s");
            if (clipToPlay != grappleReturnClip)
            {
                generatedReturnClip = clipToPlay;
            }
        }

        grappleReturnSource.clip = clipToPlay;
        grappleReturnSource.volume = grappleReturnVolume;
        grappleReturnSource.pitch = 1f;
        grappleReturnSource.Play();
    }

    private void StopReturnSfx()
    {
        if (grappleReturnSource != null)
        {
            grappleReturnSource.Stop();
            grappleReturnSource.clip = null;
        }

        ReleaseGeneratedReturnClip();
    }

    private void ReleaseGeneratedReturnClip()
    {
        if (generatedReturnClip == null)
        {
            return;
        }

        Destroy(generatedReturnClip);
        generatedReturnClip = null;
    }

    private float GetExpectedHookReturnDuration()
    {
        if (hookVisual == null || muzzleTip == null || hookRetractSpeed <= 0f)
        {
            return grappleReturnClip != null ? grappleReturnClip.length : 0f;
        }

        float distance = Vector3.Distance(hookVisual.position, muzzleTip.position);
        if (distance <= hookSnapDistance)
        {
            return minimumReturnSoundDuration;
        }

        return Mathf.Max(minimumReturnSoundDuration, distance / hookRetractSpeed);
    }

    private Vector3 BodyVelocity
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return playerBody.linearVelocity;
#else
            return playerBody.velocity;
#endif
        }
    }
}
