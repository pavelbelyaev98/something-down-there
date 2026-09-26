using System;
using UnityEngine;

namespace SomethingDownThere
{
    // Development comparison of freshly cut topsoil on the dig ground material. The first option is
    // the material's authored soil; Developer admin cycles the rest for the session only. Leaving
    // restores the first, so Editor Play Mode never keeps a comparison in the material asset.
    public sealed class TopsoilVariants : MonoBehaviour
    {
        [Serializable]
        public struct Soil
        {
            public string Name;
            public Texture Albedo, Normal, Mask;
            // Pack masks are metallic/occlusion/smoothness; the original soil's are roughness/contact/stone.
            public bool PackMask;
            // Linear multiplier on the albedo.
            public Color Tint;
            public float TileMetres, Relief, StoneRelief;
        }

        [SerializeField] private Material ground;
        [SerializeField] private Soil[] options = Array.Empty<Soil>();

        public static TopsoilVariants Active { get; private set; }
        public int Current { get; private set; }
        public string CurrentName => options.Length > 0 ? options[Current].Name : "";

        public void Configure(Material material, Soil[] soils)
        {
            ground = material;
            options = soils;
            Current = 0;
        }

        public static void Apply(Material material, Soil soil)
        {
            material.SetTexture("_SoilAlbedo", soil.Albedo);
            material.SetTexture("_SoilNormal", soil.Normal);
            material.SetTexture("_SoilRoughness", soil.Mask);
            material.SetFloat("_MaskLayout", soil.PackMask ? 1 : 0);
            // Colour properties are linearised for the shader.
            material.SetColor("_SoilTint", soil.Tint.gamma);
            material.SetFloat("_SoilTileMetres", soil.TileMetres);
            material.SetFloat("_NormalStrength", soil.Relief);
            material.SetFloat("_StoneNormalStrength", soil.StoneRelief);
        }

        private void OnEnable()
        {
            if (FpsPlayer.AdminBuild && ground != null && options.Length > 1) Active = this;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            if (Current != 0) Show(0);
        }

        public void Next()
        {
            if (options.Length > 1) Show((Current + 1) % options.Length);
        }

        private void Show(int index)
        {
            Current = index;
            Apply(ground, options[index]);
        }
    }
}
