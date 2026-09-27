using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomethingDownThere.Editor
{
    // The Ambient occlusion setting switches the camera to renderer 1: an exact serialized copy
    // of renderer 0 without renderer features. Any setup that edits renderer 0 re-runs this,
    // so the two can never drift apart; runtime settings never mutate either asset.
    public static class GraphicsQualitySetup
    {
        public const string RendererPath = "Assets/Settings/SomethingDownThereUniversalRenderer.asset";
        public const string WithoutContactShadingPath = "Assets/Settings/SomethingDownThereUniversalRenderer NoContactShading.asset";

        [MenuItem("Tools/Something Down There/Configure Graphics Quality")]
        public static void Configure()
        {
            ConfigureRenderers();
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureRenderers()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset
                ?? throw new InvalidOperationException("MainGame requires its URP pipeline.");
            var main = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath)
                ?? throw new InvalidOperationException("The main Universal renderer is missing.");
            var plain = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(WithoutContactShadingPath);
            if (plain == null)
            {
                plain = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(plain, WithoutContactShadingPath);
            }
            EditorUtility.CopySerialized(main, plain);
            plain.name = Path.GetFileNameWithoutExtension(WithoutContactShadingPath);
            var copy = new SerializedObject(plain);
            copy.FindProperty("m_RendererFeatures").ClearArray();
            copy.FindProperty("m_RendererFeatureMap").ClearArray();
            copy.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(plain);

            var asset = new SerializedObject(pipeline);
            var list = asset.FindProperty("m_RendererDataList");
            list.arraySize = UnityGameSettingsPlatform.WithoutContactShadingRenderer + 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = main;
            list.GetArrayElementAtIndex(UnityGameSettingsPlatform.WithoutContactShadingRenderer).objectReferenceValue = plain;
            asset.FindProperty("m_DefaultRendererIndex").intValue = 0;
            asset.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            // Auto-configure reads GPU and CPU frame timings in players.
            PlayerSettings.enableFrameTimingStats = true;
        }
    }
}
