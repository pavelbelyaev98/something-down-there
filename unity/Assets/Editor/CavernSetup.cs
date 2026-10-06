using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // The crystal cavern's areas (115): CavernDressing from the pieces Configure Buried Props copies out of the Crystal
    // Caverns demo (BuriedPropsSetup.CavernAreas), the crack overlay's material (art/crystal-cracks), the shards, glints
    // and dust a cluster throws (the pack's sparkle and dust flipbook), the bloom, and CavernScenery on MainGame's
    // terrain. Run Configure Buried Props first, with the saved MainGame scene open.
    public static class CavernSetup
    {
        private const string Folder = "Assets/Content/Caverns";
        private const string DressingPath = Folder + "/CavernDressing.asset", BloomPath = Folder + "/CrystalCavernBloom.asset",
            CracksPath = Folder + "/CrystalCracks.mat", CrackMaskPath = Folder + "/CrystalCracks.png", ShardsPath = Folder + "/CrystalShards.mat",
            GlintsPath = Folder + "/CrystalGlints.mat", DustPath = Folder + "/CrystalDust.mat";
        private const string Fx = "Assets/BK/PureNature_CrystalCaverns/Textures/Fx";
        // The crystal cavern's bloom: the glowing crystals spill light into the dark; the site's own bloom is faint.
        private const float BloomIntensity = 1.1f, BloomThreshold = .9f, BloomScatter = .7f;
        // Shards glow a little in their crystal's colour; glints are bright; the crack overlay's lines are glossy.
        private const float ShardGlow = 1.2f, GlintIntensity = 2.5f, CrackGloss = .85f;
        private const int FxMapSize = 1024, GlintMapSize = 512;

        [MenuItem("Tools/Something Down There/Configure Caverns")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindAnyObjectByType<TerrainVolume>();
            if (scene.path != "Assets/Scenes/MainGame.unity" || terrain == null) throw new InvalidOperationException("Open the saved MainGame scene first.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "Caverns");

            var dressing = AssetDatabase.LoadAssetAtPath<CavernDressing>(DressingPath);
            if (dressing == null) { dressing = ScriptableObject.CreateInstance<CavernDressing>(); AssetDatabase.CreateAsset(dressing, DressingPath); }
            GameObject[] Props(string[] names) => names.Select(name =>
                AssetDatabase.LoadAssetAtPath<GameObject>($"{BuriedPropsSetup.CavernFolder}/{name}.prefab")
                ?? throw new InvalidOperationException($"Missing {name} (run Configure Buried Props).")).ToArray();
            dressing.Areas = BuriedPropsSetup.CavernAreas.Select((area, i) => new CavernDressing.Area
            {
                Formations = Props(area.formations), Clusters = Props(area.clusters),
                Glows = i != (int)TerrainGround.CavernArea.Cubes,
            }).ToArray();

            dressing.Cracks = Cracks();
            dressing.Shards = Particles(ShardsPath, "Something Down There/Soil Break", m => { m.SetFloat("_Solid", 1); m.SetFloat("_Glow", ShardGlow); });
            dressing.Glints = Particles(GlintsPath, "Something Down There/Glow Particles", m =>
            {
                m.SetTexture("_MainTex", Capped($"{Fx}/CrystalSparkle_a.png", GlintMapSize));
                m.SetFloat("_Intensity", GlintIntensity);
            });
            dressing.Dust = Particles(DustPath, "Something Down There/Soil Break", m =>
            {
                m.SetFloat("_Dust", 1); m.SetFloat("_Flipbook", 1);
                m.SetTexture("_DustTex", Capped($"{Fx}/Dust_a.png", FxMapSize));
            });

            var bloomProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomPath);
            if (bloomProfile == null) { bloomProfile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(bloomProfile, BloomPath); }
            if (!bloomProfile.TryGet<Bloom>(out var bloom)) { bloom = bloomProfile.Add<Bloom>(true); AssetDatabase.AddObjectToAsset(bloom, bloomProfile); }
            bloom.active = true;
            bloom.intensity.Override(BloomIntensity);
            bloom.threshold.Override(BloomThreshold);
            bloom.scatter.Override(BloomScatter);
            EditorUtility.SetDirty(bloom); EditorUtility.SetDirty(bloomProfile);
            dressing.Bloom = bloomProfile;
            EditorUtility.SetDirty(dressing);
            AssetDatabase.SaveAssets();

            var field = UnityEngine.Object.FindAnyObjectByType<DiscoveryField>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("MainGame has no discovery field.");
            var scenery = terrain.GetComponent<CavernScenery>() ?? Undo.AddComponent<CavernScenery>(terrain.gameObject);
            using (var data = new SerializedObject(scenery))
            {
                data.FindProperty("terrain").objectReferenceValue = terrain;
                data.FindProperty("field").objectReferenceValue = field;
                data.FindProperty("dressing").objectReferenceValue = dressing;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Caverns configured: {dressing.Areas.Length} areas, {dressing.Areas.Sum(a => a.Formations.Length)} formation and {dressing.Areas.Sum(a => a.Clusters.Length)} cluster kinds.");
        }

        // The crack overlay: URP Lit clipped by the crack mask's alpha (which is when each line appears), its lines glossy and
        // glowing; CavernCrystal moves the clip threshold, CavernScenery the glow's colour.
        private static Material Cracks()
        {
            AssetDatabase.ImportAsset(CrackMaskPath);
            if (!(AssetImporter.GetAtPath(CrackMaskPath) is TextureImporter importer))
                throw new InvalidOperationException("Missing " + CrackMaskPath + " (run art/crystal-cracks/make_cracks.py).");
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true; importer.mipMapsPreserveCoverage = true; importer.alphaTestReferenceValue = .5f;
            importer.SaveAndReimport();
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(CrackMaskPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CracksPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, CracksPath); }
            material.SetTexture("_BaseMap", mask); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1); material.EnableKeyword("_ALPHATEST_ON"); material.SetFloat("_Cutoff", 1.01f);
            material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = (int)RenderQueue.AlphaTest;
            material.SetTexture("_EmissionMap", mask); material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION"); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", CrackGloss);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Particles(string path, string shaderName, Action<Material> set)
        {
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Missing shader " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            set(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A pack FX texture capped at size (the import is the pack's, only its size changes).
        internal static Texture2D Capped(string path, int size)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.maxTextureSize != size)
            {
                importer.maxTextureSize = size;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("Missing " + path + " (reimport the pack).");
        }
    }
}
