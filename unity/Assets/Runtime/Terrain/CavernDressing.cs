using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // What the crystal cavern's areas are made of (115), built by Configure Caverns from the Crystal Caverns demo's own
    // pieces (URP copies in Content/BuriedProps/CrystalCaverns/Cavern), one set per area (TerrainGround.CavernArea):
    // its big formations and the smaller clusters the tool breaks. Also the cracks a breaking cluster shows, the shards,
    // glints and dust it throws, and the bloom while the player is inside.
    [CreateAssetMenu(menuName = "Something Down There/Cavern dressing")]
    public sealed class CavernDressing : ScriptableObject
    {
        [Serializable]
        public sealed class Area
        {
            public GameObject[] Formations = new GameObject[0], Clusters = new GameObject[0];
            // Glowing crystal (tinted per area at run time) or plain stone (the cubic blocks).
            public bool Glows = true;
        }
        public Area[] Areas = new Area[0];
        public Material Cracks, Shards, Glints, Dust;
        public VolumeProfile Bloom;
    }
}
