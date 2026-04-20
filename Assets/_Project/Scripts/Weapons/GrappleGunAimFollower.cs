using UnityEngine;

[DefaultExecutionOrder(200)]
public class GrappleGunAimFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform gunPivot;
    [SerializeField] private Transform aimOrigin;

    [Header("Viewmodel Follow")]
    [SerializeField] private bool captureStartingOffset = true;
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(0.3f, -0.25f, 0.6f);
    [SerializeField] private Vector3 modelRotationOffsetEuler;
    [SerializeField] private bool smoothPosition = false;
    [SerializeField] private bool smoothRotation = true;
    [SerializeField] private bool positionSmoothingInCameraSpace = true;
    [SerializeField, Min(0f)] private float positionLerpSpeed = 24f;
    [SerializeField, Min(0f)] private float rotationLerpSpeed = 18f;
    [SerializeField] private bool applyFastTurnLag = false;
    [SerializeField, Min(0f)] private float fastTurnLagThreshold = 220f;
    [SerializeField, Range(0f, 0.95f)] private float fastTurnLagStrength = 0.45f;

    [Header("Aim Alignment")]
    [SerializeField] private bool alignToLookPoint = true;
    [SerializeField] private Vector3 gunForwardAxisLocal = Vector3.forward;
    [SerializeField, Range(0f, 1f)] private float aimAlignmentStrength = 1f;
    [SerializeField, Range(-1f, 1f)] private float minAimForwardDot = 0.05f;
    [SerializeField, Min(1f)] private float maxAimDistance = 250f;
    [SerializeField] private LayerMask aimMask = ~0;

    private Quaternion lastCameraRotation;
    private bool hasCapturedStartOffset;
    private Vector3 cachedAimOriginLocalPosition;
    private bool hasCachedAimOriginLocalPosition;

    private void Reset()
    {
        gunPivot = transform;

        if (Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }
    }

    private void Awake()
    {
        if (gunPivot == null)
        {
            gunPivot = transform;
        }

        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        if (aimOrigin == null)
        {
            aimOrigin = gunPivot;
        }

        CacheAimOriginLocalPosition();
        CacheStartOffsetIfNeeded();
        if (playerCamera != null)
        {
            lastCameraRotation = playerCamera.rotation;
        }
    }

    private void LateUpdate()
    {
        if (playerCamera == null || gunPivot == null)
        {
            return;
        }

        CacheStartOffsetIfNeeded();
        CacheAimOriginLocalPosition();

        Vector3 desiredPosition = playerCamera.TransformPoint(cameraLocalOffset);
        Quaternion desiredRotation = playerCamera.rotation * Quaternion.Euler(modelRotationOffsetEuler);

        if (alignToLookPoint && aimAlignmentStrength > 0f)
        {
            Vector3 targetPoint = GetAimTargetPoint();
            Vector3 predictedAimOriginPosition = GetPredictedAimOriginPosition(desiredPosition, desiredRotation);
            Vector3 aimDirection = targetPoint - predictedAimOriginPosition;

            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 aimDirectionNormalized = aimDirection.normalized;
                // Avoid 180-degree flips when the hit point ends up behind the muzzle
                // (for example, when the camera ray hits a very close surface).
                if (Vector3.Dot(aimDirectionNormalized, playerCamera.forward) < minAimForwardDot)
                {
                    aimDirectionNormalized = playerCamera.forward;
                }

                Vector3 forwardAxis = gunForwardAxisLocal.sqrMagnitude < 0.0001f
                    ? Vector3.forward
                    : gunForwardAxisLocal.normalized;
                Vector3 currentForward = desiredRotation * forwardAxis;
                Quaternion correction = SafeFromToRotation(currentForward, aimDirectionNormalized, playerCamera.up);
                Quaternion alignedRotation = correction * desiredRotation;
                desiredRotation = Quaternion.Slerp(desiredRotation, alignedRotation, aimAlignmentStrength);
            }
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        float lagMultiplier = 1f;
        if (applyFastTurnLag)
        {
            float cameraAngularSpeed = Quaternion.Angle(lastCameraRotation, playerCamera.rotation) / deltaTime;
            float fastTurnRatio = fastTurnLagThreshold <= 0f
                ? 0f
                : Mathf.Clamp01(cameraAngularSpeed / fastTurnLagThreshold);
            lagMultiplier = Mathf.Lerp(1f, 1f - fastTurnLagStrength, fastTurnRatio);
        }

        float effectivePositionSpeed = positionLerpSpeed * lagMultiplier;
        float effectiveRotationSpeed = rotationLerpSpeed * lagMultiplier;

        if (!smoothPosition || effectivePositionSpeed <= 0f)
        {
            gunPivot.position = desiredPosition;
        }
        else
        {
            float positionBlend = 1f - Mathf.Exp(-effectivePositionSpeed * deltaTime);
            if (positionSmoothingInCameraSpace)
            {
                Vector3 currentLocal = playerCamera.InverseTransformPoint(gunPivot.position);
                Vector3 smoothedLocal = Vector3.Lerp(currentLocal, cameraLocalOffset, positionBlend);
                gunPivot.position = playerCamera.TransformPoint(smoothedLocal);
            }
            else
            {
                gunPivot.position = Vector3.Lerp(gunPivot.position, desiredPosition, positionBlend);
            }
        }

        if (!smoothRotation || effectiveRotationSpeed <= 0f)
        {
            gunPivot.rotation = desiredRotation;
        }
        else
        {
            float rotationBlend = 1f - Mathf.Exp(-effectiveRotationSpeed * deltaTime);
            gunPivot.rotation = Quaternion.Slerp(gunPivot.rotation, desiredRotation, rotationBlend);
        }

        lastCameraRotation = playerCamera.rotation;
    }

    private Vector3 GetAimTargetPoint()
    {
        Ray aimRay = new Ray(playerCamera.position, playerCamera.forward);
        if (Physics.Raycast(aimRay, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return aimRay.GetPoint(maxAimDistance);
    }

    private void CacheStartOffsetIfNeeded()
    {
        if (!captureStartingOffset || hasCapturedStartOffset || playerCamera == null || gunPivot == null)
        {
            return;
        }

        cameraLocalOffset = playerCamera.InverseTransformPoint(gunPivot.position);
        Quaternion localRotationOffset = Quaternion.Inverse(playerCamera.rotation) * gunPivot.rotation;
        modelRotationOffsetEuler = localRotationOffset.eulerAngles;
        hasCapturedStartOffset = true;
    }

    private void CacheAimOriginLocalPosition()
    {
        if (hasCachedAimOriginLocalPosition || aimOrigin == null || gunPivot == null)
        {
            return;
        }

        if (aimOrigin == gunPivot)
        {
            cachedAimOriginLocalPosition = Vector3.zero;
        }
        else
        {
            cachedAimOriginLocalPosition = gunPivot.InverseTransformPoint(aimOrigin.position);
        }

        hasCachedAimOriginLocalPosition = true;
    }

    private Vector3 GetPredictedAimOriginPosition(Vector3 predictedPivotPosition, Quaternion predictedPivotRotation)
    {
        if (!hasCachedAimOriginLocalPosition)
        {
            return predictedPivotPosition;
        }

        return predictedPivotPosition + predictedPivotRotation * cachedAimOriginLocalPosition;
    }

    private static Quaternion SafeFromToRotation(Vector3 from, Vector3 to, Vector3 fallbackAxis)
    {
        Vector3 fromNormalized = from.normalized;
        Vector3 toNormalized = to.normalized;
        float dot = Vector3.Dot(fromNormalized, toNormalized);

        if (dot > 0.9999f)
        {
            return Quaternion.identity;
        }

        if (dot < -0.9999f)
        {
            Vector3 axis = fallbackAxis.sqrMagnitude > 0.0001f ? fallbackAxis.normalized : Vector3.up;
            if (Mathf.Abs(Vector3.Dot(axis, fromNormalized)) > 0.99f)
            {
                axis = Vector3.Cross(fromNormalized, Vector3.right);
                if (axis.sqrMagnitude < 0.0001f)
                {
                    axis = Vector3.Cross(fromNormalized, Vector3.up);
                }

                axis.Normalize();
            }

            return Quaternion.AngleAxis(180f, axis);
        }

        return Quaternion.FromToRotation(fromNormalized, toNormalized);
    }
}
