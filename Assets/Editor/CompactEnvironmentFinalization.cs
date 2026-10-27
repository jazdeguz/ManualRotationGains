using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

public static class CompactEnvironmentFinalization
{
    const string SettingsPath = "Assets/CompactEnvironment/CompactRealtime.lighting";

    [MenuItem("Tools/Terrain Environment/Finalize Compact Lighting")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        Check(scene.path == CompactTerrainResize.ScenePath && SceneManager.sceneCount == 1 && !scene.isDirty,
            "Open and save the compact scene by itself first.");
        Check(!EditorApplication.isPlayingOrWillChangePlaymode && !Lightmapping.isRunning, "Exit Play mode/baking first.");
        string backup = "Backups/CompactFinalization/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        File.Copy(CompactTerrainResize.ScenePath, backup + "/TerrainDemoScene_Compact.unity");
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = UnityEngine.Object.Instantiate(Lightmapping.lightingSettings);
            settings.name = "CompactRealtime";
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.bakedGI = false;
        settings.realtimeGI = false;
        settings.autoGenerate = false;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        Lightmapping.lightingSettings = settings;
        Lightmapping.lightingDataAsset = null;
        LightmapSettings.lightmaps = Array.Empty<LightmapData>();
        LightmapSettings.lightProbes = null;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.customReflection = null;

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        int removedMissing = 0;
        foreach (var t in all)
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0) continue;
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
            Check(source.StartsWith("Assets/TerrainDemoScene_HDRP/Prefabs/Lighting/", StringComparison.Ordinal) || t.name == "StaticLightingSky",
                "Unexpected missing gameplay script: " + PathOf(t));
            // These are obsolete HDRP additional light/probe/sky components in
            // this URP scene. Remove instance components, never source prefabs.
            removedMissing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }
        var sun = all.Select(t => t.GetComponent<Light>()).Single(l => l != null && l.name == "MorningSun");
        sun.lightmapBakeType = LightmapBakeType.Realtime;
        RenderSettings.sun = sun;
        PrefabUtility.RecordPrefabInstancePropertyModifications(sun);
        int reflections = 0, probes = 0;
        foreach (var t in all)
        {
            var reflection = t.GetComponent<ReflectionProbe>();
            if (reflection != null)
            {
                reflection.enabled = false;
                reflection.bakedTexture = null;
                reflection.customBakedTexture = null;
                PrefabUtility.RecordPrefabInstancePropertyModifications(reflection);
                reflections++;
            }
            var group = t.GetComponent<LightProbeGroup>();
            if (group != null)
            {
                group.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(group);
                probes++;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Check(EditorSceneManager.SaveScene(scene), "Failed to save compact lighting.");
        EditorSceneManager.OpenScene(CompactTerrainResize.ScenePath);
        var report = Audit();
        report.AppendLine($"Lighting finalized: realtime sun + existing ambient/skybox, {reflections} legacy reflection probes and {probes} light probe groups disabled.");
        report.AppendLine($"Removed {removedMissing} obsolete missing HDRP component references from compact scene instances.");
        report.AppendLine("No lightmap bake required for this realtime configuration. No baked occlusion or NavMesh is assigned.");
        report.AppendLine("Backup: " + backup);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/compact-finalization.txt", report.ToString());
        Debug.Log("COMPACT_FINALIZATION_SUCCESS\n" + report);
    }

    public static StringBuilder Audit()
    {
        var scene = SceneManager.GetActiveScene();
        Check(scene.path == CompactTerrainResize.ScenePath, "Wrong active scene.");
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var missing = all.Where(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0).ToArray();
        Check(missing.Length == 0, "Missing scripts in compact scene: " + string.Join("; ", missing.Select(t => PathOf(t) + " [" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) + "]")));
        Check(Lightmapping.lightingDataAsset == null && LightmapSettings.lightmaps.Length == 0, "Stale lightmap data remains.");
        Check(AssetDatabase.GetAssetPath(Lightmapping.lightingSettings) == SettingsPath && !Lightmapping.lightingSettings.bakedGI,
            "Compact lighting settings were not saved.");
        Check(RenderSettings.sun != null && RenderSettings.sun.enabled && RenderSettings.sun.lightmapBakeType == LightmapBakeType.Realtime,
            "Missing realtime sun.");
        Check(all.Select(t => t.GetComponent<ReflectionProbe>()).Where(p => p != null).All(p => !p.enabled && p.bakedTexture == null && p.customBakedTexture == null),
            "An old reflection probe is still active or has a stale cubemap.");
        var terrains = all.Select(t => t.GetComponent<Terrain>()).Where(t => t != null).ToArray();
        Check(terrains.Length == 16 && terrains.All(t => t.terrainData.size == new Vector3(500, 400, 500)), "Unexpected terrain size.");
        Check(terrains.All(t => t.GetComponent<TerrainCollider>() != null && t.GetComponent<TerrainCollider>().enabled &&
            t.GetComponent<TerrainCollider>().terrainData == t.terrainData), "Missing or mismatched terrain collider.");
        Check(all.Select(t => t.GetComponent<Coin_Collected>()).Count(c => c != null) == 5, "Missing coins.");
        var rig = all.Select(t => t.GetComponent<XROrigin>()).Single(r => r != null);
        Check(rig.transform.lossyScale == Vector3.one && rig.Camera != null && rig.Camera.isActiveAndEnabled, "XR scale/camera changed.");
        foreach (var path in new[] { HoldToReturnToMenu.MenuScenePath, MainMenuController.GameplayScenePath })
            Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), "Scene missing from build: " + path);
        var report = new StringBuilder("PASS: compact scene saved/reloaded; realtime lighting references, 16 terrain/collider pairs, five coins, XR rig and menu/build scene references valid.\n");
        // Report actual collider clearance at the authored head position. This is
        // an obstruction check, not a substitute for physical headset traversal.
        Physics.SyncTransforms();
        var blockers = Physics.OverlapSphere(rig.Camera.transform.position, 0.2f, ~0, QueryTriggerInteraction.Ignore)
            .Where(c => !c.transform.IsChildOf(rig.transform) && c.GetComponentInParent<Coin_Collected>() == null).ToArray();
        Check(blockers.Length == 0, "Spawn head overlaps scenery: " + string.Join(", ", blockers.Select(c => c.name)));
        report.AppendLine("PASS: authored spawn head has 0.2 m collider clearance.");
        int shaderWarnings = 0;
        foreach (var renderer in all.Select(t => t.GetComponent<Renderer>()).Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy))
        foreach (var material in renderer.sharedMaterials)
        {
            if (material == null || material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
            {
                report.AppendLine("REVIEW material: " + renderer.name);
                shaderWarnings++;
            }
        }
        report.AppendLine($"Active renderer missing/error material entries: {shaderWarnings}.");
        Check(shaderWarnings == 0, "Active scenery has missing/error materials; see audit.");
        return report;
    }
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
