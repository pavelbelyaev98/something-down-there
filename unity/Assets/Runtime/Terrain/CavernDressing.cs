using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // What the caverns are dressed with (115), built by Configure Caverns from Crystal Caverns' demo pieces (URP copies
    // in Content/BuriedProps/CrystalCaverns/Cavern): boulders and rubble on every cavern's floor; in the crystal cavern
    // also the demo's cubic rock formations and its big crystals lit from within, a light at each glowing cluster in the
    // demo's colours, and a bloom while the player is inside. CavernScenery sizes the demo's cave-sized pieces to
    // chambers a few metres high.
    [CreateAssetMenu(menuName = "Something Down There/Cavern dressing")]
    public sealed class CavernDressing : ScriptableObject
    {
        public GameObject[] Boulders = new GameObject[0], Rubble = new GameObject[0], Formations = new GameObject[0], Crystals = new GameObject[0];
        // The demo's glow colours: cyan, green, orange, red.
        public Color[] Glow = { new Color(0, .6f, 1), new Color(0, 1, .44f), new Color(1, .45f, 0), new Color(1, .1f, .1f) };
        public VolumeProfile Bloom;
    }
}
