using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Coordinated, recoverable terrain and scenery resize. Absolute target means
// rerunning this command cannot accidentally halve the scene again.
public static class CompactEnvironmentReduction
{
    [MenuItem("Tools/Terrain Environment/Reduce Compact Environment To 25 Percent")]
    public static void Run()
    {
        const float target = 0.25f;
        var scene = SceneManager.GetActiveScene();
        Check(scene.path == CompactTerrainResize.ScenePath && SceneManager.sceneCount == 1 && !scene.isDirty,
            "Open the compact scene by itself and save pending edits first.");
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && !Lightmapping.isRunning, "Exit Play mode/baking first.");
        var plan = JsonUtility.FromJson<CompactSceneryPlacement.Plan>(File.ReadAllText(CompactSceneryPlacement.BaselinePath));
        if (Mathf.Approximately(plan.terrainScale, target))
        {
            CompactTerrainResize.Execute(target, false);
            CompactEnvironmentFinalization.Audit();
            Debug.Log("Compact environment is already at 25%; no further reduction applied.");
            return;
        }
        Check(Mathf.Approximately(plan.terrainScale, 0.5f), "This command expects the current 50% environment.");
        var terrains = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
        Check(terrains.Length > 0 && terrains.All(t => t.terrainData.size == new Vector3(500, 400, 500)), "Unexpected current terrain geometry: " + string.Join("; ", terrains.Select(t => t.name + " size=" + t.terrainData.size + " position=" + t.transform.position)));
        Check(terrains.All(t => !EditorUtility.IsDirty(t.terrainData)), "Save pending terrain edits first.");
        var dataPaths = terrains.Select(t => AssetDatabase.GetAssetPath(t.terrainData)).Distinct().ToArray();
        Check(dataPaths.Length == terrains.Length && dataPaths.All(p => p.StartsWith("Assets/CompactEnvironment/TerrainData/", StringComparison.Ordinal)), "Terrain data is not isolated.");
        var paths = dataPaths.Concat(new[] { CompactTerrainResize.ScenePath, CompactSceneryPlacement.BaselinePath }).ToArray();
        string backup = "Backups/CompactSecondReduction/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        foreach (var path in paths)
        {
            string destination = backup + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/compact-second-reduction.txt", "BACKUP: " + backup + "\n");
        try
        {
            CompactTerrainResize.ExecuteCoordinated(target);
            CompactSceneryPlacement.RebaseForReduction(target / plan.terrainScale, target);
            var report = CompactEnvironmentFinalization.Audit();
            File.AppendAllText("Logs/compact-second-reduction.txt", report + "\nPASS: current footprint halved; existing tiles now 250 x 200 x 250 m; scenery and water repositioned; original XR/object scales preserved.\n");
            Debug.Log("COMPACT_SECOND_REDUCTION_SUCCESS\n" + report);
        }
        catch (Exception error)
        {
            // Restore exact pre-operation asset bytes as well as the scene/plan.
            // Nothing in the original environment or in project settings is touched.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var path in paths) File.Copy(backup + "/" + path, path, true);
            foreach (var path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(CompactTerrainResize.ScenePath);
            File.AppendAllText("Logs/compact-second-reduction.txt", "ROLLED BACK: " + error + "\n");
            throw;
        }
    }
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
