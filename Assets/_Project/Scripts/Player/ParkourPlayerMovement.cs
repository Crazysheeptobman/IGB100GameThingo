using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField, Min(0f)] private float groundAcceleration = 75f;
    [SerializeField, Min(0f)] private float groundDeceleration = 60f;
    [SerializeField, Min(0f)] private float airAcceleration = 28f;
    [SerializeField, Min(0f)] private float maxAirSpeed = 11f;
    [SerializeField] private bool normalizeMoveInput = true;

    [Header("Grapple Momentum")]
    [SerializeField] private GrappleGunController grappleController;
    [SerializeField, Min(0f)] private float grappleAirControlAcceleration = 16f;
    [SerializeField, Min(0f)] private float grappleAirControlMaxSpeed = 60f;
    [SerializeField] private bool preserveGrappleMomentum = true;
    [SerializeField] private bool allowAirControlDuringDirectPull = true;

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

    [Header("Rigidbody Setup")]
    [SerializeField] private bool configureRigidbodyOnAwake = true;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;

    [Header("Death")]
    [SerializeField] private float deathYLevel = -50f;

    [Header("Wind Audio")]
    [SerializeField] private AudioClip windClip;
    [SerializeField] private AudioSource windSource;
    [SerializeField, Range(0f, 1f)] private float windBaseVolume = 0.04f;
    [SerializeField, Range(0f, 1f)] private float windMaxVolume = 0.42f;
    [SerializeField, Min(0f)] private float windMinSpeed = 8f;
    [SerializeField, Min(0.01f)] private float windMaxSpeed = 40f;
    [SerializeField, Range(0f, 1f)] private float groundedWindMultiplier = 0.35f;
    [SerializeField, Range(0f, 1f)] private float airborneWindBoost = 0.15f;
    [SerializeField, Range(0f, 1f)] private float grappleWindBoost = 0.12f;
    [SerializeField, Range(0f, 1f)] private float forwardSwingWindBoost = 0.45f;
    [SerializeField, Range(0.1f, 2f)] private float windResponseCurve = 0.65f;
    [SerializeField, Min(0f)] private float windVolumeLerpSpeed = 4f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;

    public bool IsGrounded => isGrounded;
    public Vector3 CurrentVelocity => BodyVelocity;

    private Rigidbody body;
    private CapsuleCollider capsule;

    private Vector2 moveInput;
    private bool jumpHeld;

    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;

    private float timeSinceLastGrounded;
    private float jumpBufferCounter;
    private bool isRestartingScene;

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

        EnsureWindSource();
    }

    private void OnEnable()
    {
        jumpBufferCounter = 0f;
        timeSinceLastGrounded = coyoteTime;
        PlayWindLoop();
    }

    private void OnDisable()
    {
        if (windSource != null)
        {
            windSource.Stop();
        }
    }

    private void Update()
    {
        UpdateWindAudio(Time.deltaTime);

        if (isRestartingScene)
        {
            return;
        }

        ReadInput();

        // Check for manual restart (R key) - bypasses death screen
        if (WasRestartPressedThisFrame())
        {
            RestartScene();
            return;
        }

        if (transform.position.y < deathYLevel)
        {
            TriggerDeath();
            return;
        }

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        RefreshGroundState();

        if (isGrounded)
        {
            timeSinceLastGrounded = 0f;
        }
        else
        {
            timeSinceLastGrounded += Time.fixedDeltaTime;
        }

        TryConsumeJump();
        ApplyHorizontalMovement();
        ApplyGravity();
    }

    private void ReadInput()
    {
        Vector2 nextMoveInput = Vector2.zero;
        bool nextJumpHeld = false;
        bool jumpPressedThisFrame = false;
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
            nextJumpHeld |= keyboard.spaceKey.isPressed;
            jumpPressedThisFrame |= keyboard.spaceKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            readWithInputSystem = true;
#endif
        }

        if (gamepad != null)
        {
            nextMoveInput += gamepad.leftStick.ReadValue();
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
            nextJumpHeld = Input.GetButton("Jump");
            jumpPressedThisFrame = Input.GetButtonDown("Jump");
        }
