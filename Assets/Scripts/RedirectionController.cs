
using UnityEngine;
using Unity.XR.CoreUtils;

public class RedirectionController : MonoBehaviour
{
    [Header("XR References")]
    public XROrigin xrOrigin;
    public Transform rotationPivot;
    public Evaluation evaluation;

    private bool initialized = false;
    private float previousHeadYaw;
    private float accumulatedCorrection;

    private void OnEnable()
    {
        if (initialized)
            ResetRotationReference();
    }

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

        ResetRotationReference();

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
        // Only amplify physical head motion since the previous frame.
        // Joystick turns change the rig, not the camera's tracked local yaw.
        float headDelta = periodicMinusPeriodic(
            currentYaw,
            previousHeadYaw
        );
        previousHeadYaw = currentYaw;

        float correction = headDelta * (evaluation.RotationGain - 1f);

        // Compose with existing joystick rotation, around the head so an
        // off-center player does not orbit the rig origin.
        rotationPivot.RotateAround(
            xrOrigin.Camera.transform.position,
            Vector3.up,
            correction * Mathf.Rad2Deg
        );
        accumulatedCorrection = periodicMinusPeriodic(
            accumulatedCorrection + correction, 0f);

        // Keep the Evaluation correction fields up to date.
        evaluation.reset();
        evaluation.setYawOffset(currentYaw + accumulatedCorrection);
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

        previousHeadYaw = evaluation.outsideYaw;
        accumulatedCorrection = 0f;
        evaluation.reset();
    }
}
