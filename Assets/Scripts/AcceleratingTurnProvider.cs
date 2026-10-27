using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public class AcceleratingTurnProvider : ContinuousTurnProvider
{
    [Header("Joystick Deflection Amplification")]
    [Tooltip("Multiplier at full joystick deflection. Amplification increases from 1 near the center to this value at the edge.")]
    [Min(1f)] public float maximumMultiplier = 4f;

    [Header("Release Deceleration")]
    [Tooltip("Turn speed lost per second when the stick returns toward center or is released. Lower values coast longer.")]
    [Min(0.01f)] public float releaseDeceleration = 16f;

    public float CurrentMultiplier { get; private set; } = 1f;

    private float angularVelocity;

    public void SetMaximumMultiplier(float value)
    {
        maximumMultiplier = Mathf.Max(1f, value);
        ClampAngularVelocity();
        CurrentMultiplier = Mathf.Min(CurrentMultiplier, maximumMultiplier);
    }

    private void ClampAngularVelocity()
    {
        float limit = Mathf.Abs(turnSpeed) * Mathf.Max(1f, maximumMultiplier);
        angularVelocity = Mathf.Clamp(angularVelocity, -limit, limit);
    }

    protected override void OnDisable()
    {
        CurrentMultiplier = 1f;
        angularVelocity = 0f;
        base.OnDisable();
    }

    protected override float GetTurnAmount(Vector2 input)
    {
        if (Time.deltaTime <= 0f)
            return 0f;

        // Also enforce Inspector changes before integrating release coasting.
        ClampAngularVelocity();

        // The base provider combines both sticks. Keep their combined input
        // within a single stick's range so the configured maximum is respected.
        input = Vector2.ClampMagnitude(input, 1f);

        if (input.sqrMagnitude <= 0.000001f && enableTurnLeftRight)
        {
            CurrentMultiplier = 1f;
            return SlowTowards(0f);
        }

        float turnAmount = base.GetTurnAmount(input);
        Cardinal cardinal = CardinalUtility.GetNearestCardinal(input);
        if (Mathf.Approximately(turnAmount, 0f) ||
            (cardinal != Cardinal.East && cardinal != Cardinal.West))
        {
            CurrentMultiplier = 1f;
            // Preserve the provider's optional 180-degree turn-around behavior.
            if (!enableTurnLeftRight || !Mathf.Approximately(turnAmount, 0f))
            {
                angularVelocity = 0f;
                return turnAmount;
            }

            // A spring return can briefly point vertically. Keep braking
            // instead of erasing the remaining horizontal turn velocity.
            return SlowTowards(0f);
        }

        // The base turn already scales with stick magnitude. Increase its
        // gain with deflection for finer control near center and faster turns
        // near the edge, independent of how long the stick has been held.
        CurrentMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, maximumMultiplier),
            input.magnitude);
        float amplifiedTurn = turnAmount * CurrentMultiplier;
        float targetVelocity = amplifiedTurn / Time.deltaTime;
        if (Mathf.Sign(targetVelocity) == Mathf.Sign(angularVelocity) &&
            Mathf.Abs(targetVelocity) < Mathf.Abs(angularVelocity))
        {
            // Brake toward smaller inputs as well as zero. Otherwise the
            // stick's spring return consumes all momentum before release.
            return SlowTowards(targetVelocity);
        }

        // Pushing farther out or deliberately reversing responds immediately.
        angularVelocity = targetVelocity;
        return amplifiedTurn;
    }

    private float SlowTowards(float targetVelocity)
    {
        float deceleration = Mathf.Max(0.01f, releaseDeceleration);
        float previousVelocity = angularVelocity;
        // Integrate the ramp and any remaining time at the target speed.
        // This also handles the partial final frame when coming to rest.
        float elapsed = Mathf.Min(Time.deltaTime,
            Mathf.Abs(targetVelocity - previousVelocity) / deceleration);
        angularVelocity = Mathf.MoveTowards(previousVelocity, targetVelocity,
            deceleration * Time.deltaTime);
        return 0.5f * (previousVelocity + angularVelocity) * elapsed +
            angularVelocity * (Time.deltaTime - elapsed);
    }
}
