using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

/// <summary>Scene-specific placement rules; never edits source prefab or terrain assets.</summary>
public static class CompactSceneryPlacement
{
    public const string BaselinePath = "Assets/CompactEnvironment/SceneryPlacementBaseline.json";
    const string TerrainBaselinePath = "Assets/CompactEnvironment/TerrainResizeBaseline.json";
    const string ReportPath = "Logs/compact-scenery-placement.txt";
    static readonly Dictionary<Transform, string> Ids = new Dictionary<Transform, string>();

    [Serializable] public class Pose
    {
        public string id, path;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation;
    }
    [Serializable] public class Placement
    {
        public string id, name, group, rule;
        public Vector3 originalPosition, targetPosition, targetScale;
        public Vector3 samplePoint;
        public float originalGround, targetGround;
    }
    [Serializable] public class Plan
    {
        public int version = 1;
        public float terrainScale;
        public Vector3 anchor;
        public Pose[] poses;
        public Placement[] placements;
    }

    [MenuItem("Tools/Terrain Environment/Compact Scenery/Preview Placement")]
    public static void Preview() => Execute(false);
    [MenuItem("Tools/Terrain Environment/Compact Scenery/Apply Placement")]
    public static void Apply() => Execute(true);

    public static void Execute(bool apply)
    {
        var scene = SceneManager.GetActiveScene();
        Check(scene.path == CompactTerrainResize.ScenePath && SceneManager.sceneCount == 1,
            "Open the compact scene by itself first.");
        Check(!scene.isDirty && !EditorApplication.isPlayingOrWillChangePlaymode && !Lightmapping.isRunning,
            "Save pending edits and exit Play mode/baking before placement.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        CacheIds(all);
        var byId = all.ToDictionary(Id);
        var terrains = all.Select(t => t.GetComponent<Terrain>()).Where(t => t != null).ToArray();
        var terrainBaseline = JsonUtility.FromJson<CompactTerrainResize.Baseline>(File.ReadAllText(TerrainBaselinePath));
        float scale = terrains[0].terrainData.size.x / terrainBaseline.tiles[0].size.x;
        Check(scale > 0 && scale < 1 && terrains.Length == 16, "Expected the resized 16-tile terrain.");
        foreach (var t in terrains)
        {
            var original = terrainBaseline.tiles.Single(b => b.guid == AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t.terrainData)));
            Check(Vector3.Distance(t.terrainData.size, original.size * scale) < 0.001f &&
                Vector3.Distance(t.transform.position, terrainBaseline.anchor + (original.position - terrainBaseline.anchor) * scale) < 0.001f,
                "Terrain no longer matches its saved scale and anchor.");
        }
        bool saved = File.Exists(BaselinePath);
        var plan = saved ? JsonUtility.FromJson<Plan>(File.ReadAllText(BaselinePath)) : BuildPlan(all, terrains, terrainBaseline.anchor, scale);
        Check(plan.version == 1 && Mathf.Abs(plan.terrainScale - scale) < 0.0001f && plan.poses.Length == all.Length,
            "Scene/terrain changed since placement was planned; review the baseline before proceeding.");
        foreach (var pose in plan.poses) Check(byId.ContainsKey(pose.id), "A baseline transform is missing: " + pose.path);
        var report = new StringBuilder($"Compact scenery {(apply ? "APPLY" : "PREVIEW")}, terrain scale {scale:P0}\n");
        report.AppendLine("Placement uses visible mesh centres; prefab children retain their local layout.");
        report.AppendLine("Cemetery keeps horizontal spacing; each grave follows ground height. Small prop clusters move rigidly.");
        report.AppendLine("Water surface follows the terrain scale/level. All other object scales and rotations are preserved.");
        foreach (var p in plan.placements)
            report.AppendLine($"{p.name} [{p.group}, {p.rule}]: {p.originalPosition:F3} -> {p.targetPosition:F3}; ground {p.originalGround:F3} -> {p.targetGround:F3}");
        Directory.CreateDirectory("Logs");
        if (!apply) { File.WriteAllText(ReportPath, report.ToString()); Debug.Log(report.ToString()); return; }

