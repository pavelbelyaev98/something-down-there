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
    public static class SurfaceGrassSetup
    {
        public const string Folder = "Assets/Content/Nature/";
        public const string GrassPrefab = "Assets/BK/PureNature_Mountains/Prefabs/Plants/Grass1.prefab";
        public const string MaterialPath = Folder + "MountainGrass.mat";

        [MenuItem("Tools/Something Down There/Configure Approved Surface Grass")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MainGame.unity")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var terrain = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TerrainVolume>()).Single();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPrefab);
            if (prefab == null) throw new InvalidOperationException("Import the purchased Pure Nature 2: Mountains pack first.");
            var mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            var source = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            if (ShaderUtil.ShaderHasError(source.shader)) throw new InvalidOperationException("BK grass shader must compile.");
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Content", "Nature");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(source) { name = "MountainGrass" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.enableInstancing = true;
            material.SetFloat("_WindMultiplier", .8f);
            EditorUtility.SetDirty(material);
            var grass = terrain.GetComponent<SurfaceGrassRenderer>();
            if (grass == null) grass = Undo.AddComponent<SurfaceGrassRenderer>(terrain.gameObject);
            var settings = new SerializedObject(grass);
            settings.FindProperty("nearMesh").objectReferenceValue = mesh;
            settings.FindProperty("farMesh").objectReferenceValue = null;
            settings.FindProperty("cellsPerPatch").intValue = 6;
            settings.FindProperty("scaleRange").vector2Value = new Vector2(.95f, 1.35f);
            settings.FindProperty("meshScale").vector3Value = new Vector3(.35f, 1.8f, .35f);
            settings.FindProperty("windPadding").floatValue = .12f;
            settings.FindProperty("material").objectReferenceValue = material;
            settings.ApplyModifiedProperties();
            ConfigureEnvironment(terrain.transform.root);
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
            Undo.RecordObject(renderer, "Configure Pure Nature depth priming");
            renderer.depthPrimingMode = DepthPrimingMode.Forced;
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);
        }
    }
}
