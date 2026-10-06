using System;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // The whole dig plot reads a little darker than the ground around it: its surface cap is the canyon
    // mud, darkened only slightly more than the damp band. Beyond the outline the permanent ground
    // lightens from the cap through the terrain's damp band into the light lakebed. The collar and the terrain share one band share
    // (DigBandShare) and the same mud texture, relief and world mapping, so they join without a
    // texture line. Freshly cut topsoil below is clay loam, with no stone shapes that could pass for finds.
    public static partial class LakebedSiteSetup
    {
        public const string DampMudLayerPath = Folder + "/DampMud.terrainlayer";
        public const string DigCapLayerPath = Folder + "/DigCap.terrainlayer";
        public const string DigBandSharePath = Folder + "/DigBandShare.asset";
        public const string DampMudName = "DampMud", DigCapName = "DigCap";
        private const string CanyonMudLayerPath = "Assets/BK/PureNature_Highlands/Textures/Surfaces/TerrainLayers/Mud.terrainlayer";
        private const string MountainMudLayerPath = "Assets/BK/PureNature_Mountains/Textures/Surfaces/Layers/Mud01.terrainlayer";
        // Linear multipliers on the canyon mud: the damp band and the plot's cap. The user asked twice
        // for a lighter plot (2026-10-02); the band lightened with it so it never rings the plot darker,
        // which also lightens the damp silt by the water.
        private static readonly Color DampMudTint = new Color(.78f, .73f, .69f, 1);
        public static readonly Color DigCapTint = new Color(.76f, .71f, .67f, 1);
        // Metres beyond the outline over which the cap lightens into the damp band; the band
        // then fades into the lakebed over about BandWidth. The user wanted the lighter ground to start
        // sooner (2026-10-02; it was 3.5 and 16).
        public const float CapFade = 1.5f, BandStart = CapFade, BandWidth = 9;
        // The share map covers the grid and its collar around the site origin.
        private const float DigEdgeHalfSpan = 20;
        private const int DigEdgeResolution = 256;
        // Freshly cut topsoil: the Mountains mud as clay loam, tinted away from the orange surface.
        public static readonly Color ClayLoamTint = new Color(.62f, .98f, 1.25f, 1);
        public const float ClayLoamTileMetres = 4, ClayLoamOcclusion = .4f;
        // Up close the cap borrows the clay loam's grain (strength, tile metres), so it reads as the top of
        // the soil dug out of it rather than a blurry smear beside it (user, 2026-10-03).
        public const float CapGrain = .8f, CapGrainMetres = 2;

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

        // How far the cap has lightened toward the damp band: none inside the plot, all of it
        // CapFade beyond the outline (a little sooner or later along it). The collar shader and the
        // terrain paint both use this.
        public static float DigBandShare(Vector2 local)
        {
            float grain = Mathf.PerlinNoise(local.x / 2.5f + 7.1f, local.y / 2.5f + 29.3f);
            float t = Mathf.Clamp01(SiteLayout.BeyondOpening(local) / (CapFade * (.8f + .4f * grain)));
            return t * t * (3 - 2 * t);
        }

        // The canyon mud darkened: as damp silt for the lakebed's band around the plot and the wet
        // ground by the water, or as the plot's own cap just beyond its outline. World-aligned,
        // dry and non-metallic, exactly as the dig ground's shader renders it; the demo's own Mud
        // layer stays untouched.
        private static TerrainLayer DampMudLayer(Vector3 terrainPosition) => MudLayer(DampMudLayerPath, DampMudName, DampMudTint, terrainPosition);
        private static TerrainLayer DigCapLayer(Vector3 terrainPosition) => MudLayer(DigCapLayerPath, DigCapName, DigCapTint, terrainPosition);

        private static TerrainLayer MudLayer(string path, string name, Color tint, Vector3 terrainPosition)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            EditorUtility.CopySerialized(CanyonMud(), layer);
            layer.name = name;
            layer.diffuseRemapMax = tint;
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

        // DigBandShare over the grid and collar, for the shader.
        private static Texture2D DigBandShareMap()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DigBandSharePath);
            if (texture == null)
            {
                texture = new Texture2D(DigEdgeResolution, DigEdgeResolution, TextureFormat.RHalf, false, true)
                    { name = "DigBandShare", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                AssetDatabase.CreateAsset(texture, DigBandSharePath);
            }
            var pixels = new ushort[DigEdgeResolution * DigEdgeResolution];
            float cell = DigEdgeHalfSpan * 2 / DigEdgeResolution;
            for (int z = 0; z < DigEdgeResolution; z++)
            for (int x = 0; x < DigEdgeResolution; x++)
                pixels[z * DigEdgeResolution + x] = Mathf.FloatToHalf(DigBandShare(
                    new Vector2(-DigEdgeHalfSpan + (x + .5f) * cell, -DigEdgeHalfSpan + (z + .5f) * cell)));
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        // Freshly cut topsoil: clay loam from the Mountains mud.
        public static void ConfigureTopsoil(Material material)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(MountainMudLayerPath);
            if (layer == null) throw new InvalidOperationException("Missing approved Mountains mud " + MountainMudLayerPath);
            material.SetTexture("_SoilAlbedo", layer.diffuseTexture);
            material.SetTexture("_SoilNormal", layer.normalMapTexture);
            material.SetTexture("_SoilRoughness", layer.maskMapTexture);
            material.SetFloat("_MaskLayout", 1);
            // Colour properties are linearised for the shader.
            material.SetColor("_SoilTint", ClayLoamTint.gamma);
            material.SetFloat("_SoilTileMetres", ClayLoamTileMetres);
            material.SetFloat("_NormalStrength", layer.normalScale);
            material.SetFloat("_StoneNormalStrength", layer.normalScale);
            // The pack mask's baked occlusion at full strength blotches dug walls under the daylight fill.
            material.SetFloat("_SoilOcclusion", ClayLoamOcclusion);
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
            material.SetFloat("_CapGrain", CapGrain);
            material.SetFloat("_CapGrainMetres", CapGrainMetres);
            material.SetFloat("_RimMix", TerrainChunkMesh.MouthRound);
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
            material.SetTexture("_DigEdge", DigBandShareMap());
            float inverse = 1 / (DigEdgeHalfSpan * 2);
            material.SetVector("_DigEdgeRect", new Vector4(-DigEdgeHalfSpan, -DigEdgeHalfSpan, inverse, inverse));
        }
    }
}
