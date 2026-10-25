using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public class MovementModeSelector : MonoBehaviour
{
    public enum MovementMode
    {
        [InspectorName("Use Rotation")]
        UseRotation = 0,
        [InspectorName("Use Joystick")]
        UseJoystick = 1,
        [InspectorName("Continuous Rotation")]
        ContinuousRotation = 2
    }

    public MovementMode movementMode = MovementMode.UseJoystick;

    public ContinuousTurnProvider turnProvider;
    public ControllerRotationTurnInput leftHandRotationInput;
    public ControllerRotationTurnInput rightHandRotationInput;
    public RedirectionController redirectionController;
    public ContinuousRotation continuousRotation;

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
        if (redirectionController == null)
            redirectionController = GetComponent<RedirectionController>();
        if (continuousRotation == null)
            continuousRotation = GetComponent<ContinuousRotation>();

        ApplyMode();
    }

    private void OnDisable()
    {
        if (turnProvider == null)
            return;

        turnProvider.leftHandTurnInput.bypass = null;
        turnProvider.rightHandTurnInput.bypass = null;

        if (appliedMode == MovementMode.ContinuousRotation)
        {
            if (continuousRotation != null)
                continuousRotation.enabled = false;
            if (redirectionController != null)
                redirectionController.enabled = true;
            turnProvider.enabled = true;
        }
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
        bool useContinuous = movementMode == MovementMode.ContinuousRotation;
        if (useContinuous && continuousRotation == null)
        {
            Debug.LogError("MovementModeSelector: Assign a Continuous Rotation component.");
            return;
        }

        appliedMode = movementMode;

        // The original head-gain system owns the pivot exclusively. Stop the
        // manual provider (including release coasting) before enabling it.
        if (continuousRotation != null && !useContinuous)
            continuousRotation.enabled = false;
        if (redirectionController != null)
            redirectionController.enabled = !useContinuous;
        turnProvider.enabled = !useContinuous;

        bool useRotation = movementMode == MovementMode.UseRotation;
        turnProvider.leftHandTurnInput.bypass =
            useRotation ? leftHandRotationInput : null;
        turnProvider.rightHandTurnInput.bypass =
            useRotation ? rightHandRotationInput : null;

        if (useContinuous)
            continuousRotation.enabled = true;
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
