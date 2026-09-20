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
    public static class SunPresentationSetup
    {
        public const string Folder = "Assets/Content/Sun/";
        [MenuItem("Tools/Something Down There/Configure Approved Sun")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot");
            var camera = root.GetComponentInChildren<Camera>();
            var sun = root.transform.Find("Sun").GetComponent<Light>();
            Undo.RecordObjects(new UnityEngine.Object[] { sun, sun.transform }, "Set midday sunlight");
            sun.transform.rotation = Quaternion.Euler(80, -28, 0);
            sun.color = new Color(1, .985f, .95f);
            sun.intensity = 1.55f;
            RenderSettings.sun = sun;
            var importer = AssetImporter.GetAtPath(Folder + "Sun_Disc.png") as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Import the approved Blender sun disc first.");
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var shader = Shader.Find("Something Down There/Sunny Sun Sky");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The sun sky shader must compile before integration.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "SunnySun.mat");
            if (material == null) {
                material = new Material(shader) { name = "SunnySun" };
                AssetDatabase.CreateAsset(material, Folder + "SunnySun.mat");
            }
            Undo.RecordObject(material, "Configure approved sun");
            material.shader = shader;
            material.SetTexture("_SunMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Sun_Disc.png"));
            // Preserve the accepted clear cyan sky independently of cloud art.
            camera.backgroundColor = new Color(.12f, .77f, .85f, 1);
            material.SetColor("_SkyColor", camera.backgroundColor);
            material.SetColor("_HorizonColor", new Color(.42f, .88f, .9f, 1));
            material.SetVector("_SunDirection", -sun.transform.forward);
            // The authored disc occupies ~60% of the 10-degree texture width.
            material.SetFloat("_SunTangentRadius", Mathf.Tan(5 * Mathf.Deg2Rad));
            EditorUtility.SetDirty(material);
            var sky = camera.GetComponent<Skybox>();
            if (sky == null) sky = Undo.AddComponent<Skybox>(camera.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { camera, sky }, "Show approved sun in the existing sky");
            sky.material = material;
            camera.clearFlags = CameraClearFlags.Skybox;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(sky);
            ConfigureColors(root, camera);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureColors(GameObject root, Camera camera)
        {
            const string path = Folder + "NoonColors.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Noon Colors";
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet<ColorAdjustments>(out var colors))
            {
                colors = profile.Add<ColorAdjustments>(false);
                AssetDatabase.AddObjectToAsset(colors, profile);
            }
            // Saturation adds color without lifting the black level underground.
            colors.active = true;
            colors.saturation.Override(18);
            EditorUtility.SetDirty(colors);
            EditorUtility.SetDirty(profile);

            var child = root.transform.Find("Daylight Colors");
            if (child == null)
            {
                var go = new GameObject("Daylight Colors");
                Undo.RegisterCreatedObjectUndo(go, "Add daylight color volume");
                go.transform.SetParent(root.transform, false);
                child = go.transform;
            }
            var volume = child.GetComponent<Volume>();
            if (volume == null) volume = Undo.AddComponent<Volume>(child.gameObject);
            Undo.RecordObject(volume, "Configure daylight colors");
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
    }
}
