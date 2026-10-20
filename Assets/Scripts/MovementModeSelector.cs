using UnityEngine;

public class MovementModeSelector : MonoBehaviour
{
    public enum MovementMode
    {
        [InspectorName("Use Rotation")]
        UseRotation,
        [InspectorName("Use Joystick")]
        UseJoystick
    }

    public MovementMode movementMode = MovementMode.UseJoystick;
}
