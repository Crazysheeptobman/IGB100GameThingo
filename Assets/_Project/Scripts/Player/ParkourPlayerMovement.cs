using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class ParkourPlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform moveReference;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float walkSpeed = 8f;
    [SerializeField, Min(0f)] private float sprintSpeed = 12f;
    [SerializeField, Min(0f)] private float groundAcceleration = 75f;
    [SerializeField, Min(0f)] private float groundDeceleration = 60f;
    [SerializeField, Min(0f)] private float airAcceleration = 28f;
    [SerializeField, Min(0f)] private float maxAirSpeed = 11f;
    [SerializeField] private bool normalizeMoveInput = true;

    [Header("Grapple Momentum")]
    [SerializeField] private GrappleGunController grappleController;
    [SerializeField, Min(0f)] private float grappleAirControlAcceleration = 45f;
    [SerializeField, Min(0f)] private float grappleAirControlMaxSpeed = 60f;
    [SerializeField] private bool preserveGrappleMomentum = true;
    [SerializeField] private bool allowAirControlDuringDirectPull = true;

    [Header("Dash")]
    [SerializeField] private bool enableDash = true;
    [SerializeField, Min(0f)] private float dashSpeed = 22f;
    [SerializeField, Min(0f)] private float dashDuration = 0.14f;
    [SerializeField, Min(0f)] private float dashCooldown = 0.45f;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpVelocity = 8.5f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.15f;

    [Header("Gravity")]
    [SerializeField, Min(0f)] private float gravity = 30f;
    [SerializeField, Min(1f)] private float lowJumpGravityMultiplier = 1.8f;
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 2.1f;
    [SerializeField, Min(0f)] private float maxFallSpeed = 38f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0f)] private float groundCheckDistance = 0.08f;
    [SerializeField, Range(0f, 89f)] private float maxGroundAngle = 52f;

    [Header("Wall Movement")]
    [SerializeField] private bool enableWallMovement = true;
    [SerializeField] private LayerMask wallMask = ~0;
    [SerializeField, Min(0f)] private float wallCheckDistance = 0.65f;
    [SerializeField, Min(0f)] private float wallProbeHeightOffset = 0.6f;
    [SerializeField, Range(0f, 1f)] private float maxWallUpDot = 0.2f;
    [SerializeField, Min(0f)] private float wallClimbSpeed = 5f;
    [SerializeField, Min(0f)] private float wallClimbAcceleration = 16f;
    [SerializeField, Min(0f)] private float wallStickForce = 14f;
    [SerializeField, Min(0f)] private float wallSlideMaxSpeed = 3.5f;
    [SerializeField, Min(0f)] private float wallJumpUpVelocity = 8f;
    [SerializeField, Min(0f)] private float wallJumpAwayVelocity = 7f;
    [SerializeField, Min(0f)] private float wallJumpForwardBonus = 1.75f;
    [SerializeField, Range(-1f, 1f)] private float wallClimbInputThreshold = 0.05f;

    [Header("Rigidbody Setup")]
    [SerializeField] private bool configureRigidbodyOnAwake = true;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;

    public bool IsGrounded => isGrounded;
    public bool IsWallClimbing => isWallClimbing;
    public Vector3 CurrentVelocity => BodyVelocity;

    private Rigidbody body;
    private CapsuleCollider capsule;

    private Vector2 moveInput;
    private bool sprintHeld;
    private bool jumpHeld;
    private bool dashQueued;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection = Vector3.zero;

    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;

    private bool hasWall;
    private Vector3 wallNormal = Vector3.zero;
    private bool isWallClimbing;

    private float timeSinceLastGrounded;
    private float jumpBufferCounter;

    private void Reset()
    {
        moveReference = transform;
        grappleController = ResolveGrappleController();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        if (moveReference == null)
        {
            moveReference = transform;
        }

        if (grappleController == null)
        {
            grappleController = ResolveGrappleController();
        }

        body.freezeRotation = true;
        body.useGravity = false;

        if (configureRigidbodyOnAwake)
        {
            body.interpolation = interpolation;
            body.collisionDetectionMode = collisionDetection;
        }
    }

    private void OnEnable()
    {
        jumpBufferCounter = 0f;
        timeSinceLastGrounded = coyoteTime;
        dashQueued = false;
        isDashing = false;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
    }

    private void Update()
    {
        ReadInput();

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        RefreshGroundState();
        RefreshWallState();

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.fixedDeltaTime);
        }

        if (isGrounded)
        {
            timeSinceLastGrounded = 0f;
        }
        else
        {
            timeSinceLastGrounded += Time.fixedDeltaTime;
        }

        if (dashQueued)
        {
            TryStartDash();
            dashQueued = false;
        }

        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }

        TryConsumeJump();
        if (isDashing)
        {
            ApplyDashMovement();
        }
        else
        {
            ApplyHorizontalMovement();
            ApplyWallMotion();
        }
        ApplyGravity();
    }

    private void ReadInput()
    {
        Vector2 nextMoveInput = Vector2.zero;
        bool nextSprintHeld = false;
        bool nextJumpHeld = false;
        bool jumpPressedThisFrame = false;
        bool dashPressedThisFrame = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        bool readWithInputSystem = false;
#endif

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        if (keyboard != null)
        {
            float x = 0f;
            float y = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                y += 1f;
            }

            nextMoveInput += new Vector2(x, y);
            nextSprintHeld |= keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            nextJumpHeld |= keyboard.spaceKey.isPressed;
            jumpPressedThisFrame |= keyboard.spaceKey.wasPressedThisFrame;
            dashPressedThisFrame |= keyboard.eKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            readWithInputSystem = true;
#endif
        }

        if (gamepad != null)
        {
            nextMoveInput += gamepad.leftStick.ReadValue();
            nextSprintHeld |= gamepad.leftStickButton.isPressed;
            nextJumpHeld |= gamepad.buttonSouth.isPressed;
            jumpPressedThisFrame |= gamepad.buttonSouth.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            readWithInputSystem = true;
#endif
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!readWithInputSystem)
        {
            nextMoveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            nextSprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            nextJumpHeld = Input.GetButton("Jump");
            jumpPressedThisFrame = Input.GetButtonDown("Jump");
            dashPressedThisFrame = Input.GetKeyDown(KeyCode.E);
        }
