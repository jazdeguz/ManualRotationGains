using System.Collections.Generic;
using UnityEngine;
using Mode = MovementModeSelector.MovementMode;

/// <summary>A menu choice lasts for this play session only.</summary>
public static class MovementSessionSettings
{
    // One row per participant, cycling every three participants.
    private static readonly Mode[][] TurnOrders =
    {
        new[] { Mode.ContinuousRotation, Mode.UseJoystick, Mode.UseRotation },
        new[] { Mode.UseRotation, Mode.ContinuousRotation, Mode.UseJoystick },
        new[] { Mode.UseJoystick, Mode.UseRotation, Mode.ContinuousRotation },
    };

    public static bool HasSelection { get; private set; }
    public static MovementModeSelector.MovementMode SelectedMode { get; private set; }
    public static float? ManualTurnMaximum { get; private set; }
    public static int ParticipantId { get; private set; } = 1;

    /// <summary>Odd participants are conductor first; even participants are passenger first.</summary>
    public static bool ConductorFirst => ParticipantId % 2 == 1;
    public static IReadOnlyList<Mode> TurnOrder => TurnOrders[(ParticipantId - 1) % TurnOrders.Length];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        HasSelection = false;
        ManualTurnMaximum = null;
        SelectedMode = MovementModeSelector.MovementMode.UseJoystick;
        ParticipantId = 1;
    }

    public static void Select(MovementModeSelector.MovementMode mode)
    {
        SelectedMode = mode;
        HasSelection = true;
    }

    public static void SetManualTurnMaximum(float value) => ManualTurnMaximum = value;

    public static void SetParticipantId(int value) => ParticipantId = Mathf.Max(1, value);
}
