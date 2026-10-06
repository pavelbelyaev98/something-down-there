using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // What the crystal cavern's groves are made of (115), built by Configure Caverns from Crystal Caverns' demo pieces
    // (URP copies in Content/BuriedProps/CrystalCaverns/Cavern): its tall crystals for a grove's columns, its smaller
    // ones for the sprays on the wall, a shard material for the flecks they throw when chipped and broken, and a bloom
    // while the player is inside. CavernScenery colours and sizes them.
    [CreateAssetMenu(menuName = "Something Down There/Cavern dressing")]
    public sealed class CavernDressing : ScriptableObject
    {
        public GameObject[] Columns = new GameObject[0], Sprays = new GameObject[0];
        public Material Shards;
        public VolumeProfile Bloom;
    }
}
