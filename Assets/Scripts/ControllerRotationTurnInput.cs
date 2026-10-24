using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class ControllerRotationTurnInput : MonoBehaviour, IXRInputValueReader<Vector2>
{
    public XRNode hand = XRNode.RightHand;

    [Tooltip("Controller roll, in degrees, that is ignored so the controller does not need to be held perfectly upright.")]
    [Range(0f, 89f)] public float deadzoneAngle = 15f;
    [Tooltip("Controller roll, in degrees, that counts as full deflection.")]
    [Range(1f, 90f)] public float maximumAngle = 90f;

    public Vector2 ReadValue()
    {
        TryReadValue(out Vector2 value);
        return value;
    }

    public bool TryReadValue(out Vector2 value)
    {
        value = Vector2.zero;
        if (!isActiveAndEnabled)
            return false;

        InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        if (!device.isValid ||
            (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && !tracked) ||
            !device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
        {
            return false;
        }

        float roll = Mathf.Asin(Mathf.Clamp((rotation * Vector3.right).y, -1f, 1f))
                     * Mathf.Rad2Deg;
        float deflection = Mathf.InverseLerp(deadzoneAngle,
            Mathf.Max(deadzoneAngle + 0.01f, maximumAngle), Mathf.Abs(roll));
        if (deflection <= 0f)
            return false;

        value = new Vector2(-Mathf.Sign(roll) * deflection, 0f);
        return true;
    }
}
