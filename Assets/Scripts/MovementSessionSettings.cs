using UnityEngine;

/// <summary>A menu choice lasts for this play session only.</summary>
public static class MovementSessionSettings
{
    public static bool HasSelection { get; private set; }
    public static MovementModeSelector.MovementMode SelectedMode { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        HasSelection = false;
        SelectedMode = MovementModeSelector.MovementMode.UseJoystick;
    }

    public static void Select(MovementModeSelector.MovementMode mode)
    {
        SelectedMode = mode;
        HasSelection = true;
    }
}
