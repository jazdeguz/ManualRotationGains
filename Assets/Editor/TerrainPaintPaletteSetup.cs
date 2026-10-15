using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TerrainPaintPaletteSetup
{
    const string ScenePath = "Assets/TerrainDemoScene_HDRP/Scenes/TerrainDemoScene.unity";
    const string Output = "Assets/TerrainPaintPalette";
    static readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();

    [MenuItem("Tools/Terrain Environment/Add Trees and Rocks to Paint Palette")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var terrains = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)).ToArray();
        if (terrains.Length == 0) throw new Exception("No terrains found.");
        var backup = "TerrainPaintBackup/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Backup(ScenePath, backup);
        foreach (var data in terrains.Select(t => t.terrainData).Distinct()) Backup(AssetDatabase.GetAssetPath(data), backup);
        Directory.CreateDirectory(Output + "/Prefabs");
        Directory.CreateDirectory(Output + "/Materials");
        AssetDatabase.Refresh();
        var folders = new[] { "Assets/Dry_Trees/Prefab", "Assets/CrackedTree/prefabs", "Assets/TerrainDemoScene_HDRP/Prefabs/Rocks", "Assets/SadmanEnvironment/Prefabs" };
        var paths = folders.Where(AssetDatabase.IsValidFolder)
            .SelectMany(f => AssetDatabase.FindAssets("t:Prefab", new[] { f }))
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p).ToArray();
        var prefabs = paths.Select(CreatePaintPrefab).ToArray();
        var report = new List<string>();
        foreach (var data in terrains.Select(t => t.terrainData).Distinct())
        {
            var instances = data.treeInstances;
            var prototypes = data.treePrototypes.ToList();
            int before = prototypes.Count;
            foreach (var prefab in prefabs)
                if (!prototypes.Any(p => p.prefab == prefab)) prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0 });
            Undo.RecordObject(data, "Add terrain paint palette");
            data.treePrototypes = prototypes.ToArray();
            data.RefreshPrototypes();
            if (data.treeInstanceCount != instances.Length) throw new Exception("Existing instance count changed.");
            EditorUtility.SetDirty(data);
            report.Add($"{AssetDatabase.GetAssetPath(data)}: {before} -> {prototypes.Count} prototypes, {instances.Length} existing instances preserved");
        }
        foreach (var terrain in terrains)
        {
            Undo.RecordObject(terrain, "Enable terrain foliage");
            terrain.drawTreesAndFoliage = true;
            if (terrain.treeDistance <= 0) terrain.treeDistance = 400;
            if (terrain.treeMaximumFullLODCount <= 0) terrain.treeMaximumFullLODCount = 50;
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
            EditorUtility.SetDirty(terrain);
        }
        foreach (var toggle in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TerrainTreeToggle>(true)))
        {
            toggle.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(toggle);
            EditorUtility.SetDirty(toggle);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // Reopen to verify serialized terrain settings and palette references.
        scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var terrain in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>(true)))
        {
            if (!terrain.drawTreesAndFoliage) throw new Exception("Foliage is disabled after save.");
            foreach (var prefab in prefabs)
                if (!terrain.terrainData.treePrototypes.Any(p => p.prefab == prefab)) throw new Exception("Missing palette entry.");
        }
        report.Insert(0, $"Validated {prefabs.Length} paint prefabs on {terrains.Length} terrain tiles. Backup: {backup}");
        report.AddRange(prefabs.Select(AssetDatabase.GetAssetPath));
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/terrain-paint-palette.txt", report);
        Debug.Log("TERRAIN_PALETTE_SUCCESS: " + report[0]);
    }

    static void Backup(string path, string folder)
    {
        var dest = folder + "/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(dest));
        File.Copy(path, dest, false);
    }

    static GameObject CreatePaintPrefab(string path)
    {
        string output = Output + "/Prefabs/Paint_" + Path.GetFileNameWithoutExtension(path) + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(output);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = new GameObject("Paint_" + source.name);
        try
        {
            var child = UnityEngine.Object.Instantiate(source, root.transform);
            child.name = source.name;
            child.transform.localPosition = Vector3.zero;
            var renderers = child.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new Exception("No mesh renderers: " + path);
            foreach (var renderer in renderers)
            {
                if (renderer.GetComponent<MeshFilter>()?.sharedMesh == null) throw new Exception("Missing mesh: " + path);
                renderer.sharedMaterials = renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
            }
            // Terrain needs a root renderer or LODGroup. Keep source mesh scales,
            // colliders and LOD renderers underneath an identity-scale paint root.
            var groups = child.GetComponentsInChildren<LODGroup>(true);
            var group = root.AddComponent<LODGroup>();
            if (groups.Length == 1)
            {
                group.SetLODs(groups[0].GetLODs());
                group.fadeMode = groups[0].fadeMode;
                group.animateCrossFading = groups[0].animateCrossFading;
                UnityEngine.Object.DestroyImmediate(groups[0]);
            }
            else if (groups.Length == 0) group.SetLODs(new[] { new LOD(0.01f, renderers) });
            else throw new Exception("Multiple LOD groups need manual review: " + path);
            group.RecalculateBounds();
            return PrefabUtility.SaveAsPrefabAsset(root, output);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    static Material ConvertMaterial(Material source)
    {
        if (source == null) throw new Exception("Missing source material.");
        if (source.shader != null && source.shader.name.StartsWith("Universal Render Pipeline/")) return source;
        if (Materials.TryGetValue(source, out var cached)) return cached;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new Exception("URP Lit shader missing.");
        var material = new Material(shader) { name = source.name + "_TerrainPaint", enableInstancing = true };
        string textureProperty = new[] { "_BaseColorMap", "_BaseMap", "_MainTex" }.FirstOrDefault(p => source.HasProperty(p) && source.GetTexture(p) != null);
        if (textureProperty != null)
        {
            material.SetTexture("_BaseMap", source.GetTexture(textureProperty));
            material.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
        }
        if (source.HasProperty("_BaseColor")) material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
        else if (source.HasProperty("_Color")) material.SetColor("_BaseColor", source.GetColor("_Color"));
        string normal = new[] { "_NormalMap", "_BumpMap" }.FirstOrDefault(p => source.HasProperty(p) && source.GetTexture(p) != null);
        if (normal != null) { material.SetTexture("_BumpMap", source.GetTexture(normal)); material.EnableKeyword("_NORMALMAP"); }
        material.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : 0.2f);
        material.SetFloat("_Metallic", 0);
        var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        var output = Output + "/Materials/" + source.name + "_" + guid + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(output);
        if (existing != null) { UnityEngine.Object.DestroyImmediate(material); material = existing; }
        else AssetDatabase.CreateAsset(material, output);
        Materials[source] = material;
        return material;
    }
}
