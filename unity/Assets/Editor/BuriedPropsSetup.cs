using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // Project URP copies of the free buried-prop packs: Animated Old Chest (Assets/NOT_Lonely), Big old TV
    // (Assets/JustPlay) and TV Set (Assets/_Television_set). Their own materials are Built-in (Standard specular,
    // Autodesk Interactive, a Shader Forge shader) and draw magenta in URP. The vendor files stay untouched: each prop
    // gets a prefab variant under Content/BuriedProps that swaps in URP Lit materials on the vendor's maps, plus the
    // project masks written by art/old-chest/make_mask.py, art/big-old-tv/make_mask.py and art/tv-set/make_masks.py.
    // The bought Mining Tools, Ore & Ingots (Assets/REAL_DEDICATED, HDRP materials) and Pure Nature Crystal Caverns
    // (Assets/BK, its own crystal shader) props the game buries get the same treatment (113).
    public static class BuriedPropsSetup
    {
        public const string Folder = "Assets/Content/BuriedProps";
        public const string MiningFolder = Folder + "/MiningPack", CrystalFolder = Folder + "/CrystalCaverns";
        public const string MiningVendor = "Assets/REAL_DEDICATED/MiningTools_Ore_Ingots";
        private const string ChestVendor = "Assets/NOT_Lonely/OldChest", OldTvVendor = "Assets/JustPlay/Old TV", TvSetVendor = "Assets/_Television_set";
        private const string CrystalVendor = "Assets/BK/PureNature_CrystalCaverns";
        public static readonly string[] TvEras = { "70", "80", "90", "00" };
        // The pack props finds are made from: ores and ingots in copper, silver and gold, iron ore, and the crystals the
        // gems take (beryl for emerald, ruby, quartz for diamond). The coins went (user, 2026-10-06: messy).
        public static readonly string[] MiningProps =
        {
            "Ore_CopperA", "Ore_CopperB", "Ore_IronA", "Ore_IronB", "Ore_SilverA", "Ore_SilverB", "Ore_GoldA", "Ore_GoldB",
            "Ingot_Copper_Flat", "Ingot_Copper_Thin", "Ingot_Silver_Flat", "Ingot_Silver_Thin", "Ingot_Gold_Flat", "Ingot_Gold_Thin",
        };
        public static readonly string[] CrystalProps =
        {
            "Crystal_Beryl_01", "Crystal_Beryl_02", "Crystal_Ruby_1", "Crystal_Ruby_2", "Crystal_Quartz_1", "Crystal_Quartz_2",
        };
        // Crystal colours of our own on the pack's maps (any bought pack's textures may dress new items): its beryl is
        // aquamarine and its quartz pale blue, so emerald takes a deep green, diamond a clear white with a cold edge, and
        // ruby a deeper red than the pack's flat one.
        private static readonly Dictionary<string, Color> CrystalTints = new Dictionary<string, Color>
        {
            ["Crystal_Beryl_01"] = new Color(.12f, .5f, .24f), ["Crystal_Ruby_1"] = new Color(.5f, .04f, .07f),
            ["Crystal_Quartz_1"] = new Color(.9f, .95f, 1f),
        };
        // Small props seen from a metre or two in a stylized game: their maps import no larger than this.
        private const int PackMapSize = 1024;
        // Ingots after decades in the ground are tarnished, not mirror-bright: this share of the pack's smoothness.
        // Mirror silver at the bottom of an open shaft read sky blue.
        private const float Tarnish = .55f;

        [MenuItem("Tools/Something Down There/Configure Buried Props")]
        public static void Configure()
        {
            foreach (var sub in new[] { "OldChest", "BigOldTV", "TVSet", "MiningPack", "CrystalCaverns" }) EnsureFolder(sub);
            // The chest's specular map becomes a metallic mask (art/old-chest/make_mask.py): buried props draw through the
            // excavation daylight shader, whose specular setup is a shader feature that builds strip.
            var chest = LitMaterial(Folder + "/OldChest/OldChest.mat", Vendor(ChestVendor + "/ModelAndTexture/Chest.tga"),
                Vendor(ChestVendor + "/ModelAndTexture/Chest_N.tga"));
            chest.SetTexture("_MetallicGlossMap", Mask(Folder + "/OldChest/OldChest_Mask.png", "art/old-chest/make_mask.py"));
            chest.SetTexture("_SpecGlossMap", null);
            EditorUtility.SetDirty(chest);

            var oldTv = LitMaterial(Folder + "/BigOldTV/BigOldTV.mat", Vendor(OldTvVendor + "/Textures/Albedo.psd"), Vendor(OldTvVendor + "/Textures/Normal.png"));
            oldTv.SetTexture("_MetallicGlossMap", Mask(Folder + "/BigOldTV/BigOldTV_Mask.png", "art/big-old-tv/make_mask.py"));
            oldTv.SetTexture("_OcclusionMap", Vendor(OldTvVendor + "/Textures/AO.psd"));
            oldTv.SetFloat("_OcclusionStrength", 1); oldTv.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(oldTv);

            Material TvMaterial(string set)
            {
                var material = LitMaterial($"{Folder}/TVSet/{set}.mat", Vendor($"{TvSetVendor}/Sources/Materials/{set}_diff.png"),
                    Vendor($"{TvSetVendor}/Sources/Materials/{set}_normal.png"));
                material.SetTexture("_MetallicGlossMap", Mask($"{Folder}/TVSet/{set}_Mask.png", "art/tv-set/make_masks.py"));
                EditorUtility.SetDirty(material);
                return material;
            }
            var tv12 = TvMaterial("Tv_1_2"); var tv34 = TvMaterial("Tv_3_4");

            Variant(ChestVendor + "/Chest.prefab", Folder + "/OldChest/OldChest.prefab", ("OldChest", chest));
            Variant(OldTvVendor + "/Prefab/Old TV.prefab", Folder + "/BigOldTV/BigOldTV.prefab", ("Old TV", oldTv));
            foreach (var era in TvEras)
                Variant($"{TvSetVendor}/TV_{era}.prefab", $"{Folder}/TVSet/TV_{era}.prefab", ("Tv_1_2", tv12), ("Tv_3_4", tv34));

            foreach (var name in MiningProps)
                PackVariant($"{MiningVendor}/Prefabs/{name}.prefab", $"{MiningFolder}/{name}.prefab",
                    vendor => FromHdrp(vendor, $"{MiningFolder}/{vendor.name}.mat", name.StartsWith("Ore_") ? 1 : Tarnish));
            foreach (var name in CrystalProps)
                PackVariant($"{CrystalVendor}/Prefabs/Crystals/{name}.prefab", $"{CrystalFolder}/{name}.prefab", vendor => FromCrystal(vendor, $"{CrystalFolder}/{vendor.name}.mat"));
            AssetDatabase.SaveAssets();
        }

        // A variant of a pack prefab with each of its materials replaced by a project URP copy.
        private static void PackVariant(string source, string path, Func<Material, Material> convert)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source) ?? throw new InvalidOperationException("Missing " + source + " (reimport the pack).");
            var swaps = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct()
                .Select(m => (m.name, convert(m))).ToArray();
            Variant(source, path, swaps);
        }

        // A URP Lit copy of a Mining Tools, Ore & Ingots HDRP Lit material: its colour map in its tint, its normal map, and
        // its mask, whose metallic (R), occlusion (G) and smoothness (A, times shine) URP Lit reads from the same map.
        internal static Material FromHdrp(Material vendor, string path, float shine = 1)
        {
            var (maps, floats, colors) = Saved(vendor);
            var material = LitMaterial(path, Sized(maps["_BaseColorMap"]), Sized(maps["_NormalMap"]));
            var tint = colors["_BaseColor"]; tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", floats["_NormalScale"]);
            var mask = Sized(maps["_MaskMap"]);
            material.SetTexture("_MetallicGlossMap", mask); material.SetFloat("_Smoothness", floats["_SmoothnessRemapMax"] * shine);
            material.SetTexture("_OcclusionMap", mask); material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        // A URP Lit copy of a Crystal Caverns crystal: its colour map in the crystal's colour and its normal map, glassy and
        // not metal. The pack's crystal shader (translucency, inner glow) is not used: buried finds draw through the
        // excavation daylight's Lit shader.
        private static Material FromCrystal(Material vendor, string path)
        {
            var (maps, floats, colors) = Saved(vendor);
            var material = LitMaterial(path, Sized(maps["_MainTex"]), Sized(maps["_BumpMap"]));
            var tint = CrystalTints.TryGetValue(vendor.name, out var ours) ? ours : colors["_MainColor"]; tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", floats["_NormalPower"]);
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", Mathf.Min(.9f, floats["_Smoothness"]));
            EditorUtility.SetDirty(material);
            return material;
        }

        // A material's saved properties, whatever its shader (the pack's HDRP and crystal shaders do not load here).
        private static (Dictionary<string, Texture2D> maps, Dictionary<string, float> floats, Dictionary<string, Color> colors) Saved(Material material)
        {
            var maps = new Dictionary<string, Texture2D>(); var floats = new Dictionary<string, float>(); var colors = new Dictionary<string, Color>();
            using var data = new SerializedObject(material);
            var saved = data.FindProperty("m_SavedProperties");
            var list = saved.FindPropertyRelative("m_TexEnvs");
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                maps[entry.FindPropertyRelative("first").stringValue] = entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture2D;
            }
            list = saved.FindPropertyRelative("m_Floats");
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                floats[entry.FindPropertyRelative("first").stringValue] = entry.FindPropertyRelative("second").floatValue;
            }
            list = saved.FindPropertyRelative("m_Colors");
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                colors[entry.FindPropertyRelative("first").stringValue] = entry.FindPropertyRelative("second").colorValue;
            }
            return (maps, floats, colors);
        }

        // A pack map imported at PackMapSize at most.
        private static Texture2D Sized(Texture2D map)
        {
            if (map == null) throw new InvalidOperationException("A pack material lost a map (reimport the pack).");
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(map)) is TextureImporter importer && importer.maxTextureSize > PackMapSize)
            {
                importer.maxTextureSize = PackMapSize;
                importer.SaveAndReimport();
            }
            return map;
        }

        // URP Lit, metallic workflow by default: colour, normal and, once set, a metallic (R) / smoothness (A) mask.
        private static Material LitMaterial(string path, Texture2D color, Texture2D normal)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", color);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1); material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_WorkflowMode", 1); material.DisableKeyword("_SPECULAR_SETUP");
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            material.SetFloat("_Surface", 0);
            return material;
        }

        private static Texture2D Vendor(string path) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("Missing " + path + " (reimport the pack).");

        // A project mask: linear, alpha as written, high-quality compression so smooth gloss gradients do not band.
        private static Texture2D Mask(string path, string script)
        {
            AssetDatabase.ImportAsset(path);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                throw new InvalidOperationException("Missing " + path + " (run " + script + ").");
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = false; importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.maxTextureSize = 2048; importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // A variant of the vendor prefab with its Built-in materials swapped (by vendor material name) for ours.
        private static void Variant(string source, string path, params (string vendor, Material ours)[] swaps)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source) ?? throw new InvalidOperationException("Missing " + source);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials
                        .Select(m => m == null ? null : swaps.FirstOrDefault(s => s.vendor == m.name).ours ?? m).ToArray();
                    if (renderer.sharedMaterials.Any(m => m == null || !m.shader.name.StartsWith("Universal Render Pipeline/")))
                        throw new InvalidOperationException(source + ": " + renderer.name + " keeps a non-URP material.");
                }
                PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void EnsureFolder(string sub)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "BuriedProps");
            if (!AssetDatabase.IsValidFolder(Folder + "/" + sub)) AssetDatabase.CreateFolder(Folder, sub);
        }
    }
}
