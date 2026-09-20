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
    public static class GroundTextureSetup
    {
        public const string Folder = "Assets/Content/GroundTextures/";
        public const string MaterialPath = Folder + "GardenGround.mat";
        public const string ShaderName = "Something Down There/Ground Triplanar";

        [MenuItem("Tools/Something Down There/Configure Original Ground Textures")]
        public static void Configure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath && scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode to configure ground textures.");
            Transform root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            var terrain = root.GetComponentInChildren<TerrainVolume>();
            if (terrain == null) throw new InvalidOperationException("MainGame needs its existing terrain.");
            var shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The ground shader must compile before integration.");
            // Validate the complete batch before changing any existing references.
            foreach (string kind in new[] { "Soil", "Turf" })
            foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
                if (AssetImporter.GetAtPath(Folder + kind + "_" + channel + ".png") is not TextureImporter)
                    throw new InvalidOperationException("Missing Blender ground export: " + kind + "_" + channel);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "GardenGround" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            Undo.RecordObject(material, "Configure original ground textures");
            material.shader = shader;
            foreach (string kind in new[] { "Soil", "Turf" })
            foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
            {
                string path = Folder + kind + "_" + channel + ".png";
                ConfigureImport(path, channel);
                material.SetTexture("_" + kind + channel, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            material.SetFloat("_TileMetres", 1.25f);
            material.SetFloat("_SoilTileMetres", 2f);
            material.SetFloat("_NormalStrength", 0.55f);
            material.SetFloat("_StoneNormalStrength", 0.9f);
            material.SetFloat("_TurfNormalStrength", 0.4f);
            material.SetFloat("_SurfaceHeight", terrain.SurfaceHeight);
            material.SetFloat("_TurfDepth", 0.035f);
            material.SetFloat("_MacroVariation", 0.06f);
            EditorUtility.SetDirty(material);
            var settings = new SerializedObject(terrain);
            settings.FindProperty("soilMaterial").objectReferenceValue = material;
            var preview = settings.FindProperty("untouchedPreview").objectReferenceValue as GameObject;
            if (preview == null) throw new InvalidOperationException("Keep the existing edit-mode terrain preview.");
            settings.ApplyModifiedProperties();
            Assign(preview.GetComponent<Renderer>(), material);
            foreach (string side in new[] { "North", "South", "East", "West" })
                Assign(root.Find("Surface/" + side + " rim").GetComponent<Renderer>(), material);
            ConfigureLighting(root);
            if (terrain.GetComponent<ExcavationDaylight>() == null)
                Undo.AddComponent<ExcavationDaylight>(terrain.gameObject);
            var daylight = new SerializedObject(terrain.GetComponent<ExcavationDaylight>());
            daylight.FindProperty("litShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Runtime/Terrain/ExcavationLit.shader");
            daylight.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureLighting(Transform root)
        {
            // Flat sky fill matches the approved valley demo; the sun keeps soft
            // shadows so the excavated shape still shades itself.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.622f, 0.639f, 0.657f);
            RenderSettings.ambientIntensity = 1.2f;
            var sun = root.Find("Sun").GetComponent<Light>();
            Undo.RecordObject(sun, "Restore ground depth lighting");
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .9f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.12f;
            var sunData = sun.GetComponent<UniversalAdditionalLightData>();
            if (sunData == null) sunData = Undo.AddComponent<UniversalAdditionalLightData>(sun.gameObject);
            Undo.RecordObject(sunData, "Refine excavation sun shadows");
            sunData.usePipelineSettings = false;
            sunData.softShadowQuality = SoftShadowQuality.High;
            EditorUtility.SetDirty(sunData);
            EditorUtility.SetDirty(sun);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/SomethingDownThereURP.asset");
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
            settings.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
            settings.FindProperty("m_ShadowCascadeCount").intValue = 4;
            settings.FindProperty("m_ShadowDistance").floatValue = 45;
            settings.FindProperty("m_Cascade4Split").vector3Value = new Vector3(0.1f, 0.26f, 0.55f);
            settings.FindProperty("m_CascadeBorder").floatValue = 0.12f;
            settings.FindProperty("m_SoftShadowQuality").intValue = (int)SoftShadowQuality.High;
            settings.ApplyModifiedProperties();

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/SomethingDownThereUniversalRenderer.asset");
            var contact = renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            if (contact == null)
            {
                contact = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                contact.name = "Ground contact shading";
                AssetDatabase.AddObjectToAsset(contact, renderer);
                renderer.rendererFeatures.Add(contact);
            }
            var ao = new SerializedObject(contact);
            ao.FindProperty("m_Settings.Downsample").boolValue = false;
            ao.FindProperty("m_Settings.Source").enumValueIndex = 0; // Reconstruct from depth; no expensive ground normal prepass.
            ao.FindProperty("m_Settings.NormalSamples").enumValueIndex = 2;
            ao.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;
            ao.FindProperty("m_Settings.Intensity").floatValue = 1.25f;
            ao.FindProperty("m_Settings.Radius").floatValue = 0.18f;
            ao.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.28f;
            ao.FindProperty("m_Settings.Falloff").floatValue = 16;
            ao.FindProperty("m_Settings.Samples").enumValueIndex = 1;
            ao.FindProperty("m_Settings.BlurQuality").enumValueIndex = 0;
            ao.ApplyModifiedProperties();
            contact.SetActive(true);
            contact.Create();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(contact);
        }

        private static void Assign(Renderer renderer, Material material)
        {
            Undo.RecordObject(renderer, "Assign original ground surface");
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        private static void ConfigureImport(string path, string channel)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = channel == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = channel == "Albedo";
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 16;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = 2048,
                format = channel == "Normal" ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,
                compressionQuality = 100
            });
            importer.SaveAndReimport();
        }
    }
}
