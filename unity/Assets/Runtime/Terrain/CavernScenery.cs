using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SomethingDownThere
{
    // The crystals' light in the caves and geodes (115, 110; user, 2026-10-07: "crystals shine inside the caves and
    // geodes", then "act similar to a lamp ... the crystals themselves a light source when it is dark, but not illuminate
    // everything around them strongly"): each crystal glows in its colour (its material) and carries a small light of its
    // own just in front of it, lighting the stone round it like a little lamp; taking a crystal takes only its light. Made
    // whenever the ground's layout or the population changes, so it saves nothing. The lights cast shadows from the ground
    // only, as lamps do, so they never show through the stone; the nearest few light at once and fade in and out, like
    // lamps outside their budget. Only a hollow that has been opened lights (DiscoveryField.CaveOpened, GeodeOpened): a
    // sealed one can't be seen into, so walking past it costs nothing. A crystal trophy (116) lights further.
    public sealed class CavernScenery : MonoBehaviour
    {
        // A crystal's light: how far in front of its middle (towards its hollow's heart), its reach and brightness; how
        // much each neighbour within its reach dims it, so a geode's crowd of crystals lights its hollow about as a few
        // would; how many light at once (each takes six small faces of the shared shadow atlas, beside the lamps') and how
        // far off, and how long it takes to fade.
        private const float LightOut = .25f, LightRange = 2f, LightIntensity = .6f, Crowding = .5f, LightCull = 25f, LightFade = .35f;
        private const float TrophyRange = 4.5f, TrophyIntensity = 2.2f, TrophyOut = .6f;
        private const int LitCrystals = 10;

        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private DiscoveryField field;
        private Transform root;
        private TerrainGround.GroundLayout dressed;
        private long dressedRevision = -1;
        private readonly List<Glow> glows = new List<Glow>();
        private readonly List<Glow> order = new List<Glow>();
        private FpsPlayer viewer;
        public int CrystalCount => glows.Count;
        public int LitCount { get { int n = 0; foreach (var glow in glows) if (glow.Light.enabled) n++; return n; } }

        private sealed class Glow
        {
            public BuriedFind Crystal;
            public Renderer Body;
            public Light Light;
            public Vector3 Out;
            public float Shine, Distance, Strength = 1, Reach = LightOut;
            public bool Wanted, Trophy;
            // Its hollow: a cave's index or a geode's (Geode).
            public int Hollow;
            public bool Geode;
        }

        private void OnDestroy() { if (root != null) Destroy(root.gameObject); }

        private void LateUpdate()
        {
            if (terrain == null || field == null) return;
            var layout = terrain.GroundLayout;
            if (!ReferenceEquals(layout, dressed) || field.PopulationRevision != dressedRevision) Dress(layout);
            if (viewer == null) viewer = FindAnyObjectByType<FpsPlayer>();
            Shine(viewer != null && viewer.ViewCamera != null ? viewer.ViewCamera.transform.position : transform.position);
        }

        // A light for every crystal in a hollow, its way out the way to the hollow's heart (a cave's nearest chamber's).
        // Crystals still there keep their light as it is.
        private readonly Dictionary<BuriedFind, Glow> kept = new Dictionary<BuriedFind, Glow>();

        private void Dress(TerrainGround.GroundLayout layout)
        {
            if (root == null) root = new GameObject("Crystal lights").transform;
            kept.Clear();
            foreach (var glow in glows)
                if (glow.Crystal != null && ReferenceEquals(layout, dressed)) kept[glow.Crystal] = glow;
                else Destroy(glow.Light.gameObject);
            glows.Clear();
            dressed = layout;
            dressedRevision = field.PopulationRevision;
            foreach (var find in field.Finds)
            {
                if (kept.Remove(find, out var same)) { glows.Add(same); continue; }
                if (find.Collected) continue;
                var p = (float3)terrain.transform.InverseTransformPoint(find.transform.position);
                if (!Heart(layout, p, out var heart, out int hollow, out bool geode)) continue;
                var body = find.GetComponentInChildren<Renderer>();
                var middle = body != null ? body.bounds.center : find.transform.position;
                var toward = terrain.transform.TransformPoint((Vector3)heart) - middle;
                bool trophy = find.Item.Kind == DiscoveryKind.Unique;
                glows.Add(new Glow
                {
                    Crystal = find, Body = body, Light = MakeLight(Colour(body), trophy ? TrophyRange : LightRange),
                    Out = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.up, Trophy = trophy,
                    Reach = trophy ? TrophyOut : LightOut, Hollow = hollow, Geode = geode
                });
            }
            foreach (var gone in kept.Values) Destroy(gone.Light.gameObject);
            kept.Clear();
            foreach (var glow in glows)
            {
                int near = 0;
                foreach (var other in glows)
                    if (other != glow && !other.Trophy && !other.Crystal.Collected && (other.Crystal.transform.position - glow.Crystal.transform.position).sqrMagnitude < LightRange * LightRange) near++;
                glow.Strength = glow.Trophy ? TrophyIntensity / LightIntensity : 1 / (1 + Crowding * near);
            }
        }

        private static bool Heart(TerrainGround.GroundLayout layout, float3 p, out float3 heart, out int hollow, out bool geode)
        {
            geode = false;
            for (hollow = 0; hollow < layout.Caverns.Length; hollow++)
            {
                var cave = layout.Caverns[hollow];
                if (math.any(p < cave.Min) || math.any(p > cave.Max) || TerrainGround.CavernOuter(cave, p) >= 0) continue;
                int nearest = 0;
                for (int i = 1; i < cave.Centres.Length; i++)
                    if (math.distancesq(cave.Centres[i], p) < math.distancesq(cave.Centres[nearest], p)) nearest = i;
                heart = TerrainGround.CavernHeart(cave, nearest);
                return true;
            }
            geode = true;
            for (hollow = 0; hollow < layout.Geodes.Length; hollow++)
                if (math.distance(p, layout.Geodes[hollow].Centre) < layout.Geodes[hollow].Reach) { heart = layout.Geodes[hollow].Centre; return true; }
            heart = default;
            return false;
        }

        private Light MakeLight(Color colour, float range)
        {
            var light = new GameObject("Crystal light", typeof(Light), typeof(UniversalAdditionalLightData)).GetComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.shadows = LightShadows.Soft; light.shadowBias = .015f; light.shadowNormalBias = .04f; light.shadowNearPlane = .05f;
            // Shadows from the ground only, as a lamp's: the stone round the hollow holds its light in. Small shadow faces,
            // so ten of them leave the lamps' theirs.
            var data = light.GetComponent<UniversalAdditionalLightData>();
            data.usePipelineSettings = false; data.customShadowLayers = true; data.shadowRenderingLayers = TerrainVolume.LampShadowLayer;
            data.additionalLightsShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            light.enabled = false;
            return light;
        }

        // The crystal's own glow, as bright as it goes, for its light.
        private static Color Colour(Renderer body)
        {
            var material = body != null ? body.sharedMaterial : null;
            var glow = material != null && material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.white;
            float most = Mathf.Max(glow.r, Mathf.Max(glow.g, glow.b));
            return most > .001f ? new Color(glow.r / most, glow.g / most, glow.b / most) : Color.white;
        }

        // The nearest LitCrystals still in place within LightCull light, fading in and out; a taken crystal's light fades.
        private void Shine(Vector3 eye)
        {
            order.Clear();
            foreach (var glow in glows)
            {
                glow.Wanted = false;
                if (glow.Crystal == null || glow.Crystal.Collected || !(glow.Geode ? field.GeodeOpened(glow.Hollow) : field.CaveOpened(glow.Hollow))) continue;
                glow.Distance = (glow.Crystal.transform.position - eye).sqrMagnitude;
                if (glow.Distance < LightCull * LightCull) order.Add(glow);
            }
            order.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 0; i < order.Count && i < LitCrystals; i++) order[i].Wanted = true;
            foreach (var glow in glows)
            {
                glow.Shine = Mathf.MoveTowards(glow.Shine, glow.Wanted ? 1 : 0, Time.unscaledDeltaTime / LightFade);
                bool on = glow.Shine > .001f;
                if (glow.Light.enabled != on) glow.Light.enabled = on;
                if (!on) continue;
                glow.Light.intensity = LightIntensity * glow.Strength * glow.Shine;
                // A loose crystal can move: the light follows its middle (a taken one's fades where it was).
                if (glow.Wanted && glow.Body != null) glow.Light.transform.position = glow.Body.bounds.center + glow.Out * glow.Reach;
            }
        }
    }
}
