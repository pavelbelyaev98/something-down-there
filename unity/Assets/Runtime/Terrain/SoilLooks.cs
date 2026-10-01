using System;
using UnityEngine;

namespace SomethingDownThere
{
    // Developer A/B for the freshly cut soil's look (Developer admin -> Soil look): texture sets from
    // the approved packs and the original bake, applied to a session copy of the dig ground material.
    // Built by Tools/Something Down There/Configure Soil Looks; the authored ground material stays look 0.
    public sealed class SoilLooks : ScriptableObject
    {
        [Serializable] public sealed class Look
        {
            public string Name;
            public Texture2D Albedo, Normal, Mask;
            [Tooltip("A terrain layer's mask (metallic, occlusion, height, smoothness) instead of the original bake's roughness/contact/stones map.")]
            public bool TerrainLayerMask = true;
            [Tooltip("Linear colour multiplier on the albedo.")]
            public Color Tint = Color.white;
            public float TileMetres = 4, NormalStrength = 1, StoneNormalStrength = 1;
        }

        public string AuthoredName = "Clay loam";
        public Look[] Looks = Array.Empty<Look>();
    }
}
