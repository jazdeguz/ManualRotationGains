using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

// Runs the real menu/scene lifecycle. It never exits Unity or saves Play mode changes.
[InitializeOnLoad]
public static class CompactGameplayValidation
{
    const string Key = "CompactGameplayValidation.Running";
    const string Report = "Logs/compact-gameplay-validation.txt";
    static int stage, mode, frames;
    static double deadline;
    static Coin_Collected coin;
    static CoinScoreUI score;
    static bool ready;
    static readonly MovementModeSelector.MovementMode[] Modes = {
        MovementModeSelector.MovementMode.UseJoystick,
        MovementModeSelector.MovementMode.UseRotation,
        MovementModeSelector.MovementMode.ContinuousRotation
    };

    static CompactGameplayValidation()
    {
        EditorApplication.playModeStateChanged += State;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Terrain Environment/Validate Compact Gameplay")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Exit Play mode/baking first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save pending scene edits first.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, "RUNNING: real menu -> compact scene -> pickup -> menu, for all three movement modes.\n");
        EditorSceneManager.OpenScene(HoldToReturnToMenu.MenuScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            stage = mode = frames = 0;
            ready = true;
            deadline = EditorApplication.timeSinceStartup + 180;
            Application.runInBackground = true;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            if (!File.ReadAllText(Report).Contains("RESULT:"))
                File.AppendAllText(Report, "RESULT: INTERRUPTED before all checks completed.\n");
            EditorSceneManager.OpenScene(CompactTerrainResize.ScenePath);
        }
    }

    static void Tick()
    {
        if (!ready || !SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Timed out at stage " + stage);
            if (++frames < 30) return;
            frames = 0;
            string path = SceneManager.GetActiveScene().path;
            if (stage == 0)
            {
                if (path != HoldToReturnToMenu.MenuScenePath) return;
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                if (menu == null) return;
                menu.SelectMode(Modes[mode]);
                menu.StartGameplay();
                Next(1);
            }
            else if (stage == 1)
            {
                if (path != MainMenuController.GameplayScenePath) return;
                var selector = UnityEngine.Object.FindFirstObjectByType<MovementModeSelector>();
                if (selector == null) return;
                Check(selector.movementMode == Modes[mode], "Menu movement choice not applied.");
                bool continuous = Modes[mode] == MovementModeSelector.MovementMode.ContinuousRotation;
                Check(!selector.continuousRotation.enabled && selector.turnProvider.enabled &&
                    selector.redirectionController.enabled != continuous, "Incorrect enabled turn components.");
                Check(((AcceleratingTurnProvider)selector.turnProvider).UseOriginalContinuousTurning == continuous, "Incorrect stock joystick mode.");
                bool controller = Modes[mode] == MovementModeSelector.MovementMode.UseRotation;
                Check((selector.turnProvider.leftHandTurnInput.bypass != null) == controller &&
                    (selector.turnProvider.rightHandTurnInput.bypass != null) == controller, "Incorrect controller turn routing.");
                var rig = selector.GetComponent<XROrigin>();
                Check(rig.transform.lossyScale == Vector3.one, "XR rig scale changed.");
                var coins = UnityEngine.Object.FindObjectsByType<Coin_Collected>(FindObjectsSortMode.None);
                Check(coins.Length == 5, "A new run did not restore all five coins.");
                score = CoinScoreUI.ForCamera(rig.Camera);
                Check(score.Score == 0, "A new run retained the previous score.");
                var terrainPlan = JsonUtility.FromJson<CompactSceneryPlacement.Plan>(File.ReadAllText(CompactSceneryPlacement.BaselinePath));
                Check(Terrain.activeTerrains.Length > 0 && Terrain.activeTerrains.All(t => Mathf.Approximately(t.terrainData.size.x, 1000f * terrainPlan.terrainScale)), "Loaded wrong terrain.");
                coin = coins.OrderBy(c => c.name).First();
                // Simulate walking into range by moving the rig once, then invoking
                // the real distance-based pickup. Tracking/input hardware is not simulated.
                rig.transform.position += coin.transform.position - rig.Camera.transform.position;
                Physics.SyncTransforms();
                coin.SendMessage("Update");
                coin.SendMessage("Update");
                Check(score.Score == 1, "Distance pickup failed or counted twice.");
                Next(2);
            }
            else if (stage == 2)
            {
                Check(coin == null && score.Score == 1, "Collected coin was not removed exactly once.");
                File.AppendAllText(Report, $"PASS: {Modes[mode]} menu load, component routing, compact terrain, fresh score/five coins, distance pickup and single-count behavior.\n");
                var hold = UnityEngine.Object.FindFirstObjectByType<HoldToReturnToMenu>();
                Check(hold != null && hold.enabled, "Missing return handler.");
                // Hold timing/cancellation is covered by MenuReturnValidation. Here
                // exercise the same transition method after a completed hold.
                hold.ReturnToMenu();
                Next(3);
            }
            else if (stage == 3)
            {
                if (path != HoldToReturnToMenu.MenuScenePath) return;
                var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
                if (menu == null) return;
                Check(MovementSessionSettings.SelectedMode == Modes[mode], "Movement selection lost on return.");
                var selected = (MovementModeSelector.MovementMode)typeof(MainMenuController)
                    .GetField("selectedMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(menu);
                Check(selected == Modes[mode], "Menu did not restore the movement choice.");
                File.AppendAllText(Report, $"PASS: {Modes[mode]} return to menu and retained selection.\n");
                if (++mode == Modes.Length)
                {
                    // One more start proves score/coin reset after the last mode too.
                    menu.StartGameplay();
                    Next(4);
                }
                else Next(0);
            }
            else if (stage == 4)
            {
                if (path != MainMenuController.GameplayScenePath) return;
                var rig = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                if (rig == null) return;
                Check(CoinScoreUI.ForCamera(rig.Camera).Score == 0 &&
                    UnityEngine.Object.FindObjectsByType<Coin_Collected>(FindObjectsSortMode.None).Length == 5, "Last mode did not reset on restart.");
                Finish(null);
            }
        }
        catch (Exception e) { Finish(e); }
    }
    static void Next(int value) { stage = value; frames = 0; deadline = EditorApplication.timeSinceStartup + 180; }
    static void Finish(Exception error)
    {
        ready = false;
        File.AppendAllText(Report, error == null ? "RESULT: PASS. Headset tracking, controller input/haptics, comfort, frame rate and physical traversal remain manual checks.\n" : "RESULT: FAIL: " + error + "\n");
        if (error != null) Debug.LogException(error); else Debug.Log("COMPACT_GAMEPLAY_VALIDATION_SUCCESS");
        EditorApplication.isPlaying = false;
    }
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
