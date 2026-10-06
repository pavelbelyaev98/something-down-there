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
    // The caverns' dressing (115): CavernDressing from the props Configure Buried Props copies out of the Crystal Caverns
    // demo (BuriedPropsSetup.CavernRocks, CavernCrystals), the crystal cavern's bloom, and CavernScenery on MainGame's
    // terrain. Run Configure Buried Props first, with the saved MainGame scene open.
    public static class CavernSetup
    {
        private const string Folder = "Assets/Content/Caverns";
        private const string DressingPath = Folder + "/CavernDressing.asset", BloomPath = Folder + "/CrystalCavernBloom.asset";
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
            var rocks = BuriedPropsSetup.CavernRocks.Select(r => r.name).ToArray();
            dressing.Boulders = Props(rocks.Where(n => n.StartsWith("Rock_")).ToArray());
            dressing.Rubble = Props(rocks.Where(n => n.StartsWith("Rubble")).ToArray());
            dressing.Formations = Props(rocks.Where(n => n.StartsWith("BigBlock")).ToArray());
            dressing.Crystals = Props(BuriedPropsSetup.CavernCrystals);

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

            var scenery = terrain.GetComponent<CavernScenery>() ?? Undo.AddComponent<CavernScenery>(terrain.gameObject);
            using (var data = new SerializedObject(scenery))
            {
                data.FindProperty("terrain").objectReferenceValue = terrain;
                data.FindProperty("dressing").objectReferenceValue = dressing;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Caverns configured: {dressing.Boulders.Length} boulders, {dressing.Rubble.Length} rubble, {dressing.Formations.Length} formations, {dressing.Crystals.Length} crystals.");
        }
    }
}