#endif

        if (normalizeMoveInput && nextMoveInput.sqrMagnitude > 1f)
        {
            nextMoveInput.Normalize();
        }

        moveInput = nextMoveInput;
        sprintHeld = nextSprintHeld;
        jumpHeld = nextJumpHeld;

        if (jumpPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
        }

        if (dashPressedThisFrame)
        {
            dashQueued = true;
        }
    }

    private void RefreshGroundState()
    {
        isGrounded = false;
        groundNormal = Vector3.up;

        GetCapsuleWorldInfo(out Vector3 capsuleCenter, out float radius, out float halfHeight);

        float offsetAboveGround = 0.05f;
        Vector3 castOrigin = capsuleCenter + Vector3.up * offsetAboveGround;
        float castDistance = Mathf.Max(0f, halfHeight - radius) + groundCheckDistance + offsetAboveGround;

        if (!Physics.SphereCast(castOrigin, radius * 0.95f, Vector3.down, out RaycastHit hit, castDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        float angleToUp = Vector3.Angle(hit.normal, Vector3.up);
        if (angleToUp > maxGroundAngle)
        {
            return;
        }

        isGrounded = true;
        groundNormal = hit.normal;
    }

    private void RefreshWallState()
    {
        hasWall = false;
        wallNormal = Vector3.zero;

        if (!enableWallMovement || isGrounded)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * wallProbeHeightOffset;
        Transform reference = moveReference != null ? moveReference : transform;

        Vector3 forward = reference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }
        forward.Normalize();

        Vector3 right = reference.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = transform.right;
            right.y = 0f;
        }
        right.Normalize();

        Vector3 diagonalA = (forward + right).normalized;
        Vector3 diagonalB = (forward - right).normalized;

        Vector3[] directions =
        {
            forward,
            -forward,
            right,
            -right,
            diagonalA,
            -diagonalA,
            diagonalB,
            -diagonalB
        };

        float bestDistance = float.MaxValue;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i];
            if (direction.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            if (!Physics.Raycast(origin, direction, out RaycastHit hit, wallCheckDistance, wallMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            float upDot = Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up));
            if (upDot > maxWallUpDot)
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            hasWall = true;
            wallNormal = hit.normal;
        }
    }

    private void TryConsumeJump()
    {
        if (jumpBufferCounter <= 0f)
        {
            return;
        }

        if (enableWallMovement && hasWall && !isGrounded)
        {
            Vector3 horizontalVelocity = GetHorizontalVelocity();
            Vector3 forward = GetFlatForward();

            Vector3 launchVelocity = wallNormal * wallJumpAwayVelocity;
            launchVelocity += Vector3.up * wallJumpUpVelocity;
            launchVelocity += forward * wallJumpForwardBonus;

            SetBodyVelocity(horizontalVelocity * 0.35f + launchVelocity);
            jumpBufferCounter = 0f;
            isWallClimbing = false;
            return;
        }

        if (isGrounded || timeSinceLastGrounded <= coyoteTime)
        {
            Vector3 velocity = BodyVelocity;
            if (velocity.y < 0f)
            {
                velocity.y = 0f;
            }

            velocity.y += jumpVelocity;
            SetBodyVelocity(velocity);
            jumpBufferCounter = 0f;
            isGrounded = false;
            timeSinceLastGrounded = coyoteTime + 1f;
        }
    }

    private void ApplyHorizontalMovement()
    {
        Vector3 moveDirection = GetDesiredMoveDirection();
        bool hasMoveInput = moveDirection.sqrMagnitude > 0.0001f;
        Vector3 currentHorizontalVelocity = GetHorizontalVelocity();

        if (!allowAirControlDuringDirectPull && !isGrounded && IsDirectPullingWithGrapple)
        {
            return;
        }

        if (isGrounded)
        {
            float targetSpeed = sprintHeld ? sprintSpeed : walkSpeed;
            Vector3 targetVelocity = moveDirection * targetSpeed;
            Vector3 velocityDelta = targetVelocity - currentHorizontalVelocity;
            float accel = hasMoveInput ? groundAcceleration : groundDeceleration;
            Vector3 clampedDelta = Vector3.ClampMagnitude(velocityDelta, accel * Time.fixedDeltaTime);
            body.AddForce(clampedDelta, ForceMode.VelocityChange);
            return;
        }

        if (IsGrappling)
        {
            ApplyGrappleAirControl(moveDirection, hasMoveInput, currentHorizontalVelocity);
            return;
        }

        float airTargetSpeed = sprintHeld ? sprintSpeed : walkSpeed;
        Vector3 airTargetVelocity = moveDirection * airTargetSpeed;
        Vector3 airVelocityDelta = airTargetVelocity - currentHorizontalVelocity;
        Vector3 airClampedDelta = Vector3.ClampMagnitude(airVelocityDelta, airAcceleration * Time.fixedDeltaTime);
        Vector3 projectedAirVelocity = currentHorizontalVelocity + airClampedDelta;

        if (projectedAirVelocity.magnitude > maxAirSpeed)
        {
            projectedAirVelocity = projectedAirVelocity.normalized * maxAirSpeed;
            airClampedDelta = projectedAirVelocity - currentHorizontalVelocity;
        }

        body.AddForce(airClampedDelta, ForceMode.VelocityChange);
    }

    private void ApplyGrappleAirControl(Vector3 moveDirection, bool hasMoveInput, Vector3 currentHorizontalVelocity)
    {
        if (!hasMoveInput && preserveGrappleMomentum)
        {
            return;
        }

        float targetSpeed = sprintHeld ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = moveDirection * targetSpeed;
        Vector3 velocityDelta = targetVelocity - currentHorizontalVelocity;
        float controlAcceleration = grappleAirControlAcceleration > 0f ? grappleAirControlAcceleration : airAcceleration;
        Vector3 clampedDelta = Vector3.ClampMagnitude(velocityDelta, controlAcceleration * Time.fixedDeltaTime);
        Vector3 projectedVelocity = currentHorizontalVelocity + clampedDelta;
        float speedCap = Mathf.Max(maxAirSpeed, grappleAirControlMaxSpeed);

        if (speedCap > 0f && projectedVelocity.magnitude > speedCap)
        {
            float currentSpeed = currentHorizontalVelocity.magnitude;
            bool steeringSameGeneralDirection = Vector3.Dot(projectedVelocity, currentHorizontalVelocity) > 0f;
            if (preserveGrappleMomentum && currentSpeed > speedCap && steeringSameGeneralDirection)
            {
                projectedVelocity = projectedVelocity.normalized * currentSpeed;
            }
            else
            {
                projectedVelocity = projectedVelocity.normalized * speedCap;
            }
        }

        body.AddForce(projectedVelocity - currentHorizontalVelocity, ForceMode.VelocityChange);
    }

    private void TryStartDash()
    {
        if (!enableDash || dashSpeed <= 0f || dashDuration <= 0f || isDashing || dashCooldownTimer > 0f)
        {
            return;
        }

        Vector3 desiredDirection = GetDesiredMoveDirection();
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            desiredDirection = GetFlatForward();
        }

        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        dashDirection = desiredDirection.normalized;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        isDashing = true;
        isWallClimbing = false;
    }

    private void ApplyDashMovement()
    {
        Vector3 velocity = BodyVelocity;
        velocity.x = dashDirection.x * dashSpeed;
        velocity.z = dashDirection.z * dashSpeed;
        SetBodyVelocity(velocity);
    }

    private void ApplyWallMotion()
    {
        isWallClimbing = false;

        if (!enableWallMovement || isGrounded || !hasWall)
        {
            return;
        }

        Vector3 velocity = BodyVelocity;
        bool wantsClimb = moveInput.y > wallClimbInputThreshold;

        if (wantsClimb)
        {
            isWallClimbing = true;
            float climbBlend = 1f - Mathf.Exp(-wallClimbAcceleration * Time.fixedDeltaTime);
            velocity.y = Mathf.Lerp(velocity.y, wallClimbSpeed, climbBlend);
        }
        else if (velocity.y < -wallSlideMaxSpeed)
        {
            velocity.y = -wallSlideMaxSpeed;
        }

        SetBodyVelocity(velocity);

        if (wallStickForce > 0f)
        {
            body.AddForce(-wallNormal * wallStickForce, ForceMode.Acceleration);
        }
    }

    private void ApplyGravity()
    {
        Vector3 velocity = BodyVelocity;

        float gravityMultiplier = 1f;

        if (velocity.y > 0f && !jumpHeld)
        {
            gravityMultiplier = lowJumpGravityMultiplier;
        }
        else if (velocity.y < 0f)
        {
            gravityMultiplier = fallGravityMultiplier;
        }

        if (isWallClimbing)
        {
            gravityMultiplier *= 0.35f;
        }

        velocity.y -= gravity * gravityMultiplier * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);

        SetBodyVelocity(velocity);
    }

    private Vector3 GetDesiredMoveDirection()
    {
        Transform reference = moveReference != null ? moveReference : transform;

        Vector3 forward = reference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }
        forward.Normalize();

        Vector3 right = reference.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = transform.right;
            right.y = 0f;
        }
        right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        if (isGrounded)
        {
            moveDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal);
            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                moveDirection.Normalize();
            }
        }

        return moveDirection;
    }

    private Vector3 GetHorizontalVelocity()
    {
        Vector3 velocity = BodyVelocity;
        velocity.y = 0f;
        return velocity;
    }

    private Vector3 GetFlatForward()
    {
        Transform reference = moveReference != null ? moveReference : transform;
        Vector3 flatForward = reference.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = transform.forward;
            flatForward.y = 0f;
        }

        return flatForward.normalized;
    }

    private void GetCapsuleWorldInfo(out Vector3 center, out float radius, out float halfHeight)
    {
        Vector3 lossyScale = transform.lossyScale;
        float maxHorizontalScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));

        center = transform.TransformPoint(capsule.center);
        radius = capsule.radius * maxHorizontalScale;
        halfHeight = Mathf.Max(capsule.height * Mathf.Abs(lossyScale.y) * 0.5f, radius);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * wallProbeHeightOffset, wallCheckDistance);

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (groundCheckDistance + 0.5f));

        if (hasWall)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up * wallProbeHeightOffset, wallNormal);
        }
    }

    private Vector3 BodyVelocity
    {
        get
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }
    }

    private void SetBodyVelocity(Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = velocity;
#else
        body.velocity = velocity;
#endif
    }

    private GrappleGunController ResolveGrappleController()
    {
        GrappleGunController[] controllers = GetComponentsInChildren<GrappleGunController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            GrappleGunController controller = controllers[i];
            if (controller != null && controller.enabled && controller.gameObject.activeInHierarchy)
            {
                return controller;
            }
        }

        return controllers.Length > 0 ? controllers[0] : null;
    }

    private bool IsAnyControllerGrappling(bool directPullOnly)
    {
        if (grappleController != null)
        {
            if (directPullOnly ? grappleController.IsDirectPulling : grappleController.IsGrappling)
            {
                return true;
            }
        }

        GrappleGunController[] controllers = GetComponentsInChildren<GrappleGunController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            GrappleGunController controller = controllers[i];
            if (controller == null || controller == grappleController)
            {
                continue;
            }

            if (directPullOnly ? controller.IsDirectPulling : controller.IsGrappling)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsGrappling => IsAnyControllerGrappling(false);
    private bool IsDirectPullingWithGrapple => IsAnyControllerGrappling(true);
}
