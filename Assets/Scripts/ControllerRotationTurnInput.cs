using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class ControllerRotationTurnInput : MonoBehaviour, IXRInputValueReader<Vector2>
{
    public XRNode hand = XRNode.RightHand;

    [Tooltip("Controller roll, in degrees, that is ignored so the controller does not need to be held perfectly upright.")]
    [Range(0f, 89f)] public float deadzoneAngle = 15f;
    [Tooltip("Controller roll, in degrees, that counts as full deflection.")]
    [Range(1f, 90f)] public float maximumAngle = 90f;

    [Tooltip("Record tracking transitions and device events in tilt-diagnostics.log in the app's persistent data folder.")]
    public bool logTrackingDiagnostics = true;

    private string lastStatus;
    private InputDevice lastDevice;
    private bool focused = true;
    private bool paused;
    private readonly StringBuilder pendingDiagnostics = new StringBuilder();
    private float nextLogFlush;

    private void OnEnable()
    {
        focused = Application.isFocused;
        lastStatus = null;
        InputDevices.deviceConnected += OnDeviceConnected;
        InputDevices.deviceDisconnected += OnDeviceDisconnected;
        InputDevices.deviceConfigChanged += OnDeviceConfigChanged;
    }

    private void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceConnected;
        InputDevices.deviceDisconnected -= OnDeviceDisconnected;
        InputDevices.deviceConfigChanged -= OnDeviceConfigChanged;
        FlushDiagnostics();
    }

    private void OnApplicationFocus(bool value)
    {
        focused = value;
        LogDiagnostic("Application focus=" + value);
        FlushDiagnostics();
    }

    private void OnApplicationPause(bool value)
    {
        paused = value;
        LogDiagnostic("Application paused=" + value);
        FlushDiagnostics();
    }

    private void OnDeviceConnected(InputDevice device) => LogDeviceEvent("connected", device);
    private void OnDeviceDisconnected(InputDevice device) => LogDeviceEvent("disconnected", device);
    private void OnDeviceConfigChanged(InputDevice device) => LogDeviceEvent("configuration changed", device);

    private void LogDeviceEvent(string action, InputDevice device)
    {
        var side = hand == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
        if ((lastStatus != null && device.Equals(lastDevice)) || (device.characteristics & side) != 0)
            LogDiagnostic("Device " + action + ": " + device.name);
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextLogFlush) FlushDiagnostics();
    }

    private void ReportStatus(string status, InputDevice device)
    {
        if (lastStatus == status && (!device.isValid || device.Equals(lastDevice))) return;
        lastStatus = status;
        if (device.isValid) lastDevice = device;
        string battery = device.TryGetFeatureValue(CommonUsages.batteryLevel, out float level)
            ? level.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "unavailable";
        LogDiagnostic(status + "; device=" + device.name + "; battery=" + battery);
    }

    private void LogDiagnostic(string message)
    {
        if (!logTrackingDiagnostics || pendingDiagnostics.Length > 16000) return;
        pendingDiagnostics.Append(DateTime.UtcNow.ToString("O")).Append(" [Tilt ")
            .Append(hand).Append("] ").AppendLine(message);
    }

    private void FlushDiagnostics()
    {
        nextLogFlush = Time.unscaledTime + 2f;
        if (pendingDiagnostics.Length == 0) return;
        string message = pendingDiagnostics.ToString();
        pendingDiagnostics.Clear();
        Debug.Log(message, this);
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "tilt-diagnostics.log");
            // Bound disk usage even if a device repeatedly drops and reconnects.
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
            {
                File.Copy(path, path + ".previous", true);
                File.WriteAllText(path, "");
            }
            File.AppendAllText(path, message);
        }
        catch (Exception exception)
        {
            logTrackingDiagnostics = false;
            Debug.LogWarning("Tilt diagnostic file unavailable; file logging disabled: " + exception.Message, this);
        }
    }

    public Vector2 ReadValue()
    {
        TryReadValue(out Vector2 value);
        return value;
    }

    public bool TryReadValue(out Vector2 value)
    {
        value = Vector2.zero;
        if (!isActiveAndEnabled)
            return false;

        // Resolve the node on every read so a reconnected/replaced device is picked up.
        InputDevice device = InputDevices.GetDeviceAtXRNode(hand);
        if (!focused || paused)
        {
            ReportStatus("Input suspended: application inactive", device);
            return false;
        }
        if (!device.isValid)
        {
            ReportStatus("No valid device at hand node", device);
            return false;
        }

        bool hasTrackingState = device.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState trackingState);
        bool hasTracked = device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
        // Rotation-only tracking is enough for tilt. A position/general tracking loss
        // must not disable a rotation that the runtime explicitly reports as tracked.
        if (hasTrackingState ? (trackingState & InputTrackingState.Rotation) == 0 : hasTracked && !tracked)
        {
            ReportStatus("Rotation tracking unavailable", device);
            return false;
        }
        if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
        {
            ReportStatus("Rotation feature read failed", device);
            return false;
        }
        float lengthSquared = rotation.x * rotation.x + rotation.y * rotation.y +
            rotation.z * rotation.z + rotation.w * rotation.w;
        if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) || lengthSquared < 0.000001f)
        {
            ReportStatus("Invalid rotation sample", device);
            return false;
        }
        rotation = rotation.normalized;
        ReportStatus(hasTrackingState && hasTracked && !tracked
            ? "Rotation available despite general tracking loss"
            : "Rotation available", device);

        float roll = Mathf.Asin(Mathf.Clamp((rotation * Vector3.right).y, -1f, 1f))
                     * Mathf.Rad2Deg;
        float deflection = Mathf.InverseLerp(deadzoneAngle,
            Mathf.Max(deadzoneAngle + 0.01f, maximumAngle), Mathf.Abs(roll));
        if (deflection <= 0f)
            return true; // A neutral controller is a valid sample, not a disconnect.

        value = new Vector2(-Mathf.Sign(roll) * deflection, 0f);
        return true;
    }
}
