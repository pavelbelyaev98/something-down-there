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
    // whenever the ground's layout or the population changes, so it saves nothing. The lights cast no shadows: shadowed,
    // each one that came on or went out as the player moved or turned changed URP's shadow atlas, which it reallocates to
    // fit, a GPU stall of 20-40 ms (user, 2026-10-07: "microfreezes" flying out of a cave); a light's short reach can
    // show through a thin wall beside an opened hollow. Every crystal within LightCull lights, fading out from LightFull,
    // so none starts to shine as the player comes near ("from a distance a rock is not shining, but when I get closer it
    // shines"). Only a hollow that has been opened lights (DiscoveryField.CaveOpened, GeodeOpened): a
    // sealed one can't be seen into, so walking past it costs nothing. A crystal trophy (116) lights further. A crystal
    // lights only the dark (user, 2026-10-07: "it shouldn't emit light when it is light, it is not a lamp"): its light
    // fades out where daylight reaches it (ExcavationDaylight.SampleAmbient), from DarkAmbient to LitAmbient, and its own
    // glow down to DayGlow, so in daylight it still reads as a bright crystal ("too dark when there is light"), a trophy
    // standing at camp too. Its light lights the ground only, never a crystal, so none shows a hot spot inside.
    public sealed partial class CavernScenery : MonoBehaviour
    {
        // A crystal's light: how far in front of its middle (towards its hollow's heart), its reach and brightness; how
        // much each neighbour within its reach dims it, so a geode's crowd of crystals lights its hollow about as a few
        // would; how far from the eye it is at full strength and how far it reaches at all (fading between), the most lit at
        // once, nearest first, and how long a light takes to come and go.
        private const float LightOut = .25f, LightRange = 2.5f, LightIntensity = .6f, Crowding = .5f, LightFull = 28f, LightCull = 40f,
            LightFade = .35f;
        private const float DarkAmbient = .3f, LitAmbient = .7f;
        // How much of its glow a crystal keeps in daylight, and how often the glow follows the daylight.
        private const float DayGlow = .75f, ShadeEvery = .25f;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private const float TrophyRange = 4.5f, TrophyIntensity = 2.2f, TrophyOut = .6f;
        private const int LitCrystals = 48;

        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private DiscoveryField field;
        private Transform root;
        private TerrainGround.GroundLayout dressed;
        private long dressedRevision = -1;
        private readonly List<Glow> glows = new List<Glow>();
        private readonly List<Glow> order = new List<Glow>();
        private readonly Dictionary<Renderer, Shaded> shaded = new Dictionary<Renderer, Shaded>();
        private readonly HashSet<Renderer> shading = new HashSet<Renderer>();
        private readonly List<Renderer> stale = new List<Renderer>();
        private MaterialPropertyBlock block;
        private float shadeAt;
        private FpsPlayer viewer;
        private ExcavationDaylight daylight;
        public int CrystalCount => glows.Count;
        public int LitCount { get { int n = 0; foreach (var glow in glows) if (glow.Light.enabled) n++; return n; } }

        private sealed class Glow
        {
            public BuriedFind Crystal;
            public Renderer Body;
            public Light Light;
            public Vector3 Out;
            public float Shine, Distance, Strength = 1, Reach = LightOut, Dark = 1;
            public bool Wanted, Trophy;
            // Its hollow: a cave's index or a geode's (Geode).
            public int Hollow;
            public bool Geode;
        }

        // A crystal's own glow (its material's), dimmed as far as daylight reaches it.
        private sealed class Shaded
        {
            public Renderer Body;
            public Color Emission;
            public float Dark = 1;
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            ClearPuddles();
            if (puddleRoot != null) Destroy(puddleRoot.gameObject);
        }

        private void LateUpdate()
        {
            if (terrain == null || field == null) return;
            var layout = terrain.GroundLayout;
            if (!ReferenceEquals(layout, dressed) || field.PopulationRevision != dressedRevision) Dress(layout);
            UpdatePuddles(layout);
            if (viewer == null) viewer = FindAnyObjectByType<FpsPlayer>();
            Shine(viewer != null && viewer.ViewCamera != null ? viewer.ViewCamera.transform.position : transform.position);
            ShadeGlows();
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
            shading.Clear();
            foreach (var find in field.Finds)
            {
                if (kept.Remove(find, out var same)) { glows.Add(same); Shade(same.Body); continue; }
                if (find.Collected)
                {
                    // A trophy standing at camp keeps its glow to the daylight's measure.
                    if (find.StandsUpright && find.State == FindState.Stored) Shade(find.GetComponentInChildren<Renderer>());
                    continue;
                }
                var p = (float3)terrain.transform.InverseTransformPoint(find.transform.position);
                if (!Heart(layout, p, out var heart, out int hollow, out bool geode)) continue;
                var body = find.GetComponentInChildren<Renderer>();
                Shade(body);
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
            stale.Clear();
            foreach (var body in shaded.Keys) if (!shading.Contains(body)) stale.Add(body);
            foreach (var body in stale)
            {
                if (body != null) body.SetPropertyBlock(null);
                shaded.Remove(body);
            }
            shadeAt = 0;
            foreach (var glow in glows)
            {
                int near = 0;
                foreach (var other in glows)
                    if (other != glow && !other.Trophy && !other.Crystal.Collected && (other.Crystal.transform.position - glow.Crystal.transform.position).sqrMagnitude < LightRange * LightRange) near++;
                glow.Strength = glow.Trophy ? TrophyIntensity / LightIntensity : 1 / (1 + Crowding * near);
            }
        }

        private void Shade(Renderer body)
        {
            if (body == null || !shading.Add(body) || shaded.ContainsKey(body)) return;
            var material = body.sharedMaterial;
            if (material == null || !material.IsKeywordEnabled("_EMISSION") || !material.HasProperty(EmissionId)) return;
            shaded.Add(body, new Shaded { Body = body, Emission = material.GetColor(EmissionId) });
        }

        // Each crystal's own glow to the daylight round it, now and then (the daylight changes only as the ground is dug).
        private void ShadeGlows()
        {
            if (Time.unscaledTime < shadeAt) return;
            shadeAt = Time.unscaledTime + ShadeEvery;
            foreach (var shade in shaded.Values)
            {
                if (shade.Body == null) continue;
                float dark = Darkness(shade.Body.bounds.center);
                if (Mathf.Abs(dark - shade.Dark) < .02f && (dark < 1 || shade.Dark == 1)) continue;
                shade.Dark = dark;
                if (dark >= 1) { shade.Body.SetPropertyBlock(null); continue; }
                block ??= new MaterialPropertyBlock();
                block.Clear();
                block.SetColor(EmissionId, shade.Emission * Mathf.Lerp(DayGlow, 1, dark));
                shade.Body.SetPropertyBlock(block);
            }
        }

        private float Darkness(Vector3 at)
        {
            if (daylight == null) daylight = terrain.GetComponent<ExcavationDaylight>();
            return daylight == null ? 1 : 1 - Mathf.InverseLerp(DarkAmbient, LitAmbient, daylight.SampleAmbient(at));
        }

        private static bool Heart(TerrainGround.GroundLayout layout, float3 p, out float3 heart, out int hollow, out bool geode)
        {
            geode = false;
            for (hollow = 0; hollow < layout.Caverns.Length; hollow++)
            {
                var cave = layout.Caverns[hollow];
                if (math.any(p < cave.Min) || math.any(p > cave.Max) || !NearChamber(cave, p) || TerrainGround.CavernOuter(cave, p) >= 0) continue;
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

        // Within reach of one of a cave's chambers (its radii grown by its shell and a margin): the cheap test before its shape.
        private static bool NearChamber(in TerrainGround.Cavern cave, float3 p)
        {
            for (int i = 0; i < cave.Centres.Length; i++)
                if (math.lengthsq((p - cave.Centres[i]) / (cave.Radii[i] + cave.Shell + 1.5f)) < 1) return true;
            return false;
        }

        private Light MakeLight(Color colour, float range)
        {
            var light = new GameObject("Crystal light", typeof(Light), typeof(UniversalAdditionalLightData)).GetComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.shadows = LightShadows.None;
            // It lights the ground only (the chunks carry the lamps' ground layer): a crystal it sits in front of would show
            // it as a hot spot inside.
            var data = light.GetComponent<UniversalAdditionalLightData>();
            data.renderingLayers = TerrainVolume.LampShadowLayer;
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

        // The nearest LitCrystals still in place within LightCull light, fading in and out, weaker past LightFull; a taken
        // crystal's light fades. One in daylight has no light to give and leaves its place to the next.
        private void Shine(Vector3 eye)
        {
            order.Clear();
            foreach (var glow in glows)
            {
                glow.Wanted = false;
                if (glow.Crystal == null || glow.Crystal.Collected || !(glow.Geode ? field.GeodeOpened(glow.Hollow) : field.CaveOpened(glow.Hollow))) continue;
                glow.Distance = (glow.Crystal.transform.position - eye).sqrMagnitude;
                if (glow.Distance >= LightCull * LightCull) continue;
                glow.Dark = Darkness(glow.Body != null ? glow.Body.bounds.center + glow.Out * glow.Reach : glow.Light.transform.position);
                if (glow.Dark > .01f) order.Add(glow);
            }
            order.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            for (int i = 0; i < order.Count && i < LitCrystals; i++) order[i].Wanted = true;
            foreach (var glow in glows)
            {
                glow.Shine = Mathf.MoveTowards(glow.Shine, glow.Wanted ? 1 : 0, Time.unscaledDeltaTime / LightFade);
                bool on = glow.Shine > .001f;
                if (glow.Light.enabled != on) glow.Light.enabled = on;
                if (!on) continue;
                float far = 1 - Mathf.InverseLerp(LightFull * LightFull, LightCull * LightCull, glow.Distance);
                glow.Light.intensity = LightIntensity * glow.Strength * glow.Shine * glow.Dark * far;
                // A loose crystal can move: the light follows its middle (a taken one's fades where it was).
                if (glow.Wanted && glow.Body != null) glow.Light.transform.position = glow.Body.bounds.center + glow.Out * glow.Reach;
            }
        }
    }
}
