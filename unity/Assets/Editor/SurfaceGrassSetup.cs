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
            settings.FindProperty("coverage").floatValue = .8f;
            var layers = new[] {
                MeadowLayer("Grass1", 3, .55f, .85f),
                MeadowLayer("Grass2", 2, .4f, .65f),
                MeadowLayer("Grass3", 2, .16f, .6f),
                MeadowLayer("Grass4", 2, .25f, .85f),
                MeadowLayer("Daisy", 2, .18f, 1f),
                MeadowLayer("DaisyBlue", 2, .12f, 1f),
                MeadowLayer("Carot1", 2, .05f, .85f),
                MeadowLayer("Lupin1", 2, .04f, .9f),
                MeadowLayer("Gorse1", 2, .045f, .85f),
                MeadowLayer("Fern1", 2, .08f, .85f),
                MeadowLayer("Sorrel", 2, .12f, .8f)
            };
            var serializedLayers = settings.FindProperty("detailLayers");
            serializedLayers.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
            {
                var item = serializedLayers.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("mesh").objectReferenceValue = layers[i].mesh;
                item.FindPropertyRelative("farMesh").objectReferenceValue = null;
                item.FindPropertyRelative("material").objectReferenceValue = layers[i].material;
                item.FindPropertyRelative("cellsPerPatch").intValue = layers[i].cellsPerPatch;
                item.FindPropertyRelative("coverage").floatValue = layers[i].coverage;
                item.FindPropertyRelative("meshScale").vector3Value = layers[i].meshScale;
                item.FindPropertyRelative("scaleRange").vector2Value = layers[i].scaleRange;
                item.FindPropertyRelative("patchiness").floatValue = layers[i].patchiness;
            }
            settings.ApplyModifiedProperties();
            ConfigureEnvironment(terrain.transform.root);
            ConfigurePipeline();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static SurfaceGrassRenderer.DetailLayer MeadowLayer(string name, int cells, float coverage, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BK/PureNature_Mountains/Prefabs/Plants/" + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing approved meadow plant: " + name);
            var filter = prefab.GetComponentInChildren<MeshFilter>();
            var source = filter.GetComponent<MeshRenderer>().sharedMaterial;
            string folder = Folder + "Meadow";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Meadow");
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else { material.shader = source.shader; material.CopyPropertiesFromMaterial(source); }
            material.enableInstancing = true;
            material.SetFloat("_WindMultiplier", .8f);
            EditorUtility.SetDirty(material);
            return new SurfaceGrassRenderer.DetailLayer {
                mesh = filter.sharedMesh, material = material, cellsPerPatch = cells, coverage = coverage,
                meshScale = Vector3.one * scale, scaleRange = new Vector2(.7f, 1.3f), patchiness = .9f
            };
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
