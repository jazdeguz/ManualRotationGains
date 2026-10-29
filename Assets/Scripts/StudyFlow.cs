using System.Collections.Generic;
using UnityEngine;
using Mode = MovementModeSelector.MovementMode;

public enum StudyScene { Menu, Demo, Area }

public readonly struct StudyStep
{
    public readonly StudyScene Scene;
    public readonly Mode Mode;
    public readonly bool AsConductor;

    public StudyStep(StudyScene scene, Mode mode, bool asConductor)
    {
        Scene = scene;
        Mode = mode;
        AsConductor = asConductor;
    }

    public string ScenePath => Scene == StudyScene.Menu ? HoldToReturnToMenu.MenuScenePath
        : Scene == StudyScene.Demo ? MainMenuController.DemoScenePath : MainMenuController.GameplayScenePath;
}

// One participant's latin-square run: both roles in PID order, each covering every turn type in PID order.
// Pressing B or collecting every coin moves to the next step; menu steps wait on the ready screen for B.
public static class StudyFlow
{
    private static readonly List<StudyStep> steps = new List<StudyStep>();
    private static int index = -1;

    public static bool Running => index >= 0;
    public static StudyStep Current => steps[index];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void End()
    {
        steps.Clear();
        index = -1;
    }

    public static void Begin()
    {
        steps.Clear();
        AddRole(MovementSessionSettings.ConductorFirst);
        AddRole(!MovementSessionSettings.ConductorFirst);
        index = 0;
        StudyLog.Begin();
        StudyFrameRecorder.Begin();
    }

    // Moves to the next step; false once the last one is done and the study has ended.
    public static bool Advance()
    {
        if (!Running) return false;
        if (++index < steps.Count) return true;
        End();
        return false;
    }

    // Conductors get the demo right before every turn type's area and go straight from it into the
    // area. Passengers only get one demo, in the first turn type, to get used to the headset.
    private static void AddRole(bool asConductor)
    {
        IReadOnlyList<Mode> order = MovementSessionSettings.TurnOrder;
        for (int i = 0; i < order.Count; i++)
        {
            if (steps.Count > 0) Add(StudyScene.Menu, order[i], asConductor);
            if (asConductor || i == 0) Add(StudyScene.Demo, order[i], asConductor);
            if (!asConductor && i == 0) Add(StudyScene.Menu, order[i], asConductor);
            Add(StudyScene.Area, order[i], asConductor);
        }
    }

    private static void Add(StudyScene scene, Mode mode, bool asConductor) =>
        steps.Add(new StudyStep(scene, mode, asConductor));
}
