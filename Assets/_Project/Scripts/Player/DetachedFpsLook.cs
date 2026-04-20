using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-100)]
public class DetachedFpsLook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerYawRoot;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform cameraFollowTarget;

    [Header("Look")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float gamepadSensitivity = 180f;
    [SerializeField] private bool invertY;
    [SerializeField, Range(-89f, 0f)] private float minPitch = -80f;
    [SerializeField, Range(0f, 89f)] private float maxPitch = 85f;

    [Header("Camera Follow")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private bool smoothCameraFollow = false;
    [SerializeField, Min(0f)] private float followLerpSpeed = 30f;

    [Header("Speed FOV")]
    [SerializeField] private bool enableSpeedFov = true;
    [SerializeField] private Rigidbody speedSourceBody;
    [SerializeField, Min(1f)] private float baseFieldOfView = 71f;
    [SerializeField, Min(1f)] private float maxFieldOfView = 95f;
    [SerializeField, Min(0.1f)] private float speedForMaxFov = 40f;
    [SerializeField, Min(0f)] private float fovLerpSpeed = 7f;
    [SerializeField] private bool ignoreVerticalSpeedForFov = true;

    [Header("Cursor")]
    [SerializeField] private bool lockCursor = true;
    [SerializeField] private bool hideCursor = true;

    private float yaw;
    private float pitch;
    private Camera playerCameraComponent;

    private void Reset()
    {
        playerYawRoot = transform;
        cameraFollowTarget = transform;

        if (Camera.main != null)
        {
            playerCamera = Camera.main.transform;
            baseFieldOfView = Camera.main.fieldOfView;
        }
    }

    private void Awake()
    {
        if (playerYawRoot == null)
        {
            playerYawRoot = transform;
        }

        if (cameraFollowTarget == null)
        {
            cameraFollowTarget = transform;
        }

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (speedSourceBody == null && playerYawRoot != null)
        {
            speedSourceBody = playerYawRoot.GetComponent<Rigidbody>();
        }

        if (speedSourceBody == null)
        {
            speedSourceBody = GetComponent<Rigidbody>();
        }

        playerCameraComponent = playerCamera != null ? playerCamera.GetComponent<Camera>() : null;
        if (playerCameraComponent != null)
        {
            if (baseFieldOfView <= 0f)
            {
                baseFieldOfView = playerCameraComponent.fieldOfView;
            }

            if (maxFieldOfView < baseFieldOfView)
            {
                maxFieldOfView = baseFieldOfView;
            }
        }

        yaw = playerYawRoot.rotation.eulerAngles.y;

        if (playerCamera != null)
        {
            pitch = NormalizeAngle(playerCamera.rotation.eulerAngles.x);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    private void OnEnable()
    {
        ApplyCursorState();

        if (playerCameraComponent != null)
        {
            playerCameraComponent.fieldOfView = baseFieldOfView;
        }
    }

    private void OnDisable()
    {
        if (!lockCursor)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (playerCamera == null)
        {
            return;
        }

        Vector2 lookDeltaDegrees = ReadLookInputDegrees();
        yaw += lookDeltaDegrees.x;

        float pitchDelta = invertY ? lookDeltaDegrees.y : -lookDeltaDegrees.y;
        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);

        playerYawRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
        playerCamera.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void LateUpdate()
    {
        if (playerCamera == null || cameraFollowTarget == null)
        {
            return;
        }

        Vector3 targetPosition = cameraFollowTarget.TransformPoint(cameraOffset);

        if (!smoothCameraFollow || followLerpSpeed <= 0f)
        {
            playerCamera.position = targetPosition;
            UpdateDynamicFov();
            return;
        }

        float blend = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
        playerCamera.position = Vector3.Lerp(playerCamera.position, targetPosition, blend);

        UpdateDynamicFov();
    }

    private Vector2 ReadLookInputDegrees()
    {
        Vector2 look = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            look += Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        if (Gamepad.current != null)
        {
            look += Gamepad.current.rightStick.ReadValue() * (gamepadSensitivity * Time.deltaTime);
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (look == Vector2.zero)
        {
            look += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * mouseSensitivity;
        }
#endif

        return look;
    }

    private void ApplyCursorState()
    {
        if (!lockCursor)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = !hideCursor;
    }

    private void UpdateDynamicFov()
    {
        if (!enableSpeedFov || playerCameraComponent == null)
        {
            return;
        }

        float speed = 0f;
        if (speedSourceBody != null)
        {
            Vector3 velocity = BodyVelocity(speedSourceBody);
            if (ignoreVerticalSpeedForFov)
            {
                velocity.y = 0f;
            }

            speed = velocity.magnitude;
        }

        float speedT = speedForMaxFov <= 0f ? 1f : Mathf.Clamp01(speed / speedForMaxFov);
        float targetFov = Mathf.Lerp(baseFieldOfView, maxFieldOfView, speedT);

        if (fovLerpSpeed <= 0f)
        {
            playerCameraComponent.fieldOfView = targetFov;
            return;
        }

        float blend = 1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime);
        playerCameraComponent.fieldOfView = Mathf.Lerp(playerCameraComponent.fieldOfView, targetFov, blend);
    }

    private static Vector3 BodyVelocity(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        return body.linearVelocity;
#else
        return body.velocity;
#endif
    }

    private static float NormalizeAngle(float angleDegrees)
    {
        while (angleDegrees > 180f)
        {
            angleDegrees -= 360f;
        }

        return angleDegrees;
    }
}
