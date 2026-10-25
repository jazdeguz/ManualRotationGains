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
    private static int stage, frames;

    static ContinuousRotationValidation()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/TerrainDemoScene_HDRP/Scenes/TerrainDemoScene.unity");
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
            var camera = new GameObject("Head").AddComponent<Camera>();
            head = camera.transform;
            head.SetParent(root.transform, false);
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
            continuous.rotationPivot = root.transform;
            continuous.evaluation = evaluation;
            continuous.enabled = false;
            root.AddComponent<LocomotionMediator>();
            var turn = root.AddComponent<AcceleratingTurnProvider>();
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
                    head.localRotation = Quaternion.Euler(0f, 350f, 0f);
                    selector.SetMovementMode(MovementModeSelector.MovementMode.ContinuousRotation);
                    Check(selector.continuousRotation.enabled && !selector.redirectionController.enabled &&
                        !selector.turnProvider.enabled, "Continuous mode has competing turn components.");
                    break;
                case 1:
                    CheckYaw(30f);
                    head.localRotation = Quaternion.Euler(0f, 10f, 0f);
                    break;
                case 2:
                    CheckYaw(50f); // 20 physical degrees add 20 extra degrees at gain 2.
                    selector.SetMovementMode(MovementModeSelector.MovementMode.UseRotation);
                    Check(!selector.continuousRotation.enabled && selector.redirectionController.enabled &&
                        selector.turnProvider.enabled, "Manual turn components were not restored.");
                    Check(selector.turnProvider.leftHandTurnInput.bypass == selector.leftHandRotationInput &&
                        selector.turnProvider.rightHandTurnInput.bypass == selector.rightHandRotationInput,
                        "Controller rotation inputs were not restored.");
                    selector.SetMovementMode(MovementModeSelector.MovementMode.UseJoystick);
                    Check(selector.turnProvider.leftHandTurnInput.bypass == null &&
                        selector.turnProvider.rightHandTurnInput.bypass == null, "Joystick bypass was not cleared.");
                    head.localRotation = Quaternion.Euler(0f, 50f, 0f);
                    selector.SetMovementMode(MovementModeSelector.MovementMode.ContinuousRotation);
                    break;
                case 3:
                    CheckYaw(50f); // Re-entering takes a fresh reference without snapping.
                    head.localRotation = Quaternion.Euler(0f, 70f, 0f);
                    break;
                case 4:
                    CheckYaw(70f);
                    selector.enabled = false;
                    Check(!selector.continuousRotation.enabled && selector.turnProvider.enabled &&
                        selector.redirectionController.enabled, "Disabling selector stranded continuous mode.");
                    selector.enabled = true;
                    Check(selector.continuousRotation.enabled && !selector.turnProvider.enabled,
                        "Re-enabling selector did not restore the selected mode.");
                    break;
                default:
                    CheckYaw(70f);
                    Finish(null);
                    break;
            }
        }
        catch (Exception e) { Finish(e); }
    }

    private static void CheckYaw(float expected)
    {
        Check(Mathf.Abs(Mathf.DeltaAngle(selector.transform.localEulerAngles.y, expected)) < 0.01f,
            $"Expected pivot yaw {expected}, got {selector.transform.localEulerAngles.y}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        if (error != null) Debug.LogException(error);
        else Debug.Log("ContinuousRotationValidation PASSED: scene wiring, yaw wrap, gain, mode switching, and re-entry.");
        SessionState.SetInt(Key + ".Result", error == null ? 0 : 1);
        EditorApplication.isPlaying = false;
    }
}
