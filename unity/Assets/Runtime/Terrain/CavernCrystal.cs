using UnityEngine;

namespace SomethingDownThere
{
    // One of a grove's big crystals (115, CavernScenery): the tool works it like ground, chipping shards off it as it
    // cracks and shrinks, until it has taken its work (seconds of the tool working, so shovel and drill take as long)
    // and breaks: its sealed pieces fall out and its grove's light dims (user, 2026-10-06: "break big structures and then
    // they can be turned into smaller crystals").
    public sealed class CavernCrystal : MonoBehaviour, IDigTarget
    {
        private CavernScenery scenery;
        private object grove;
        private Vector3 centre, scale;
        private float reach, needed, work, shrink;
        private Color colour;

        internal void Initialize(CavernScenery owner, object ownGrove, Vector3 middle, float piecesReach, float workNeeded, Color glow, float crackShrink)
        {
            scenery = owner; grove = ownGrove; centre = middle; reach = piecesReach; needed = workNeeded; colour = glow; shrink = crackShrink;
            scale = transform.localScale;
        }

        public bool CanDig => isActiveAndEnabled && scenery != null && work < needed;
        public string DigPrompt => "";
        public float Cracked => needed > 0 ? Mathf.Clamp01(work / needed) : 1;

        public bool TryDig(RaycastHit hit)
        {
            if (!CanDig) return false;
            work += scenery.StrokeWork;
            scenery.Chipped(hit.point, hit.normal, colour);
            transform.localScale = scale * (1 - shrink * Cracked);
            if (work >= needed) scenery.Broke(this, grove, centre, reach, colour);
            return true;
        }
    }
}
