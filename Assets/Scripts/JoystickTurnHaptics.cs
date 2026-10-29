using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

[RequireComponent(typeof(ContinuousTurnProvider))]
public class JoystickTurnHaptics : MonoBehaviour
{
    public ContinuousTurnProvider turnProvider;

    [Tooltip("Joystick rotation between ticks, in degrees.")]
    [Min(0.01f)] public float degreesPerTick = 0.5f;
    [Range(0f, 1f)] public float amplitude = 0.25f;
    [Min(0.001f)] public float duration = 0.02f;
    [Tooltip("Caps the tick rate so fast turns remain distinct pulses.")]
    [Min(0.01f)] public float minimumTickInterval = 0.05f;

    private float accumulatedDegrees;
    private float nextTickTime;
    private bool steppedThisFrame;
    private bool tickLeftHand;
    private bool tickRightHand;
    private readonly Queue<(XRNode hand, int pulses)> adjustments = new Queue<(XRNode, int)>();
    private Coroutine adjustmentRoutine;
    private bool adjustmentActive;
    private XRNode adjustmentHand;

    public void QueueAdjustment(XRNode hand, bool increase)
    {
        if (!isActiveAndEnabled) return;
        adjustments.Enqueue((hand, increase ? 2 : 1));
        if (adjustmentRoutine == null)
            adjustmentRoutine = StartCoroutine(PlayAdjustments());
    }

    private IEnumerator PlayAdjustments()
    {
        adjustmentActive = true;
        // Separate feedback from any turning tick that is already playing.
        yield return new WaitForSecondsRealtime(Mathf.Max(duration, 0.02f) + 0.1f);
        while (adjustments.Count > 0)
        {
            var request = adjustments.Dequeue();
            adjustmentHand = request.hand;
            for (int pulse = 0; pulse < request.pulses; pulse++)
            {
                SendImpulse(request.hand, 0.5f, 0.07f);
                yield return new WaitForSecondsRealtime(0.17f);
            }
            yield return new WaitForSecondsRealtime(0.15f);
        }
        ResetTicks();
        adjustmentActive = false;
        adjustmentRoutine = null;
    }

    public void CancelAdjustment()
    {
        if (adjustmentRoutine != null)
        {
            StopCoroutine(adjustmentRoutine);
            var device = InputDevices.GetDeviceAtXRNode(adjustmentHand);
            if (device.isValid) device.StopHaptics();
        }
        adjustmentRoutine = null;
        adjustmentActive = false;
        adjustments.Clear();
    }

    private void OnEnable()
    {
        if (turnProvider == null)
            turnProvider = GetComponent<ContinuousTurnProvider>();

        turnProvider.afterStepLocomotion += OnTurnApplied;
        ResetTicks();
    }

    private void OnDisable()
    {
        CancelAdjustment();
        if (turnProvider != null)
            turnProvider.afterStepLocomotion -= OnTurnApplied;
        ResetTicks();
    }

    private void LateUpdate()
    {
        if (!steppedThisFrame)
            ResetTicks();
        steppedThisFrame = false;
    }

    private void ResetTicks()
    {
        accumulatedDegrees = 0f;
        nextTickTime = 0f;
        steppedThisFrame = false;
        tickLeftHand = false;
        tickRightHand = false;
    }

    private void OnTurnApplied(LocomotionProvider provider)
    {
        // Continuous Rotation turns silently; other modes retain their ticks.
        if (turnProvider is AcceleratingTurnProvider accelerating &&
            accelerating.UseOriginalContinuousTurning)
        {
            ResetTicks();
            return;
        }

        if (!turnProvider.isActiveAndEnabled || adjustmentActive)
            return;

        // This callback runs only after the joystick provider's queued turn
        // is applied. Head-only redirection does not produce joystick ticks.
        float degrees = Mathf.Abs(turnProvider.transformation.angleDelta);
        if (degrees <= Mathf.Epsilon)
            return;

        steppedThisFrame = true;
        bool leftActive = turnProvider.leftHandTurnInput.ReadValue().sqrMagnitude > 0.0001f;
        bool rightActive = turnProvider.rightHandTurnInput.ReadValue().sqrMagnitude > 0.0001f;
        if (leftActive || rightActive)
        {
            tickLeftHand = leftActive;
            tickRightHand = rightActive;
        }
        // Retain the last active hand during release coasting. The applied
        // angular distance still determines the tick rate as the rig slows.
        accumulatedDegrees += degrees;
        float spacing = Mathf.Max(0.01f, degreesPerTick);
        if (accumulatedDegrees < spacing || Time.unscaledTime < nextTickTime)
            return;

        // Drop excess ticks instead of delivering a burst after a fast turn.
        accumulatedDegrees %= spacing;
        nextTickTime = Time.unscaledTime +
            Mathf.Max(minimumTickInterval, duration + 0.01f);

        if (tickLeftHand)
            SendTick(XRNode.LeftHand);
        if (tickRightHand)
            SendTick(XRNode.RightHand);
    }

    private void SendTick(XRNode hand)
    {
        SendImpulse(hand, Mathf.Clamp01(amplitude), Mathf.Max(0.001f, duration));
    }

    private static void SendImpulse(XRNode hand, float strength, float seconds)
    {
        // Resolve on each tick so reconnecting a controller needs no restart.
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        if (device.isValid &&
            device.TryGetHapticCapabilities(out HapticCapabilities capabilities) &&
            capabilities.supportsImpulse && capabilities.numChannels > 0)
        {
            device.SendHapticImpulse(0, strength, seconds);
        }
    }
}
