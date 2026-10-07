using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SomethingDownThere
{
    // The glow of the caves and geodes (115, 110; user 2026-10-07: "crystals shine inside the caves and geodes so they
    // illuminate the area"): each hollow's crystals glow in their own colour (their materials), and a light in that
    // colour at its heart lights its walls, dimming as its crystals are taken and going out with the last. Made whenever
    // the ground's layout or the population changes, so it saves nothing. The light casts shadows from the ground only,
    // like a lamp's, so it never shows through the stone; only the nearest few hollows light at once. A bloom fades in
    // while the view is inside a lit hollow.
    public sealed class CavernScenery : MonoBehaviour
    {
        // A hollow's light at its heart: its reach beyond the hollow, how bright with all its crystals, and how much is
        // left with one; how many hollows light at once (each takes six faces of the shared shadow atlas) and how far.
        private const float LightReach = 3.5f, LightIntensity = 3.5f, LightLeft = .3f, LightCull = 30f, BloomFade = 1.5f;
        private const int LitHollows = 2;

        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private DiscoveryField field;
        [SerializeField] private CavernDressing dressing;
        private Transform root;
        private TerrainGround.GroundLayout dressed;
        private long dressedRevision = -1;
        private readonly List<Hollow> hollows = new List<Hollow>();
        private readonly List<Hollow> order = new List<Hollow>();
        private Volume bloom;
        private FpsPlayer viewer;
        public int HollowCount => hollows.Count;
        public int LitCount { get { int n = 0; foreach (var h in hollows) if (h.Light.enabled) n++; return n; } }
        public float BloomWeight => bloom != null ? bloom.weight : 0;

        private sealed class Hollow
        {
            public Light Light;
            public Vector3 Heart;
            public readonly List<BuriedFind> Crystals = new List<BuriedFind>();
            public System.Func<float3, bool> Inside;
            public float Share = 1;
        }

        private void OnDestroy() { if (root != null) Destroy(root.gameObject); }

        private void LateUpdate()
        {
            if (terrain == null || field == null) return;
            var layout = terrain.GroundLayout;
            if (!ReferenceEquals(layout, dressed) || field.PopulationRevision != dressedRevision) Dress(layout);
            if (viewer == null) viewer = FindAnyObjectByType<FpsPlayer>();
            var eye = viewer != null && viewer.ViewCamera != null ? viewer.ViewCamera.transform.position : transform.position;
            Shine(eye);
            UpdateBloom(eye);
        }

        private void Dress(TerrainGround.GroundLayout layout)
        {
            if (root == null) root = new GameObject("Hollow lights").transform;
            foreach (Transform child in root) Destroy(child.gameObject);
            hollows.Clear();
            dressed = layout;
            dressedRevision = field.PopulationRevision;
            foreach (var cave in layout.Caverns)
            {
                var c = cave;
                Add(TerrainGround.CavernHeart(cave, cave.Centres.Length / 2), math.cmax(cave.Max - cave.Min) * .5f,
                    p => math.all(p > c.Min) && math.all(p < c.Max) && TerrainGround.CavernOuter(c, p) < 0);
            }
            foreach (var geode in layout.Geodes)
            {
                var g = geode;
                Add(geode.Centre, geode.Reach, p => math.distance(p, g.Centre) < g.Reach);
            }
        }

        private void Add(float3 heart, float size, System.Func<float3, bool> inside)
        {
            var hollow = new Hollow { Heart = terrain.transform.TransformPoint((Vector3)heart), Inside = inside };
            foreach (var find in field.Finds)
                if (!find.Collected && inside((float3)terrain.transform.InverseTransformPoint(find.transform.position))) hollow.Crystals.Add(find);
            if (hollow.Crystals.Count == 0) return;
            var light = new GameObject("Hollow light", typeof(Light), typeof(UniversalAdditionalLightData)).GetComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = hollow.Heart;
            light.type = LightType.Point;
            light.color = Colour(hollow.Crystals[0]);
            light.range = size + LightReach;
            light.shadows = LightShadows.Soft; light.shadowBias = .015f; light.shadowNormalBias = .04f; light.shadowNearPlane = .05f;
            // Shadows from the ground only, as a lamp's: the stone round the hollow holds its light in.
            var data = light.GetComponent<UniversalAdditionalLightData>();
            data.usePipelineSettings = false; data.customShadowLayers = true; data.shadowRenderingLayers = TerrainVolume.LampShadowLayer;
            light.enabled = false;
            hollow.Light = light;
            hollows.Add(hollow);
        }

        // The crystals' own glow, as bright as it goes, for the light.
        private static Color Colour(BuriedFind crystal)
        {
            var renderer = crystal.GetComponentInChildren<Renderer>();
            var material = renderer != null ? renderer.sharedMaterial : null;
            var glow = material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.white;
            float most = Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b));
            return most > .001f ? new Color(glow.r / most, glow.g / most, glow.b / most) : Color.white;
        }

        // The nearest LitHollows within LightCull light, each as bright as the share of its crystals still in it.
        private void Shine(Vector3 eye)
        {
            order.Clear();
            foreach (var hollow in hollows)
            {
                int left = 0;
                foreach (var crystal in hollow.Crystals) if (crystal != null && !crystal.Collected) left++;
                hollow.Share = left / (float)hollow.Crystals.Count;
                if (left > 0 && (hollow.Heart - eye).sqrMagnitude < LightCull * LightCull) order.Add(hollow);
                else hollow.Light.enabled = false;
            }
            order.Sort((a, b) => (a.Heart - eye).sqrMagnitude.CompareTo((b.Heart - eye).sqrMagnitude));
            for (int i = 0; i < order.Count; i++)
            {
                var light = order[i].Light;
                light.enabled = i < LitHollows;
                light.intensity = LightIntensity * Mathf.Lerp(LightLeft, 1, order[i].Share);
            }
        }

        private void UpdateBloom(Vector3 eye)
        {
            if (dressing == null || dressing.Bloom == null) return;
            if (bloom == null)
            {
                var go = new GameObject("Hollow bloom");
                go.transform.SetParent(transform, false);
                bloom = go.AddComponent<Volume>();
                bloom.isGlobal = true; bloom.priority = 20; bloom.sharedProfile = dressing.Bloom; bloom.weight = 0;
            }
            var p = (float3)terrain.transform.InverseTransformPoint(eye);
            float target = 0;
            foreach (var hollow in hollows) if (hollow.Light.enabled && hollow.Inside(p)) { target = 1; break; }
            bloom.weight = Mathf.MoveTowards(bloom.weight, target, Time.unscaledDeltaTime * BloomFade);
        }
    }
}
