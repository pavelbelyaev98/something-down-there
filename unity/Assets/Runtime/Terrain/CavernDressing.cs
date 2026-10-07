using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // What the caves' and geodes' glow needs beyond their crystals (115): the bloom while the view is inside one, built
    // by Configure Caverns.
    [CreateAssetMenu(menuName = "Something Down There/Cavern dressing")]
    public sealed class CavernDressing : ScriptableObject
    {
        public VolumeProfile Bloom;
    }
}
