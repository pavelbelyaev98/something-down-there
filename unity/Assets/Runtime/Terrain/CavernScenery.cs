using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // The crystal cavern's areas (115, TerrainGround.Grove), made whenever the ground's layout or the population changes
    // (new game, load, Ground Lab) and seeded from the cavern, so they save nothing of their own. Each chamber is one of
    // the Crystal Caverns demo's areas, lit by one light in its colour: big formations of its kind standing from the
    // floor, out of the walls and down from the roof, which stay as they are (CavernFormation), and smaller clusters the
    // tool cracks and breaks (CavernCrystal). A cluster still holds its sealed pieces (DiscoveryField) until it breaks;
    // one whose pieces are out is not made again. Everything is lit like the dig (ExcavationDaylight): only the glow and
    // the area's light show it without lamps. A bloom fades in while the view is inside the crystal cavern. Props live
    // outside the terrain's hierarchy, so the tool takes them for themselves.
    public sealed class CavernScenery : MonoBehaviour
    {
        // The areas' names in catalogs and their colours, deep and saturated after the demo: green hex columns, blue quartz,
        // ruby red, and the cubic blocks' amber.
        public static readonly string[] AreaNames = { "hex", "quartz", "ruby", "cubes" };
        public static readonly Color[] Glow = { new Color(0f, .9f, .5f), new Color(.08f, .38f, 1f), new Color(1f, .05f, .02f), new Color(1f, .42f, .06f) };
        // A glowing crystal shows its own texture in its colour, GlowBase of it lit and glowing from its heart (its glow
        // map, BuriedPropsSetup.GlowMap) GlowIntensity times a colour as bright as GlowLuminance: brighter colours (the
        // green, the amber) glow less, so they keep their hue instead of burning pale. A crack glows CrackGlow times brighter.
        public const float GlowBase = .3f, GlowIntensity = 1.6f, GlowLuminance = .45f, CrackGlow = 2.5f;

        public static float GlowScale(int area)
        {
            var c = Glow[area];
            return GlowIntensity * Mathf.Min(1, GlowLuminance / (.2126f * c.r + .7152f * c.g + .0722f * c.b));
        }
        // An area's light, LightRise above the chamber's heart, dimming to LightLeft of it as its clusters break (the cube
        // blocks' warm light at CubeLight of it); a cluster's work to break, per metre of it, in seconds of the tool
        // working; how far its sealed pieces may lie from its middle, in its lengths; how deep a piece stands in the stone,
        // in its lengths; how much bigger a crack overlay is than its crystal, so it lies on the surface.
        private const float LightRange = 9f, LightIntensity = 3.2f, LightLeft = .45f, LightRise = .6f, CubeLight = .55f,
            WorkPerMetre = 1.6f, PieceReach = .6f, BloomFade = 1.5f, Sink = .18f, CrackLift = 1.006f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private DiscoveryField field;
        [SerializeField] private CavernDressing dressing;
        private Transform root;
        private CavernShatter shatter;
        private TerrainGround.GroundLayout dressed;
        private long dressedRevision = -1;
        private readonly List<AreaLight> lights = new List<AreaLight>();
        private readonly Dictionary<GameObject, Bounds> sizes = new Dictionary<GameObject, Bounds>();
        private Volume bloom;
        private FpsPlayer viewer;
        public int CrystalCount { get; private set; }
        public int FormationCount { get; private set; }
        public int LightCount => lights.Count;
        public float BloomWeight => bloom != null ? bloom.weight : 0;

        private sealed class AreaLight
        {
            public Light Light;
            public float Full;
            public int Clusters, Left;
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
            if (root == null)
            {
                root = new GameObject("Cavern scenery").transform;
                shatter = new CavernShatter(root, dressing);
            }
            foreach (Transform child in root) if (child.GetComponent<ParticleSystem>() == null) Destroy(child.gameObject);
            lights.Clear();
            CrystalCount = FormationCount = 0;
            dressed = layout;
            dressedRevision = field.PopulationRevision;
            var daylight = terrain.GetComponent<ExcavationDaylight>();
            foreach (var cavern in layout.Caverns)
                if (cavern.Crystal)
                    for (int chamber = 0; chamber < cavern.Centres.Length; chamber++)
                        DressArea(TerrainGround.Grove(cavern, chamber), terrain.transform.TransformPoint((Vector3)TerrainGround.CavernHeart(cavern, chamber)), daylight);
        }

        private void DressArea(List<TerrainGround.GroveCrystal> grove, Vector3 heart, ExcavationDaylight daylight)
        {
            if (grove.Count == 0) return;
            var area = grove[0].Area;
            var set = (int)area < dressing.Areas.Length ? dressing.Areas[(int)area] : null;
            if (set == null) return;
            var lit = new AreaLight();
            foreach (var crystal in grove)
            {
                if (crystal.Kind == TerrainGround.GroveKind.Shard) continue;
                var kinds = crystal.Kind == TerrainGround.GroveKind.Formation ? set.Formations : set.Clusters;
                if (kinds == null || kinds.Length == 0) continue;
                if (crystal.Kind == TerrainGround.GroveKind.Formation)
                {
                    Make(kinds[crystal.Variant % kinds.Length], crystal, set.Glows, daylight).AddComponent<CavernFormation>();
                    FormationCount++;
                    continue;
                }
                lit.Clusters++;
                var centre = terrain.transform.TransformPoint((Vector3)crystal.Centre);
                if (!field.AnySealedWithin(centre, crystal.Size * PieceReach)) continue;
                var prop = Make(kinds[crystal.Variant % kinds.Length], crystal, true, daylight);
                var cracks = AddCracks(prop, (int)area, daylight);
                prop.AddComponent<CavernCrystal>().Initialize(this, lit, centre, crystal.Size * PieceReach, crystal.Size * WorkPerMetre, Glow[(int)area], cracks);
                lit.Left++;
                CrystalCount++;
            }
            var light = new GameObject("Area light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = heart + Vector3.up * LightRise;
            light.type = LightType.Point;
            light.color = Glow[(int)area];
            light.range = LightRange;
            light.shadows = LightShadows.None;
            lit.Light = light;
            lit.Full = LightIntensity * (area == TerrainGround.CavernArea.Cubes ? CubeLight : 1);
            lights.Add(lit);
            Dim(lit);
        }

        private GameObject Make(GameObject kind, TerrainGround.GroveCrystal crystal, bool glows, ExcavationDaylight daylight)
        {
            var size = Size(kind);
            float s = crystal.Size / Mathf.Max(size.size.y, .01f);
            Vector3 up = crystal.Up, foot = crystal.Foot;
            var turn = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, crystal.Turn, 0);
            var pivot = foot - up * (crystal.Size * Sink) - turn * (size.center - Vector3.up * size.extents.y) * s;
            var prop = Instantiate(kind, terrain.transform.TransformPoint(pivot), terrain.transform.rotation * turn, root);
            prop.transform.localScale = kind.transform.localScale * s;
            var colour = Glow[(int)crystal.Area];
            float glow = GlowScale((int)crystal.Area);
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (daylight != null) daylight.Register(renderer);
                if (!glows) continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour * GlowBase);
                block.SetColor(EmissionColorId, colour * glow);
                renderer.SetPropertyBlock(block);
            }
            return prop;
        }

        // The crack overlay: each of the crystal's meshes again, a hair larger, in the crack material, clipped until it
        // cracks (CavernCrystal).
        private List<Renderer> AddCracks(GameObject prop, int area, ExcavationDaylight daylight)
        {
            var glow = Color.Lerp(Glow[area], Color.white, .5f) * (CrackGlow * GlowScale(area) / GlowIntensity);
            var overlays = new List<Renderer>();
            if (dressing.Cracks == null) return overlays;
            // Only the full-detail meshes: the overlays stand outside the crystal's detail levels.
            var levels = prop.GetComponentInChildren<LODGroup>();
            var sources = levels != null ? levels.GetLODs()[0].renderers : prop.GetComponentsInChildren<Renderer>(true);
            foreach (var source in sources)
            {
                var filter = source != null ? source.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null) continue;
                var overlay = new GameObject("Cracks");
                overlay.transform.SetParent(filter.transform, false);
                overlay.transform.localPosition = filter.sharedMesh.bounds.center * (1 - CrackLift);
                overlay.transform.localScale = Vector3.one * CrackLift;
                overlay.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = overlay.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = dressing.Cracks;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                if (daylight != null) daylight.Register(renderer);
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, glow);
                renderer.SetPropertyBlock(block);
                overlays.Add(renderer);
            }
            return overlays;
        }

        // A cluster broke: its pieces fall out, its area's light dims, shards fly from all of it.
        internal void Broke(CavernCrystal crystal, object area, Vector3 centre, float reach, Color colour)
        {
            field.UnsealWithin(centre, reach);
            shatter?.Burst(centre, Vector3.up, reach * .7f, colour, 70);
            if (area is AreaLight lit) { lit.Left = Mathf.Max(0, lit.Left - 1); Dim(lit); }
            CrystalCount = Mathf.Max(0, CrystalCount - 1);
            Destroy(crystal.gameObject);
        }

        // A C4 blast (026): every cluster whose middle lies within `radius` of `point` bursts.
        public int BlastWithin(Vector3 point, float radius)
        {
            if (root == null) return 0;
            int burst = 0;
            foreach (var crystal in root.GetComponentsInChildren<CavernCrystal>()) if (crystal.Blast(point, radius)) burst++;
            return burst;
        }

        internal void Chipped(Vector3 point, Vector3 normal, Color colour) => shatter?.Burst(point + normal * .02f, normal, .04f, colour, 9);

        // The work of one tool stroke, in seconds of the tool working: the viewer's stroke interval.
        internal float StrokeWork
        {
            get
            {
                if (viewer == null) viewer = FindAnyObjectByType<FpsPlayer>();
                return viewer != null ? viewer.EffectiveDigInterval : .25f;
            }
        }

        private static void Dim(AreaLight lit)
        {
            float share = lit.Clusters > 0 ? (float)lit.Left / lit.Clusters : 1;
            lit.Light.intensity = lit.Full * Mathf.Lerp(LightLeft, 1, share);
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
