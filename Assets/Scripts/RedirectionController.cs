
using UnityEngine;
using Unity.XR.CoreUtils;

public class RedirectionController : MonoBehaviour
{
    [Header("XR References")]
    public XROrigin xrOrigin;
    public Transform rotationPivot;
    public Evaluation evaluation;

    private bool initialized = false;
    private Quaternion basePivotRotation;

    private void Start()
    {
        if (xrOrigin == null ||
            xrOrigin.Camera == null ||
            rotationPivot == null ||
            evaluation == null)
        {
            Debug.LogError(
                "RedirectionController: Assign XR Origin, Rotation Pivot, and Evaluation."
            );

            enabled = false;
            return;
        }

        basePivotRotation = rotationPivot.localRotation;

        // Initialize the starting yaw in radians.
        evaluation.outsideYaw = GetCameraYawRadians();
        evaluation.start_yaw = evaluation.getYaw();

        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized ||
            xrOrigin == null ||
            xrOrigin.Camera == null ||
            rotationPivot == null ||
            evaluation == null)
        {
            return;
        }

        // Current tracked head yaw, in radians.
        float currentYaw = GetCameraYawRadians();
        evaluation.outsideYaw = currentYaw;

        UpdateRotationGain(currentYaw);
    }

    private float GetCameraYawRadians()
    {
        return xrOrigin.Camera.transform.localEulerAngles.y
               * Mathf.Deg2Rad;
    }

    private void UpdateRotationGain(float currentYaw)
    {
        // Calculate head rotation relative to the starting reference.
        float diff = periodicMinusPeriodic(
            currentYaw,
            evaluation.start_yaw
        );

        // Preserve the original gain equation.
        float yaw = evaluation.start_yaw
                    + diff * evaluation.RotationGain;

        // Extra virtual rotation beyond the physical head rotation.
        float correction = periodicMinusPeriodic(
            yaw,
            currentYaw
        );

        // Apply the extra rotation to the parent pivot.
        rotationPivot.localRotation =
            basePivotRotation *
            Quaternion.Euler(
                0f,
                correction * Mathf.Rad2Deg,
                0f
            );

        // Keep the Evaluation correction fields up to date.
        evaluation.reset();
        evaluation.setYawOffset(yaw);
    }

    private float periodicMinusPeriodic(float a, float b)
    {
        float diff = a - b;

        while (diff > Mathf.PI)
            diff -= 2f * Mathf.PI;

        while (diff < -Mathf.PI)
            diff += 2f * Mathf.PI;

        return diff;
    }

    public void ResetRotationReference()
    {
        if (xrOrigin == null ||
            xrOrigin.Camera == null ||
            rotationPivot == null ||
            evaluation == null)
        {
            return;
        }

        evaluation.outsideYaw = GetCameraYawRadians();
        evaluation.start_yaw = evaluation.getYaw();

        basePivotRotation = rotationPivot.localRotation;
    }
}
