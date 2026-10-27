using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ManualTurnAdjustmentValidation
{
    [MenuItem("Tools/Movement/Validate Manual Turn Adjustment")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            ValidateButtons();
            var rig = new GameObject("Manual turn validation");
            rig.SetActive(false);
            try
            {
                var provider = rig.AddComponent<AcceleratingTurnProvider>();
                provider.turnSpeed = 2f;
                var velocity = typeof(AcceleratingTurnProvider).GetField("angularVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
                velocity.SetValue(provider, 16f);
                provider.SetMaximumMultiplier(2f);
                Check((float)velocity.GetValue(provider) == 4f, "Positive momentum exceeds new limit.");
                velocity.SetValue(provider, -16f);
                provider.SetMaximumMultiplier(1.5f);
                Check((float)velocity.GetValue(provider) == -3f, "Negative momentum exceeds new limit.");
                provider.SetMaximumMultiplier(4f);
                Check((float)velocity.GetValue(provider) == -3f, "Increasing limit must not create momentum.");
            }
            finally { UnityEngine.Object.DestroyImmediate(rig); }
            ValidateSession();
            File.WriteAllText("Logs/manual-turn-adjustment-validation.txt",
                "PASS: All four buttons, press edges, simultaneous input, reconnect/reset, limits, step sizes, signed momentum clamp and session reset/retention. Hardware haptic patterns still require headset testing.");
            Debug.Log("Manual turn adjustment validation passed.");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/manual-turn-adjustment-validation.txt", "FAIL: " + exception);
            Debug.LogException(exception);
        }
    }

    public static void ValidateButtons()
    {
        var state = new ManualTurnButtonState();
        Check(state.Read(8, out _) == 0, "Startup held button must not fire.");
        foreach (int button in new[] { 1, 2, 4, 8 })
        {
            state.Read(0, out _);
            Check(state.Read(button, out int hands) == ((button & 10) != 0 ? 1 : -1), "Wrong button direction.");
            Check(hands == (button < 4 ? 1 : 2), "Feedback on wrong hand.");
            Check(state.Read(button, out _) == 0, "Held button repeated.");
        }
        state.Read(0, out _);
        Check(state.Read(10, out int both) == 1 && both == 3, "Both increases must combine.");
        state.Read(0, out _);
        Check(state.Read(5, out both) == -1 && both == 3, "Both decreases must combine.");
        state.Read(0, out _);
        Check(state.Read(9, out _) == 0, "Opposing buttons must cancel.");
        Check(state.Read(8, out _) == 0, "Releasing opposing button must not create an edge.");
        state.Reset();
        Check(state.Read(8, out _) == 0, "Reset must require release.");
        state.Read(0, out _, 3);
        Check(state.Read(8, out int right, 3) == 1 && right == 2, "Disconnected left controller must not block right.");
        Check(state.Read(2, out _) == 0, "Reconnect while held must not fire.");
        state.Read(0, out _);
        Check(state.Read(2, out _) == 1, "Reconnect after release must work.");
        Check(ManualTurnButtonState.Adjust(2f, 1, 0.5f, 1f, 8f) == 2.5f, "Wrong increase step.");
        Check(ManualTurnButtonState.Adjust(2f, -1, 0.5f, 1f, 8f) == 1.5f, "Wrong decrease step.");
        Check(ManualTurnButtonState.Adjust(8f, 1, 0.5f, 1f, 8f) == 8f, "Upper limit exceeded.");
        Check(ManualTurnButtonState.Adjust(1f, -1, 0.5f, 1f, 8f) == 1f, "Lower limit exceeded.");
        Check(ManualTurnButtonState.Adjust(7.8f, 1, 0.5f, 1f, 8f) == 8f, "Partial final step must clamp.");
    }

    private static void ValidateSession()
    {
        var previousMaximum = MovementSessionSettings.ManualTurnMaximum;
        bool hadSelection = MovementSessionSettings.HasSelection;
        var previousMode = MovementSessionSettings.SelectedMode;
        var reset = typeof(MovementSessionSettings).GetMethod("ResetSession", BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            MovementSessionSettings.SetManualTurnMaximum(3.5f);
            MovementSessionSettings.Select(MovementModeSelector.MovementMode.UseRotation);
            Check(MovementSessionSettings.ManualTurnMaximum == 3.5f, "Mode selection lost the maximum.");
            reset.Invoke(null, null);
            Check(!MovementSessionSettings.ManualTurnMaximum.HasValue, "Restart must clear adjusted maximum.");
        }
        finally
        {
            reset.Invoke(null, null);
            if (hadSelection) MovementSessionSettings.Select(previousMode);
            if (previousMaximum.HasValue) MovementSessionSettings.SetManualTurnMaximum(previousMaximum.Value);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
