using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

[InitializeOnLoad]
public static class CoinScoreValidation
{
    const string Key = "CoinScoreValidation.Running";
    static Camera camera;
    static CoinScoreUI score;
    static GameObject coin;
    static int stage, frames;

    static CoinScoreValidation()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    public static void Run()
    {
        SessionState.SetBool(Key, true);
        SessionState.SetInt(Key + ".Result", 1);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(SessionState.GetInt(Key + ".Result", 1));
        }
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        try
        {
            var origin = new GameObject("Validation XR Player").AddComponent<XROrigin>();
            camera = new GameObject("Validation Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.SetParent(origin.transform, false);
            origin.Camera = camera;
            score = CoinScoreUI.ForCamera(camera);
            coin = MakeCoin();
            stage = frames = 0;
            EditorApplication.update += Tick;
        }
        catch (Exception e) { Finish(e); }
    }

    static GameObject MakeCoin()
    {
        var go = new GameObject("Validation Coin");
        go.transform.position = new Vector3(3, 0, 0);
        go.AddComponent<BoxCollider>().size = Vector3.one * 0.2f;
        go.AddComponent<Coin_Collected>();
        return go;
    }

    static void Tick()
    {
        if (++frames < 20) return;
        frames = 0;
        try
        {
            if (stage == 0)
            {
                Check(coin != null && coin.activeSelf && score.Score == 0, "Distant coin collected unexpectedly.");
                Check(score.GetComponentInChildren<Canvas>().renderMode == RenderMode.WorldSpace, "HUD is not world space.");
                Check(score.GetComponentInChildren<Text>().text == "COINS  0", "Initial label incorrect.");
                // 0.8m from collider: outside old 0.35m radius, inside new 1m radius.
                camera.transform.position = new Vector3(2.1f, 0, 0);
                stage++;
            }
            else if (stage == 1)
            {
                Check(coin == null && score.Score == 1, "Larger-radius automatic pickup failed.");
                coin = MakeCoin();
                coin.SendMessage("Update");
                coin.SendMessage("Update");
                Check(score.Score == 2, "Coin counted more than once.");
                Check(score.GetComponentInChildren<Text>().text == "COINS  2", "HUD did not update.");
                Check(camera.GetComponents<CoinScoreUI>().Length == 1, "Duplicate HUDs created.");
                stage++;
            }
            else
            {
                Check(coin == null, "Collected coin not destroyed.");
                var newCamera = new GameObject("New Run Camera").AddComponent<Camera>();
                Check(CoinScoreUI.ForCamera(newCamera).Score == 0, "New player retained old score.");
                Finish(null);
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        if (error == null) Debug.Log("COIN_SCORE_VALIDATION_PASSED: distance, automatic pickup, duplicate prevention, HUD updates, and fresh counter.");
        else Debug.LogException(error);
        SessionState.SetInt(Key + ".Result", error == null ? 0 : 1);
        EditorApplication.isPlaying = false;
    }
}
