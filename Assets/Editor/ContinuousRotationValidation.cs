using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// Run with -batchmode -executeMethod ContinuousRotationValidation.Run.
[InitializeOnLoad]
public static class ContinuousRotationValidation
{
    private const string Key = "ContinuousRotationValidation.Running";
    private static MovementModeSelector selector;
    private static Transform head;
    private static Transform pivot;
    private static AcceleratingTurnProvider turn;
    private static int stage, frames;

    static ContinuousRotationValidation()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene(MainMenuController.GameplayScenePath);
            var saved = UnityEngine.Object.FindFirstObjectByType<MovementModeSelector>();
            Check(saved != null && saved.movementMode == MovementModeSelector.MovementMode.UseJoystick,
                "Scene default changed.");
            Check(saved.continuousRotation != null && !saved.continuousRotation.enabled,
                "Scene is missing the disabled Continuous Rotation component.");
            Check(saved.continuousRotation.xrOrigin != null && saved.continuousRotation.rotationPivot != null &&
                saved.continuousRotation.evaluation != null && saved.redirectionController != null,
                "Scene references are incomplete.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Result", 1);
            EditorApplication.isPlaying = true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void OnState(PlayModeStateChange state)
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
            var root = new GameObject("Rotation validation rig");
            root.SetActive(false);
            var origin = root.AddComponent<XROrigin>();
            pivot = new GameObject("Camera Rotation Pivot").transform;
            pivot.SetParent(root.transform, false);
            var camera = new GameObject("Head").AddComponent<Camera>();
            head = camera.transform;
            head.SetParent(pivot, false);
            head.localPosition = new Vector3(0.4f, 1.6f, -0.2f);
            origin.Camera = camera;
            origin.CameraFloorOffsetObject = root;
            var evaluation = root.AddComponent<Evaluation>();
            evaluation.RotationGain = 2f;
            var redirection = root.AddComponent<RedirectionController>();
            redirection.xrOrigin = origin;
            redirection.rotationPivot = root.transform;
            redirection.evaluation = evaluation;
            var continuous = root.AddComponent<ContinuousRotation>();
            continuous.xrOrigin = origin;
            continuous.rotationPivot = pivot;
            continuous.evaluation = evaluation;
            continuous.enabled = false;
            root.AddComponent<LocomotionMediator>();
            turn = root.AddComponent<AcceleratingTurnProvider>();
            turn.turnSpeed = 2f;
            turn.maximumMultiplier = 8f;
            selector = root.AddComponent<MovementModeSelector>();
            selector.turnProvider = turn;
            root.SetActive(true);
            stage = frames = 0;
            EditorApplication.update += Tick;
        }
        catch (Exception e) { Finish(e); }
    }

    private static void Tick()
    {
        if (++frames < 5) return;
        frames = 0;
        try
        {
            switch (stage++)
            {
                case 0:
                    selector.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                    selector.SetMovementMode(MovementModeSelector.MovementMode.ContinuousRotation);
                    CheckOriginalMode();
                    CheckTurn(Vector2.right, 10f * Time.deltaTime);
                    CheckTurn(Vector2.left, -10f * Time.deltaTime);
                    CheckTurn(new Vector2(0.5f, 0f), 5f * Time.deltaTime);
                    CheckTurn(Vector2.zero, 0f);
                    Check(turn.CurrentMultiplier == 1f, "Original turning used the manual multiplier.");
                    head.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    break;
                case 1:
                    Check(Mathf.Abs(Mathf.DeltaAngle(selector.transform.eulerAngles.y, 30f)) < 0.01f,
                        "Physical head motion redirected the rig in original joystick mode.");
                    Check(Mathf.Abs(Mathf.DeltaAngle(pivot.localEulerAngles.y, 0f)) < 0.01f,
                        "Physical head motion redirected the camera pivot.");
                    selector.SetMovementMode(MovementModeSelector.MovementMode.UseRotation);
                    Check(!turn.UseOriginalContinuousTurning && selector.redirectionController.enabled,
                        "Tilt mode did not restore enhanced turning.");
                    Check(ReferenceEquals(turn.leftHandTurnInput.bypass, selector.leftHandRotationInput) &&
                        ReferenceEquals(turn.rightHandTurnInput.bypass, selector.rightHandRotationInput),
                        "Tilt input bypass was not restored.");
                    CheckTurn(Vector2.right, 16f * Time.deltaTime);
                    Check(ReadTurn(Vector2.zero) > 0f, "Enhanced turning lost release coasting.");
                    selector.SetMovementMode(MovementModeSelector.MovementMode.ContinuousRotation);
                    CheckOriginalMode();
                    CheckTurn(Vector2.zero, 0f);
                    Check(turn.maximumMultiplier == 8f, "Original mode overwrote the saved manual maximum.");
                    break;
                case 2:
                    selector.SetMovementMode(MovementModeSelector.MovementMode.UseJoystick);
                    Check(!turn.UseOriginalContinuousTurning && turn.leftHandTurnInput.bypass == null &&
                        turn.rightHandTurnInput.bypass == null, "Joystick mode was not restored.");
                    selector.SetMovementMode(MovementModeSelector.MovementMode.ContinuousRotation);
                    selector.enabled = false;
                    Check(!turn.UseOriginalContinuousTurning && selector.redirectionController.enabled,
                        "Disabling selector did not restore the manual provider.");
                    selector.enabled = true;
                    CheckOriginalMode();
                    Finish(null);
                    break;
            }
        }
        catch (Exception e) { Finish(e); }
    }

    private static void CheckOriginalMode()
    {
        Check(turn.enabled && turn.UseOriginalContinuousTurning &&
            !selector.continuousRotation.enabled && !selector.redirectionController.enabled,
            "Original mode has incorrect or competing turn components.");
        Check(turn.leftHandTurnInput.bypass == null && turn.rightHandTurnInput.bypass == null,
            "Original mode must read the joystick, not controller tilt.");
    }

    private static float ReadTurn(Vector2 input) => (float)typeof(AcceleratingTurnProvider)
        .GetMethod("GetTurnAmount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
        .Invoke(turn, new object[] { input });

    private static void CheckTurn(Vector2 input, float expected)
    {
        Check(Time.deltaTime > 0f, "Turn tests require a running Play Mode frame.");
        float actual = ReadTurn(input);
        Check(Mathf.Abs(actual - expected) < 0.00001f, $"Expected {expected}, got {actual}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        if (error != null) Debug.LogException(error);
        else Debug.Log("ContinuousRotationValidation PASSED: 10-degree-per-second continuous joystick speed, immediate stop, disabled head gain, enhanced modes, and switching.");
        SessionState.SetInt(Key + ".Result", error == null ? 0 : 1);
        EditorApplication.isPlaying = false;
    }
}
