using UnityEngine;
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

    [Header("Direct Pull (Left Click)")]
    [SerializeField, Min(0f)] private float directPullAcceleration = 90f;
    [SerializeField, Min(0f)] private float directPullMaxSpeed = 48f;
    [SerializeField, Min(0f)] private float directPullSteerStrength = 30f;

    [Header("Input")]
    [SerializeField] private bool holdToMaintainGrapple = true;

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

    private HookVisualState hookVisualState = HookVisualState.Idle;
    private Vector3 hookHomeLocalPosition;
    private Quaternion hookHomeLocalRotation;
    private bool hasHookHomePose;

    public bool IsGrappling => isGrappling;
    public bool IsDirectPulling => isGrappling && activeGrappleMode == GrappleMode.DirectPull;

    private enum GrappleMode
    {
        None = 0,
        SwingPull = 1,
        DirectPull = 2
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
        SetRopeVisible(false);
        isPrimaryController = IsPrimaryControllerForPlayerBody();
    }

    private void OnDisable()
    {
        EndGrapple(playRetract: false, immediateVisualReset: true);
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

        if (hasPendingGrapple)
        {
            if (rightPressed && pendingGrappleMode != GrappleMode.SwingPull)
            {
                TryStartGrapple(GrappleMode.SwingPull);
                return;
            }

            if (leftPressed && pendingGrappleMode != GrappleMode.DirectPull)
            {
                TryStartGrapple(GrappleMode.DirectPull);
                return;
            }

            bool pendingPressed = pendingGrappleMode == GrappleMode.SwingPull ? rightPressed : leftPressed;
            bool pendingHeld = pendingGrappleMode == GrappleMode.SwingPull ? rightHeld : leftHeld;

            if (holdToMaintainGrapple)
            {
                if (!pendingHeld)
                {
                    EndGrapple(playRetract: true, immediateVisualReset: false);
                }
            }
            else if (pendingPressed)
            {
                EndGrapple(playRetract: true, immediateVisualReset: false);
            }

            return;
        }

        if (!isGrappling)
        {
            if (rightPressed)
            {
                TryStartGrapple(GrappleMode.SwingPull);
            }
            else if (leftPressed)
            {
                TryStartGrapple(GrappleMode.DirectPull);
            }

            return;
        }

        if (rightPressed && activeGrappleMode != GrappleMode.SwingPull)
        {
            TryStartGrapple(GrappleMode.SwingPull);
            return;
        }

        if (leftPressed && activeGrappleMode != GrappleMode.DirectPull)
        {
            TryStartGrapple(GrappleMode.DirectPull);
            return;
        }

        bool activePressed = activeGrappleMode == GrappleMode.SwingPull ? rightPressed : leftPressed;
        bool activeHeld = activeGrappleMode == GrappleMode.SwingPull ? rightHeld : leftHeld;

        if (holdToMaintainGrapple)
        {
            if (!activeHeld)
            {
                EndGrapple(playRetract: true, immediateVisualReset: false);
            }
        }
        else if (activePressed)
        {
            EndGrapple(playRetract: true, immediateVisualReset: false);
        }
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

        if (activeGrappleMode == GrappleMode.DirectPull)
        {
            ApplyDirectPullForces(toAnchor, distanceToAnchor);
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

    private void TryStartGrapple(GrappleMode grappleMode)
    {
        if (playerBody == null || muzzleTip == null)
        {
            return;
        }

        if (!TryGetGrappleHitPoint(out RaycastHit hit))
        {
            return;
        }

        if (Vector3.Distance(playerBody.worldCenterOfMass, hit.point) < minimumAttachDistance)
        {
            return;
        }

        EndGrapple(playRetract: false, immediateVisualReset: false);
        CaptureAnchor(hit);

        hasPendingGrapple = true;
        pendingGrappleMode = grappleMode;
        activeGrappleMode = GrappleMode.None;
        isGrappling = false;
        SetRopeVisible(true);

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

#if ENABLE_LEGACY_INPUT_MANAGER
        bool readWithInputSystem = false;
#endif

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            leftPressed |= Mouse.current.leftButton.wasPressedThisFrame;
            leftHeld |= Mouse.current.leftButton.isPressed;
            leftReleased |= Mouse.current.leftButton.wasReleasedThisFrame;
            rightPressed |= Mouse.current.rightButton.wasPressedThisFrame;
            rightHeld |= Mouse.current.rightButton.isPressed;
            rightReleased |= Mouse.current.rightButton.wasReleasedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            readWithInputSystem = true;
#endif
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!readWithInputSystem)
        {
            leftPressed |= Input.GetButtonDown("Fire1");
            leftHeld |= Input.GetButton("Fire1");
            leftReleased |= Input.GetButtonUp("Fire1");
            rightPressed |= Input.GetButtonDown("Fire2");
            rightHeld |= Input.GetButton("Fire2");
            rightReleased |= Input.GetButtonUp("Fire2");
        }
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

        GrappleMode mode = pendingGrappleMode;
        hasPendingGrapple = false;
        pendingGrappleMode = GrappleMode.None;

        activeGrappleMode = mode;
        isGrappling = true;

        if (mode == GrappleMode.SwingPull)
        {
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
        }
        else
        {
            grappleJoint = null;
        }

        hookVisualState = HookVisualState.Latched;
        if (orientHookToSurfaceOnLatch)
        {
            OrientHookToSurface();
        }

        SetRopeVisible(true);
    }

    private void EndGrapple(bool playRetract, bool immediateVisualReset)
    {
        bool hadActiveHookState = hasPendingGrapple || isGrappling || hookVisualState == HookVisualState.Firing || hookVisualState == HookVisualState.Latched;

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

        if (immediateVisualReset || hookVisual == null)
        {
            hookVisualState = HookVisualState.Idle;
            ResetHookVisualImmediate();
            SetRopeVisible(false);
            return;
        }

        if (playRetract && hadActiveHookState)
        {
            hookVisualState = HookVisualState.Retracting;
            SetRopeVisible(true);
            return;
        }

        hookVisualState = HookVisualState.Idle;
        ResetHookVisualImmediate();
        SetRopeVisible(false);
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

    private void ApplyDirectPullForces(Vector3 toAnchor, float distanceToAnchor)
    {
        if (distanceToAnchor < 0.001f || directPullAcceleration <= 0f)
        {
            return;
        }

        Vector3 pullDirection = toAnchor / distanceToAnchor;
        Vector3 velocity = BodyVelocity;

        if (directPullSteerStrength > 0f)
        {
            Vector3 lateralVelocity = Vector3.ProjectOnPlane(velocity, pullDirection);
            if (lateralVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 steerDelta = Vector3.ClampMagnitude(-lateralVelocity, directPullSteerStrength * Time.fixedDeltaTime);
                playerBody.AddForce(steerDelta, ForceMode.VelocityChange);
                velocity += steerDelta;
            }
        }

        float currentTowardSpeed = Vector3.Dot(velocity, pullDirection);
        float speedScale = directPullMaxSpeed <= 0f
            ? 1f
            : Mathf.Clamp01(1f - (currentTowardSpeed / directPullMaxSpeed));

        if (speedScale <= 0f)
        {
            return;
        }

        playerBody.AddForce(pullDirection * (directPullAcceleration * speedScale), ForceMode.Acceleration);
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
