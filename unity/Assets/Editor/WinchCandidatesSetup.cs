using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // Stands the two rim-winch candidates (art/winch-candidates) on the camp arc beside the working
    // winch so the user can pick one in play. They are props only: nothing references them, and the
    // working SalvageWinch keeps its own fixture until the winner replaces it. Each model keeps its
    // articulation (a fixed root and a slewing root parenting drum, sheaves, rope, jib and the
    // attachment that clamps onto the find), standing in its home pose.
    public static class WinchCandidatesSetup
    {
        public const string Folder = "Assets/Content/WinchCandidates";
        private const string Prefix = "SDT_Winch_";
        private static readonly Dictionary<string, (Color colour, float metallic, float smoothness)> Looks = new()
        {
            ["Yellow"] = (new Color(.72f, .43f, .07f), .1f, .35f),
            ["Blue"] = (new Color(.16f, .31f, .42f), .1f, .35f),
            ["DarkSteel"] = (new Color(.16f, .17f, .18f), .6f, .3f),
            ["Steel"] = (new Color(.55f, .56f, .57f), .75f, .45f),
            ["Grey"] = (new Color(.42f, .44f, .45f), .3f, .35f),
            ["Rope"] = (new Color(.52f, .5f, .46f), .45f, .4f),
            ["Rubber"] = (new Color(.05f, .05f, .05f), 0f, .2f),
            ["Black"] = (new Color(.06f, .06f, .065f), .1f, .3f),
            ["Red"] = (new Color(.62f, .09f, .06f), .1f, .4f),
            ["Green"] = (new Color(.12f, .45f, .16f), .1f, .4f),
            ["Amber"] = (new Color(.95f, .55f, .08f), 0f, .6f),
            ["Cream"] = (new Color(.86f, .82f, .7f), 0f, .3f),
            ["Wood"] = (new Color(.42f, .27f, .15f), 0f, .2f),
            ["Rust"] = (new Color(.38f, .2f, .1f), .3f, .2f),
            ["Canvas"] = (new Color(.44f, .39f, .27f), 0f, .15f)
        };
        // Bearing on the camp arc and metres past the opening edge for each machine's origin: the
        // base stands behind the survey tape (LakebedSiteSetup.TapeOffset) and the attachment hangs
        // out over the player's hole.
        private static readonly (string model, float compass, float offset)[] Placements =
        {
            ("RimWinchA", 120.5f, 5.05f),
            ("RimWinchB", 220f, 6.45f)
        };

        [MenuItem("Tools/Something Down There/Place Winch Candidates")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var materials = Looks.ToDictionary(pair => pair.Key, pair => Material(pair.Key, pair.Value));
            var surface = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform.Find("Surface");
            var group = surface.Find("WinchCandidates");
            if (group == null) { group = new GameObject("WinchCandidates").transform; group.SetParent(surface, false); }
            foreach (var (name, compass, offset) in Placements)
            {
                string path = Folder + "/Models/" + name + ".fbx";
                Import(path, materials);
                var old = group.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), group);
                model.name = name;
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.sharedMaterials.Any(m => m == null || !AssetDatabase.GetAssetPath(m).StartsWith(Folder + "/")))
                        throw new InvalidOperationException("Unmapped winch material on " + renderer.name);
                // Solid machine parts collide; ropes and the hanging attachment stay clear of the hole.
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                    if (!new[] { "_Rope", "_Pendants", "_Attachment" }.Any(filter.name.EndsWith))
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                var spot = SiteLayout.OpeningPoint(compass, offset);
                var attachment = model.GetComponentsInChildren<Transform>().Single(t => t.name.EndsWith("_Attachment"));
                var drop = model.transform.InverseTransformPoint(attachment.position);
                var inward = new Vector3(-spot.x, 0, -spot.y);
                model.transform.SetPositionAndRotation(new Vector3(spot.x, SiteLayout.GroundTop, spot.y),
                    Quaternion.LookRotation(inward) * Quaternion.Inverse(Quaternion.LookRotation(new Vector3(drop.x, 0, drop.z))));
            }
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Winch candidates placed in MainGame.");
        }

        // Every face carries a shared Blender material; remap each one by name to its project copy.
        private static void Import(string path, Dictionary<string, Material> materials)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing " + path + "; run art/winch-candidates/create_assets.py.");
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.bakeAxisConversion = true; importer.isReadable = true; importer.generateMeshLods = true;
            importer.SetPreBakeCollisionMesh(false, true); // Frame and Drum carry triangle MeshColliders
            importer.SaveAndReimport();
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).ToArray())
            {
                if (!source.StartsWith(Prefix) || !materials.TryGetValue(source.Substring(Prefix.Length), out var material))
                    throw new InvalidOperationException("No project material for " + source);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), material);
            }
            importer.SaveAndReimport();
        }

        private static Material Material(string name, (Color colour, float metallic, float smoothness) look)
        {
            string path = Folder + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.SetColor("_BaseColor", look.colour); result.SetFloat("_Metallic", look.metallic); result.SetFloat("_Smoothness", look.smoothness);
            EditorUtility.SetDirty(result);
            return result;
        }
    }
}
