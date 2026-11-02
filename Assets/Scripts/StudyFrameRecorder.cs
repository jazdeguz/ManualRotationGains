using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps the participant's head position for every frame of a study area in memory only.
// P##Frames.csv is rewritten once the area closes and the menu loads, on a background thread,
// so the headset never spends time on disk writes while the participant is playing.
// Row 1 participant, row 2 one column per role and turn type, then one cell per frame:
// "seconds (x, y, z)", with seconds counted from when the area started, as in P##Coins.
public sealed class StudyFrameRecorder : MonoBehaviour
{
    private struct Frame
    {
        public float Time;
        public Vector3 Position;
    }

    private static readonly string[] Headers =
    {
        "Continuous Turn - Passenger - elapsed time (s) / head world XYZ (Unity units)",
        "Joystick - Passenger - elapsed time (s) / head world XYZ (Unity units)",
        "Tilt - Passenger - elapsed time (s) / head world XYZ (Unity units)",
        "Continuous Turn - Conductor - elapsed time (s) / head world XYZ (Unity units)",
        "Joystick - Conductor - elapsed time (s) / head world XYZ (Unity units)",
        "Tilt - Conductor - elapsed time (s) / head world XYZ (Unity units)",
    };

    // Recordings made before descriptive labels were added remain resumable.
    private static readonly string[] LegacyHeaders =
    {
        "ContinuousAsPassenger", "JoystickAsPassenger", "TiltAsPassenger",
        "ContinuousAsConductor", "JoystickAsConductor", "TiltAsConductor",
    };

    // The latest session of each column; a replayed area replaces its column.
    private static string[][] columns = new string[Headers.Length][];
    private static Task saving = Task.CompletedTask;

    private readonly List<Frame> frames = new List<Frame>(1 << 15);
    private Transform head;
    private int column;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        columns = new string[Headers.Length][];
        saving = Task.CompletedTask;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    //Starts a fresh file for the current participant.
    public static void Begin()
    {
        columns = new string[Headers.Length][];
        Save();
    }

    public static void WaitForSave() => saving.GetAwaiter().GetResult();

    public static void Resume()
    {
        WaitForSave();
        var restored = new List<string>[Headers.Length];
        for (int c = 0; c < restored.Length; c++) restored[c] = new List<string>();
        string path = StudyLog.ParticipantFile("Frames");
        if (File.Exists(path))
        {
            using (var reader = new StreamReader(path))
            {
                string participant = reader.ReadLine();
                string header = reader.ReadLine();
                if (participant != MovementSessionSettings.ParticipantId.ToString(CultureInfo.InvariantCulture) ||
                    (header != string.Join(",", Headers) && header != string.Join(",", LegacyHeaders)))
                    throw new InvalidDataException("The saved frame file has an unsupported format.");
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // Preserve quoted coordinate cells exactly, including their commas.
                    var cells = new List<string>();
                    bool quoted = false;
                    int start = 0;
                    for (int i = 0; i < line.Length; i++)
                    {
                        if (line[i] == '"') quoted = !quoted;
                        if (line[i] == ',' && !quoted)
                        {
                            cells.Add(line.Substring(start, i - start));
                            start = i + 1;
                        }
                    }
                    cells.Add(line.Substring(start));
                    if (quoted || cells.Count != Headers.Length)
                        throw new InvalidDataException("The saved frame file is incomplete. Its backup has been preserved.");
                    for (int c = 0; c < cells.Count; c++) restored[c].Add(cells[c]);
                }
            }
        }
        columns = new string[Headers.Length][];
        for (int c = 0; c < columns.Length; c++) columns[c] = restored[c].ToArray();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Leaving an area early with the menu button loads the menu while the area is still the current step.
        if (!StudyFlow.Running || StudyFlow.Current.Scene != StudyScene.Area ||
            scene.path != StudyFlow.Current.ScenePath)
            return;

        var origin = FindFirstObjectByType<XROrigin>();
        if (origin == null || origin.Camera == null)
        {
            Debug.LogWarning("StudyFrameRecorder: no XR Origin camera to record in " + scene.path + ".");
            return;
        }
        var recorder = origin.gameObject.AddComponent<StudyFrameRecorder>();
        StudyLog.RestartArea();
        recorder.head = origin.Camera.transform;
        recorder.column = (StudyFlow.Current.AsConductor ? 3 : 0) + StudyLog.TurnTypeIndex(StudyFlow.Current.Mode);
    }

    // After tracking has moved the camera for this frame.
    private void LateUpdate() =>
        frames.Add(new Frame { Time = Time.timeSinceLevelLoad, Position = head.position });

    // Areas always lead back to the menu, so this runs as the menu loads.
    private void OnDestroy()
    {
        columns[column] = frames.ConvertAll(frame => "\"" + Format(frame.Time) + " (" +
            Format(frame.Position.x) + ", " + Format(frame.Position.y) + ", " +
            Format(frame.Position.z) + ")\"").ToArray();
        Save();
    }

    private static void Save()
    {
        string[][] snapshot = (string[][])columns.Clone();
        string path = StudyLog.ParticipantFile("Frames");
        string participant = MovementSessionSettings.ParticipantId.ToString(CultureInfo.InvariantCulture);
        // Chained so an older snapshot can never finish after, and overwrite, a newer one.
        saving = saving.ContinueWith(_ => Write(path, participant, snapshot), TaskScheduler.Default);
    }

    private static void Write(string path, string participant, string[][] snapshot)
    {
        try
        {
            int rows = 0;
            foreach (string[] session in snapshot)
                if (session != null) rows = Math.Max(rows, session.Length);

            string temporary = path + ".tmp";
            using (var writer = new StreamWriter(temporary, false))
            {
                writer.WriteLine(participant);
                writer.WriteLine(string.Join(",", Headers));
                var line = new StringBuilder();
                for (int r = 0; r < rows; r++)
                {
                    line.Clear();
                    for (int c = 0; c < snapshot.Length; c++)
                    {
                        if (c > 0) line.Append(',');
                        if (snapshot[c] == null || r >= snapshot[c].Length) continue;
                        line.Append(snapshot[c][r]);
                    }
                    writer.WriteLine(line);
                }
            }
            if (File.Exists(path)) File.Replace(temporary, path, null, true);
            else File.Move(temporary, path);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static string Format(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
}
