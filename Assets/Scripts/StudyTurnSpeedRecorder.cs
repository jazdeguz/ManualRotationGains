using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// Observe completed locomotion and write one sample per rendered frame.
[DefaultExecutionOrder(10000)]
public sealed class StudyTurnSpeedRecorder : MonoBehaviour
{
    public const string Header = "Participant PID,Recording session,UTC timestamp,Session elapsed (s),Step number,Scene,Role,Turn type,Scene elapsed (s),Frame duration (s),Applied manual turn (deg/s),Full-input speed limit (deg/s),Saved starting speed (deg/s),Turn provider available";
    private static StudyTurnSpeedRecorder instance;
    private static Task saving = Task.CompletedTask;
    private readonly StringBuilder pending = new StringBuilder();
    private AcceleratingTurnProvider provider;
    private StudyStep step;
    private int stepNumber;
    private string sceneLabel;
    private string path;
    private string session;
    private int participant;
    private double started;
    private double nextFlush;
    private int appliedFrame = -1;
    private float appliedAngle;
    private bool recording;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Stop();
        instance = null;
    }

    public static void StartRecording(bool resume)
    {
        Stop();
        string target = StudyLog.ParticipantFile("TurnSpeed");
        string pid = MovementSessionSettings.ParticipantId.ToString(CultureInfo.InvariantCulture);
        if (resume && File.Exists(target))
        {
            bool onlyHeaders;
            using (var reader = new StreamReader(target))
            {
                if (reader.ReadLine() != pid || reader.ReadLine() != Header)
                    throw new InvalidDataException("The saved turn-speed file has an unsupported format.");
                onlyHeaders = reader.EndOfStream;
            }
            // A crash immediately after creating the header may omit its final newline.
            if (onlyHeaders) File.WriteAllText(target, pid + "\n" + Header + "\n");
            else RemoveIncompleteLastRow(target);
        }
        else File.WriteAllText(target, pid + "\n" + Header + "\n");

        var go = new GameObject("Study turn speed recorder");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<StudyTurnSpeedRecorder>();
        instance.path = target;
        instance.participant = MovementSessionSettings.ParticipantId;
        instance.session = Guid.NewGuid().ToString("N");
        instance.started = Time.realtimeSinceStartupAsDouble;
        instance.nextFlush = instance.started + 1d;
        instance.recording = true;
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
        instance.BindScene(SceneManager.GetActiveScene());
    }

    // A crash can leave an incomplete final append. Preserve all complete rows.
    private static void RemoveIncompleteLastRow(string target)
    {
        using (var stream = new FileStream(target, FileMode.Open, FileAccess.ReadWrite))
        {
            long end = stream.Length;
            while (end > 0)
            {
                stream.Position = end - 1;
                if (stream.ReadByte() == '\n') break;
                end--;
            }
            stream.SetLength(end);
        }
    }

    public static void Stop()
    {
        if (instance != null)
        {
            instance.recording = false;
            SceneManager.sceneLoaded -= instance.OnSceneLoaded;
            instance.UnbindProvider();
            instance.Flush();
            Destroy(instance.gameObject);
            instance = null;
        }
        saving.GetAwaiter().GetResult();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindScene(scene);

    private void BindScene(Scene scene)
    {
        UnbindProvider();
        appliedFrame = -1;
        if (!StudyFlow.Running) return;
        step = StudyFlow.Current;
        stepNumber = StudyFlow.CurrentStepNumber;
        sceneLabel = scene.path == MainMenuController.GameplayScenePath ? "Game area"
            : scene.path == MainMenuController.DemoScenePath ? "Test demo" : "Menu";
        if (sceneLabel == "Menu") return;
        provider = FindAnyObjectByType<AcceleratingTurnProvider>();
        if (provider != null) provider.afterStepLocomotion += OnTurnApplied;
    }

    private void UnbindProvider()
    {
        if (provider != null) provider.afterStepLocomotion -= OnTurnApplied;
        provider = null;
    }

    private void OnTurnApplied(LocomotionProvider source)
    {
        if (appliedFrame != Time.frameCount) appliedAngle = 0f;
        appliedFrame = Time.frameCount;
        appliedAngle += provider.transformation.angleDelta;
    }

    private void LateUpdate()
    {
        if (!recording) return;
        bool available = provider != null && provider.isActiveAndEnabled;
        float speed = available && appliedFrame == Time.frameCount && Time.deltaTime > 0f
            ? appliedAngle / Time.deltaTime : 0f;
        string limit = available ? Format(provider.UseOriginalContinuousTurning
            ? MovementSessionSettings.StartingTurnDegreesPerSecond
            : Mathf.Abs(provider.turnSpeed) * provider.maximumMultiplier) : "";
        pending.Append(participant.ToString(CultureInfo.InvariantCulture)).Append(',').Append(session).Append(',')
            .Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)).Append(',')
            .Append(Format(Time.realtimeSinceStartupAsDouble - started)).Append(',').Append(stepNumber).Append(',')
            .Append(sceneLabel).Append(',').Append(step.AsConductor ? "Conductor" : "Passenger").Append(',')
            .Append(MainMenuController.TurnLabel(step.Mode)).Append(',').Append(Format(Time.timeSinceLevelLoad)).Append(',')
            .Append(Format(Time.deltaTime)).Append(',').Append(Format(speed)).Append(',').Append(limit).Append(',')
            .Append(Format(MovementSessionSettings.StartingTurnDegreesPerSecond)).Append(',')
            .Append(available ? "true" : "false").Append('\n');
        if (Time.realtimeSinceStartupAsDouble >= nextFlush)
        {
            nextFlush = Time.realtimeSinceStartupAsDouble + 1d;
            Flush();
        }
    }

    private void Flush()
    {
        if (pending.Length == 0) return;
        string batch = pending.ToString();
        pending.Clear();
        string target = path;
        saving = saving.ContinueWith(_ =>
        {
            try { File.AppendAllText(target, batch); }
            catch (Exception exception) { Debug.LogException(exception); }
        }, TaskScheduler.Default);
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        Flush();
        saving.GetAwaiter().GetResult();
    }

    private void OnApplicationQuit() => Stop();
    private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
}
