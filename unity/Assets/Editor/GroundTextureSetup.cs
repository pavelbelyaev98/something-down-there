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
        public const string SedimentPath = "Assets/Content/Nature/ReservoirSediment.mat";
        public const string PackTextureFolder = "Assets/Content/Nature/GroundTextures/";

        [MenuItem("Tools/Something Down There/Configure Pack Meadow Ground")]
        public static void ConfigurePackMeadow()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            ConfigureMeadowMaterials(root);
            SurfaceGrassSetup.Configure();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureMeadowMaterials(Transform root)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The ground shader must compile before integration.");
            var terrain = root.GetComponentInChildren<TerrainVolume>();
            var sediment = AssetDatabase.LoadAssetAtPath<Material>(SedimentPath);
            if (sediment == null)
            {
                sediment = new Material(shader) { name = "ReservoirSediment" };
                AssetDatabase.CreateAsset(sediment, SedimentPath);
            }
            Undo.RecordObject(sediment, "Use pack meadow ground");
            sediment.shader = shader;
            foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
            {
                sediment.SetTexture("_Soil" + channel, PackTexture("Mud01", channel));
                // Retain custom art and its material, but don't pull it into the
                // active terrain or player build through dormant comparison slots.
                sediment.SetTexture("_Comparison" + channel, null);
            }
            ConfigurePackTurf(sediment, terrain.SurfaceHeight);
            sediment.SetFloat("_MaskLayout", 1);
            sediment.SetFloat("_SoilTileMetres", 4.2f);
            sediment.SetFloat("_NormalStrength", .45f);
            sediment.SetFloat("_StoneNormalStrength", .55f);
            ConfigureDeposits(sediment);
            sediment.SetFloat("_SoilComparison", 0);
            sediment.SetFloat("_SoilSplitX", terrain.transform.TransformPoint(
                new Vector3(terrain.Dimensions.x * terrain.CellSize * .5f, 0, 0)).x);
            sediment.SetFloat("_ComparisonMaskLayout", 0);
            sediment.SetFloat("_ComparisonTileMetres", 2);
            sediment.SetFloat("_ComparisonNormalStrength", .55f);
            sediment.SetFloat("_ComparisonStoneNormalStrength", .9f);
            EditorUtility.SetDirty(sediment);

            var camp = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (camp == null) throw new InvalidOperationException("Keep the existing camp material.");
            Undo.RecordObject(camp, "Replace custom turf with pack meadow");
            ConfigurePackTurf(camp, terrain.SurfaceHeight);
            camp.SetFloat("_SoilComparison", 0);
            EditorUtility.SetDirty(camp);
            var settings = new SerializedObject(terrain);
            settings.FindProperty("soilMaterial").objectReferenceValue = sediment;
            var preview = settings.FindProperty("untouchedPreview").objectReferenceValue as GameObject;
            if (preview == null) throw new InvalidOperationException("Keep the existing edit-mode preview.");
            settings.ApplyModifiedProperties();
            Assign(preview.GetComponent<Renderer>(), sediment);
            foreach (string side in new[] { "North", "East", "West" })
                Assign(root.Find("Surface/" + side + " rim")?.GetComponent<Renderer>(), sediment);
            Assign(root.Find("Surface/South rim")?.GetComponent<Renderer>(), sediment);
            ConfigureSunBias(root);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        private static void ConfigurePackTurf(Material material, float surfaceHeight)
        {
            foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
                material.SetTexture("_Turf" + channel, PackTexture("Grass01", channel));
            material.SetFloat("_TurfMaskLayout", 1);
            material.SetFloat("_TileMetres", 10f);
            material.SetFloat("_TurfNormalStrength", 1f);
            material.SetFloat("_SurfaceHeight", surfaceHeight);
            material.SetFloat("_TurfDepth", .045f);
            material.SetFloat("_MaxSmoothness", .15f);
            material.SetFloat("_MacroVariation", .06f);
        }

        [MenuItem("Tools/Something Down There/Configure Ground Deposits")]
        public static void ConfigureDeposits()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SedimentPath);
            if (material == null || ShaderUtil.ShaderHasError(material.shader))
                throw new InvalidOperationException("The active ground material must compile first.");
            ConfigureDeposits(material);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureDeposits(Material material)
        {
            // Fine packed grains read as sediment; fractured rock has its own relief.
            material.SetTexture("_ClayAlbedo", PackTexture("Gravel", "Albedo"));
            material.SetTexture("_ClayNormal", PackTexture("Gravel", "Normal"));
            material.SetTexture("_ClayMask", PackTexture("Gravel", "Roughness"));
            material.SetColor("_ClayTint", new Color(.92f, .46f, .27f));
            material.SetFloat("_ClayTileMetres", 2.8f);
            material.SetFloat("_ClayNormalStrength", .22f);
            material.SetTexture("_RockAlbedo", RockDetail("Albedo"));
            material.SetTexture("_RockNormal", RockDetail("Normal"));
            material.SetTexture("_RockMask", null);
            material.SetColor("_RockTint", new Color(.4f, .4f, .4f));
            material.SetFloat("_RockTileMetres", 3.2f);
            material.SetFloat("_RockNormalStrength", .6f);
            EditorUtility.SetDirty(material);
        }

        private static Texture2D RockDetail(string channel)
        {
            string file = "_RockDetail_" + (channel == "Albedo" ? "a" : "n") + ".png";
            string path = PackTextureFolder + file;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null && !AssetDatabase.CopyAsset(
                "Assets/BK/PureNature_Mountains/Models/Rocks/Textures/" + file, path))
                throw new InvalidOperationException("Missing approved rock texture: " + file);
            ConfigureImport(path, channel, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D PackTexture(string surface, string channel)
        {
            string folder = PackTextureFolder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Content/Nature", "GroundTextures");
            string suffix = channel == "Albedo" ? "a" : channel == "Normal" ? "n" : "m";
            string file = surface + "_" + suffix + ".png";
            string path = PackTextureFolder + file;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null &&
                !AssetDatabase.CopyAsset("Assets/BK/PureNature_Mountains/Textures/Surfaces/" + file, path))
                throw new InvalidOperationException("Missing approved ground texture: " + file);
            // Project copies allow close-range filtering and linear masks without
            // modifying vendor imports shared by the demo and the valley terrain.
            ConfigureImport(path, channel, true);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        [MenuItem("Tools/Something Down There/Configure Original Soil With Meadow")]
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
            foreach (string kind in new[] { "Soil" })
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
            foreach (string kind in new[] { "Soil" })
            foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
            {
                string path = Folder + kind + "_" + channel + ".png";
                ConfigureImport(path, channel);
                material.SetTexture("_" + kind + channel, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            ConfigurePackTurf(material, terrain.SurfaceHeight);
            material.SetFloat("_SoilComparison", 0);
            material.SetFloat("_MaskLayout", 0f); // Original: roughness R, contact G, stone coverage B.
            material.SetFloat("_MaxSmoothness", .15f);
            material.SetFloat("_SoilTileMetres", 2f);
            material.SetFloat("_NormalStrength", 0.55f);
            material.SetFloat("_StoneNormalStrength", 0.9f);
            material.SetFloat("_SurfaceHeight", terrain.SurfaceHeight);
            material.SetFloat("_TurfDepth", .045f);
            material.SetFloat("_MacroVariation", 0.06f);
            EditorUtility.SetDirty(material);
            var settings = new SerializedObject(terrain);
            settings.FindProperty("soilMaterial").objectReferenceValue = material;
            var preview = settings.FindProperty("untouchedPreview").objectReferenceValue as GameObject;
            if (preview == null) throw new InvalidOperationException("Keep the existing edit-mode terrain preview.");
            settings.ApplyModifiedProperties();
            Assign(preview.GetComponent<Renderer>(), material);
            foreach (string side in new[] { "North", "South", "East", "West" })
                Assign(root.Find("Surface/" + side + " rim")?.GetComponent<Renderer>(), material);
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
            ConfigureSunBias(root);
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

        private static void ConfigureSunBias(Transform root)
        {
            var sun = root.Find("Sun").GetComponent<Light>();
            Undo.RecordObject(sun, "Prevent turf self-shadow contours");
            // Near-overhead light needs enough depth bias to keep the flat cap
            // from tracing its own tessellation around freshly excavated rims.
            sun.shadowBias = 0.5f;
            EditorUtility.SetDirty(sun);
        }

        private static void Assign(Renderer renderer, Material material)
        {
            if (renderer == null) return;
            Undo.RecordObject(renderer, "Assign original ground surface");
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
        }

        private static void ConfigureImport(string path, string channel, bool packedMask = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = channel == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = channel == "Albedo";
            importer.alphaSource = packedMask && channel == "Roughness"
                ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
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
