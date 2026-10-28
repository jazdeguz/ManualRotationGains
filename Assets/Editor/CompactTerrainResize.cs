using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

// Terrain only: moving destination groups is a separate, reviewed operation.
public sealed class CompactTerrainResize : EditorWindow
{
    public const string ScenePath = "Assets/CompactEnvironment/TerrainDemoScene_Compact.unity";
    const string BaselinePath = "Assets/CompactEnvironment/TerrainResizeBaseline.json";
    const string ReportPath = "Logs/compact-terrain-resize.txt";
    [SerializeField] float factor = 0.5f;

    [Serializable] public class Tile
    {
        public string guid;
        public Vector3 position, size;
        public int heightmapResolution, treeCount;
    }

    [Serializable] public class Baseline
    {
        public int version = 1;
        public Vector3 anchor;
        public Tile[] tiles;
        public float seamError;
    }

    [MenuItem("Tools/Terrain Environment/Resize Compact Terrain")]
    static void ShowWindow() => GetWindow<CompactTerrainResize>("Compact Terrain");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Scale relative to the original terrain", EditorStyles.boldLabel);
        factor = EditorGUILayout.Slider("Scale", factor, 0.1f, 1f);
        EditorGUILayout.HelpBox("Open the compact scene and save it first. Only terrain sizes and tile positions change. Separate scenery, water and the XR rig stay in place. 1 restores the baseline terrain geometry. Reapplying 0.5 does not shrink it again.", MessageType.Info);
        if (GUILayout.Button("Preview report")) RunFromWindow(false);
        if (GUILayout.Button("Apply and save terrain")) RunFromWindow(true);
    }

    void RunFromWindow(bool apply)
    {
        try { Execute(factor, apply); }
        catch (Exception e) { Debug.LogException(e); }
    }

    public static void ApplyHalf() => Execute(0.5f, true);

    public static void Execute(float scale, bool apply)
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && !Lightmapping.isRunning,
            "Exit Play mode and finish the lighting bake first.");
        Require(scale >= 0.1f && scale <= 1f && !float.IsNaN(scale), "Scale must be between 0.1 and 1.");
        if (File.Exists(CompactSceneryPlacement.BaselinePath))
        {
            var scenery = JsonUtility.FromJson<CompactSceneryPlacement.Plan>(File.ReadAllText(CompactSceneryPlacement.BaselinePath));
            Require(Mathf.Abs(scale - scenery.terrainScale) < 0.0001f,
                "Scenery is already placed for this terrain scale. Restore the pre-placement scene backup and archive the scenery baseline before changing terrain scale.");
        }
        var scene = SceneManager.GetActiveScene();
        Require(scene.path == ScenePath && SceneManager.sceneCount == 1,
            "Open the compact scene by itself first.");
        Require(!scene.isDirty, "Save or revert pending scene edits before resizing.");
        var terrains = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
        Require(terrains.Length == 16, "Expected all 16 terrain tiles, including inactive tiles.");
        foreach (var terrain in terrains)
        {
            Require(terrain.terrainData != null, "Missing TerrainData.");
            Require(AssetDatabase.GetAssetPath(terrain.terrainData).StartsWith("Assets/CompactEnvironment/TerrainData/", StringComparison.Ordinal),
                "Refusing to modify terrain data outside the compact copy.");
            Require(!EditorUtility.IsDirty(terrain.terrainData), "Save pending terrain edits first.");
            Require(Vector3.Distance(terrain.transform.lossyScale, Vector3.one) < 0.0001f &&
                Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) < 0.001f,
                "Terrain and its ancestors must have unit scale and no rotation.");
            var collider = terrain.GetComponent<TerrainCollider>();
            Require(collider != null && collider.terrainData == terrain.terrainData, "Terrain/collider data mismatch.");
        }
        Require(terrains.Select(t => t.terrainData).Distinct().Count() == 16, "Tiles must have distinct data assets.");
        bool hasBaseline = File.Exists(BaselinePath);
        var baseline = hasBaseline ? JsonUtility.FromJson<Baseline>(File.ReadAllText(BaselinePath)) : Capture(terrains, scene);
        Require(baseline != null && baseline.version == 1 && baseline.tiles.Length == 16, "Invalid baseline.");
        foreach (var terrain in terrains)
        {
            var tile = FindTile(baseline, terrain);
            Require(terrain.terrainData.heightmapResolution == tile.heightmapResolution && terrain.terrainData.treeInstanceCount == tile.treeCount,
                "Terrain resolution or tree count changed since the baseline; review before resizing.");
        }

        var report = new StringBuilder();
        report.AppendLine($"Compact terrain {(apply ? "APPLY" : "PREVIEW")}: {scale:P0} of baseline");
        report.AppendLine($"Anchor at spawn ground: {baseline.anchor.ToString("F4")}");
        report.AppendLine("Only terrain dimensions and tile positions change. Separate scenery and XR transforms are unchanged.");
        foreach (var terrain in terrains.OrderBy(t => t.name))
        {
            var tile = FindTile(baseline, terrain);
            report.AppendLine($"{terrain.name}: size {terrain.terrainData.size} -> {tile.size * scale}; position {terrain.transform.position} -> {Position(baseline, tile, scale)}");
        }
        Directory.CreateDirectory("Logs");
        if (!apply)
        {
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log(report.ToString());
            return;
        }

        // Save a scene snapshot before mutation. The untouched source terrain data
        // remains available, and the baseline permits absolute size/position restore.
        string backup = "Backups/CompactTerrainResize/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(ScenePath, backup + "/TerrainDemoScene_Compact.unity");
        File.WriteAllText(backup + "/TerrainResizeBaseline.json", JsonUtility.ToJson(baseline, true));
        if (!hasBaseline)
        {
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));
            AssetDatabase.ImportAsset(BaselinePath);
        }

        var untouched = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.GetComponent<Terrain>() == null)
            .Select(t => (transform: t, position: t.localPosition, rotation: t.localRotation, scale: t.localScale)).ToArray();
        foreach (var terrain in terrains)
        {
            var tile = FindTile(baseline, terrain);
            terrain.terrainData.size = tile.size * scale;
            terrain.transform.position = Position(baseline, tile, scale);
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain.transform);
            EditorUtility.SetDirty(terrain.terrainData);
            terrain.Flush();
        }
        foreach (var terrain in terrains)
        {
            var p = terrain.transform.position;
            var s = terrain.terrainData.size;
            terrain.SetNeighbors(At(terrains, p.x - s.x, p.z), At(terrains, p.x, p.z + s.z),
                At(terrains, p.x + s.x, p.z), At(terrains, p.x, p.z - s.z));
        }
        foreach (var item in untouched)
            Require(item.transform.localPosition == item.position && item.transform.localRotation == item.rotation && item.transform.localScale == item.scale,
                "A non-terrain transform changed.");
        Validate(terrains, baseline, scale, report);
        foreach (var terrain in terrains) AssetDatabase.SaveAssetIfDirty(terrain.terrainData);
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Failed to save compact scene.");
        // Reload to verify the serialized prefab overrides and collider references.
        scene = EditorSceneManager.OpenScene(ScenePath);
        terrains = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
        Validate(terrains, baseline, scale, report);
        report.AppendLine($"PASS: {untouched.Length} non-terrain transforms preserved; saved scene reopened and validated.");
        report.AppendLine("Scene snapshot: " + backup);
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("COMPACT_TERRAIN_RESIZE_SUCCESS\n" + report);
        SceneView.RepaintAll();
    }

    static Baseline Capture(Terrain[] terrains, Scene scene)
    {
        var rig = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<XROrigin>(true)).Single();
        Vector3 anchor = rig.transform.position;
        var ground = terrains.Single(t => anchor.x >= t.transform.position.x && anchor.x < t.transform.position.x + t.terrainData.size.x &&
            anchor.z >= t.transform.position.z && anchor.z < t.transform.position.z + t.terrainData.size.z);
        anchor.y = ground.SampleHeight(anchor) + ground.transform.position.y;
        return new Baseline
        {
            anchor = anchor,
            seamError = SeamError(terrains, out _),
            tiles = terrains.Select(t => new Tile
            {
                guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t.terrainData)),
                position = t.transform.position, size = t.terrainData.size,
                heightmapResolution = t.terrainData.heightmapResolution, treeCount = t.terrainData.treeInstanceCount
            }).ToArray()
        };
    }

    static Tile FindTile(Baseline baseline, Terrain terrain) => baseline.tiles.Single(t =>
        t.guid == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(terrain.terrainData)));
    static Vector3 Position(Baseline baseline, Tile tile, float scale) => baseline.anchor + (tile.position - baseline.anchor) * scale;
    static Terrain At(Terrain[] terrains, float x, float z) => terrains.SingleOrDefault(t =>
        Mathf.Abs(t.transform.position.x - x) < 0.01f && Mathf.Abs(t.transform.position.z - z) < 0.01f);

    static void Validate(Terrain[] terrains, Baseline baseline, float scale, StringBuilder report)
    {
        Require(terrains.Length == 16, "Tile count changed.");
        foreach (var terrain in terrains)
        {
            var tile = FindTile(baseline, terrain);
            Require(Vector3.Distance(terrain.terrainData.size, tile.size * scale) < 0.001f &&
                Vector3.Distance(terrain.transform.position, Position(baseline, tile, scale)) < 0.001f, "Saved geometry differs from requested scale.");
            Require(terrain.GetComponent<TerrainCollider>().terrainData == terrain.terrainData, "Collider data changed.");
            Require(terrain.terrainData.heightmapResolution == tile.heightmapResolution && terrain.terrainData.treeInstanceCount == tile.treeCount,
                "Heightmap resolution or tree count changed.");
        }
        float seamError = SeamError(terrains, out int edges);
        Require(edges == 24, "Expected 24 shared edges in the 4 x 4 grid.");
        Require(seamError <= baseline.seamError * scale + 0.002f, "Terrain seam height mismatch increased.");
        report.AppendLine($"PASS: 16 tile dimensions/positions and colliders; 24 shared edges; maximum seam height difference {seamError:F6} m (baseline {baseline.seamError:F6} m).");
    }

    static float SeamError(Terrain[] terrains, out int edges)
    {
        float worst = 0;
        edges = 0;
        foreach (var t in terrains)
        {
            var p = t.transform.position;
            var s = t.terrainData.size;
            foreach (bool right in new[] { true, false })
            {
                var other = At(terrains, p.x + (right ? s.x : 0), p.z + (right ? 0 : s.z));
                if (other == null) continue;
                edges++;
                int n = t.terrainData.heightmapResolution;
                Require(other.terrainData.heightmapResolution == n, "Adjacent terrain resolutions differ.");
                for (int i = 0; i < n; i++)
                {
                    float a = t.terrainData.GetHeight(right ? n - 1 : i, right ? i : n - 1) + p.y;
                    float b = other.terrainData.GetHeight(right ? 0 : i, right ? i : 0) + other.transform.position.y;
                    worst = Mathf.Max(worst, Mathf.Abs(a - b));
                }
            }
        }
        return worst;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