#endif

        if (normalizeMoveInput && nextMoveInput.sqrMagnitude > 1f)
        {
            nextMoveInput.Normalize();
        }

        moveInput = nextMoveInput;
        jumpHeld = nextJumpHeld;

        if (jumpPressedThisFrame)
        {
            jumpBufferCounter = jumpBufferTime;
        }
    }

    private bool WasRestartPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }

    private void TriggerDeath()
    {
        Debug.Log("Player died");

        isRestartingScene = true;
        DeathScreenController.ShowDeathScreen();
    }

    private void RestartScene()
    {
        isRestartingScene = true;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
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

    private void TryConsumeJump()
    {
        if (jumpBufferCounter <= 0f)
        {
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
            Vector3 targetVelocity = moveDirection * walkSpeed;
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

        Vector3 airTargetVelocity = moveDirection * walkSpeed;
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
        if (!hasMoveInput)
        {
            return;
        }

        Vector3 controlDirection = moveDirection;
        if (TryGetActiveGrapplePoint(out Vector3 grapplePoint))
        {
            Vector3 toAnchor = grapplePoint - body.worldCenterOfMass;
            if (toAnchor.sqrMagnitude > 0.0001f)
            {
                controlDirection = Vector3.ProjectOnPlane(controlDirection, toAnchor.normalized);
            }
        }

        if (controlDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        controlDirection.Normalize();

        if (preserveGrappleMomentum)
        {
            Vector3 currentVelocity = BodyVelocity;
            if (currentVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 velocityDirection = currentVelocity.normalized;
                Vector3 alongVelocity = Vector3.Project(controlDirection, velocityDirection);
                if (Vector3.Dot(alongVelocity, velocityDirection) < 0f)
                {
                    controlDirection -= alongVelocity;
                    if (controlDirection.sqrMagnitude < 0.0001f)
                    {
                        return;
                    }

                    controlDirection.Normalize();
                }
            }
        }

        float controlAcceleration = grappleAirControlAcceleration > 0f ? grappleAirControlAcceleration : airAcceleration;
        float speedCap = Mathf.Max(maxAirSpeed, grappleAirControlMaxSpeed);
        float speedAlongControl = Vector3.Dot(BodyVelocity, controlDirection);
        float availableSpeed = speedCap > 0f ? speedCap - Mathf.Max(0f, speedAlongControl) : float.PositiveInfinity;

        if (availableSpeed <= 0f)
        {
            return;
        }

        float controlSpeed = controlAcceleration * Time.fixedDeltaTime;
        if (!float.IsPositiveInfinity(availableSpeed))
        {
            controlSpeed = Mathf.Min(controlSpeed, availableSpeed);
        }

        if (controlSpeed <= 0f)
        {
            return;
        }

        Vector3 velocityDelta = controlDirection * controlSpeed;
        if (preserveGrappleMomentum && currentHorizontalVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 horizontalDelta = velocityDelta;
            horizontalDelta.y = 0f;

            if (Vector3.Dot(currentHorizontalVelocity, horizontalDelta) < 0f)
            {
                velocityDelta -= Vector3.Project(horizontalDelta, currentHorizontalVelocity.normalized);
            }
        }

        body.AddForce(velocityDelta, ForceMode.VelocityChange);
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

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * (groundCheckDistance + 0.5f));
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

    private bool TryGetActiveGrapplePoint(out Vector3 point)
    {
        if (grappleController != null && grappleController.TryGetActiveGrapplePoint(out point))
        {
            return true;
        }

        GrappleGunController[] controllers = GetComponentsInChildren<GrappleGunController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            GrappleGunController controller = controllers[i];
            if (controller == null || controller == grappleController)
            {
                continue;
            }

            if (controller.TryGetActiveGrapplePoint(out point))
            {
                return true;
            }
        }

        point = default;
        return false;
    }

    private bool IsGrappling => IsAnyControllerGrappling(false);
    private bool IsDirectPullingWithGrapple => IsAnyControllerGrappling(true);

    private void EnsureWindSource()
    {
        if (windClip == null)
        {
            return;
        }

        if (windSource == null)
        {
            windSource = gameObject.AddComponent<AudioSource>();
        }

        windSource.playOnAwake = false;
        windSource.loop = true;
        windSource.spatialBlend = 0f;
        windSource.dopplerLevel = 0f;
        windSource.pitch = 1f;
        windSource.clip = windClip;
    }

    private void PlayWindLoop()
    {
        if (windClip == null)
        {
            return;
        }

        EnsureWindSource();

        if (windSource == null)
        {
            return;
        }

        windSource.clip = windClip;
        windSource.volume = windBaseVolume;

        if (!windSource.isPlaying)
        {
            windSource.Play();
        }
    }

    private void UpdateWindAudio(float deltaTime)
    {
        if (windClip == null || body == null)
        {
            return;
        }

        EnsureWindSource();

        if (windSource == null)
        {
            return;
        }

        if (!windSource.isPlaying)
        {
            windSource.Play();
        }

        Vector3 velocity = BodyVelocity;
        bool grappling = IsGrappling;
        float maxSpeed = Mathf.Max(windMinSpeed + 0.01f, windMaxSpeed);
        float speedAmount = Mathf.InverseLerp(windMinSpeed, maxSpeed, velocity.magnitude);

        if (grappling)
        {
            Vector3 forward = GetFlatForward();
            float forwardSpeed = Mathf.Max(0f, Vector3.Dot(velocity, forward));
            float forwardAmount = Mathf.InverseLerp(windMinSpeed, maxSpeed, forwardSpeed);
            speedAmount = Mathf.Max(speedAmount, Mathf.Clamp01(forwardAmount + forwardSwingWindBoost));
        }

        speedAmount = Mathf.Pow(Mathf.Clamp01(speedAmount), windResponseCurve);

        if (isGrounded && !grappling)
        {
            speedAmount *= groundedWindMultiplier;
        }
        else
        {
            speedAmount = Mathf.Clamp01(speedAmount + airborneWindBoost);
        }

        if (grappling)
        {
            speedAmount = Mathf.Clamp01(speedAmount + grappleWindBoost);
        }

        float targetVolume = Mathf.Lerp(windBaseVolume, windMaxVolume, speedAmount);
        float blend = deltaTime > 0f ? 1f - Mathf.Exp(-windVolumeLerpSpeed * deltaTime) : 1f;
        windSource.volume = Mathf.Lerp(windSource.volume, targetVolume, blend);
    }
}