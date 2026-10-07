using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // The caves' and geodes' glow (115, 110): CavernDressing (the bloom while the view is inside a lit hollow) and
    // CavernScenery on MainGame's terrain, which lights each hollow in its crystals' colour. Run with the saved MainGame
    // scene open.
    public static class CavernSetup
    {
        private const string Folder = "Assets/Content/Caverns";
        private const string DressingPath = Folder + "/CavernDressing.asset", BloomPath = Folder + "/CrystalCavernBloom.asset";
        // A soft bloom: the crystals' glow spills a little into the dark without burning white.
        private const float BloomIntensity = .6f, BloomThreshold = 1.1f, BloomScatter = .65f;

        [MenuItem("Tools/Something Down There/Configure Caverns")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindAnyObjectByType<TerrainVolume>();
            if (scene.path != "Assets/Scenes/MainGame.unity" || terrain == null) throw new InvalidOperationException("Open the saved MainGame scene first.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "Caverns");

            var dressing = AssetDatabase.LoadAssetAtPath<CavernDressing>(DressingPath);
            if (dressing == null) { dressing = ScriptableObject.CreateInstance<CavernDressing>(); AssetDatabase.CreateAsset(dressing, DressingPath); }
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
            Debug.Log("Caverns configured: hollow glow and bloom.");
        }
    }
}
