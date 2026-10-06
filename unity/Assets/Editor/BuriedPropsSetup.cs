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
        // The pack props finds are made from: copper, iron, silver and gold ores, bronze, silver and gold ingots (the
        // shallow chests' bronze, user 2026-10-06), and the crystals the gems take (beryl for emerald, ruby, quartz for diamond). The coins went (user, 2026-10-06: messy).
        public static readonly string[] MiningProps =
        {
            "Ore_CopperA", "Ore_CopperB", "Ore_IronA", "Ore_IronB", "Ore_SilverA", "Ore_SilverB", "Ore_GoldA", "Ore_GoldB",
            "Ingot_Bronze_Flat", "Ingot_Bronze_Thin", "Ingot_Silver_Flat", "Ingot_Silver_Thin", "Ingot_Gold_Flat", "Ingot_Gold_Thin",
        };
        public static readonly string[] CrystalProps =
        {
            "Crystal_Beryl_01", "Crystal_Beryl_02", "Crystal_Ruby_1", "Crystal_Ruby_2", "Crystal_Quartz_1", "Crystal_Quartz_2",
        };
        // The same crystals dirty, as dug out of the ground (114), and pyrite, the ground's fool's gold.
        public static readonly string[] GroundCrystalProps =
        {
            "Crystal_Beryl_01", "Crystal_Beryl_02", "Crystal_Ruby_1", "Crystal_Ruby_2", "Crystal_Quartz_1", "Crystal_Quartz_2",
            "Crystal_Pyrite_1", "Crystal_Pyrite_2",
        };
        // The geodes' crystals (110), from the pack's families the ground and the chests leave unused, clean (they grew in
        // air), each with the mineral it dresses: cobalt clusters for celestine, fluorite's octahedra, and prism clusters for
        // amethyst and citrine, both quartz. (Its gemstones are cut stones, not crystals.)
        public static readonly (string prop, string mineral)[] GeodeCrystalProps =
        {
            ("Crystal_Cobalt_01", "Celestine"), ("Crystal_Cobalt_02", "Celestine"), ("Crystal_Fluorite_01", "Fluorite"), ("Crystal_Fluorite_2", "Fluorite"),
            ("Crystal_Prism_1", "Amethyst"), ("Crystal_Prism_2", "Amethyst"), ("Crystal_Prism_3", "Citrine"), ("Crystal_Prism_4", "Citrine"),
        };
        // Real geode minerals' colours: celestine's pale sky blue, fluorite's sea green, amethyst's purple, citrine's honey.
        // Glossier than the chests' (GeodeGloss) so they sparkle under a lamp.
        private static readonly Dictionary<string, Color> GeodeTints = new Dictionary<string, Color>
        {
            ["Celestine"] = new Color(.42f, .58f, .78f), ["Fluorite"] = new Color(.24f, .58f, .46f),
            ["Amethyst"] = new Color(.42f, .2f, .6f), ["Citrine"] = new Color(.82f, .52f, .14f),
        };
        private const float GeodeGloss = .8f;
        // Crystal colours of our own on the pack's maps (any bought pack's textures may dress new items): its beryl is
        // aquamarine and its quartz pale blue, so emerald takes a deep green, diamond a cool grey-blue and ruby a deep red.
        // Dark enough that sunlight down a shaft shades them instead of burning them white (user, 2026-10-06: the near-
        // white diamond read as a flat white shape); CrystalGloss keeps a glint without a mirror.
        private static readonly Dictionary<string, Color> CrystalTints = new Dictionary<string, Color>
        {
            ["Crystal_Beryl_01"] = new Color(.08f, .38f, .18f), ["Crystal_Ruby_1"] = new Color(.42f, .03f, .06f),
            ["Crystal_Quartz_1"] = new Color(.45f, .5f, .56f),
            // Pyrite, fool's gold: brassy, with a metal's sheen (PyriteMetal), so at a glance it passes for gold.
            ["Crystal_Pyrite_1"] = new Color(.72f, .6f, .3f),
        };
        private const float PyriteMetal = .6f;
        private const float CrystalGloss = .75f;
        // Small props seen from a metre or two in a stylized game: their maps import no larger than this.
        private const int PackMapSize = 1024;
        // Ingots after decades in the ground are aged, dull metal: their colour map at AgedTint, half metal and AgedGloss
        // smooth, so the light shades their faces. Fully metal, they showed only the reflected sky, as a flat glowing
        // colour (user, 2026-10-06: "ingots light is fucked"); mirror silver read sky blue.
        private const float AgedTint = .62f, AgedMetal = .5f, AgedGloss = .42f;

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
                    vendor => FromHdrp(vendor, $"{MiningFolder}/{vendor.name}.mat",
                        name.StartsWith("Ore_Silver") ? PackStyle.Pack : name.StartsWith("Ore_") ? PackStyle.Ore : PackStyle.Aged));
            if (!AssetDatabase.IsValidFolder(MiningFolder + "/Rocks")) AssetDatabase.CreateFolder(MiningFolder, "Rocks");
            foreach (var (rock, output, style) in RockFinds) RockFind(rock, output, style);
            foreach (var name in CrystalProps)
                PackVariant($"{CrystalVendor}/Prefabs/Crystals/{name}.prefab", $"{CrystalFolder}/{name}.prefab", vendor => FromCrystal(vendor, $"{CrystalFolder}/{vendor.name}.mat", false));
            if (!AssetDatabase.IsValidFolder(CrystalFolder + "/Dirty")) AssetDatabase.CreateFolder(CrystalFolder, "Dirty");
            foreach (var name in GroundCrystalProps)
                PackVariant($"{CrystalVendor}/Prefabs/Crystals/{name}.prefab", $"{CrystalFolder}/Dirty/{name}.prefab",
                    vendor => FromCrystal(vendor, $"{CrystalFolder}/Dirty/{vendor.name}_Dirty.mat", true));
            if (!AssetDatabase.IsValidFolder(CrystalFolder + "/Geode")) AssetDatabase.CreateFolder(CrystalFolder, "Geode");
            foreach (var (prop, mineral) in GeodeCrystalProps)
                PackVariant($"{CrystalVendor}/Prefabs/Crystals/{prop}.prefab", $"{CrystalFolder}/Geode/{prop}.prefab",
                    vendor => FromGeodeCrystal(vendor, $"{CrystalFolder}/Geode/{mineral}.mat", GeodeTints[mineral]));
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

        // How a Mining pack material is copied: as the pack has it (silver ore: "could be higher quality", user 2026-10-06);
        // as ore in the ground, plainer and duller, closer to the photo rock the user likes (user, 2026-10-06: "a bit too
        // high quality, too reflective": maps at OreMapSize, the normal map at OreRelief, OreGloss of the smoothness); or as
        // aged metal (AgedTint, AgedMetal, AgedGloss). Iron ore takes its rust-veined colour map (OreColours).
        internal enum PackStyle { Pack, Ore, Aged }
        private const int OreMapSize = 512;
        private const float OreRelief = .7f, OreGloss = .5f;

        // A URP Lit copy of a Mining Tools, Ore & Ingots HDRP Lit material: its colour map in its tint, its normal map, and
        // its mask, whose metallic (R), occlusion (G) and smoothness (A) URP Lit reads from the same map. Aged metal keeps
        // the mask's occlusion only.
        internal static Material FromHdrp(Material vendor, string path, PackStyle style = PackStyle.Pack)
        {
            var (maps, floats, colors) = Saved(vendor);
            int size = style == PackStyle.Ore ? OreMapSize : PackMapSize;
            bool aged = style == PackStyle.Aged;
            var colour = OreColours.TryGetValue(vendor.name, out var own) ? Project(own, "art/mining-pack/make_maps.py", size)
                : Sized(maps["_BaseColorMap"], size);
            var material = LitMaterial(path, colour, Sized(maps["_NormalMap"], size));
            var tint = aged ? colors["_BaseColor"] * AgedTint : colors["_BaseColor"]; tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", floats["_NormalScale"] * (style == PackStyle.Ore ? OreRelief : 1));
            var mask = Sized(maps["_MaskMap"], size);
            if (aged)
            {
                material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic", AgedMetal); material.SetFloat("_Smoothness", AgedGloss);
            }
            else
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetFloat("_Smoothness", floats["_SmoothnessRemapMax"] * (style == PackStyle.Ore ? OreGloss : 1));
            }
            material.SetTexture("_OcclusionMap", mask); material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        // A URP Lit copy of a Crystal Caverns crystal: its colour map in the crystal's colour and its normal map, glassy and
        // not metal. The pack's crystal shader (translucency, inner glow) is not used: buried finds draw through the
        // excavation daylight's Lit shader. A dirty one, for crystals dug out of the ground, takes the dusty copy of its map
        // with its colour baked in (art/pure-nature-crystal-caverns/make_dirty.py) and DirtyGloss.
        private const float DirtyGloss = .35f;
        private static Material FromCrystal(Material vendor, string path, bool dirty)
        {
            var (maps, floats, colors) = Saved(vendor);
            var colour = dirty ? DirtyMap(vendor.name) : Sized(maps["_MainTex"]);
            var material = LitMaterial(path, colour, Sized(maps["_BumpMap"]));
            var tint = dirty ? Color.white : CrystalTints.TryGetValue(vendor.name, out var ours) ? ours : colors["_MainColor"]; tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", floats["_NormalPower"]);
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", vendor.name.Contains("Pyrite") ? PyriteMetal : 0);
            material.SetFloat("_Smoothness", dirty ? DirtyGloss : Mathf.Min(CrystalGloss, floats["_Smoothness"]));
            EditorUtility.SetDirty(material);
            return material;
        }

        // A geode crystal (110): the clean crystal in its geode colour, glossier.
        private static Material FromGeodeCrystal(Material vendor, string path, Color tint)
        {
            var material = FromCrystal(vendor, path, false);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", GeodeGloss);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Iron ore's own colour map: the pack's, its orange veins (read as lava, user 2026-10-06) turned rust.
        private static readonly Dictionary<string, string> OreColours = new Dictionary<string, string>
        {
            ["M_Ore_Iron"] = MiningFolder + "/Ore_Iron_Rust.png",
        };

        // Finds built on the pack's rock models, each a project prefab of its full-detail mesh (copied into Rocks/, so the
        // pack's 4K rock maps stay out of the repository) on a material from 1024 px copies of its maps
        // (art/mining-pack/make_maps.py):
        // - coal on the layered rocks, blocky with bedding planes, black with a dull sheen (user, 2026-10-06: the photo
        //   rock's coal "looks too much like a rock");
        // - native copper (copper's second look, an A/B with the ore chunk) on the knobbly jagged one, metal in copper's
        //   colour.
        internal enum RockStyle { Coal, NativeCopper }
        internal static readonly (string rock, string output, RockStyle style)[] RockFinds =
        {
            ("Layered_Large", "Coal_Layered_Large", RockStyle.Coal), ("Layered_Small", "Coal_Layered_Small", RockStyle.Coal),
            ("Jagged_Large", "Copper_Native", RockStyle.NativeCopper),
        };
        private static readonly Color CoalTint = new Color(.17f, .17f, .18f), CopperTint = new Color(.86f, .52f, .32f);
        private const float CoalGloss = .5f, CopperMetal = .7f, CopperGloss = .55f;

        private static void RockFind(string rock, string output, RockStyle style)
        {
            string family = rock.Split('_')[0], source = $"{MiningVendor}/Prefabs/Rocks_{family}/Rock_{rock}_Plain.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source) ?? throw new InvalidOperationException("Missing " + source + " (reimport the pack).");
            var lods = prefab.GetComponentInChildren<LODGroup>(true)?.GetLODs();
            var detail = (lods != null ? lods[0].renderers[0].GetComponent<MeshFilter>() : prefab.GetComponentInChildren<MeshFilter>(true)).sharedMesh;
            string meshPath = $"{MiningFolder}/Rocks/{rock}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = UnityEngine.Object.Instantiate(detail); AssetDatabase.CreateAsset(mesh, meshPath); }
            else
            {
                // Refilled in place, never copied over (PropBake.Save: a serialized copy scrambles the triangles).
                mesh.Clear(); mesh.indexFormat = detail.indexFormat;
                mesh.SetVertices(detail.vertices); mesh.SetNormals(detail.normals); mesh.SetTangents(detail.tangents);
                mesh.SetUVs(0, detail.uv); mesh.SetTriangles(detail.triangles, 0); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
            }
            mesh.name = rock;

            string maps = $"{MiningFolder}/Rocks/{rock}";
            var material = LitMaterial($"{MiningFolder}/{output}.mat", Project(maps + "_BC.png", "art/mining-pack/make_maps.py"),
                Project(maps + "_N.png", "art/mining-pack/make_maps.py", PackMapSize, true));
            var mask = Project(maps + "_Mask.png", "art/mining-pack/make_maps.py", PackMapSize, false, true);
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", mask); material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_OCCLUSIONMAP");
            bool coal = style == RockStyle.Coal;
            material.SetColor("_BaseColor", coal ? CoalTint : CopperTint);
            material.SetFloat("_Metallic", coal ? 0 : CopperMetal);
            material.SetFloat("_Smoothness", coal ? CoalGloss : CopperGloss);
            EditorUtility.SetDirty(material);

            var root = new GameObject(output);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(root, $"{MiningFolder}/{output}.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // A map of the project's own (written by `script`), imported for a buried prop at `size` at most.
        private static Texture2D Project(string path, string script, int size = PackMapSize, bool normal = false, bool linear = false)
        {
            AssetDatabase.ImportAsset(path);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) throw new InvalidOperationException("Missing " + path + " (run " + script + ").");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !linear; importer.mipmapEnabled = true; importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The dusty colour map of a crystal family ("Crystal_Beryl_01" takes Beryl_Dirty.png).
        private static Texture2D DirtyMap(string vendorMaterial)
        {
            string family = vendorMaterial.Split('_')[1], path = $"{CrystalFolder}/{family}_Dirty.png";
            AssetDatabase.ImportAsset(path);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                throw new InvalidOperationException("Missing " + path + " (run art/pure-nature-crystal-caverns/make_dirty.py).");
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = true;
            importer.maxTextureSize = PackMapSize; importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

        // A pack map imported at `size` (PackMapSize unless given) at most.
        private static Texture2D Sized(Texture2D map, int size = PackMapSize)
        {
            if (map == null) throw new InvalidOperationException("A pack material lost a map (reimport the pack).");
            if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(map)) is TextureImporter importer && importer.maxTextureSize != size)
            {
                importer.maxTextureSize = size;
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
