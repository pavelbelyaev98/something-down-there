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
    // The crystal cavern's groves (115): CavernDressing from the crystals Configure Buried Props copies out of the Crystal
    // Caverns demo (BuriedPropsSetup.CavernColumns, CavernSprays), the shards' material, the bloom, and CavernScenery on
    // MainGame's terrain. Run Configure Buried Props first, with the saved MainGame scene open.
    public static class CavernSetup
    {
        private const string Folder = "Assets/Content/Caverns";
        private const string DressingPath = Folder + "/CavernDressing.asset", BloomPath = Folder + "/CrystalCavernBloom.asset",
            ShardsPath = Folder + "/CrystalShards.mat";
        // The crystal cavern's bloom: the glowing crystals spill light into the dark; the site's own bloom is faint.
        private const float BloomIntensity = 1.1f, BloomThreshold = .9f, BloomScatter = .7f;

        [MenuItem("Tools/Something Down There/Configure Caverns")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindAnyObjectByType<TerrainVolume>();
            if (scene.path != "Assets/Scenes/MainGame.unity" || terrain == null) throw new InvalidOperationException("Open the saved MainGame scene first.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "Caverns");

            var dressing = AssetDatabase.LoadAssetAtPath<CavernDressing>(DressingPath);
            if (dressing == null) { dressing = ScriptableObject.CreateInstance<CavernDressing>(); AssetDatabase.CreateAsset(dressing, DressingPath); }
            GameObject[] Props(params string[] names) => names.Select(name =>
                AssetDatabase.LoadAssetAtPath<GameObject>($"{BuriedPropsSetup.CavernFolder}/{name}.prefab")
                ?? throw new InvalidOperationException($"Missing {name} (run Configure Buried Props).")).ToArray();
            dressing.Columns = Props(BuriedPropsSetup.CavernColumns);
            dressing.Sprays = Props(BuriedPropsSetup.CavernSprays);
            // Shards: unlit flecks in the colour each burst gives them.
            var shards = AssetDatabase.LoadAssetAtPath<Material>(ShardsPath);
            if (shards == null) { shards = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(shards, ShardsPath); }
            shards.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(shards);
            dressing.Shards = shards;

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

            var field = UnityEngine.Object.FindObjectsByType<DiscoveryField>(FindObjectsInactive.Include).FirstOrDefault(f => !f.name.Contains("Development"))
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
            Debug.Log($"Caverns configured: {dressing.Columns.Length} column and {dressing.Sprays.Length} spray crystals, field {field.name}.");
        }
    }
}
