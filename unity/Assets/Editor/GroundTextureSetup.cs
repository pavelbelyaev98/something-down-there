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
        public const string ShaderName = "Something Down There/Ground Triplanar";
        public const string SedimentPath = "Assets/Content/Nature/ReservoirSediment.mat";
        public const string PackTextureFolder = "Assets/Content/Nature/GroundTextures/";
        // Applied after opaques it also darkens sunlit creases; at 1.25 every crease between shovel bites
        // turned dug soil into dark-edged blocks. The user settled on 0.45: 0.3 too little, 0.6 too much
        // (2026-10-03). The Developer admin's Contact shading slider tries other values for the session.
        public const float ContactShadingIntensity = .45f;

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

            var settings = new SerializedObject(terrain);
            settings.FindProperty("soilMaterial").objectReferenceValue = sediment;
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

        // The dig surface cap follows the authored dig ground treatment: a little darker than the ground
        // around it, blending into the terrain's damp band at the plot outline.
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
            // Backfill (106): the soil turned over with stones churned in, one texture set built from the packs' own
            // ground textures by art/pure-nature-highlands/make_backfill.py. Its soil repeats about as the dig ground's
            // does (the set holds 2 x 2 soil tiles); a stronger relief turned the lamp-lit clods' creases black.
            material.SetTexture("_BackfillAlbedo", Backfill("Albedo"));
            material.SetTexture("_BackfillNormal", Backfill("Normal"));
            material.SetTexture("_BackfillMask", Backfill("Roughness"));
            material.SetColor("_BackfillTint", Color.white);
            material.SetFloat("_BackfillTileMetres", 7f);
            material.SetFloat("_BackfillNormalStrength", 1f);
            // Geode shell (110): Crystal Caverns' porous rock detail graded dark grey (art/pure-nature-crystal-caverns/make_shell.py).
            material.SetTexture("_ShellAlbedo", Shell("Albedo"));
            material.SetTexture("_ShellNormal", Shell("Normal"));
            material.SetTexture("_ShellMask", Shell("Roughness"));
            material.SetColor("_ShellTint", Color.white);
            material.SetFloat("_ShellTileMetres", 2f);
            material.SetFloat("_ShellNormalStrength", 1f);
            // Cave rock (116): Crystal Caverns' cave wall stone made seamless (art/pure-nature-crystal-caverns/make_cave_rock.py).
            material.SetTexture("_CaveAlbedo", Set("CaveRock", "Albedo", "art/pure-nature-crystal-caverns/make_cave_rock.py"));
            material.SetTexture("_CaveNormal", Set("CaveRock", "Normal", "art/pure-nature-crystal-caverns/make_cave_rock.py"));
            material.SetTexture("_CaveMask", Set("CaveRock", "Roughness", "art/pure-nature-crystal-caverns/make_cave_rock.py"));
            material.SetColor("_CaveTint", Color.white);
            material.SetFloat("_CaveTileMetres", 2.5f);
            material.SetFloat("_CaveNormalStrength", 1f);
            EditorUtility.SetDirty(material);
        }

        // The geode shell's texture set, written by art/pure-nature-crystal-caverns/make_shell.py.
        private static Texture2D Shell(string channel) => Set("GeodeShell", channel, "art/pure-nature-crystal-caverns/make_shell.py");

        // A ground's texture set <name>_Albedo/Normal/Mask, written by its script.
        private static Texture2D Set(string name, string channel, string script)
        {
            string path = Folder + name + "_" + (channel == "Roughness" ? "Mask" : channel) + ".png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                throw new InvalidOperationException("Missing " + path + " (run " + script + ").");
            ConfigureImport(path, channel);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Backfill's one texture set, written by art/pure-nature-highlands/make_backfill.py.
        private static Texture2D Backfill(string channel)
        {
            string path = Folder + "Backfill_" + (channel == "Roughness" ? "Mask" : channel) + ".png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
                throw new InvalidOperationException("Missing " + path + " (run art/pure-nature-highlands/make_backfill.py).");
            ConfigureImport(path, channel);
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

        // The excavation's lighting: the sun's shadows, the contact shading and the daylight that reaches into holes.
        public static void ConfigureExcavationLighting()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath && scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode to configure the excavation lighting.");
            Transform root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            var terrain = root.GetComponentInChildren<TerrainVolume>();
            if (terrain == null) throw new InvalidOperationException("MainGame needs its existing terrain.");
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
            Undo.RecordObject(renderer, "Assign the dig ground");
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
