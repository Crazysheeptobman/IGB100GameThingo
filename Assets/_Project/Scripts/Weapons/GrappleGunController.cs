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

    private SpringJoint grappleJoint;
    private Vector3 grapplePoint;
    private float targetRopeLength;
    private bool isGrappling;
    private GrappleMode activeGrappleMode = GrappleMode.None;

    public bool IsGrappling => isGrappling;
    public bool IsDirectPulling => isGrappling && activeGrappleMode == GrappleMode.DirectPull;

    private enum GrappleMode
    {
        None = 0,
        SwingPull = 1,
        DirectPull = 2
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
            if (foundTip != null)
            {
                muzzleTip = foundTip;
            }
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
            if (foundTip != null)
            {
                muzzleTip = foundTip;
            }
            else
            {
                muzzleTip = gunPivot;
            }
        }

        EnsureRopeRenderer();
        SetRopeVisible(false);
    }

    private void OnDisable()
    {
        StopGrapple();
    }

    private void Update()
    {
        ReadGrappleInput(
            out bool leftPressed,
            out bool leftHeld,
            out _,
            out bool rightPressed,
            out bool rightHeld,
            out _);

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
                StopGrapple();
            }
        }
        else if (activePressed)
        {
            StopGrapple();
        }
    }

    private void FixedUpdate()
    {
        if (!isGrappling || playerBody == null)
        {
            return;
        }

        Vector3 toAnchor = grapplePoint - playerBody.worldCenterOfMass;
        float distanceToAnchor = toAnchor.magnitude;

        if (distanceToAnchor <= autoDetachDistance)
        {
            StopGrapple();
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
        if (!isGrappling || ropeRenderer == null || muzzleTip == null)
        {
            return;
        }

        ropeRenderer.positionCount = 2;
        ropeRenderer.SetPosition(0, muzzleTip.position);
        ropeRenderer.SetPosition(1, grapplePoint);
    }

    private void TryStartGrapple(GrappleMode grappleMode)
    {
        if (playerBody == null || muzzleTip == null)
        {
            return;
        }

        Vector3 castOrigin = muzzleTip.position;
        Vector3 castDirection = GetCastDirectionFromTip(castOrigin);
        if (castDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        if (!Physics.Raycast(castOrigin, castDirection, out RaycastHit hit, maxGrappleDistance, grappleMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float distance = Vector3.Distance(playerBody.worldCenterOfMass, hit.point);
        if (distance < minimumAttachDistance)
        {
            return;
        }

        StopGrapple();

        if (grappleMode == GrappleMode.SwingPull)
        {
            grappleJoint = playerBody.gameObject.AddComponent<SpringJoint>();
            grappleJoint.autoConfigureConnectedAnchor = false;
            grappleJoint.connectedAnchor = hit.point;
            grappleJoint.enableCollision = false;
            grappleJoint.spring = swingSpring;
            grappleJoint.damper = swingDamper;
            grappleJoint.massScale = swingMassScale;

            targetRopeLength = Mathf.Clamp(distance * initialRopeTightness, minimumRopeLength, distance);
            grappleJoint.maxDistance = targetRopeLength;
            grappleJoint.minDistance = 0f;
        }

        grapplePoint = hit.point;
        activeGrappleMode = grappleMode;
        isGrappling = true;
        SetRopeVisible(true);
    }

    private void StopGrapple()
    {
        isGrappling = false;
        activeGrappleMode = GrappleMode.None;

        if (grappleJoint != null)
        {
            Destroy(grappleJoint);
            grappleJoint = null;
        }

        SetRopeVisible(false);
    }

    private Vector3 GetCastDirectionFromTip(Vector3 tipPosition)
    {
        if (useCameraAssistAim && playerCamera != null)
        {
            Ray lookRay = new Ray(playerCamera.position, playerCamera.forward);
            Vector3 lookPoint = Physics.Raycast(lookRay, out RaycastHit lookHit, maxGrappleDistance, grappleMask, QueryTriggerInteraction.Ignore)
                ? lookHit.point
                : lookRay.GetPoint(maxGrappleDistance);

            Vector3 towardLookPoint = lookPoint - tipPosition;
            if (towardLookPoint.sqrMagnitude > 0.0001f)
            {
                return towardLookPoint.normalized;
            }
        }

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

    private void ApplySwingPullForces(Vector3 toAnchor, float distanceToAnchor)
    {
        if (grappleJoint == null)
        {
            StopGrapple();
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
