using System.Collections.Generic;
using System.IO;
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
    public static int CurrentStepNumber => index + 1;
    private static string ProgressKey => "Study.Progress.v1.P" + MovementSessionSettings.ParticipantId;
    public static bool CanResume => PlayerPrefs.HasKey(ProgressKey) || File.Exists(StudyLog.FilePath);
    public static int SavedStep => PlayerPrefs.GetInt(ProgressKey, 0);

    public static List<StudyStep> GetSteps()
    {
        var result = new List<StudyStep>();
        AddRole(result, MovementSessionSettings.ConductorFirst);
        AddRole(result, !MovementSessionSettings.ConductorFirst);
        return result;
    }

    private static void SaveProgress()
    {
        PlayerPrefs.SetInt(ProgressKey, index);
        PlayerPrefs.Save();
    }

    public static void Resume(int stepIndex)
    {
        var schedule = GetSteps();
        if (!CanResume || stepIndex < 0 || stepIndex >= schedule.Count)
            throw new System.InvalidOperationException("Choose an existing participant and a valid step.");
        StudyFrameRecorder.WaitForSave();
        StudyTurnSpeedRecorder.Stop();
        // Keep originals even when replaying an already recorded role/turn type.
        string suffix = ".resume-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".bak";
        foreach (string kind in new[] { "Coins", "Frames", "TurnSpeed" })
        {
            string path = StudyLog.ParticipantFile(kind);
            if (File.Exists(path)) File.Copy(path, path + suffix);
        }
        StudyFrameRecorder.Resume();
        steps.Clear();
        steps.AddRange(schedule);
        index = stepIndex;
        try { StudyTurnSpeedRecorder.StartRecording(true); }
        catch { End(); throw; }
        SaveProgress();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void End()
    {
        StudyTurnSpeedRecorder.Stop();
        steps.Clear();
        index = -1;
    }

    public static void Begin()
    {
        steps.Clear();
        steps.AddRange(GetSteps());
        index = 0;
        StudyLog.Begin();
        StudyFrameRecorder.Begin();
        StudyTurnSpeedRecorder.StartRecording(false);
        SaveProgress();
    }

    // Moves to the next step; false once the last one is done and the study has ended.
    public static bool Advance()
    {
        if (!Running) return false;
        ++index;
        SaveProgress();
        if (index < steps.Count) return true;
        End();
        return false;
    }

    // Conductors get the demo right before every turn type's area and go straight from it into the
    // area. Passengers only get one demo, in the first turn type, to get used to the headset.
    private static void AddRole(List<StudyStep> steps, bool asConductor)
    {
        IReadOnlyList<Mode> order = MovementSessionSettings.TurnOrder;
        for (int i = 0; i < order.Count; i++)
        {
            if (steps.Count > 0) steps.Add(new StudyStep(StudyScene.Menu, order[i], asConductor));
            if (asConductor || i == 0) steps.Add(new StudyStep(StudyScene.Demo, order[i], asConductor));
            if (!asConductor && i == 0) steps.Add(new StudyStep(StudyScene.Menu, order[i], asConductor));
            steps.Add(new StudyStep(StudyScene.Area, order[i], asConductor));
        }
    }

}