        // Reject accidental overwriting of manual edits: a transform must still be
        // at its initial pose or at this plan's already-applied pose.
        foreach (var pose in plan.poses)
        {
            var t = byId[pose.id];
            var p = plan.placements.SingleOrDefault(x => x.id == pose.id);
            Check(Quaternion.Angle(t.localRotation, pose.localRotation) < 0.001f, "Rotation was edited: " + pose.path);
            Check(Vector3.Distance(t.localScale, pose.localScale) < 0.001f ||
                (p != null && Vector3.Distance(t.localScale, p.targetScale) < 0.001f), "Scale was edited: " + pose.path);
            Check(Vector3.Distance(t.localPosition, pose.localPosition) < 0.001f ||
                (p != null && Vector3.Distance(t.position, p.targetPosition) < 0.001f), "Position was edited: " + pose.path);
        }
        string backup = "Backups/CompactScenery/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(CompactTerrainResize.ScenePath, backup + "/TerrainDemoScene_Compact.unity");
        if (!saved)
        {
            File.WriteAllText(BaselinePath, JsonUtility.ToJson(plan, true));
            AssetDatabase.ImportAsset(BaselinePath);
        }
        var before = all.Select(Snapshot).ToArray();
        try
        {
            foreach (var p in plan.placements)
            {
                var t = byId[p.id];
                t.position = p.targetPosition;
                t.localScale = p.targetScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
            Physics.SyncTransforms();
            Validate(plan, all, terrains, report);
            EditorSceneManager.MarkSceneDirty(scene);
            Check(EditorSceneManager.SaveScene(scene), "Could not save compact scenery.");
        }
        catch
        {
            foreach (var pose in before)
            {
                var t = byId[pose.id];
                t.localPosition = pose.localPosition; t.localRotation = pose.localRotation; t.localScale = pose.localScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            throw;
        }
        scene = EditorSceneManager.OpenScene(CompactTerrainResize.ScenePath);
        all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        terrains = all.Select(t => t.GetComponent<Terrain>()).Where(t => t != null).ToArray();
        Physics.SyncTransforms();
        Validate(plan, all, terrains, report);
        report.AppendLine("PASS: saved scene reopened and placement validated.");
        report.AppendLine("Scene backup: " + backup);
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log("COMPACT_SCENERY_SUCCESS\n" + report);
        SceneView.RepaintAll();
    }

    static Plan BuildPlan(Transform[] all, Terrain[] terrains, Vector3 anchor, float scale)
    {
        var candidates = all.Where(t => t.parent == null && t.GetComponentsInChildren<Renderer>(true).Length > 0 &&
            t.GetComponent<XROrigin>() == null && t.GetComponent<Canvas>() == null && t.name != "Rocks" && t.name != "Water" && t.name != "Skybox").ToList();
        candidates.AddRange(all.Where(t => t.parent != null && t.parent.name == "Rocks"));
        var water = all.Single(t => t.name == "WaterPlane" && t.parent != null && t.parent.name == "Water");
        Check(candidates.All(t => !candidates.Any(other => t != other && t.IsChildOf(other))), "Nested placement candidates would move twice.");
        var placements = new List<Placement>();
        foreach (var group in candidates.GroupBy(Group))
        {
            var members = group.ToArray();
            var bounds = VisibleBounds(members[0]);
            foreach (var t in members.Skip(1)) bounds.Encapsulate(VisibleBounds(t));
            Vector3 centre = bounds.center;
            Vector3 mapped = anchor + (centre - anchor) * scale;
            Vector3 delta = new Vector3(mapped.x - centre.x, 0, mapped.z - centre.z);
            bool rigid = group.Key.StartsWith("Prop cluster", StringComparison.Ordinal);
            float clusterOld = OriginalHeight(terrains, centre, anchor, scale);
            float clusterNew = Ground(terrains, centre + delta);
            foreach (var t in members)
            {
                Vector3 sample = rigid ? centre : VisibleBounds(t).center;
                float oldGround = rigid ? clusterOld : OriginalHeight(terrains, sample, anchor, scale);
                float newGround = rigid ? clusterNew : Ground(terrains, sample + delta);
                Vector3 target = t.position + delta + Vector3.up * (newGround - oldGround);
                placements.Add(new Placement { id = Id(t), name = PathOf(t), group = group.Key, rule = rigid ? "rigid cluster" : "ground offset",
                    originalPosition = t.position, targetPosition = target, targetScale = t.localScale,
                    samplePoint = sample, originalGround = oldGround, targetGround = newGround });
            }
        }
        placements.Add(new Placement { id = Id(water), name = PathOf(water), group = "Water", rule = "terrain footprint and water level",
            originalPosition = water.position, targetPosition = anchor + (water.position - anchor) * scale,
            targetScale = Vector3.Scale(water.localScale, new Vector3(scale, 1, scale)) });
        return new Plan { terrainScale = scale, anchor = anchor, poses = all.Select(Snapshot).ToArray(), placements = placements.ToArray() };
    }

    static string Group(Transform t)
    {
        string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
        if (source.StartsWith("Assets/Cemetery Kit", StringComparison.Ordinal) || t.name == "blboard") return "Cemetery";
        if (t.name == "SM_fish_midpoly" || t.name == "RustySword") return "Prop cluster: fish and sword A";
        if (t.name == "SM_fish_midpoly (1)" || t.name == "RustySword (1)" || t.name == "RustySword (2)") return "Prop cluster: fish and swords B";
        if (t.name.StartsWith("SM_AFS_Log10", StringComparison.Ordinal)) return "Prop cluster: fallen logs";
        if (t.name == "SM_AFS_Log03_Cinematic" || t.name == "SM_AFS_Log16_Cinematic") return "Prop cluster: paired logs";
        // Root names are not unique for the background rocks; IDs keep them separate.
        return t.name + " (" + Id(t) + ")";
    }

    static Bounds VisibleBounds(Transform t)
    {
        var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        Check(renderers.Length > 0, "No mesh bounds for " + PathOf(t));
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
        return b;
    }
    static float OriginalHeight(Terrain[] terrains, Vector3 point, Vector3 anchor, float scale) =>
        anchor.y + (Ground(terrains, anchor + (point - anchor) * scale) - anchor.y) / scale;
    static float Ground(Terrain[] terrains, Vector3 p)
    {
        var terrain = terrains.SingleOrDefault(t => p.x >= t.transform.position.x && p.x < t.transform.position.x + t.terrainData.size.x &&
            p.z >= t.transform.position.z && p.z < t.transform.position.z + t.terrainData.size.z);
        Check(terrain != null, "Placement sample lies outside terrain: " + p);
        return terrain.SampleHeight(p) + terrain.transform.position.y;
    }

    static void Validate(Plan plan, Transform[] all, Terrain[] terrains, StringBuilder report)
    {
        CacheIds(all);
        var byId = all.ToDictionary(Id);
        Check(all.Length == plan.poses.Length, "Transform count changed.");
        foreach (var pose in plan.poses)
        {
            var t = byId[pose.id];
            var p = plan.placements.SingleOrDefault(x => x.id == pose.id);
            Check(Quaternion.Angle(t.localRotation, pose.localRotation) < 0.001f, "Rotation changed: " + pose.path);
            Check(Vector3.Distance(t.localScale, p == null ? pose.localScale : p.targetScale) < 0.001f, "Scale mismatch: " + pose.path);
            Check(p == null ? Vector3.Distance(t.localPosition, pose.localPosition) < 0.001f : Vector3.Distance(t.position, p.targetPosition) < 0.001f,
                "Position mismatch: " + pose.path);
            if (p != null && p.group != "Water")
            {
                Vector3 sample = p.samplePoint + p.targetPosition - p.originalPosition;
                Check(Mathf.Abs(Ground(terrains, sample) - p.targetGround) < 0.002f, "Ground sample changed: " + pose.path);
                Check(Mathf.Abs((t.position.y - p.targetGround) - (p.originalPosition.y - p.originalGround)) < 0.002f,
                    "Placement height offset was lost: " + pose.path);
            }
        }
        var coins = all.Select(t => t.GetComponent<Coin_Collected>()).Where(c => c != null).ToArray();
        Check(coins.Length == 5, "Expected all five coins.");
        var rig = all.Select(t => t.GetComponent<XROrigin>()).Single(r => r != null);
        foreach (var coin in coins)
        {
            float pickup = new SerializedObject(coin).FindProperty("pickupDistance").floatValue;
            float closest = coin.GetComponentsInChildren<Collider>(true).Where(c => c.enabled)
                .Select(c => Vector3.Distance(c.ClosestPoint(rig.Camera.transform.position), rig.Camera.transform.position)).DefaultIfEmpty(float.PositiveInfinity).Min();
            report.AppendLine($"Coin spawn clearance: {coin.name}, collider distance {closest:F3} m, pickup distance {pickup:F3} m.");
            Check(closest > pickup + 0.1f, "Coin would collect at spawn: " + coin.name);
        }
        report.AppendLine($"PASS: {plan.placements.Length} placements; all {all.Length} transforms accounted for; object sizes, rotations, prefab child layout and XR rig preserved (water footprint intentionally scaled).");
        foreach (string landmark in new[] { "Chest", "Tower_A", "Medieval_Windmill", "ShootingTarget", "blboard" })
        {
            var p = plan.placements.Single(x => x.name == landmark);
            var current = VisibleBounds(byId[p.id]).center;
            var original = current - (p.targetPosition - p.originalPosition);
            float before = Vector2.Distance(new Vector2(original.x, original.z), new Vector2(plan.anchor.x, plan.anchor.z));
            float after = Vector2.Distance(new Vector2(current.x, current.z), new Vector2(plan.anchor.x, plan.anchor.z));
            report.AppendLine($"Landmark horizontal distance: {landmark}: {before:F2} -> {after:F2} m.");
        }
    }
    static Pose Snapshot(Transform t) => new Pose { id = Id(t), path = PathOf(t), localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale };
    static void CacheIds(Transform[] transforms)
    {
        var ids = new GlobalObjectId[transforms.Length];
        GlobalObjectId.GetGlobalObjectIdsSlow(transforms.Cast<UnityEngine.Object>().ToArray(), ids);
        Ids.Clear();
        for (int i = 0; i < transforms.Length; i++) Ids.Add(transforms[i], ids[i].ToString());
    }
    static string Id(Transform t) => Ids[t];
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
