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
    // Single owner of the outdoor presentation: the approved Pure Nature 2: Mountains
    // demo look. Sky and post profiles are project-owned copies of the vendor assets;
    // the sun arrives at 45 degrees with warm intensity and the exponential blue haze
    // carries the distant peaks, exactly like Mountain_Demo.unity.
    public static class SunPresentationSetup
    {
        public const string Folder = "Assets/Content/Environment/";
        public const string SkyPath = Folder + "ReservoirSky.mat";
        public const string PostPath = Folder + "ReservoirPostProcess.asset";
        private const string VendorSky = "Assets/BK/PureNature_Mountains/Textures/Sky/Sky_Mountains.mat";
        private const string VendorPost = "Assets/BK/PureNature_Mountains/Settings/Mountains_PostProcess.asset";

        [MenuItem("Tools/Something Down There/Configure Approved Sun")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot");
            var camera = root.GetComponentInChildren<Camera>();
            var sun = root.transform.Find("Sun").GetComponent<Light>();
            Undo.RecordObjects(new UnityEngine.Object[] { sun, sun.transform }, "Set approved valley sunlight");
            sun.transform.rotation = Quaternion.Euler(45, 45, 0);
            sun.color = new Color(1, 1, 1);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .9f;
            var sunData = sun.GetComponent<UniversalAdditionalLightData>();
            if (sunData == null) sunData = Undo.AddComponent<UniversalAdditionalLightData>(sun.gameObject);
            Undo.RecordObject(sunData, "Refine valley sun shadows");
            sunData.usePipelineSettings = false;
            sunData.softShadowQuality = SoftShadowQuality.High;
            EditorUtility.SetDirty(sunData);
            EditorUtility.SetDirty(sun);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.622f, .639f, .657f);
            RenderSettings.ambientIntensity = 1.2f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(.162f, .459f, .591f);
            RenderSettings.fogDensity = .003f;
            var sky = SkyMaterial();
            RenderSettings.skybox = sky;
            var skybox = camera.GetComponent<Skybox>();
            if (skybox == null) skybox = Undo.AddComponent<Skybox>(camera.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { camera, skybox }, "Show the valley sky");
            skybox.material = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = RenderSettings.fogColor;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(skybox);
            ConfigurePost(root, camera);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static Material SkyMaterial()
        {
            EnsureFolder();
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                AssetDatabase.CopyAsset(VendorSky, SkyPath);
                sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
                if (sky == null) throw new InvalidOperationException("The approved valley sky could not be copied.");
            }
            if (sky.shader == null || sky.shader.name != "BK/Sky" || ShaderUtil.ShaderHasError(sky.shader))
                throw new InvalidOperationException("The approved valley sky shader must compile before integration.");
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
                if (profile == null) throw new InvalidOperationException("The approved valley post profile could not be copied.");
            }

            var child = root.transform.Find("Daylight Colors");
            if (child == null)
            {
                var go = new GameObject("Daylight Colors");
                Undo.RegisterCreatedObjectUndo(go, "Add valley color volume");
                go.transform.SetParent(root.transform, false);
                child = go.transform;
            }
            var volume = child.GetComponent<Volume>();
            if (volume == null) volume = Undo.AddComponent<Volume>(child.gameObject);
            Undo.RecordObject(volume, "Configure valley colors");
            volume.isGlobal = true;
            volume.weight = 1;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
            Undo.RecordObject(data, "Enable world color grading");
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1 << child.gameObject.layer;
            EditorUtility.SetDirty(data);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Content/Environment"))
                AssetDatabase.CreateFolder("Assets/Content", "Environment");
        }
    }
}
