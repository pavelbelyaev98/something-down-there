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
    // Shared requirements of the approved Pure Nature vegetation and water: the vendor wind
    // globals and the URP depth/opaque textures their shaders sample.
    public static class NatureEnvironmentSetup
    {
        [MenuItem("Tools/Something Down There/Configure Pure Nature Environment")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MainGame.unity")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            ConfigureEnvironment(scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform);
            ConfigurePipeline();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureEnvironment(Transform root)
        {
            // Vendor scripts live in Assembly-CSharp. Editor discovery keeps our
            // asmdefs independent of the imported package's script layout.
            var type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .Single(t => t.FullName == "BKPureNature.BK_EnvironmentManager");
            var manager = root.GetComponentInChildren(type, true);
            if (manager == null)
            {
                var owner = new GameObject("Nature Wind");
                Undo.RegisterCreatedObjectUndo(owner, "Configure nature wind");
                owner.transform.SetParent(root, false);
                manager = Undo.AddComponent(owner, type);
            }
            var settings = new SerializedObject(manager);
            foreach (string property in new[] { "overrideSunColor", "overrideFogColor", "overrideCloudColor", "overrideAmbientColor" })
                settings.FindProperty(property).boolValue = false;
            settings.FindProperty("cloudsMaterial").objectReferenceValue = null;
            settings.FindProperty("directionalLight").objectReferenceValue = null;
            settings.FindProperty("microPower").floatValue = .12f;
            settings.FindProperty("microSpeed").floatValue = 1f;
            settings.FindProperty("renderDistance").floatValue = 100f;
            settings.ApplyModifiedProperties();
        }

        private static void ConfigurePipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("MainGame requires its existing URP pipeline.");
            Undo.RecordObject(pipeline, "Configure Pure Nature URP requirements");
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            var serialized = new SerializedObject(pipeline);
            var renderer = serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(
                serialized.FindProperty("m_DefaultRendererIndex").intValue).objectReferenceValue as UniversalRendererData;
            if (renderer == null) throw new InvalidOperationException("A Universal renderer is required.");
            // URP primes depth only without MSAA; its equal-depth pass then drops the
            // runtime excavation ground. Every anti-aliasing setting shares one path.
            Undo.RecordObject(renderer, "Configure Pure Nature depth priming");
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);
        }
    }
}
