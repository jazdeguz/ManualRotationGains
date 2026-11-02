using UnityEngine;
using UnityEngine.XR;

// Read buttons before locomotion queues this frame's turn.
[DefaultExecutionOrder(-220)]
[RequireComponent(typeof(AcceleratingTurnProvider), typeof(MovementModeSelector), typeof(JoystickTurnHaptics))]
public sealed class ManualTurnAdjustment : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float step = 0.5f;
    [SerializeField, Min(0.01f)] private float minimum = 5f / 15f;
    [SerializeField, Min(1f)] private float maximum = 8f;

    private AcceleratingTurnProvider provider;
    private MovementModeSelector selector;
    private JoystickTurnHaptics haptics;
    private readonly ManualTurnButtonState buttons = new ManualTurnButtonState();
    private bool focused = true;
    private bool paused;
    private int unavailable;

    private void Awake()
    {
        provider = GetComponent<AcceleratingTurnProvider>();
        selector = GetComponent<MovementModeSelector>();
        haptics = GetComponent<JoystickTurnHaptics>();
        float lower = Mathf.Max(0.01f, minimum);
        provider.SetMaximumMultiplier(Mathf.Clamp(
            MovementSessionSettings.ManualTurnMaximum ??
                MovementSessionSettings.StartingTurnDegreesPerSecond / Mathf.Max(0.01f, Mathf.Abs(provider.turnSpeed)),
            lower, Mathf.Max(lower, maximum)));
    }

    private void Update()
    {
        if (!focused || paused || !selector.isActiveAndEnabled ||
            selector.movementMode == MovementModeSelector.MovementMode.ContinuousRotation)
        {
            ResetInput();
            return;
        }

        unavailable = 0;
        int pressed = ReadHand(XRNode.LeftHand, 1, 2);
        int direction = buttons.Read(pressed, out int hands, unavailable);
        if (direction == 0) return;
        float next = ManualTurnButtonState.Adjust(provider.maximumMultiplier, direction, step, minimum, maximum);
        if (Mathf.Approximately(next, provider.maximumMultiplier)) return;
        provider.SetMaximumMultiplier(next);
        MovementSessionSettings.SetManualTurnMaximum(next);
        if ((hands & 1) != 0) haptics.QueueAdjustment(XRNode.LeftHand, direction > 0);
    }

    private int ReadHand(XRNode hand, int primaryMask, int secondaryMask)
    {
        var device = InputDevices.GetDeviceAtXRNode(hand);
        if (!device.isValid ||
            !device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary) ||
            !device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary))
        {
            unavailable |= primaryMask | secondaryMask;
            // Keep unavailable buttons disarmed until an actual release is observed.
            return 0;
        }
        return (primary ? primaryMask : 0) | (secondary ? secondaryMask : 0);
    }

    private void OnApplicationFocus(bool value) { focused = value; ResetInput(); }
    private void OnApplicationPause(bool value) { paused = value; ResetInput(); }
    private void OnDisable() { ResetInput(); }
    private void ResetInput()
    {
        buttons.Reset();
        if (haptics != null) haptics.CancelAdjustment();
    }
}

/// <summary>X/Y button edges, with release required after startup or interruption.</summary>
public sealed class ManualTurnButtonState
{
    private int previous = 3;
    public void Reset() => previous = 3;

    // Bits: X=1, Y=2. All other buttons are ignored. Hands: left=1.
    public int Read(int pressed, out int hands, int unavailable = 0)
    {
        pressed &= 3;
        unavailable &= 3;
        int edges = pressed & ~previous & ~unavailable;
        previous = pressed | unavailable;
        hands = 0;
        bool increase = (pressed & 2) != 0;
        bool decrease = (pressed & 1) != 0;
        if (increase == decrease) return 0;
        int matching = edges & (increase ? 2 : 1);
        if (matching == 0) return 0;
        if ((matching & 3) != 0) hands |= 1;
        return increase ? 1 : -1;
    }

    public static float Adjust(float current, int direction, float step, float minimum, float maximum)
    {
        float lower = System.Math.Max(0.01f, minimum);
        float upper = System.Math.Max(lower, maximum);
        return System.Math.Min(upper, System.Math.Max(lower, current + direction * System.Math.Max(0.01f, step)));
    }
}
