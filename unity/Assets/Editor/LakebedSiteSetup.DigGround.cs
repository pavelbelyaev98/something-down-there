using System;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // The dig plot reads darkest: its surface cap is the canyon mud darkened toward the plot centre,
    // and toward the outline it becomes the terrain's damp band itself (same texture, tint and world
    // mapping), so the collar joins the terrain without a texture line. Beyond it the band fades into
    // the light lakebed. Freshly cut topsoil below is fine-grained, recoloured away from the surface
    // mud, with no stone shapes that could pass for finds.
    public static partial class LakebedSiteSetup
    {
        public const string DampMudLayerPath = Folder + "/DampMud.terrainlayer";
        public const string DigEdgePath = Folder + "/DigEdgeDistance.asset";
        public const string DampMudName = "DampMud";
        private const string CanyonMudLayerPath = "Assets/BK/PureNature_Highlands/Textures/Surfaces/TerrainLayers/Mud.terrainlayer";
        private const string MountainMudLayerPath = "Assets/BK/PureNature_Mountains/Textures/Surfaces/Layers/Mud01.terrainlayer";
        private const string SandLayerPath = "Assets/BK/PureNature_Highlands/Textures/Surfaces/TerrainLayers/Sand.terrainlayer";
        // Linear multipliers on the canyon mud: the damp band and the plot's darkest centre.
        private static readonly Color DampMudTint = new Color(.66f, .62f, .58f, 1);
        public static readonly Color DigCapTint = new Color(.42f, .39f, .37f, 1);
        // Metres inside the outline over which the plot darkens; the band covers the terrain fully
        // out to BandStart beyond it, past the collar skirt, then fades over about BandWidth.
        public const float DigInnerFade = 8, BandStart = 1.5f, BandWidth = 16;
        // The edge map covers the grid and its collar around the site origin.
        private const float DigEdgeHalfSpan = 20;
        private const int DigEdgeResolution = 256;

        private static TerrainLayer CanyonMud()
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(CanyonMudLayerPath);
            if (layer == null) throw new InvalidOperationException("Missing approved Highlands mud layer.");
            return layer;
        }

        // Share of the damp band at a site-local point: full over the collar join, then a smooth fade
        // whose width varies a little along the outline.
        public static float DigBand(Vector2 local)
        {
            float grain = Mathf.PerlinNoise(local.x / 3.5f + 41.3f, local.y / 3.5f + 17.9f);
            float t = Mathf.Clamp01((SiteLayout.BeyondOpening(local) - BandStart) / (BandWidth * (.8f + .4f * grain)));
            return 1 - t * t * (3 - 2 * t);
        }

        // The canyon mud darkened as damp silt: the lakebed's own layer for the band around the plot
        // and the wet ground by the water. World-aligned, dry and non-metallic, exactly as the dig
        // ground's shader renders it; the demo's own Mud layer stays untouched.
        private static TerrainLayer DampMudLayer(Vector3 terrainPosition)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DampMudLayerPath);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, DampMudLayerPath); }
            EditorUtility.CopySerialized(CanyonMud(), layer);
            layer.name = DampMudName;
            layer.diffuseRemapMax = DampMudTint;
            layer.tileOffset = new Vector2(Mathf.Repeat(terrainPosition.x, layer.tileSize.x), Mathf.Repeat(terrainPosition.z, layer.tileSize.y));
            var min = layer.maskMapRemapMin;
            var max = layer.maskMapRemapMax;
            min.x = max.x = 0;
            max.w = Mathf.Min(max.w, GroundTextureSetup.MaxGroundSmoothness);
            min.w = Mathf.Min(min.w, max.w);
            layer.maskMapRemapMin = min;
            layer.maskMapRemapMax = max;
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssetIfDirty(layer);
            return layer;
        }

        // Metres beyond the plot outline (SiteLayout) over the grid and collar, for the shader.
        private static Texture2D DigEdgeDistance()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DigEdgePath);
            if (texture == null)
            {
                texture = new Texture2D(DigEdgeResolution, DigEdgeResolution, TextureFormat.RHalf, false, true)
                    { name = "DigEdgeDistance", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                AssetDatabase.CreateAsset(texture, DigEdgePath);
            }
            var pixels = new ushort[DigEdgeResolution * DigEdgeResolution];
            float cell = DigEdgeHalfSpan * 2 / DigEdgeResolution;
            for (int z = 0; z < DigEdgeResolution; z++)
            for (int x = 0; x < DigEdgeResolution; x++)
                pixels[z * DigEdgeResolution + x] = Mathf.FloatToHalf(SiteLayout.BeyondOpening(
                    new Vector2(-DigEdgeHalfSpan + (x + .5f) * cell, -DigEdgeHalfSpan + (z + .5f) * cell)));
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        // Freshly cut topsoil candidates, compared in Developer admin; the first is authored into the
        // ground material. The original soil art (its muted colour copy) and plain pack soils, tinted
        // away from the orange surface mud. Clay and rock keep their own textures.
        public static TopsoilVariants.Soil[] TopsoilOptions() => new[]
        {
            OriginalSoil("Loam", new Color(.33f, .28f, .21f)),
            OriginalSoil("Dark loam", new Color(.24f, .2f, .15f)),
            OriginalSoil("Olive silt", new Color(.3f, .31f, .26f)),
            PackSoil("Clay loam", MountainMudLayerPath, new Color(.45f, .75f, 1), 4),
            PackSoil("Damp humus", MountainMudLayerPath, new Color(.32f, .55f, .9f), 4),
            PackSoil("Grey silt", SandLayerPath, new Color(.1f, .1f, .11f), 5),
        };

        private static TopsoilVariants.Soil OriginalSoil(string name, Color tint)
        {
            Texture2D Channel(string channel) =>
                AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTextureSetup.Folder + "Soil_" + channel + ".png")
                ?? throw new InvalidOperationException("Missing original soil art: " + channel);
            return new TopsoilVariants.Soil
            {
                Name = name, Albedo = GroundTextureSetup.MutedSoilAlbedo(), Normal = Channel("Normal"), Mask = Channel("Roughness"), Tint = tint,
                TileMetres = GroundTextureSetup.OriginalSoilTileMetres,
                Relief = GroundTextureSetup.OriginalSoilRelief, StoneRelief = GroundTextureSetup.OriginalStoneRelief,
            };
        }

        private static TopsoilVariants.Soil PackSoil(string name, string layerPath, Color tint, float tileMetres)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null) throw new InvalidOperationException("Missing approved pack soil " + layerPath);
            return new TopsoilVariants.Soil
            {
                Name = name, Albedo = layer.diffuseTexture, Normal = layer.normalMapTexture, Mask = layer.maskMapTexture,
                PackMask = true, Tint = tint, TileMetres = tileMetres, Relief = layer.normalScale, StoneRelief = layer.normalScale,
            };
        }

        public static void ConfigureTopsoil(Material material) => TopsoilVariants.Apply(material, TopsoilOptions()[0]);

        private static void BuildTopsoilVariants(Transform environment, Material ground)
        {
            var variants = new GameObject("Topsoil variants").AddComponent<TopsoilVariants>();
            variants.transform.SetParent(environment, false);
            variants.Configure(ground, TopsoilOptions());
        }

        // Binds the plot's surface cap and the damp band to a dig ground material. Before the lakebed
        // exists there is no band, and the cap keeps the plain darkened mud.
        public static void ConfigureDigGround(Material material)
        {
            var mud = CanyonMud();
            // Colour properties are linearised for the shader; terrain layer remaps are not.
            material.SetTexture("_TurfAlbedo", mud.diffuseTexture);
            material.SetTexture("_TurfNormal", mud.normalMapTexture);
            material.SetTexture("_TurfRoughness", mud.maskMapTexture);
            material.SetColor("_TurfTint", DigCapTint.gamma);
            material.SetFloat("_TileMetres", mud.tileSize.x);
            var band = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DampMudLayerPath);
            material.SetFloat("_BandBlend", band != null ? 1 : 0);
            if (band == null) return;
            material.SetTexture("_BandAlbedo", band.diffuseTexture);
            material.SetTexture("_BandNormal", band.normalMapTexture);
            material.SetTexture("_BandMask", band.maskMapTexture);
            material.SetColor("_BandTint", ((Color)band.diffuseRemapMax).gamma);
            material.SetFloat("_BandTileMetres", band.tileSize.x);
            material.SetFloat("_BandNormalStrength", band.normalScale);
            material.SetVector("_BandMaskMin", band.maskMapRemapMin);
            material.SetVector("_BandMaskMax", band.maskMapRemapMax);
            material.SetFloat("_BandInnerFade", DigInnerFade);
            material.SetTexture("_DigEdge", DigEdgeDistance());
            float inverse = 1 / (DigEdgeHalfSpan * 2);
            material.SetVector("_DigEdgeRect", new Vector4(-DigEdgeHalfSpan, -DigEdgeHalfSpan, inverse, inverse));
        }
    }
}
