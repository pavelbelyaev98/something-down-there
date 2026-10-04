using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // Places the tool rig under the MainGame player camera: the purchased Stylized Western Shovel (Assets/StylizedShovel)
    // for levels 1-6 and the purchased Hand Mining Drill (Assets/HandMiningDrill) for 7-12, both untouched and drawn
    // with project URP materials. The drill's head turns on a pivot named `...Spin` about the drill's axis.
    public static class ToolRigSetup
    {
        public const string Folder = "Assets/Content/ToolRig";
        // Part names the presenter reads: L<from>[-<to>]_<Part>; children of a part show and hide with it.
        public const string ShovelBlade = "L01-06_Blade__Western", Drill = "L07-12_Drill", DrillHead = "L07-12_BitSpin";
        public static IEnumerable<string> PartNames => new[] { ShovelBlade, Drill, DrillHead };

        private const string ShovelVendor = "Assets/StylizedShovel", DrillVendor = "Assets/HandMiningDrill";
        private const string ShovelFolder = Folder + "/WesternShovel", DrillFolder = Folder + "/MiningDrill";
        // The shovel's model stands on its blade's tip (y = 0) with the grip up: its tip sits at z = ShovelTip in the rig.
        // The drill runs along its model's +x with its head in front: the head's point sits at z = DrillTip.
        private const float ShovelTip = 1.245f, DrillTip = 1.3f;

        [MenuItem("Tools/Something Down There/Configure Tool Rig")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var player = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").GetComponentInChildren<FpsPlayer>();
            var camera = player.GetComponentInChildren<Camera>(true).transform;
            var rig = camera.Find("ToolRig");
            if (rig == null) { rig = new GameObject("ToolRig").transform; rig.SetParent(camera, false); }
            var old = rig.Find("Model");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var model = new GameObject("Model").transform;
            model.SetParent(rig, false);
            PlaceShovel(model);
            PlaceDrill(model);
            foreach (var transform in model.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 2;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
            var presenter = rig.TryGetComponent<ToolRigPresenter>(out var existing) ? existing : rig.gameObject.AddComponent<ToolRigPresenter>();
            using (var data = new SerializedObject(presenter))
            {
                data.FindProperty("player").objectReferenceValue = player;
                data.FindProperty("model").objectReferenceValue = model;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Tool rig configured under the MainGame player camera.");
        }

        // The shovel in the rig's model space: its tip toward +z, its strapped side toward the view (as in the vendor's
        // pictures).
        private static void PlaceShovel(Transform model)
        {
            string path = ShovelVendor + "/Mesh/SM_Shovel.fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                throw new InvalidOperationException("Missing the purchased Stylized Western Shovel in " + ShovelVendor + ".");
            var material = ShovelMaterial();
            // The model's material slot points at ours: the pack's demo materials are not kept (art/stylized-western-shovel).
            var slots = importer.GetExternalObjectMap().Where(p => p.Key.type == typeof(Material) && p.Value != material).Select(p => p.Key).ToList();
            foreach (var slot in slots) importer.AddRemap(slot, material);
            if (slots.Count > 0) importer.SaveAndReimport();
            var part = new GameObject(ShovelBlade);
            part.transform.SetParent(model, false);
            // Its y (grip) toward -z, its z (riveted, cupped side) up.
            part.transform.SetLocalPositionAndRotation(new Vector3(0, 0, ShovelTip), Quaternion.Euler(-90, 0, 0));
            part.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<MeshFilter>().sharedMesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        // The drill's meshes copied out of its model (so the head can sit on its own pivot) with their placements,
        // its +x (the head's way) along the rig's +z and its axis on the rig's: the head turns about its pivot's forward.
        private static void PlaceDrill(Transform model)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DrillVendor + "/Fbx/Fbx.fbx");
            if (source == null) throw new InvalidOperationException("Missing the purchased Hand Mining Drill in " + DrillVendor + ".");
            var filters = source.GetComponentsInChildren<MeshFilter>(true);
            var headFilter = filters.Single(f => f.name.Contains("Head"));
            var head = Bounds(headFilter);
            // Built at the origin in the model's own space, then placed.
            var drill = new GameObject(Drill).transform;
            var pivot = new GameObject(DrillHead).transform;
            pivot.SetParent(drill, false);
            pivot.SetLocalPositionAndRotation(new Vector3(head.min.x, head.center.y, head.center.z), Quaternion.LookRotation(Vector3.right, Vector3.up));
            var body = DrillMaterial("Body");
            var parts = DrillMaterial("Parts");
            foreach (var filter in filters)
            {
                var part = new GameObject(filter.name.Trim()).transform;
                part.SetParent(filter == headFilter ? pivot : drill, false);
                part.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                part.localScale = filter.transform.lossyScale;
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                bool isPart = filter.name.Contains("Parts") || filter.name.Contains("fan");
                part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = isPart ? parts : body;
            }
            // Its model's (x, y, z) become the rig's (-z, y, x) shifted so the axis runs along z through the origin and
            // the head's point ends at DrillTip.
            drill.SetParent(model, false);
            drill.SetLocalPositionAndRotation(new Vector3(head.center.z, -head.center.y, DrillTip - head.max.x), Quaternion.Euler(0, -90, 0));
        }

        // A mesh's bounds in its model's space (the asset's root sits at the origin, unrotated).
        private static Bounds Bounds(MeshFilter filter)
        {
            var local = filter.sharedMesh.bounds;
            var world = new Bounds(filter.transform.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                world.Encapsulate(filter.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                    new Vector3(i % 2 == 0 ? -1 : 1, i / 2 % 2 == 0 ? -1 : 1, i / 4 == 0 ? -1 : 1))));
            return world;
        }

        // A project-owned URP copy of the vendor's Standard material on its maps as imported (its metallic-smoothness
        // and occlusion maps import as sRGB, which is how its author saw them; read as linear, the blade turned black).
        private static Material ShovelMaterial()
        {
            if (!AssetDatabase.IsValidFolder(ShovelFolder)) AssetDatabase.CreateFolder(Folder, "WesternShovel");
            var material = LitMaterial(ShovelFolder + "/WesternShovel.mat");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ShovelVendor + "/Textures/T_Shovel_BC.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ShovelVendor + "/Textures/T_Shovel_N.png"));
            // Half the vendor's metallic (a project copy, written by art/stylized-western-shovel/make_half_metal.py): in
            // the game's light the full value read as near-black steel with hot highlights, not the product's grey.
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ShovelFolder + "/T_Shovel_MS_Half.png"));
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ShovelVendor + "/Textures/T_Shovel_AO.png"));
            EditorUtility.SetDirty(material);
            return material;
        }

        // The drill's URP material for one of its two texture sets (Body, Parts), on the project's 2K copies of its maps
        // (art/hand-mining-drill/make_textures.py): colour, OpenGL normal and a mask of metallic (R), occlusion (G) and
        // smoothness (A).
        private static Material DrillMaterial(string set)
        {
            var material = LitMaterial(DrillFolder + "/MiningDrill" + set + ".mat");
            material.SetTexture("_BaseMap", DrillTexture(set + "_Albedo", TextureImporterType.Default, true));
            material.SetTexture("_BumpMap", DrillTexture(set + "_Normal", TextureImporterType.NormalMap, false));
            var mask = DrillTexture(set + "_Mask", TextureImporterType.Default, false);
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetTexture("_OcclusionMap", mask);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LitMaterial(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", 1);
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_OcclusionStrength", 1);
            material.EnableKeyword("_OCCLUSIONMAP");
            return material;
        }

        private static Texture2D DrillTexture(string name, TextureImporterType type, bool colour)
        {
            string path = DrillFolder + "/" + name + ".png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                throw new InvalidOperationException("Missing " + path + " (run art/hand-mining-drill/make_textures.py).");
            importer.textureType = type; importer.sRGBTexture = colour; importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.maxTextureSize = 2048;
            importer.anisoLevel = 4;
            // High-quality block compression (BC7): DXT1's 5-6-5 colours band soft gradients into contour lines once the
            // colour grade lifts contrast.
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
