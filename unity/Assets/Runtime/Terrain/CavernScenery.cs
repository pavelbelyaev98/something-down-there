using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // The crystal cavern's groves (115, TerrainGround.Grove), made whenever the ground's layout or the population changes
    // (new game, load, Ground Lab) and seeded from the cavern, so they save nothing of their own. Each chamber's grove is
    // one colour, as in the Crystal Caverns demo, lit by one light in it: its columns and sprays are the demo's big
    // crystals lit from within, which the player breaks with the tool (CavernCrystal). A crystal still holds its sealed
    // pieces (DiscoveryField) until it breaks; one whose pieces are out is not made again. Everything is lit like the dig
    // (ExcavationDaylight): only the glow and the grove's light show it without lamps. A bloom fades in while the view is
    // inside the crystal cavern. Props live outside the terrain's hierarchy, so the tool takes them for themselves.
    public sealed class CavernScenery : MonoBehaviour
    {
        // The demo's glow colours: cyan, green, red (catalog names in GlowNames).
        public static readonly string[] GlowNames = { "blue", "green", "red" };
        public static readonly Color[] Glow = { new Color(0, .55f, 1), new Color(0, 1, .45f), new Color(1, .12f, .12f) };
        // A glowing crystal shows its own texture in its colour (GlowBase of it lit, GlowIntensity of it glowing).
        public const float GlowBase = .3f, GlowIntensity = .75f;
        // A grove's light, LightRise above its middle, dimming to LightLeft of it as its crystals break; a crystal's work
        // to break, per metre of it, in seconds of the tool working, and how far it shrinks as it cracks; how far its
        // sealed pieces may lie from its middle, in its lengths; how deep it stands in the stone, in its lengths.
        private const float LightRange = 7.5f, LightIntensity = 3f, LightLeft = .35f, LightRise = .9f, WorkPerMetre = 1.1f,
            CrackShrink = .12f, PieceReach = .6f, BloomFade = 1.5f, Sink = .15f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private DiscoveryField field;
        [SerializeField] private CavernDressing dressing;
        private Transform root;
        private TerrainGround.GroundLayout dressed;
        private long dressedRevision = -1;
        private readonly List<Grove> groves = new List<Grove>();
        private readonly Dictionary<GameObject, Bounds> sizes = new Dictionary<GameObject, Bounds>();
        private Volume bloom;
        private FpsPlayer viewer;
        public int CrystalCount { get; private set; }
        public int LightCount => groves.Count;
        public float BloomWeight => bloom != null ? bloom.weight : 0;

        private sealed class Grove
        {
            public Light Light;
            public int Crystals, Left;
        }

        private void OnDestroy() { if (root != null) Destroy(root.gameObject); }

        private void LateUpdate()
        {
            if (terrain == null || dressing == null || field == null) return;
            var layout = terrain.GroundLayout;
            if (!ReferenceEquals(layout, dressed) || field.PopulationRevision != dressedRevision) Dress(layout);
            UpdateBloom();
        }

        private void Dress(TerrainGround.GroundLayout layout)
        {
            if (root == null) root = new GameObject("Cavern scenery").transform;
            foreach (Transform child in root) Destroy(child.gameObject);
            groves.Clear();
            CrystalCount = 0;
            dressed = layout;
            dressedRevision = field.PopulationRevision;
            var daylight = terrain.GetComponent<ExcavationDaylight>();
            foreach (var cavern in layout.Caverns)
                if (cavern.Crystal)
                    for (int chamber = 0; chamber < cavern.Centres.Length; chamber++) DressGrove(TerrainGround.Grove(cavern, chamber), daylight);
        }

        private void DressGrove(List<TerrainGround.GroveCrystal> crystals, ExcavationDaylight daylight)
        {
            if (crystals.Count == 0) return;
            var grove = new Grove();
            var middle = float3.zero;
            foreach (var crystal in crystals)
            {
                middle += crystal.Centre;
                if (crystal.Kind == TerrainGround.GroveKind.Shard) continue;
                grove.Crystals++;
                var centre = terrain.transform.TransformPoint((Vector3)crystal.Centre);
                if (!field.AnySealedWithin(centre, crystal.Size * PieceReach)) continue;
                var kinds = crystal.Kind == TerrainGround.GroveKind.Column ? dressing.Columns : dressing.Sprays;
                if (kinds == null || kinds.Length == 0) continue;
                Make(kinds[crystal.Variant % kinds.Length], crystal, grove, centre, daylight);
                grove.Left++;
            }
            middle /= crystals.Count;
            var light = new GameObject("Grove light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = terrain.transform.TransformPoint((Vector3)middle + Vector3.up * LightRise);
            light.type = LightType.Point;
            light.color = Glow[crystals[0].Glow];
            light.range = LightRange;
            light.shadows = LightShadows.None;
            grove.Light = light;
            groves.Add(grove);
            Dim(grove);
        }

        private void Make(GameObject kind, TerrainGround.GroveCrystal crystal, Grove grove, Vector3 centre, ExcavationDaylight daylight)
        {
            var size = Size(kind);
            float s = crystal.Size / Mathf.Max(size.size.y, .01f);
            Vector3 up = crystal.Up, foot = crystal.Foot;
            var turn = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, crystal.Turn, 0);
            var pivot = foot - up * (crystal.Size * Sink) - turn * (size.center - Vector3.up * size.extents.y) * s;
            var prop = Instantiate(kind, terrain.transform.TransformPoint(pivot), terrain.transform.rotation * turn, root);
            prop.transform.localScale = kind.transform.localScale * s;
            var colour = Glow[crystal.Glow];
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (daylight != null) daylight.Register(renderer);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour * GlowBase);
                block.SetColor(EmissionColorId, colour * GlowIntensity);
                renderer.SetPropertyBlock(block);
            }
            prop.AddComponent<CavernCrystal>().Initialize(this, grove, centre, crystal.Size * PieceReach, crystal.Size * WorkPerMetre, colour, CrackShrink);
            CrystalCount++;
        }

        // A crystal broke: its pieces fall out, its grove's light dims, shards fly.
        internal void Broke(CavernCrystal crystal, object grove, Vector3 centre, float reach, Color colour)
        {
            field.UnsealWithin(centre, reach);
            CavernShatter.Burst(dressing.Shards, centre, reach * .8f, colour, 60, root);
            if (grove is Grove g) { g.Left = Mathf.Max(0, g.Left - 1); Dim(g); }
            CrystalCount = Mathf.Max(0, CrystalCount - 1);
            Destroy(crystal.gameObject);
        }

        internal void Chipped(Vector3 point, Vector3 normal, Color colour) => CavernShatter.Burst(dressing.Shards, point + normal * .03f, .08f, colour, 8, root);

        // The work of one tool stroke, in seconds of the tool working: the viewer's stroke interval.
        internal float StrokeWork
        {
            get
            {
                if (viewer == null) viewer = FindAnyObjectByType<FpsPlayer>();
                return viewer != null ? viewer.EffectiveDigInterval : .25f;
            }
        }

        private static void Dim(Grove grove)
        {
            float share = grove.Crystals > 0 ? (float)grove.Left / grove.Crystals : 1;
            grove.Light.intensity = LightIntensity * Mathf.Lerp(LightLeft, 1, share);
        }

        // A prefab's bounds in its own frame, unscaled by its root.
        private Bounds Size(GameObject kind)
        {
            if (sizes.TryGetValue(kind, out var bounds)) return bounds;
            bool any = false;
            var own = kind.transform.worldToLocalMatrix;
            foreach (var filter in kind.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                var matrix = own * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; } else bounds.Encapsulate(corner);
                }
            }
            sizes[kind] = bounds;
            return bounds;
        }

        private void UpdateBloom()
        {
            if (dressing.Bloom == null) return;
            if (bloom == null)
            {
                var go = new GameObject("Crystal cavern bloom");
                go.transform.SetParent(transform, false);
                bloom = go.AddComponent<Volume>();
                bloom.isGlobal = true; bloom.priority = 20; bloom.sharedProfile = dressing.Bloom; bloom.weight = 0;
            }
            if (viewer == null) viewer = FindAnyObjectByType<FpsPlayer>();
            float target = 0;
            if (viewer != null && viewer.ViewCamera != null && dressed != null)
            {
                var p = (float3)terrain.transform.InverseTransformPoint(viewer.ViewCamera.transform.position);
                foreach (var cavern in dressed.Caverns)
                    if (cavern.Crystal && math.all(p > cavern.Min) && math.all(p < cavern.Max) && TerrainGround.CavernHollow(cavern, p) < .3f) target = 1;
            }
            bloom.weight = Mathf.MoveTowards(bloom.weight, target, Time.unscaledDeltaTime * BloomFade);
        }
    }
}
