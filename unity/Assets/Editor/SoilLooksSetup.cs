using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // Soil look A/B variants for Developer admin: the user's original soil bakes (art/ground-textures:
    // the first and the current bake, and the muted copy with the earlier topsoil tints) and approved
    // pack layers (art/pure-nature-mountains, art/pure-nature-highlands) as the dig ground's soil.
    public static class SoilLooksSetup
    {
        private const string AssetPath = "Assets/Content/GroundTextures/SoilLooks.asset";
        private const string Folder = GroundTextureSetup.Folder;
        private const string MountainMud = "Assets/BK/PureNature_Mountains/Textures/Surfaces/Layers/Mud01.terrainlayer";
        private const string HighlandsMud = "Assets/BK/PureNature_Highlands/Textures/Surfaces/TerrainLayers/Mud.terrainlayer";
        private const float PackTileMetres = 4;
        // The user's favourite so far: a paler tint of the clay loam's mud.
        private static readonly Color LightClayLoamTint = new Color(LakebedSiteSetup.ClayLoamTint.r * 1.35f, LakebedSiteSetup.ClayLoamTint.g * 1.35f, LakebedSiteSetup.ClayLoamTint.b * 1.35f, 1);
        // The user asked for the brown mud darker, twice.
        private static readonly Color BrownMudTint = new Color(.76f, .76f, .76f, 1);
        // The current bake blows out in a sunlit pit at full strength.
        private static readonly Color OriginalSoilTint = new Color(.8f, .8f, .8f, 1);

        [MenuItem("Tools/Something Down There/Configure Soil Looks")]
        public static void Configure()
        {
            var looks = AssetDatabase.LoadAssetAtPath<SoilLooks>(AssetPath);
            if (looks == null)
            {
                looks = ScriptableObject.CreateInstance<SoilLooks>();
                AssetDatabase.CreateAsset(looks, AssetPath);
            }
            looks.AuthoredName = "Clay loam";
            looks.Looks = new[]
            {
                Layer("Light clay loam", MountainMud, LakebedSiteSetup.ClayLoamTileMetres, LightClayLoamTint),
                Layer("Brown mud", HighlandsMud, PackTileMetres, BrownMudTint),
                Original("Original soil", "Soil_Albedo.png", "Soil", OriginalSoilTint),
                Original("First soil bake", "SoilFirstBake_Albedo.png", "SoilFirstBake", Color.white),
                // The earlier topsoil comparison's tints on the muted copy of the current bake.
                Original("Loam", "Soil_Albedo_Muted.png", "Soil", new Color(.33f, .28f, .21f, 1)),
                Original("Dark loam", "Soil_Albedo_Muted.png", "Soil", new Color(.24f, .2f, .15f, 1)),
                Original("Olive silt", "Soil_Albedo_Muted.png", "Soil", new Color(.3f, .31f, .26f, 1)),
            };
            EditorUtility.SetDirty(looks);
            AssetDatabase.SaveAssets();
            var terrain = UnityEngine.Object.FindAnyObjectByType<TerrainVolume>();
            if (terrain == null) throw new InvalidOperationException("Open MainGame first: no TerrainVolume to wire the soil looks to.");
            var settings = new SerializedObject(terrain);
            settings.FindProperty("soilLooks").objectReferenceValue = looks;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        }

        private static SoilLooks.Look Layer(string name, string path, float tileMetres, Color tint)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) throw new InvalidOperationException("Missing approved ground layer " + path);
            return new SoilLooks.Look
            {
                Name = name, Albedo = layer.diffuseTexture, Normal = layer.normalMapTexture, Mask = layer.maskMapTexture,
                TerrainLayerMask = true, Tint = tint, TileMetres = tileMetres,
                NormalStrength = layer.normalScale, StoneNormalStrength = layer.normalScale
            };
        }

        // An original bake: its albedo, and the normal and roughness/contact/stones mask of `set`.
        private static SoilLooks.Look Original(string name, string albedo, string set, Color tint) => new SoilLooks.Look
        {
            Name = name, Albedo = Texture(albedo, "Albedo"), Normal = Texture(set + "_Normal.png", "Normal"),
            Mask = Texture(set + "_Roughness.png", "Roughness"), TerrainLayerMask = false, Tint = tint,
            TileMetres = GroundTextureSetup.OriginalSoilTileMetres, NormalStrength = GroundTextureSetup.OriginalSoilRelief,
            StoneNormalStrength = GroundTextureSetup.OriginalStoneRelief
        };

        private static Texture2D Texture(string file, string channel)
        {
            string path = Folder + file;
            // Freshly restored files still carry Unity's default import (no anisotropy).
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.anisoLevel != 16)
                GroundTextureSetup.ConfigureImport(path, channel);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("Missing original soil texture " + path);
        }
    }
}
