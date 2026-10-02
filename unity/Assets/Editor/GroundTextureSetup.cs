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
        // The original soil art's own mapping and relief.
        public const float OriginalSoilTileMetres = 2, OriginalSoilRelief = .55f, OriginalStoneRelief = .9f;
        // Applied after opaques it also darkens sunlit creases; at 1.25 every crease between shovel bites
        // turned dug soil into dark-edged blocks. The user chose 0.3 from the admin steps (2026-10-02).
        public const float ContactShadingIntensity = .3f;

        [MenuItem("Tools/Something Down There/Configure Pack Lakebed Ground")]
        public static void ConfigurePackGround()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            ConfigureGroundMaterials(root);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureGroundMaterials(Transform root)
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
            Undo.RecordObject(sediment, "Use pack lakebed ground");
            sediment.shader = shader;
            ConfigureSurfaceCap(sediment, terrain.SurfaceHeight);
            LakebedSiteSetup.ConfigureTopsoil(sediment);
            ConfigureDeposits(sediment);
            EditorUtility.SetDirty(sediment);

            var camp = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (camp == null) throw new InvalidOperationException("Keep the existing camp material.");
            Undo.RecordObject(camp, "Use the pack lakebed surface cap");
            ConfigureSurfaceCap(camp, terrain.SurfaceHeight);
            EditorUtility.SetDirty(camp);
            var settings = new SerializedObject(terrain);
            settings.FindProperty("soilMaterial").objectReferenceValue = sediment;
            ConfigurePourFeedback(settings);
            ConfigureXrayMarker(settings);
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

        // Gravel pours (097) reuse the recovery crumb and dust particle materials.
        public static void ConfigurePourFeedback(SerializedObject terrain)
        {
            terrain.FindProperty("pourChipsMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Folder + "SoilCrumbs.mat");
            terrain.FindProperty("pourDustMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Folder + "SoilDust.mat");
        }

        // Developer ground X-ray markers: one instanced unlit material, coloured per ground at draw time.
        public static void ConfigureXrayMarker(SerializedObject terrain)
        {
            const string path = "Assets/Content/GroundTextures/GroundXrayMarker.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            terrain.FindProperty("groundXrayMarker").objectReferenceValue = material;
        }

        // The lakebed's packed sediment terrain layer.
        public static readonly Color PackedSedimentTint = new Color(1.12f, .9f, .68f, 1);
        public const float PackedSedimentTileMetres = 6;

        // Dry ground never turns glossy; the damp band's terrain layer shares this ceiling.
        public const float MaxGroundSmoothness = .15f;

        // The dig surface cap follows the authored dig ground treatment: the darkest ground on site,
        // blending into the terrain's damp band at the plot outline.
        private static void ConfigureSurfaceCap(Material material, float surfaceHeight)
        {
            LakebedSiteSetup.ConfigureDigGround(material);
            material.SetFloat("_TurfMaskLayout", 1);
            material.SetFloat("_TurfNormalStrength", .8f);
            material.SetFloat("_SurfaceHeight", surfaceHeight);
            material.SetFloat("_TurfDepth", .045f);
            material.SetFloat("_MaxSmoothness", MaxGroundSmoothness);
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
            material.SetColor("_ClayTint", new Color(1.1f, .66f, .4f));
            material.SetFloat("_ClayTileMetres", 2.8f);
            material.SetFloat("_ClayNormalStrength", .22f);
            material.SetTexture("_RockAlbedo", RockDetail("Albedo"));
            material.SetTexture("_RockNormal", RockDetail("Normal"));
            material.SetTexture("_RockMask", null);
            material.SetColor("_RockTint", new Color(.4f, .4f, .4f));
            material.SetFloat("_RockTileMetres", 3.2f);
            material.SetFloat("_RockNormalStrength", .6f);
            // Gravel and concrete: original generated surfaces (the packs have neither).
            material.SetTexture("_GravelAlbedo", DepositTextures.Gravel("Albedo"));
            material.SetTexture("_GravelNormal", DepositTextures.Gravel("Normal"));
            material.SetTexture("_GravelMask", DepositTextures.Gravel("Roughness"));
            material.SetColor("_GravelTint", Color.white);
            material.SetFloat("_GravelTileMetres", 2.5f);
            material.SetFloat("_GravelNormalStrength", .8f);
            material.SetTexture("_ConcreteAlbedo", DepositTextures.Concrete("Albedo"));
            material.SetTexture("_ConcreteNormal", DepositTextures.Concrete("Normal"));
            material.SetTexture("_ConcreteMask", DepositTextures.Concrete("Roughness"));
            // Pale but never glaring under a lamp, and never the boundary's bedrock look.
            material.SetColor("_ConcreteTint", new Color(.8f, .79f, .76f));
            material.SetFloat("_ConcreteTileMetres", 2f);
            material.SetFloat("_ConcreteNormalStrength", .45f);
            // Clay basins' old pond clay: the clay textures, grey-blue, larger and smoother.
            material.SetColor("_PondClayTint", new Color(.68f, .74f, .8f));
            material.SetFloat("_PondClayTileMetres", 3.6f);
            material.SetFloat("_PondClayNormalStrength", .12f);
            // Zone palettes (concept 09 §2): rust-red clay veins in the deep stone, cold ancient rock.
            material.SetColor("_ClayDeepTint", new Color(.86f, .6f, .5f));
            material.SetColor("_RockColdTint", new Color(.9f, .95f, 1.02f));
            material.SetVector("_ZoneDepths", new Vector4(TerrainGround.ZoneBorders[1], TerrainGround.ZoneBorders[2], 3, 0));
            material.SetFloat("_StrataStrength", .13f);
            material.SetFloat("_StrataCool", .6f);
            // Cracks (096): a paler band of angular shards beside a pale mineral line with dark edges.
            material.SetFloat("_FractureTileMetres", .9f);
            material.SetFloat("_FractureShardMetres", .16f);
            material.SetFloat("_FractureLift", .35f);
            material.SetColor("_CrackColour", new Color(.86f, .84f, .78f));
            // Backfill (099): dark loose stones with blocky clay chunks.
            material.SetColor("_BackfillTint", new Color(.62f, .5f, .4f));
            material.SetFloat("_BackfillChunkMetres", .4f);
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

        public static Texture2D PackTexture(string surface, string channel)
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

        [MenuItem("Tools/Something Down There/Configure Original Soil")]
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
            ConfigureSurfaceCap(material, terrain.SurfaceHeight);
            material.SetFloat("_MaskLayout", 0f); // Original: roughness R, contact G, stone coverage B.
            material.SetFloat("_MaxSmoothness", .15f);
            material.SetFloat("_SoilTileMetres", OriginalSoilTileMetres);
            material.SetFloat("_NormalStrength", OriginalSoilRelief);
            material.SetFloat("_StoneNormalStrength", OriginalStoneRelief);
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
            // MainGame's sky and ground-bounce fill belong to SunPresentationSetup; the sun keeps soft
            // shadows so the excavated shape still shades itself.
            SunPresentationSetup.ConfigureAmbient();
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
            EditorUtility.SetDirty(sunData);
            EditorUtility.SetDirty(sun);
            SunPresentationSetup.ConfigureShadowBudget(sun);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/SomethingDownThereURP.asset");
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            settings.FindProperty("m_SoftShadowsSupported").boolValue = true;
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
            // Before-opaque AO needs scene depth before the opaque pass, which forces a full depth
            // prepass of every renderer (~1 ms at 1440p). The 0.18 m contact term is visually
            // identical applied after opaques at half resolution, using the copied depth.
            ao.FindProperty("m_Settings.AfterOpaque").boolValue = true;
            ao.FindProperty("m_Settings.Downsample").boolValue = true;
            ao.FindProperty("m_Settings.Source").enumValueIndex = 0; // Reconstruct from depth; no normals prepass.
            ao.FindProperty("m_Settings.NormalSamples").enumValueIndex = 2;
            ao.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;
            ao.FindProperty("m_Settings.Intensity").floatValue = ContactShadingIntensity;
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
            GraphicsQualitySetup.ConfigureRenderers();
        }

        private static void ConfigureSunBias(Transform root)
        {
            var sun = root.Find("Sun").GetComponent<Light>();
            Undo.RecordObject(sun, "Prevent surface cap self-shadow contours");
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

        internal static void ConfigureImport(string path, string channel, bool packedMask = false)
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
