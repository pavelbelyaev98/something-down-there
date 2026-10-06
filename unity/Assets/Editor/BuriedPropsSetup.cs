using System;
using System.Collections.Generic;
using System.IO;
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
        // The pack props finds are made from: copper ore, and the chests' bronze, silver and gold ingots (user, 2026-10-06:
        // chests hold ingots only). Iron, silver and gold are native nuggets (RockFinds); the coins went (user: messy).
        public static readonly string[] MiningProps =
        {
            "Ore_CopperA", "Ore_CopperB",
            "Ingot_Bronze_Flat", "Ingot_Bronze_Thin", "Ingot_Silver_Flat", "Ingot_Silver_Thin", "Ingot_Gold_Flat", "Ingot_Gold_Thin",
        };
        // The crystals the ground's gems take, dirty as dug out of the ground (114): beryl for emerald, ruby, quartz for
        // diamond, and pyrite, the ground's fool's gold.
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
        // The crystal cavern's areas (115, CavernDressing; in TerrainGround.CavernArea order), each from the pieces the
        // Crystal Caverns demo builds that area of: its big formations, the clusters the tool breaks, and the small crystals
        // of the same kind that fall out of a cluster or lie in the stone (finds, cavern.json). Crystals glow, white for
        // CavernScenery to tint, the finds in their area's colour; the cubic blocks keep the pack's rock. No thin single
        // prisms or slabs as clusters (the pack's ruby 3, 4, 7, 8 and its long red sprays): alone they read as sticks
        // (user, 2026-10-06).
        public const string CavernFolder = CrystalFolder + "/Cavern";
        internal static readonly (string[] formations, string[] clusters, string[] finds)[] CavernAreas =
        {
            (new[] { "Crystal_BigHex_1", "Crystal_BigHex_2", "Crystal_BigHex_5", "Crystal_BigHex_6", "Crystal_BigHex_7", "Crystal_BigHex_8", "Crystal_BigHex_9" },
                new[] { "Crystal_Prism_1", "Crystal_Prism_2", "Crystal_Prism_3", "Crystal_Prism_4" }, new[] { "Crystal_Prism_3", "Crystal_Prism_4" }),
            (new[] { "Crystal_Quartz_1", "Crystal_Quartz_2", "Crystal_Quartz_3", "Crystal_Quartz_4", "Crystal_Beryl_01", "Crystal_Beryl_02" },
                new[] { "Crystal_Beryl_03", "Crystal_Beryl_04", "Crystal_Beryl_05", "Crystal_Beryl_06", "Crystal_Beryl_07" }, new[] { "Crystal_Beryl_03", "Crystal_Beryl_05" }),
            (new[] { "CrystalBig1_0", "CrystalBig1_1", "CrystalBig1_5", "CrystalBig1_6", "Crystal_Ruby_1" },
                new[] { "Crystal_Ruby_2", "Crystal_Ruby_5", "Crystal_Ruby_6" }, new[] { "Crystal_Ruby_5", "Crystal_Ruby_9" }),
            (new[] { "BigBlock_1", "BigBlock_2", "BigBlock_3", "BigBlock_4", "BigBlock_5" },
                new[] { "Crystal_Pyrite_1", "Crystal_Pyrite_2", "Crystal_Pyrite_3", "Crystal_Pyrite_4" }, new[] { "Crystal_Pyrite_6", "Crystal_Pyrite_8" }),
        };
        internal static string CavernVendor(string name) => $"{CrystalVendor}/Prefabs/{(name.StartsWith("BigBlock") ? "BigBlocks" : "Crystals")}/{name}.prefab";
        private const float CavernRockGloss = .35f;
        // Pyrite, fool's gold, has a metal's sheen, so at a glance it passes for gold. CrystalGloss keeps a clean
        // crystal's glint without a mirror.
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
                    vendor => FromHdrp(vendor, $"{MiningFolder}/{vendor.name}.mat", name.StartsWith("Ore_") ? PackStyle.Pack : PackStyle.Aged));
            if (!AssetDatabase.IsValidFolder(MiningFolder + "/Rocks")) AssetDatabase.CreateFolder(MiningFolder, "Rocks");
            foreach (var find in RockFinds) RockFind(find);
            if (!AssetDatabase.IsValidFolder(CrystalFolder + "/Dirty")) AssetDatabase.CreateFolder(CrystalFolder, "Dirty");
            foreach (var name in GroundCrystalProps)
                PackVariant($"{CrystalVendor}/Prefabs/Crystals/{name}.prefab", $"{CrystalFolder}/Dirty/{name}.prefab",
                    vendor => FromCrystal(vendor, $"{CrystalFolder}/Dirty/{vendor.name}_Dirty.mat", true));
            if (!AssetDatabase.IsValidFolder(CrystalFolder + "/Geode")) AssetDatabase.CreateFolder(CrystalFolder, "Geode");
            foreach (var (prop, mineral) in GeodeCrystalProps)
                PackVariant($"{CrystalVendor}/Prefabs/Crystals/{prop}.prefab", $"{CrystalFolder}/Geode/{prop}.prefab",
                    vendor => FromGeodeCrystal(vendor, $"{CrystalFolder}/Geode/{mineral}.mat", GeodeTints[mineral]));
            if (!AssetDatabase.IsValidFolder(CavernFolder)) AssetDatabase.CreateFolder(CrystalFolder, "Cavern");
            for (int area = 0; area < CavernAreas.Length; area++)
            {
                var (formations, clusters, finds) = CavernAreas[area];
                foreach (var name in formations.Concat(clusters).Distinct())
                    PackVariant(CavernVendor(name), $"{CavernFolder}/{name}.prefab", vendor => vendor.shader.name.Contains("Crystal")
                        ? FromGlowCrystal(vendor, $"{CavernFolder}/{vendor.name}.mat", Color.white, 1) : FromLayered(vendor, $"{CavernFolder}/{vendor.name}.mat"));
                string label = CavernScenery.AreaNames[area];
                var tint = CavernScenery.Glow[area];
                foreach (var name in finds)
                    PackVariant(CavernVendor(name), $"{CavernFolder}/{name}_{label}.prefab",
                        vendor => FromGlowCrystal(vendor, $"{CavernFolder}/{vendor.name}_{label}.mat", tint * CavernScenery.GlowBase, CavernScenery.GlowScale(area) / CavernScenery.GlowBase));
            }
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

        // How a Mining pack material is copied: as the pack has it (copper ore, user 2026-10-06: "a bit higher quality,
        // like silver and gold"), or as aged metal (AgedTint, AgedMetal, AgedGloss). Copper ore takes its copper-rich maps
        // (OreMaps).
        internal enum PackStyle { Pack, Aged }

        // A URP Lit copy of a Mining Tools, Ore & Ingots HDRP Lit material: its colour map in its tint, its normal map, and
        // its mask, whose metallic (R), occlusion (G) and smoothness (A) URP Lit reads from the same map. Aged metal keeps
        // the mask's occlusion only.
        internal static Material FromHdrp(Material vendor, string path, PackStyle style = PackStyle.Pack)
        {
            var (maps, floats, colors) = Saved(vendor);
            int size = PackMapSize;
            bool aged = style == PackStyle.Aged;
            bool own = OreMaps.TryGetValue(vendor.name, out var ours);
            var colour = own ? Project(ours.colour, "art/mining-pack/make_maps.py", size) : Sized(maps["_BaseColorMap"], size);
            var material = LitMaterial(path, colour, Sized(maps["_NormalMap"], size));
            var tint = aged ? colors["_BaseColor"] * AgedTint : colors["_BaseColor"]; tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", floats["_NormalScale"]);
            var mask = own ? Project(ours.mask, "art/mining-pack/make_maps.py", size, false, true) : Sized(maps["_MaskMap"], size);
            if (aged)
            {
                material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Metallic", AgedMetal); material.SetFloat("_Smoothness", AgedGloss);
            }
            else
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetFloat("_Smoothness", floats["_SmoothnessRemapMax"]);
            }
            material.SetTexture("_OcclusionMap", mask); material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        // A URP Lit copy of a Crystal Caverns crystal: its colour map in the pack's colour and its normal map, glassy and
        // not metal. The pack's crystal shader (translucency, inner glow) is not used: buried finds draw through the
        // excavation daylight's Lit shader. A dirty one, for crystals dug out of the ground, takes the dusty copy of its map
        // with its colour baked in (art/pure-nature-crystal-caverns/make_dirty.py) and DirtyGloss.
        private const float DirtyGloss = .35f;
        private static Material FromCrystal(Material vendor, string path, bool dirty)
        {
            var (maps, floats, colors) = Saved(vendor);
            var colour = dirty ? DirtyMap(vendor.name) : Sized(maps["_MainTex"]);
            var material = LitMaterial(path, colour, Sized(maps["_BumpMap"]));
            var tint = dirty ? Color.white : colors["_MainColor"]; tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", floats["_NormalPower"]);
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", vendor.name.Contains("Pyrite") ? PyriteMetal : 0);
            material.SetFloat("_Smoothness", dirty ? DirtyGloss : Mathf.Min(CrystalGloss, floats["_Smoothness"]));
            EditorUtility.SetDirty(material);
            return material;
        }

        // A URP Lit copy of a Crystal Caverns rock (its layered shader): its colour, normal and metallic-gloss maps at
        // CavernRockGloss; the moss and sand layer on top is left out underground.
        private static Material FromLayered(Material vendor, string path)
        {
            var (maps, floats, colors) = Saved(vendor);
            var material = LitMaterial(path, Sized(maps["_MainTex"]), Sized(maps["_BumpMap"]));
            var tint = colors.TryGetValue("_Color", out var own) ? own : Color.white; tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", floats.TryGetValue("_NormalPower", out var relief) ? relief : 1);
            if (maps.TryGetValue("_MetallicGlossMap", out var mask) && mask != null) material.SetTexture("_MetallicGlossMap", Sized(mask));
            else { material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP"); material.SetFloat("_Metallic", 0); }
            material.SetFloat("_Smoothness", CavernRockGloss);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A Crystal Caverns crystal lit from within, for the crystal cavern: the clean crystal in tint, glowing glow times
        // the tint (white for the areas' crystals, which CavernScenery colours per area) where the pack's crystal shader
        // glows (GlowMap).
        private static Material FromGlowCrystal(Material vendor, string path, Color tint, float glow)
        {
            var material = FromCrystal(vendor, path, false);
            tint.a = 1;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_EmissionMap", GlowMap(vendor));
            var emission = tint * glow; emission.a = 1;
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Where a crystal glows, as the pack's crystal shader has it: its colour map's alpha (bright in its heart, dark at
        // its skin) to a power, over a GlowFloor so no face goes out. The power is set so every map averages GlowMean, so
        // the areas glow alike (the pack's own contrasts left the hex columns white-hot and the rubies dark). A linear
        // grey map per vendor material, beside the cavern's crystals.
        private const float GlowFloor = .12f, GlowMean = .35f;
        private static Texture2D GlowMap(Material vendor)
        {
            var (maps, _, _) = Saved(vendor);
            var source = Sized(maps["_MainTex"]);
            string path = $"{CavernFolder}/{vendor.name}_Glow.png";
            int size = Mathf.Min(PackMapSize, source.width);
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var active = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt);
            var pixels = read.GetPixels32();
            var counts = new int[256];
            foreach (var pixel in pixels) counts[pixel.a]++;
            float Mean(float power)
            {
                double sum = 0;
                for (int a = 0; a < 256; a++) sum += counts[a] * Mathf.Lerp(GlowFloor, 1, Mathf.Pow(a / 255f, power));
                return (float)(sum / pixels.Length);
            }
            float low = .05f, high = 16;
            for (int step = 0; step < 30; step++) { float mid = Mathf.Sqrt(low * high); if (Mean(mid) > GlowMean) low = mid; else high = mid; }
            float contrast = Mathf.Sqrt(low * high);
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)Mathf.RoundToInt(Mathf.Lerp(GlowFloor, 1, Mathf.Pow(pixels[i].a / 255f, contrast)) * 255);
                pixels[i] = new Color32(v, v, v, 255);
            }
            var glow = new Texture2D(size, size, TextureFormat.RGB24, false, true);
            glow.SetPixels32(pixels);
            File.WriteAllBytes(path, glow.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(read); UnityEngine.Object.DestroyImmediate(glow);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = false; importer.mipmapEnabled = true;
            importer.maxTextureSize = PackMapSize; importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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

        // Copper ore's own colour map and mask: the pack's, with copper on about half the stone instead of a fifth (user,
        // 2026-10-06: "copper is fine to be rock and copper in one, but I need more copper on it").
        private static readonly Dictionary<string, (string colour, string mask)> OreMaps = new Dictionary<string, (string, string)>
        {
            ["M_Ore_Copper"] = (MiningFolder + "/Ore_Copper_Rich.png", MiningFolder + "/Ore_Copper_Rich_Mask.png"),
        };

        // Finds built on the pack's rock models, each a project prefab of its full-detail mesh (copied into Rocks/, so the
        // pack's 4K rock maps stay out of the repository) on a material from 1024 px copies of its maps
        // (art/mining-pack/make_maps.py):
        // - coal on the layered rocks, blocky with bedding planes, black with a dull sheen (user, 2026-10-06: the photo
        //   rock's coal "looks too much like a rock");
        // - native iron and silver on the knobbly jagged ones and gold on the rounded ones, solid metal (user, 2026-10-06:
        //   "gold, silver and others can be full gold/silver"): the rock's light and dark only (its _Metal map) in the
        //   metal's colour, iron a dull dark grey, silver bright, gold warm yellow. Gold on the jagged rocks, glossier,
        //   read as crumpled foil ("like gold wrappers"): a water-worn lump with a softer sheen reads as a nugget.
        internal readonly struct RockFindLook
        {
            public readonly string Rock, Output;
            public readonly Color Tint;
            public readonly float Metal, Gloss;
            public RockFindLook(string rock, string output, Color tint, float metal, float gloss)
            { Rock = rock; Output = output; Tint = tint; Metal = metal; Gloss = gloss; }
        }
        internal static readonly RockFindLook[] RockFinds =
        {
            new RockFindLook("Layered_Large", "Coal_Layered_Large", new Color(.17f, .17f, .18f), 0, .5f),
            new RockFindLook("Layered_Small", "Coal_Layered_Small", new Color(.17f, .17f, .18f), 0, .5f),
            new RockFindLook("Jagged_Large", "Iron_Native_A", new Color(.46f, .44f, .43f), .85f, .5f),
            new RockFindLook("Jagged_Small", "Iron_Native_B", new Color(.46f, .44f, .43f), .85f, .5f),
            new RockFindLook("Jagged_Large", "Silver_Native_A", new Color(.86f, .87f, .89f), .85f, .6f),
            new RockFindLook("Jagged_Small", "Silver_Native_B", new Color(.86f, .87f, .89f), .85f, .6f),
            new RockFindLook("Rounded_Large", "Gold_Native_A", new Color(1f, .77f, .34f), .85f, .48f),
            new RockFindLook("Rounded_Small", "Gold_Native_B", new Color(1f, .77f, .34f), .85f, .48f),
        };

        private static void RockFind(RockFindLook look)
        {
            string rock = look.Rock, output = look.Output;
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
            var material = LitMaterial($"{MiningFolder}/{output}.mat", Project(maps + (look.Metal > 0 ? "_Metal.png" : "_BC.png"), "art/mining-pack/make_maps.py"),
                Project(maps + "_N.png", "art/mining-pack/make_maps.py", PackMapSize, true));
            var mask = Project(maps + "_Mask.png", "art/mining-pack/make_maps.py", PackMapSize, false, true);
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", mask); material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_OCCLUSIONMAP");
            material.SetColor("_BaseColor", look.Tint);
            material.SetFloat("_Metallic", look.Metal);
            material.SetFloat("_Smoothness", look.Gloss);
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
