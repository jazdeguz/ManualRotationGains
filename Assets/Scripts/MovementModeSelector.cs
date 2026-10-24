using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

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

    public ContinuousTurnProvider turnProvider;
    public ControllerRotationTurnInput leftHandRotationInput;
    public ControllerRotationTurnInput rightHandRotationInput;

    private MovementMode appliedMode;

    private void OnEnable()
    {
        if (turnProvider == null)
            turnProvider = GetComponent<ContinuousTurnProvider>();

        if (turnProvider == null)
        {
            Debug.LogError(
                "MovementModeSelector: Assign a Continuous Turn Provider."
            );

            enabled = false;
            return;
        }

        if (leftHandRotationInput == null)
            leftHandRotationInput = GetRotationInput(XRNode.LeftHand);
        if (rightHandRotationInput == null)
            rightHandRotationInput = GetRotationInput(XRNode.RightHand);

        ApplyMode();
    }

    private void OnDisable()
    {
        if (turnProvider == null)
            return;

        turnProvider.leftHandTurnInput.bypass = null;
        turnProvider.rightHandTurnInput.bypass = null;
    }

    private void Update()
    {
        if (movementMode != appliedMode)
            ApplyMode();
    }

    public void SetMovementMode(MovementMode mode)
    {
        movementMode = mode;

        if (isActiveAndEnabled)
            ApplyMode();
    }

    private void ApplyMode()
    {
        appliedMode = movementMode;

        bool useRotation = movementMode == MovementMode.UseRotation;
        turnProvider.leftHandTurnInput.bypass =
            useRotation ? leftHandRotationInput : null;
        turnProvider.rightHandTurnInput.bypass =
            useRotation ? rightHandRotationInput : null;
    }

    private ControllerRotationTurnInput GetRotationInput(XRNode hand)
    {
        foreach (ControllerRotationTurnInput input in
                 GetComponents<ControllerRotationTurnInput>())
        {
            if (input.hand == hand)
                return input;
        }

        ControllerRotationTurnInput created =
            gameObject.AddComponent<ControllerRotationTurnInput>();
        created.hand = hand;
        return created;
    }
}
