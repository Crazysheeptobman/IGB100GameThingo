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
    [SerializeField, Min(0f)] private float followLerpSpeed = 30f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursor = true;
    [SerializeField] private bool hideCursor = true;

    private float yaw;
    private float pitch;

    private void Reset()
    {
        playerYawRoot = transform;
        cameraFollowTarget = transform;

        if (Camera.main != null)
        {
            playerCamera = Camera.main.transform;
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

        if (followLerpSpeed <= 0f)
        {
            playerCamera.position = targetPosition;
            return;
        }

        float blend = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
        playerCamera.position = Vector3.Lerp(playerCamera.position, targetPosition, blend);
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

    private static float NormalizeAngle(float angleDegrees)
    {
        while (angleDegrees > 180f)
        {
            angleDegrees -= 360f;
        }

        return angleDegrees;
    }
}
