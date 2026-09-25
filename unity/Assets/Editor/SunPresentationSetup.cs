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
    // MainGame lighting with the approved pack sky and overhead midday sun.
    public static class SunPresentationSetup
    {
        public const string Folder = "Assets/Content/Environment/";
        public const string SkyPath = Folder + "LakebedSky.mat";
        public const string PostPath = Folder + "ReservoirPostProcess.asset";
        private const string VendorSky = "Assets/BK/PureNature_Highlands/Textures/Sky/Sky_Highlands.mat";
        private const string VendorPost = "Assets/BK/PureNature_Highlands/Settings/Highlands_PostProcess.asset";

        [MenuItem("Tools/Something Down There/Configure Approved Sun")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot");
            var camera = root.GetComponentInChildren<Camera>();
            var sun = root.transform.Find("Sun").GetComponent<Light>();
            Undo.RecordObjects(new UnityEngine.Object[] { sun, sun.transform }, "Set approved midday site sunlight");
            sun.transform.rotation = Quaternion.Euler(88, 45, 0);
            // Highlands demo sun colour and strength; the site keeps its near-overhead noon angle.
            sun.color = new Color(1, .964f, .836f);
            sun.intensity = 1;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .9f;
            ConfigureShadowBudget(sun);
            EditorUtility.SetDirty(sun);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.736f, .736f, .736f);
            RenderSettings.ambientIntensity = 1;
            // The Highlands demo's aerial haze carries the lakebed canyon and distant peaks.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(.359f, .519f, .783f);
            RenderSettings.fogDensity = .001f;
            var sky = SkyMaterial();
            // The demo's pinpoint sun reads as a star overhead; keep a clear disc with a soft glow.
            Undo.RecordObject(sky, "Size the midday sun disc");
            sky.SetFloat("_SunSize", .05f);
            sky.SetFloat("_SunSizeConvergence", 4);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            var skybox = camera.GetComponent<Skybox>();
            if (skybox == null) skybox = Undo.AddComponent<Skybox>(camera.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { camera, skybox }, "Show the midday site sky");
            skybox.material = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = RenderSettings.fogColor;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(skybox);
            ConfigurePost(root, camera);
            ConfigureReflections();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureShadowBudget(Light sun)
        {
            var sunData = sun.GetComponent<UniversalAdditionalLightData>();
            if (sunData == null) sunData = Undo.AddComponent<UniversalAdditionalLightData>(sun.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { sun, sunData }, "Budget soft worksite shadows");
            sun.shadows = LightShadows.Soft;
            sunData.usePipelineSettings = false;
            sunData.softShadowQuality = SoftShadowQuality.Medium;
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(sunData);
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("The lakebed requires URP.");
            Undo.RecordObject(pipeline, "Budget worksite sun cascades");
            pipeline.mainLightShadowmapResolution = 2048;
            pipeline.shadowCascadeCount = 2;
            // URP shares this range with point-light shadows. Never shorten the lamp range.
            pipeline.shadowDistance = WorksiteTools.LightCullDistance + WorksiteTools.LightRange;
            pipeline.cascade2Split = .3f;
            pipeline.cascadeBorder = .2f;
            EditorUtility.SetDirty(pipeline);
        }

        private static Material SkyMaterial()
        {
            EnsureFolder();
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                AssetDatabase.CopyAsset(VendorSky, SkyPath);
                sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
                if (sky == null) throw new InvalidOperationException("The approved midday site sky could not be copied.");
            }
            if (sky.shader == null || sky.shader.name != "BK/Sky" || ShaderUtil.ShaderHasError(sky.shader))
                throw new InvalidOperationException("The approved midday site sky shader must compile before integration.");
            return sky;
        }

        private static void ConfigurePost(GameObject root, Camera camera)
        {
            EnsureFolder();
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
            if (profile == null)
            {
                AssetDatabase.CopyAsset(VendorPost, PostPath);
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostPath);
                if (profile == null) throw new InvalidOperationException("The approved midday site post profile could not be copied.");
                if (profile.TryGet<Bloom>(out var bloom))
                {
                    bloom.intensity.Override(.12f);
                    bloom.threshold.Override(1.1f);
                    EditorUtility.SetDirty(bloom);
                }
            }

            var child = root.transform.Find("Daylight Colors");
            if (child == null)
            {
                var go = new GameObject("Daylight Colors");
                Undo.RegisterCreatedObjectUndo(go, "Add midday site color volume");
                go.transform.SetParent(root.transform, false);
                child = go.transform;
            }
            var volume = child.GetComponent<Volume>();
            if (volume == null) volume = Undo.AddComponent<Volume>(child.gameObject);
            Undo.RecordObject(volume, "Configure midday site colors");
            volume.isGlobal = true;
            volume.weight = 1;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
            Undo.RecordObject(data, "Enable world color grading");
            data.renderPostProcessing = true;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
            data.volumeLayerMask |= 1 << child.gameObject.layer;
            EditorUtility.SetDirty(data);
        }

        private static void ConfigureReflections()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("The lakebed requires URP.");
            Undo.RecordObject(pipeline, "Enable Highlands water reflection projection");
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
            settings.FindProperty("m_ReflectionProbeBlending").boolValue = true;
            settings.ApplyModifiedProperties();
            EditorUtility.SetDirty(pipeline);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Content/Environment"))
                AssetDatabase.CreateFolder("Assets/Content", "Environment");
        }
    }
}
