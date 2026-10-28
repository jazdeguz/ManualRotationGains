using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.XR.CoreUtils;

public static class OriginalContinuousRotationValidation
{
    [MenuItem("Tools/Movement/Validate Jaz Original Rotation")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var root = new GameObject("Original rotation validation") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        int sleepTimeout = Screen.sleepTimeout;
        try
        {
            root.transform.rotation = Quaternion.Euler(0, 30, 0);
            var origin = root.AddComponent<XROrigin>();
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root.transform, false);
            var head = new GameObject("Head").AddComponent<Camera>();
            head.transform.SetParent(pivot, false);
            head.transform.localPosition = new Vector3(0.4f, 1.6f, -0.2f);
            origin.Camera = head;
            var evaluation = root.AddComponent<Evaluation>();
            evaluation.RotationGain = 2f;
            var controller = root.AddComponent<ContinuousRotation>();
            controller.xrOrigin = origin;
            controller.rotationPivot = pivot;
            controller.evaluation = evaluation;
            head.transform.localRotation = Quaternion.Euler(0, 350, 0);
            Invoke(controller, "Start");
            Invoke(controller, "LateUpdate");
            Vector3 physicalPosition = head.transform.position;
            head.transform.localRotation = Quaternion.Euler(0, 10, 0);
            Invoke(controller, "LateUpdate");
            Check(Mathf.Abs(evaluation.offsetYaw * Mathf.Rad2Deg - 20f) < 0.01f, "Expected original 20-degree correction across yaw wrap.");
            Check(Mathf.Abs(Mathf.DeltaAngle(head.transform.eulerAngles.y, 60f)) < 0.01f, "Wrong virtual heading.");
            Check(Vector3.Distance(head.transform.position, physicalPosition) < 0.001f, "Eye position orbited with rotation gain.");
            Invoke(controller, "LateUpdate");
            Check(Mathf.Abs(evaluation.offsetYaw * Mathf.Rad2Deg - 20f) < 0.01f, "Correction fed back into physical yaw.");
            Check(Mathf.Abs(Mathf.DeltaAngle(root.transform.eulerAngles.y, 30f)) < 0.01f, "Rig orientation changed.");
            controller.ResetRotationReference();
            Invoke(controller, "LateUpdate");
            Check(Mathf.Abs(evaluation.offsetYaw) < 0.001f, "Re-entry did not reset reference.");
            evaluation.RotationGain = 1f;
            head.transform.localRotation = Quaternion.Euler(0, 40, 0);
            Invoke(controller, "LateUpdate");
            Check(Mathf.Abs(evaluation.offsetYaw) < 0.001f, "Gain one must add no correction.");
            Check(!evaluation.insideUpdate, "Tracking update flag was not cleared.");
            File.WriteAllText("Logs/jaz-original-rotation-validation.txt", "PASS: original yaw-wrap/gain equation, no correction feedback, stationary eye position, unchanged rig, re-entry and neutral gain. Edit-mode validation; headset testing remains.");
            Debug.Log("Jaz original rotation validation passed.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Logs/jaz-original-rotation-validation.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            Screen.sleepTimeout = sleepTimeout;
        }
    }

    private static void Invoke(ContinuousRotation target, string name) =>
        typeof(ContinuousRotation).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
