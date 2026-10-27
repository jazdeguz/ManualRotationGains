using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MenuReturnValidation
{
    [MenuItem("Tools/Movement/Validate Hold To Return")]
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
            var hold = new MenuReturnHold();
            Check(!hold.Tick(true, 3f, 2f), "A held button on startup must not trigger.");
            hold.Tick(false, 0f, 2f);
            Check(!hold.Tick(true, 0.5f, 2f), "A short press must not trigger.");
            hold.Tick(false, 0f, 2f);
            Check(hold.Elapsed == 0f, "Release must cancel progress.");
            Check(!hold.Tick(true, 1.5f, 2f), "Separate holds must not accumulate.");
            Check(hold.Tick(true, 0.5f, 2f), "A full hold must trigger.");
            Check(!hold.Tick(true, 3f, 2f), "Continued holding must not trigger twice.");
            hold.Reset();
            Check(!hold.Tick(true, 3f, 2f), "Focus loss/reset must require release.");
            hold.Tick(false, 0f, 2f);
            Check(hold.Tick(true, 2f, 2f), "A fresh hold must work after reset.");
            foreach (var path in new[] { HoldToReturnToMenu.MenuScenePath, MainMenuController.GameplayScenePath })
                Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), "Missing build scene: " + path);
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuController.GameplayScenePath);
            try
            {
                var handlers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HoldToReturnToMenu>(true)).ToArray();
                Check(handlers.Length == 1 && handlers[0].enabled, "Expected one enabled return handler.");
                Check(handlers[0].GetComponent<MovementModeSelector>() != null, "Handler must share the movement selector's rig.");
                var origin = handlers[0].GetComponent<Unity.XR.CoreUtils.XROrigin>();
                Check(origin != null && origin.Camera != null, "Handler requires the rig camera.");
                Check(new SerializedObject(handlers[0]).FindProperty("holdDuration").floatValue == 2f, "Expected a two-second hold.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            File.WriteAllText("Logs/menu-return-validation.txt", "PASS: Hold threshold, cancellation, rearming, repeat suppression, reset/focus semantics, terrain component references and enabled menu/gameplay scenes. Hardware interaction and scene transitions still need Play Mode testing.");
            Debug.Log("Hold-to-return validation passed.");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/menu-return-validation.txt", "FAIL: " + exception);
            Debug.LogException(exception);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
