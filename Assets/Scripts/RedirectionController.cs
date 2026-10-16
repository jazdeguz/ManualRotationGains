using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public Evaluation evaluation;
    public GameObject cameraRig;
    
    private bool e_initialized = false;
    float c_yawOffset = 0;
    float e_maxAngle = 0;
    float e_maxDistance = 0;

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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void OnEnable() => cameraRig.GetComponent<OVRCameraRig>().UpdatedAnchors += OnUpdatedAnchors;
    private void OnDisable() => cameraRig.GetComponent<OVRCameraRig>().UpdatedAnchors -= OnUpdatedAnchors;
    
    private void OnUpdatedAnchors(OVRCameraRig rig)
    {
        Transform centerEye = rig.centerEyeAnchor;
        Transform leftEye = rig.leftEyeAnchor;
        Transform rightEye = rig.rightEyeAnchor;
        Vector3 leftOffset = leftEye.localPosition - centerEye.localPosition;
        Vector3 rightOffset = rightEye.localPosition - centerEye.localPosition;

        evaluation.outsideYaw = centerEye.transform.eulerAngles.y * Mathf.Deg2Rad;
        evaluation.outsidePos = new Vector2(centerEye.localPosition.x, centerEye.localPosition.z);
        evaluation.insideUpdate = true;

        UpdateInternal();

        Quaternion correctionRot = Quaternion.Euler(new Vector3(0, (evaluation.offsetYaw + evaluation.resetYaw) * Mathf.Rad2Deg, 0));
        centerEye.localRotation = correctionRot * centerEye.localRotation;

        correctionRot = Quaternion.Euler(new Vector3(0, evaluation.resetYaw * Mathf.Rad2Deg, 0));
        Vector3 centerPosOld = centerEye.localPosition;
        Vector3 centerPosNew = centerPosOld + new Vector3(evaluation.offsetPos.x, 0, evaluation.offsetPos.y) + new Vector3(evaluation.resetPos.x, 0, evaluation.resetPos.y);
        centerEye.localPosition = correctionRot * centerPosNew;

        //if (leftEye != null)
        //{
        //    leftEye.localPosition = centerPosNew + (correctionRot * leftOffset);
        //    leftEye.localRotation = correctionRot * leftEye.localRotation;
        //}
        //if (rightEye != null)
        //{
        //    rightEye.localPosition = centerPosNew + (correctionRot * rightOffset);
        //    rightEye.localRotation = correctionRot * rightEye.localRotation;
        //}

        evaluation.insideUpdate = false;
        Debug.Log("pos(" + evaluation.getPos().x + ", " + evaluation.getPos().y + ") -- offsetPos(" + evaluation.offsetPos.x + ", " + evaluation.offsetPos.y + ")");

        //        Debug.Log("yaw(" + evaluation.getYaw() / DTOR + ") -- yaw(" + evaluation.start_yaw / DTOR + ") -- offset(" + evaluation.offsetYaw / DTOR + ") -- reset(" + evaluation.resetYaw / DTOR + ")");
    }

    // Update is called once per frame
    void Update()
    {
        
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
        evaluation.writeLog(
            0, 0, 0, 0,
            diff / (float)DTOR,
            diff * gain / (float)DTOR
        );
    }
}
