using System;
using UnityEngine;

namespace SomethingDownThere
{
    // A texture set a zone's main ground can wear (111), derived from the packs by
    // art/pure-nature-highlands/make_zone_grounds.py and wired by GroundTextureSetup. The ground material carries each
    // ground's first look; the Developer admin tries the others in play so the user can pick one.
    [Serializable]
    public sealed class GroundLook
    {
        public TerrainMaterialId Ground;
        public string Name;
        public Texture2D Albedo, Normal, Mask;
        public Color Tint = Color.white;
        public float TileMetres = 3, Relief = 1, Bedding, BedMetres = 6;
    }

    // Developer look comparison: a session copy of the ground material wears the chosen looks, so the material asset
    // never changes in Play Mode; the choice holds for the session (Ground Lab restarts included) until normal rules
    // are restored.
    public sealed partial class TerrainVolume
    {
        [SerializeField] private GroundLook[] groundLooks = Array.Empty<GroundLook>();
        // Each ground's chosen look (index into its looks; 0 is the authored one), for the session.
        private static readonly int[] ChosenLooks = new int[(int)TerrainMaterialSnapshot.Last + 1];
        private Material lookMaterial;

        public static string ShaderPrefix(TerrainMaterialId ground) => ground switch
        {
            TerrainMaterialId.LakeSediment => "_Sediment",
            TerrainMaterialId.Riverbed => "_Riverbed",
            _ => null
        };

        public int LookCount(TerrainMaterialId ground) => Array.FindAll(groundLooks, look => look.Ground == ground).Length;
        public static int ChosenLook(TerrainMaterialId ground) => ChosenLooks[(int)ground];
        public static bool HasLookOverrides => Array.Exists(ChosenLooks, look => look != 0);

        public string LookName(TerrainMaterialId ground)
        {
            var look = Look(ground, ChosenLooks[(int)ground]);
            return look != null ? look.Name : "-";
        }

        // Steps one ground on to its next look; returns its name.
        public string CycleLook(TerrainMaterialId ground)
        {
            int count = LookCount(ground);
            if (count == 0) return "-";
            ChosenLooks[(int)ground] = (ChosenLooks[(int)ground] + 1) % count;
            ApplyLooks();
            return LookName(ground);
        }

        public void RestoreLooks()
        {
            Array.Clear(ChosenLooks, 0, ChosenLooks.Length);
            ApplyLooks();
        }

        private GroundLook Look(TerrainMaterialId ground, int index)
        {
            foreach (var look in groundLooks)
                if (look.Ground == ground && index-- == 0) return look;
            return null;
        }

        // Session start and every change: the authored material while every ground wears its first look, else a copy
        // wearing the chosen ones (the X-ray copy follows).
        private void ApplyLooks()
        {
            if (soilMaterial == null) return;
            if (!HasLookOverrides && lookMaterial == null) return;
            if (lookMaterial == null) lookMaterial = new Material(soilMaterial) { name = "Excavation ground (looks)", hideFlags = HideFlags.DontSave };
            for (int ground = 0; ground < ChosenLooks.Length; ground++)
            {
                var look = Look((TerrainMaterialId)ground, ChosenLooks[ground]);
                if (look == null) continue;
                Wear(lookMaterial, look);
                if (xrayMaterial != null) Wear(xrayMaterial, look);
            }
            foreach (var chunk in chunks.Values) chunk.Renderer.sharedMaterial = CurrentSoilMaterial;
        }

        public static void Wear(Material material, GroundLook look)
        {
            string prefix = ShaderPrefix(look.Ground);
            if (prefix == null) return;
            material.SetTexture(prefix + "Albedo", look.Albedo);
            material.SetTexture(prefix + "Normal", look.Normal);
            material.SetTexture(prefix + "Mask", look.Mask);
            material.SetColor(prefix + "Tint", look.Tint);
            material.SetFloat(prefix + "TileMetres", look.TileMetres);
            material.SetFloat(prefix + "NormalStrength", look.Relief);
            material.SetFloat(prefix + "Bedding", look.Bedding);
            material.SetFloat(prefix + "BedMetres", look.BedMetres);
        }
    }
}
