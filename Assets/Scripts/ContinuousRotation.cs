using UnityEngine;
using Unity.XR.CoreUtils;

// Gain arithmetic and logging restored from Jaz's 8e83415 (pre-XR-rig version).
// The pose adapter below replaces OVRCameraRig.UpdatedAnchors for XROrigin.
public class ContinuousRotation : MonoBehaviour
{
    public XROrigin xrOrigin;
    [Tooltip("The camera-only parent pivot, not the locomotion rig.")]
    public Transform rotationPivot;
    public Evaluation evaluation;

    private bool e_initialized;
    private bool initialized;
    private Quaternion basePivotRotation;
    private Vector3 basePivotPosition;

    private void OnEnable()
    {
        if (initialized) ResetRotationReference();
    }

    private void Start()
    {
        if (!HasReferences())
        {
            Debug.LogError("ContinuousRotation: Assign XR Origin, camera rotation pivot, and Evaluation.", this);
            enabled = false;
            return;
        }
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        ResetRotationReference();
        initialized = true;
    }

    private bool HasReferences() => xrOrigin != null && xrOrigin.Camera != null &&
        rotationPivot != null && evaluation != null;

    private void LateUpdate()
    {
        if (!initialized || !HasReferences()) return;
        Transform head = xrOrigin.Camera.transform;
        Vector3 physicalPosition = head.localPosition;
        // Reconstruct the uncorrected world yaw. Reading head.eulerAngles here
        // would feed last frame's pivot correction back into the gain equation.
        Quaternion parentRotation = rotationPivot.parent != null
            ? rotationPivot.parent.rotation : Quaternion.identity;
        evaluation.outsideYaw = (parentRotation * basePivotRotation * head.localRotation).eulerAngles.y * Mathf.Deg2Rad;
        evaluation.outsidePos = new Vector2(physicalPosition.x, physicalPosition.z);
        evaluation.insideUpdate = true;
        try
        {
            UpdateInternal();
            Quaternion correction = Quaternion.Euler(0f,
                (evaluation.offsetYaw + evaluation.resetYaw) * Mathf.Rad2Deg, 0f);
            Quaternion reset = Quaternion.Euler(0f, evaluation.resetYaw * Mathf.Rad2Deg, 0f);
            Vector3 correctedPosition = reset * (physicalPosition +
                new Vector3(evaluation.offsetPos.x + evaluation.resetPos.x, 0f,
                    evaluation.offsetPos.y + evaluation.resetPos.y));
            rotationPivot.localRotation = basePivotRotation * correction;
            // Original OVR code rotates the eye orientation separately from
            // its position. Counteract the pivot's orbit to preserve that behavior.
            rotationPivot.localPosition = basePivotPosition + basePivotRotation *
                (correctedPosition - correction * physicalPosition);
        }
        finally { evaluation.insideUpdate = false; }
    }

    public void ResetRotationReference()
    {
        if (!HasReferences()) return;
        basePivotRotation = rotationPivot.localRotation;
        basePivotPosition = rotationPivot.localPosition;
        e_initialized = false;
        evaluation.reset();
    }

    const double DTOR = Mathf.PI / 180.0;
    //NOTE: Not very efficient, but numerically stable
    float reducePeriodic(float p)
    {
        // Reduces periodic p from real number space to [0, 2PI[
        const float PI2 = 2 * (float)Mathf.PI;
        while (p >= PI2)
            p -= PI2;
        while (p < 0)
            p += PI2;
        return p;
    }

    //NOTE: Not very efficient, but numerically stable
    float periodicMinusPeriodic(float p1, float p2)
    {
        p1 = reducePeriodic(p1);
        p2 = reducePeriodic(p2);

        // p1 in [0, 2PI[
        // p2 in [0, 2PI[
        // result in [-PI, PI[

        // We make the assumption that there is only a slight difference.
        // This means that we can be in one of the following four cases:
        //
        //        [0 . . . . . . . .2PI][0 . . . . . . . .2PI][0 . . . . . . . .2PI]
        // Case A:  p2            p1      p2            p1      p2            p1
        // Case B:  p1            p2      p1            p2      p1            p2
        // Case C:      p1    p2              p1    p2              p1    p2
        // Case C':     p2    p1              p2    p1              p2    p1

        const float PI2 = 2 * (float)Mathf.PI;
        float caseA = p1 - (p2 + PI2);
        float caseB = p1 - (p2 - PI2);
        float caseC = p1 - p2;
        float absA = Mathf.Abs(caseA);
        float absB = Mathf.Abs(caseB);
        float absC = Mathf.Abs(caseC);

        float tmp = 0;
        if (absA < absB)
        {
            if (absA < absC)
            {
                // a < b, a < c
                tmp = caseA;
            }
            else
            {
                // c <= a < b
                tmp = caseC;
            }
        }
        else
        {
            if (absC < absB)
            {
                // c < b <= a
                tmp = caseC;
            }
            else
            {
                // b <= a, b <= c
                tmp = caseB;
            }
        }
        return tmp;
    }

    private void UpdateInternal()
    {
        float gain = evaluation.RotationGain;
        float currentYaw = evaluation.getYaw();

        // Capture the starting yaw once
        if (!e_initialized)
        {
            evaluation.start_yaw = currentYaw;
            evaluation.start_p = evaluation.getPos();
            e_initialized = true;
        }

        // Physical rotation since the starting yaw
        float diff = periodicMinusPeriodic(
            currentYaw,
            evaluation.start_yaw
        );

        // Apply only the additional rotation correction
        evaluation.offsetYaw = diff * (gain - 1f);

        // Log physical and virtual rotation
        if (Application.isPlaying) evaluation.writeLog(
            0, 0, 0, 0,
            diff / (float)DTOR,
            diff * gain / (float)DTOR
        );
    }
}
