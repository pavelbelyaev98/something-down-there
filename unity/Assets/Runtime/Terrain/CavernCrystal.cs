using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // A crystal cluster in a cavern area (115, CavernScenery): the tool works it like ground, its fractures spreading over
    // it as it takes work (a crack overlay clipped at a falling threshold; user, 2026-10-06: "don't shrink, crack") and
    // shards flying off where it is hit, until it has taken its work (seconds of the tool working, so shovel and drill
    // take as long) and breaks: its sealed pieces fall out and its light dims.
    public sealed class CavernCrystal : MonoBehaviour, IDigTarget
    {
        // The crack overlay's clip threshold, unbroken to broken: above every crack, then nearly all of them showing.
        private const float Whole = 1.01f, Shattered = .06f;
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private CavernScenery scenery;
        private object area;
        private Vector3 centre;
        private float reach, needed, work;
        private Color colour;
        private readonly List<Renderer> cracks = new List<Renderer>();
        private MaterialPropertyBlock block;

        internal void Initialize(CavernScenery owner, object ownArea, Vector3 middle, float piecesReach, float workNeeded, Color glow, List<Renderer> crackOverlays)
        {
            scenery = owner; area = ownArea; centre = middle; reach = piecesReach; needed = workNeeded; colour = glow;
            cracks.AddRange(crackOverlays);
            Crack();
        }

        public bool CanDig => isActiveAndEnabled && scenery != null && work < needed;
        public string DigPrompt => "";
        public float Cracked => needed > 0 ? Mathf.Clamp01(work / needed) : 1;

        public bool TryDig(RaycastHit hit)
        {
            if (!CanDig) return false;
            work += scenery.StrokeWork;
            scenery.Chipped(hit.point, hit.normal, colour);
            Crack();
            if (work >= needed) scenery.Broke(this, area, centre, reach, colour);
            return true;
        }

        private void Crack()
        {
            block ??= new MaterialPropertyBlock();
            float cutoff = Mathf.Lerp(Whole, Shattered, Mathf.Sqrt(Cracked));
            foreach (var overlay in cracks)
            {
                if (overlay == null) continue;
                overlay.GetPropertyBlock(block);
                block.SetFloat(CutoffId, cutoff);
                overlay.SetPropertyBlock(block);
            }
        }
    }

    // A cavern area's big formation (115): scenery that lights the area and stays as it is. The tool says so instead of
    // striking it; the clusters and the stone round it break.
    public sealed class CavernFormation : MonoBehaviour, IDigTarget
    {
        public bool CanDig => false;
        public string DigPrompt => "Too big to break: dig the crystals and stone round it";
        public bool TryDig(RaycastHit hit) => false;
    }
}
