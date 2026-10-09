using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere.Editor
{
    // The exposed lakebed floor: drying channels that still trickle around the dig plot into the
    // lake, packed sediment on the flats, damp silt, pebble strands and sandy banks toward the
    // water, clumps of dry grass and reeds, and stranded debris.
    public static partial class LakebedSiteSetup
    {
        public const string BareRockMaterialPath = Folder + "/LakebedRock.mat";
        private const string MountainTerrainPath = "Assets/BK/PureNature_Mountains/Models/TerrainMountain.asset";
        private const string MountainPrefabs = "Assets/BK/PureNature_Mountains/Prefabs/";
        private const string VendorRockMaterial = "Assets/BK/PureNature_Highlands/Models/Rocks/Rock/Textures/Materials/Rock.mat";
        private const float TrickleDepth = .15f, BankWidth = 2.4f, MouthLift = .005f, MaxBedGrade = .08f, DryBedGrade = .25f;
        // A bend this tight (metres of radius) or tighter has the full point bar inside it and cut bank outside.
        private const float BendRadius = 7f;
        // Metres beyond the plot outline: channels leave a walkable margin around it.
        public const float ChannelClearance = 6f;
        public const string TrickleMeshPath = Folder + "/Trickles.asset";

        // How a course carries water: a stream still trickles into the lake, a gully is a dry channel that
        // ends on one, and a rill is a shallow erosion groove the retreating water cut down a slope.
        private enum Flow { Stream, Gully, Rill }

        // Site-local courses, upstream first, laid the way water runs on the drained shelf: west from seeps
        // at the cliff foot down into the lake by the most direct way, passing the dig plot rather than
        // wrapping it (user, 2026-10-09: channels hugging the plot and running along the shore read as
        // artificial). The north stream splits into two mouths; gullies join the streams and rills drain the
        // shore slopes, some with a side branch.
        private static readonly (Vector2[] course, Flow flow, float seed)[] Courses =
        {
            (new[] { new Vector2(27, 31), new Vector2(16, 27.5f), new Vector2(4, 29.5f), new Vector2(-8, 26.5f), new Vector2(-19, 29),
                new Vector2(-29, 27.5f), new Vector2(-37, 30.5f), new Vector2(-47, 32), new Vector2(-56, 33) }, Flow.Stream, 1.3f),
            (new[] { new Vector2(-29, 27.5f), new Vector2(-34, 22.5f), new Vector2(-43, 20), new Vector2(-52, 18.5f) }, Flow.Stream, 6.2f),
            (new[] { new Vector2(13, -42), new Vector2(3, -39.5f), new Vector2(-8, -35), new Vector2(-19, -37.5f), new Vector2(-29, -34.5f),
                new Vector2(-38, -37.5f), new Vector2(-49, -37), new Vector2(-58, -38) }, Flow.Stream, 4.1f),
            (new[] { new Vector2(34, 16), new Vector2(30, 22.5f), new Vector2(25, 29.5f) }, Flow.Gully, 2.9f),
            (new[] { new Vector2(-4, 49), new Vector2(-7, 40), new Vector2(-12, 33), new Vector2(-13, 27.5f) }, Flow.Gully, 8.4f),
            (new[] { new Vector2(-22, 9), new Vector2(-30, 11), new Vector2(-40, 12.5f), new Vector2(-46, 13) }, Flow.Rill, 3.7f),
            (new[] { new Vector2(-27, 16), new Vector2(-31, 13.5f), new Vector2(-33, 11.3f) }, Flow.Rill, 5.1f),
            (new[] { new Vector2(-20, -3), new Vector2(-27, -1.5f), new Vector2(-31, -.5f) }, Flow.Rill, 7.7f),
            (new[] { new Vector2(-18, -50), new Vector2(-27, -53), new Vector2(-38, -51.5f), new Vector2(-45, -51) }, Flow.Rill, 9.2f),
            (new[] { new Vector2(-20, -58), new Vector2(-26, -55), new Vector2(-29, -53.2f) }, Flow.Rill, 2.2f),
            (new[] { new Vector2(-6, -54), new Vector2(-12, -62), new Vector2(-17, -70), new Vector2(-19, -76) }, Flow.Rill, 6.6f),
            (new[] { new Vector2(-22, 41), new Vector2(-26, 47), new Vector2(-27, 55), new Vector2(-27.5f, 61) }, Flow.Rill, 4.4f),
            (new[] { new Vector2(-15, 38), new Vector2(-19, 44), new Vector2(-24.5f, 46.5f) }, Flow.Rill, 1.9f),
        };

        private sealed class Stream
        {
            public Vector2[] Points;  // site-local centreline, upstream first
            public float[] Bed;       // demo-space bed height, never rising downstream
            public float[] HalfWidth; // bed half-width
            public float[] Turn;      // signed bend (+ left), 1 at BendRadius or tighter
            public Flow Flow;
            public bool Wet => Flow == Flow.Stream;
            // Bank width as a share of BankWidth: rills are narrow grooves.
            public float Banks => Flow == Flow.Stream ? 1 : Flow == Flow.Gully ? .8f : .4f;
        }

        // Grass around the plot (user, 2026-10-02): none on the worked ground just past the tape, patches
        // from about GreenStart metres beyond the outline thinning out toward the open lakebed, sparser on
        // the camp's side and never by the water. The turf and its green blades share it.
        private const float GreenStart = 4.5f, GreenEnd = 17f;

        // Where the patches lie, 0-1, with an edge `soft` noise units wide: the turf's edge is crisp, the
        // blades' wider so they can fray out past it.
        private static float GreenShape(Vector2 local, float aboveWater, float channel, float soft)
        {
            float beyond = SiteLayout.BeyondOpening(local);
            float ring = Smooth((beyond - GreenStart) / 2.5f) * (1 - Smooth((beyond - GreenEnd) / 5));
            float patch = Smooth((Noise(local, 5.5f, 8.1f) - .56f + soft) / soft);
            return ring * patch * Smooth((aboveWater - .35f) / .3f) * Smooth((channel - 1.2f) / .8f);
        }

        // How dense the patches grow: sparser on the camp's side.
        private static float GreenDensity(Vector2 local) =>
            Mathf.Lerp(.35f, 1, Smooth((Mathf.Abs(Mathf.DeltaAngle(SiteLayout.Compass(local), SiteLayout.CampCompass)) - SiteLayout.CampHalfArc) / 20));

        // Knee-high meadow grass round every tree's foot hides its root flare, whose pack grass layer stays
        // lime (user, 2026-10-02): full cover out to RootTuftRadius (times the tree's width), fading over
        // RootTuftFade.
        private const float RootTuftRadius = 1.5f, RootTuftFade = .8f;

        private static void TuftTreeBases(TerrainData data, int first)
        {
            int cells = data.detailWidth, layer = first + (int)LakebedDetail.GreenGrass;
            var map = data.GetDetailLayer(0, 0, cells, cells, layer);
            float cell = data.size.x / cells;
            foreach (var tree in data.treeInstances)
            {
                float cx = tree.position.x * cells, cz = tree.position.z * cells, radius = RootTuftRadius * Mathf.Max(tree.widthScale, .5f);
                int reach = Mathf.CeilToInt((radius + RootTuftFade) / cell);
                for (int v = Mathf.Max(0, (int)cz - reach); v <= Mathf.Min(cells - 1, (int)cz + reach); v++)
                for (int u = Mathf.Max(0, (int)cx - reach); u <= Mathf.Min(cells - 1, (int)cx + reach); u++)
                {
                    float distance = new Vector2(u + .5f - cx, v + .5f - cz).magnitude * cell;
                    int cover = Mathf.RoundToInt(255 * (1 - Smooth((distance - radius) / RootTuftFade)));
                    if (cover > map[v, u]) map[v, u] = cover;
                }
            }
            data.SetDetailLayer(0, 0, layer, map);
        }

        // Grass cover whose patches thin out into stray tufts instead of ending in a line: a fine noise is
        // thresholded against the patch's strength, so a patch is full in its core and ragged at its edge.
        // Evenly dense mats with crisp outlines read as laid turf (user, 2026-10-09).
        private static float Frayed(float cover, Vector2 local, float seed) =>
            cover * Smooth((1.5f * cover - .45f + .6f * (Noise(local, 1.1f, seed) - .5f)) / .3f);

        private static float Band(float value, float low, float high, float soft = .15f) =>
            Smooth((value - low) / soft) * Smooth((high - value) / soft);

        private static Vector3 DemoPoint(Vector2 local) => new Vector3(local.x + SiteInDemo.x, 0, local.y + SiteInDemo.z);

        private static void CarveChannels(Section s)
        {
            int n = s.Samples;
            s.Uncarved = (float[,])s.After.Clone();
            s.Channel = new float[n, n];
            s.Rill = new float[n, n];
            s.Bar = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) s.Channel[z, x] = s.Rill[z, x] = float.MaxValue;
            // Dry courses first: a wet channel then follows their mouths instead of a gully
            // cutting a hole beneath the running water.
            foreach (var (course, flow, seed) in Courses.OrderBy(c => c.flow == Flow.Stream))
            {
                bool wet = flow == Flow.Stream;
                var points = Around(Meander(course, .5f, seed, flow == Flow.Rill ? .35f : flow == Flow.Gully ? .6f : 1), s.Rocks);
                var stream = new Stream { Points = points, Flow = flow, Bed = new float[points.Length], HalfWidth = new float[points.Length], Turn = Turns(points) };
                float total = 0;
                for (int i = 1; i < points.Length; i++) total += Vector2.Distance(points[i], points[i - 1]);
                float along = 0, lowest = float.MaxValue;
                bool reachedLake = false;
                for (int i = 0; i < points.Length; i++)
                {
                    if (i > 0) along += Vector2.Distance(points[i], points[i - 1]);
                    float t = along / total;
                    // The width wanders along the course: wide shallow riffles, narrow deeper runs.
                    float wide = Mathf.PerlinNoise(along / 8, seed * 5.3f);
                    // Occasional pools widen and deepen the wet channels.
                    float pool = wet ? Mathf.Pow(Mathf.PerlinNoise(along / 13, seed * 3.1f), 3) * 1.6f : 0;
                    float depth, half;
                    if (wet)
                    {
                        depth = Mathf.Lerp(.2f, .5f, Smooth(t / .35f)) * Mathf.Lerp(1.2f, .85f, wide) + .12f * pool;
                        half = Mathf.Lerp(.9f, 1.9f, t) * Mathf.Lerp(.7f, 1.3f, wide) * (1 + .4f * pool);
                    }
                    else if (flow == Flow.Gully)
                    {
                        depth = Mathf.Lerp(.08f, .28f, Smooth(t / .5f));
                        half = Mathf.Lerp(.5f, .9f, t) * Mathf.Lerp(.75f, 1.25f, wide);
                    }
                    else
                    {
                        // A rill starts as a faint groove and deepens down the slope; narrower than about a
                        // metre, the terrain's half-metre samples would show it as a jagged trench.
                        depth = Mathf.Lerp(.04f, .15f, Smooth(t / .6f));
                        half = Mathf.Lerp(.3f, .55f, t) * Mathf.Lerp(.8f, 1.2f, wide);
                    }
                    half *= Mathf.Lerp(.35f, 1, Smooth(along / 5)); // Seeps emerge narrow from the bank.
                    // The bed is cut below the lowest ground across the whole section, so on a side
                    // slope both banks still rise above the water instead of it spilling downhill.
                    float ground = SectionFloor(s, s.Uncarved, points, i, half + BankWidth * stream.Banks);
                    // Beds stay just above the lake plane until the course reaches open lake water,
                    // so no inland stretch dips into a pool the lake would show through; from there
                    // the bed follows the ground down at the gentle grade below.
                    reachedLake |= s.Sample(s.Uncarved, DemoPoint(points[i])) < WaterLevel - .05f;
                    float floor = reachedLake ? float.MinValue : WaterLevel + .03f;
                    lowest = Mathf.Max(Mathf.Min(lowest, ground - depth), floor);
                    // A wet bed falls at most a gentle grade per metre: a sudden drop leaves a straight ledge
                    // across the channel that the water outlines as a polygon. Dry beds follow steeper slopes.
                    if (i > 0) lowest = Mathf.Max(lowest, stream.Bed[i - 1] - (wet ? MaxBedGrade : DryBedGrade) * Vector2.Distance(points[i], points[i - 1]));
                    stream.Bed[i] = lowest;
                    stream.HalfWidth[i] = half;
                }
                Carve(s, stream);
                s.Streams.Add(stream);
            }
            s.StreamLevel = StreamLevels(s);
        }

        // Signed bend of a centreline at each point (+ turning left), 1 at BendRadius or tighter,
        // measured over a few metres so the small meander kinks do not count.
        private static float[] Turns(Vector2[] points)
        {
            const int reach = 4;
            int count = points.Length;
            var raw = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 a = points[Mathf.Max(i - reach, 0)], b = points[i], c = points[Mathf.Min(i + reach, count - 1)];
                float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ac = Vector2.Distance(a, c);
                if (ab < 1e-3f || bc < 1e-3f || ac < 1e-3f) continue;
                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                raw[i] = Mathf.Clamp(2 * cross / (ab * bc * ac) * BendRadius, -1, 1);
            }
            var turn = new float[count];
            for (int i = 0; i < count; i++)
            {
                float sum = 0; int n = 0;
                for (int j = Mathf.Max(0, i - reach); j <= Mathf.Min(count - 1, i + reach); j++) { sum += raw[j]; n++; }
                turn[i] = sum / n;
            }
            return turn;
        }

        // Lowest height of a field across a course's section at one point: the centre and both banks.
        private static float SectionFloor(Section s, float[,] field, Vector2[] points, int i, float reach)
        {
            var tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
            var normal = new Vector2(-tangent.y, tangent.x) * reach;
            return Mathf.Min(s.Sample(field, DemoPoint(points[i])),
                Mathf.Min(s.Sample(field, DemoPoint(points[i] + normal)), s.Sample(field, DemoPoint(points[i] - normal))));
        }

        // Pushes a course clear of the demo's boulders and relaxes the bends, so streams flow
        // around rocks instead of undercutting them. Both ends stay where they were authored.
        private static Vector2[] Around(Vector2[] points, List<(Vector2 centre, float radius)> rocks)
        {
            var result = (Vector2[])points.Clone();
            void Push()
            {
                for (int i = 1; i < result.Length - 1; i++)
                foreach (var (centre, radius) in rocks)
                {
                    var away = result[i] - centre;
                    float need = radius + 3.5f;
                    if (away.sqrMagnitude < need * need) result[i] = centre + (away.sqrMagnitude > 1e-4f ? away.normalized : Vector2.right) * need;
                }
            }
            for (int pass = 0; pass < 6; pass++)
            {
                Push();
                var relaxed = (Vector2[])result.Clone();
                for (int i = 1; i < result.Length - 1; i++) relaxed[i] = .25f * result[i - 1] + .5f * result[i] + .25f * result[i + 1];
                result = relaxed;
            }
            Push();
            return result;
        }

        // Catmull-Rom through the authored course with meanders faded out at both ends, so seeps and
        // junctions stay where they were authored. Their wavelength and swing wander along the course
        // (scaled down for small channels): straighter runs give way to deeper bends, never a regular
        // wave, and a little noise kinks them.
        private static Vector2[] Meander(Vector2[] controls, float spacing, float seed, float scale)
        {
            var dense = new List<Vector2>();
            for (int i = 0; i < controls.Length - 1; i++)
            {
                Vector2 p0 = controls[Mathf.Max(i - 1, 0)], p1 = controls[i], p2 = controls[i + 1], p3 = controls[Mathf.Min(i + 2, controls.Length - 1)];
                int steps = Mathf.CeilToInt(Vector2.Distance(p1, p2) / spacing);
                for (int k = 0; k < steps; k++)
                {
                    float t = k / (float)steps;
                    dense.Add(.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t));
                }
            }
            dense.Add(controls[controls.Length - 1]);
            var along = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++) along[i] = along[i - 1] + Vector2.Distance(dense[i], dense[i - 1]);
            float total = along[dense.Count - 1], phase = seed;
            var result = new Vector2[dense.Count];
            for (int i = 0; i < dense.Count; i++)
            {
                var tangent = (dense[Mathf.Min(i + 1, dense.Count - 1)] - dense[Mathf.Max(i - 1, 0)]).normalized;
                if (i > 0) phase += 2 * Mathf.PI * (along[i] - along[i - 1]) / (Mathf.Lerp(13, 30, Mathf.PerlinNoise(along[i] / 23, seed * 2.3f)) * Mathf.Sqrt(scale));
                float swing = Mathf.Lerp(.2f, 2.6f, Mathf.Pow(Mathf.PerlinNoise(along[i] / 17, seed * 4.7f + 3), 1.6f)) * scale;
                float wiggle = (swing * Mathf.Sin(phase) + .7f * scale * (Mathf.PerlinNoise(along[i] / 4.5f, seed) - .5f))
                    * Smooth(along[i] / 6) * Smooth((total - along[i]) / 6);
                result[i] = dense[i] + new Vector2(-tangent.y, tangent.x) * wiggle;
            }
            return result;
        }

        // Bends are lopsided, as in a real channel: the deepest line swings to the outside, where the bank
        // is short and steep (the cut bank), while the inside shelves gently out of the water as a point
        // bar of gravel and sand (Section.Bar). Straight runs keep a symmetric bed.
        private static void Carve(Section s, Stream stream)
        {
            float bankWidth = BankWidth * stream.Banks;
            float reach = stream.HalfWidth.Max() + bankWidth * 2.4f;
            var min = stream.Points.Aggregate(Vector2.positiveInfinity, Vector2.Min) - Vector2.one * reach;
            var max = stream.Points.Aggregate(Vector2.negativeInfinity, Vector2.Max) + Vector2.one * reach;
            int Index(float local, float site, float origin) => Mathf.Clamp(Mathf.RoundToInt((local + site - origin) / s.Cell), 0, s.Samples - 1);
            int x0 = Index(min.x, SiteInDemo.x, s.Origin.x), x1 = Index(max.x, SiteInDemo.x, s.Origin.x);
            int z0 = Index(min.y, SiteInDemo.z, s.Origin.y), z1 = Index(max.y, SiteInDemo.z, s.Origin.y);
            var points = stream.Points;
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                var p = s.Local(x, z);
                // Beds keep clear of the plot and never uncover the collar roofing the rest of the
                // grid; banks fade out over two metres there instead of ending in a cut.
                float keep = Mathf.Min(SiteLayout.BeyondOpening(p) - ChannelClearance, BeyondCollar(p) - 1.5f);
                // Banks also fade out before a boulder, so no rock overhangs a cut.
                foreach (var (centre, radius) in s.Rocks) keep = Mathf.Min(keep, Vector2.Distance(p, centre) - radius);
                if (keep <= 0) continue;
                // The deepest cut of every stretch in reach, not just the nearest one: where a course
                // bends, the nearest stretch can jump to one with a different bed and leave a
                // straight-walled step.
                float ground = s.After[z, x], cut = ground, edge = float.MaxValue, bar = 0;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var ab = points[i + 1] - points[i];
                    var ap = p - points[i];
                    float u = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    float distance = Vector2.Distance(points[i] + ab * u, p);
                    float half = Mathf.Lerp(stream.HalfWidth[i], stream.HalfWidth[i + 1], u);
                    float turn = Mathf.Lerp(stream.Turn[i], stream.Turn[i + 1], u);
                    // Positive on the inside of a bend, negative outside it.
                    float side = ab.x * ap.y - ab.y * ap.x >= 0 ? 1 : -1, inside = side * turn;
                    float bank = bankWidth * (inside > 0 ? 1 + 1.4f * inside : 1 + .55f * inside);
                    if (distance > half + bank) continue;
                    float bed = Mathf.Lerp(stream.Bed[i], stream.Bed[i + 1], u);
                    float across = side * distance / half + .45f * turn, rim = side * Mathf.Min(distance / half, 1) + .45f * turn;
                    cut = Mathf.Min(cut, distance < half ? bed + .06f * across * across
                        : Mathf.Lerp(bed + .06f * rim * rim, ground, Smooth((distance - half) / bank)));
                    edge = Mathf.Min(edge, distance - half);
                    if (inside > 0 && stream.Flow != Flow.Rill)
                        bar = Mathf.Max(bar, Smooth(inside * 1.6f) * Smooth((distance - .25f * half) / (.35f * half))
                            * (1 - Smooth((distance - half - .3f * bank) / (.6f * bank))) * (stream.Wet ? 1 : .5f));
                }
                if (edge == float.MaxValue) continue;
                float fade = Smooth(keep / 2);
                s.After[z, x] = Mathf.Min(ground, Mathf.Lerp(ground, cut, fade));
                if (stream.Flow == Flow.Rill) s.Rill[z, x] = Mathf.Min(s.Rill[z, x], edge);
                else s.Channel[z, x] = Mathf.Min(s.Channel[z, x], edge);
                s.Bar[z, x] = Mathf.Max(s.Bar[z, x], bar * fade);
            }
        }

        // Metres outside the collar's rectangular roof, conservative at its corners.
        private static float BeyondCollar(Vector2 p) =>
            Mathf.Max(Mathf.Abs(p.x) - SiteLayout.Extent.x * .5f, Mathf.Abs(p.y) - SiteLayout.Extent.z * .5f) - CollarOverlap;

        // Water surface of the wet streams at every terrain sample within reach of a bed: the carved
        // bed height along the course plus a shallow depth, blended across nearby stretches and
        // courses so it stays continuous at bends and the fork. It follows the bed's gentle grade down to 5 mm above the lake plane.
        private static float[,] StreamLevels(Section s)
        {
            int n = s.Samples;
            var level = new float[n, n];
            var weight = new float[n, n];
            int Index(float local, float site, float origin) => Mathf.Clamp(Mathf.RoundToInt((local + site - origin) / s.Cell), 0, n - 1);
            foreach (var stream in s.Streams.Where(st => st.Wet))
            {
                var surface = stream.Points.Select(p => Mathf.Max(s.Sample(s.After, DemoPoint(p)) + TrickleDepth, WaterLevel + MouthLift)).ToArray();
                int end = Array.FindIndex(surface, v => v <= WaterLevel + MouthLift);
                if (end < 0) end = surface.Length - 1;
                if (end < 1) continue;
                var points = stream.Points.Take(end + 1).ToArray();
                // A point bar's gentle slope lets the water spread a little further on the inside of a bend.
                float banks = BankWidth * 1.5f, reach = stream.HalfWidth.Max() + banks;
                var min = points.Aggregate(Vector2.positiveInfinity, Vector2.Min) - Vector2.one * reach;
                var max = points.Aggregate(Vector2.negativeInfinity, Vector2.Max) + Vector2.one * reach;
                for (int z = Index(min.y, SiteInDemo.z, s.Origin.y); z <= Index(max.y, SiteInDemo.z, s.Origin.y); z++)
                for (int x = Index(min.x, SiteInDemo.x, s.Origin.x); x <= Index(max.x, SiteInDemo.x, s.Origin.x); x++)
                {
                    var p = s.Local(x, z);
                    for (int i = 0; i < points.Length - 1; i++)
                    {
                        var ab = points[i + 1] - points[i];
                        float u = Mathf.Clamp01(Vector2.Dot(p - points[i], ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                        float span = Mathf.Lerp(stream.HalfWidth[i], stream.HalfWidth[i + 1], u) + banks;
                        float distance = Vector2.Distance(points[i] + ab * u, p);
                        if (distance > span) continue;
                        // Nearer stretches (and streams, at the fork) weigh more; the blend keeps the
                        // surface continuous where the nearest stretch would jump.
                        float w = Smooth(1 - distance / span) + 1e-4f;
                        level[z, x] += w * Mathf.Lerp(surface[i], surface[i + 1], u);
                        weight[z, x] += w;
                    }
                }
            }
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++) level[z, x] = weight[z, x] > 0 ? level[z, x] / weight[z, x] : float.NaN;
            return level;
        }

        // Walkable water over the wet beds: terrain-sample cells wherever the ground lies below the
        // stream's surface, so the water always meets its banks at a natural waterline (cell edges
        // stay under the ground) and flows around rocks. With the lake's own material it runs out
        // over the mouth just above the lake plane and draws first, hiding the lake beneath: its
        // outer edge is then a step of a few millimetres between identical water.
        private static void BuildTrickles(Section s, Transform water)
        {
            int n = s.Samples;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var index = new Dictionary<int, int>();
            int Vertex(int x, int z)
            {
                int key = z * n + x;
                if (index.TryGetValue(key, out int existing)) return existing;
                var local = s.Local(x, z);
                // Dry corners, and corners at the end of the stream's reach on dry land, dip just under
                // the ground: the waterline is then the water meeting the ground, never a cell edge that
                // floats or shows where the rendered terrain simplifies. Over the lake the level holds.
                float level = s.StreamLevel[z, x];
                bool edge = x == 0 || z == 0 || x == n - 1 || z == n - 1;
                for (int dz = -1; dz <= 1 && !edge; dz++)
                for (int dx = -1; dx <= 1 && !edge; dx++) edge = float.IsNaN(s.StreamLevel[z + dz, x + dx]);
                bool open = Wet(x, z) && (!edge || s.After[z, x] < WaterLevel);
                float height = open ? level : Mathf.Min(level, s.After[z, x] - .06f);
                vertices.Add(new Vector3(local.x, height - SiteInDemo.y, local.y));
                uv.Add(s.Demo(x, z) / TileSize);
                index[key] = vertices.Count - 1;
                return vertices.Count - 1;
            }
            bool Wet(int x, int z) => s.After[z, x] < s.StreamLevel[z, x];
            for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                if (float.IsNaN(s.StreamLevel[z, x]) || float.IsNaN(s.StreamLevel[z, x + 1])
                    || float.IsNaN(s.StreamLevel[z + 1, x]) || float.IsNaN(s.StreamLevel[z + 1, x + 1])) continue;
                if (!Wet(x, z) && !Wet(x + 1, z) && !Wet(x, z + 1) && !Wet(x + 1, z + 1)) continue;
                int a = Vertex(x, z), b = Vertex(x + 1, z), c = Vertex(x, z + 1), d = Vertex(x + 1, z + 1);
                triangles.AddRange(new[] { a, c, d, a, d, b });
            }
            var mesh = new Mesh { name = "Trickles", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var trickles = new GameObject("Trickles").transform;
            trickles.SetParent(water, false);
            trickles.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, TrickleMeshPath);
            // ConfigureWater binds the project trickle material by name.
            trickles.gameObject.AddComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        private static DetailPrototype[] LakebedDetails(TerrainData canyon)
        {
            var mountain = AssetDatabase.LoadAssetAtPath<TerrainData>(MountainTerrainPath);
            if (mountain == null) throw new InvalidOperationException("Missing approved Mountains terrain for lakebed plants.");
            DetailPrototype From(string name, GameObject prefab = null, TerrainData source = null)
            {
                var p = (source ?? mountain).detailPrototypes.Single(d => d.prototype != null && d.prototype.name == name);
                return new DetailPrototype
                {
                    prototype = prefab != null ? prefab : p.prototype, usePrototypeMesh = true, renderMode = p.renderMode, useInstancing = p.useInstancing,
                    minWidth = p.minWidth, maxWidth = p.maxWidth, minHeight = Mathf.Min(p.minHeight, p.maxHeight), maxHeight = Mathf.Max(p.minHeight, p.maxHeight),
                    noiseSeed = p.noiseSeed, noiseSpread = p.noiseSpread, density = p.density, holeEdgePadding = p.holeEdgePadding,
                    healthyColor = p.healthyColor, dryColor = p.dryColor, alignToGround = p.alignToGround, positionJitter = p.positionJitter,
                    useDensityScaling = p.useDensityScaling
                };
            }
            GameObject Plant(TerrainData source, string name) => source.detailPrototypes.Single(d => d.prototype != null && d.prototype.name == name).prototype;
            var reedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPrefabs + "Plants/Reeds.prefab");
            if (reedPrefab == null) throw new InvalidOperationException("Missing approved Mountains reeds.");
            var reeds = From("GrassMountain2", DryVariant(reedPrefab, ReedMaterialPath, new Color(.72f, .74f, .6f), new Color(.62f, .66f, .52f)));
            reeds.minWidth = .8f; reeds.maxWidth = 1.3f; reeds.minHeight = .9f; reeds.maxHeight = 1.6f; reeds.noiseSpread = 2;
            // The vendor reeds barely move (wind 0.1); they sway with the grass, a little less as they are taller.
            var reedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ReedMaterialPath);
            reedMaterial.SetFloat("_WindMultiplier", .5f);
            EditorUtility.SetDirty(reedMaterial);
            // The canyon's own grass, sun-dried to muted olive on the exposed bed, and dusty rushes and reeds.
            var dryGrass = From("Grass_3", DryVariant(Plant(canyon, "Grass_3"), DryGrassMaterialPath, new Color(.4f, .44f, .25f), new Color(.32f, .37f, .2f)), canyon);
            var dryTall = From("Grass_4", DryVariant(Plant(canyon, "Grass_4"), DryGrassMaterialPath, new Color(.4f, .44f, .25f), new Color(.32f, .37f, .2f)), canyon);
            var rushes = From("GrassMountain2", DryVariant(Plant(mountain, "GrassMountain2"), RushMaterialPath, new Color(.6f, .64f, .46f), new Color(.5f, .55f, .38f)));
            // Knee-high tufts, not the canyon's tall meadow cards.
            dryTall.minWidth = .9f; dryTall.maxWidth = 1.5f; dryTall.minHeight = .6f; dryTall.maxHeight = 1.25f;
            dryGrass.minHeight = .5f; dryGrass.maxHeight = 1;
            // The canyon's grass in its meadow colours, for the patches around the plot.
            var green = From("Grass_3", MeadowVariant(Plant(canyon, "Grass_3")), canyon);
            green.minHeight = .45f; green.maxHeight = .9f; green.density = 3;
            // Order is the detail layer order after the demo's prototypes; see LakebedDetail.
            return new[] { reeds, rushes, From("GrassMountain4", MeadowVariant(Plant(mountain, "GrassMountain4"))), From("Pebble1"), From("Pebble2"), From("Pebble3"), From("Branchs"), dryGrass, dryTall, green };
        }

        private enum LakebedDetail { Reeds, Rushes, Feather, Pebble1, Pebble2, Pebble3, Twigs, DryGrass, DryTall, GreenGrass }

        // Coverage for the lakebed plant/pebble layers appended after the demo's detail layers.
        private static void DressDetails(Section s, TerrainData data, int scale, int first, Rect[] stations)
        {
            int cells = WindowCells / scale, count = Enum.GetValues(typeof(LakebedDetail)).Length;
            var maps = new int[count][,];
            for (int l = 0; l < count; l++) maps[l] = new int[cells, cells];
            for (int v = 0; v < cells; v++)
            for (int u = 0; u < cells; u++)
            {
                int x = u * scale, z = v * scale;
                var local = s.Local(x, z);
                float a = s.After[z, x] - WaterLevel, c = s.Channel[z, x], rill = s.Rill[z, x], bar = s.Bar[z, x];
                bool island = s.Drained[z, x] <= 0 && IslandHeight(local) > WaterLevel + .05f;
                bool region = s.Drained[z, x] > .3f || island || (s.Lake[z, x] && a > -.3f && a < 1.2f && local.magnitude < 120);
                if (!region || SiteLayout.BeyondOpening(local) < DressingClearance || stations.Any(r => r.Contains(local))) continue;
                float tuft = Noise(local, 3.2f, 21.7f);
                // Grasses keep their roots dry: nothing but reeds and pebbles stands in the water.
                float stream = s.StreamLevel[z, x];
                float dry = Smooth((a - .05f) / .1f) * (float.IsNaN(stream) ? 1 : Smooth((s.After[z, x] - stream - .03f) / .08f));
                // Plants follow water and time rather than an even sprinkle of look-alike clumps (user,
                // 2026-10-09): ground exposed longest (higher, toward the cliff) has grown over most, the
                // band the water left last is nearly bare, damp channel banks set back from the water and
                // rill margins are greenest, and gravel bars stay bare. Cover comes as broad patches of
                // clumps with sparse sprigs between them, soft-edged, so no two patches share a size.
                float age = Smooth((a - .7f) / .8f) * Mathf.Lerp(.45f, 1, Smooth((local.x + 20) / 45));
                float moist = Mathf.Max(Band(c, .6f, 3.6f, .7f) * (1 - .9f * bar), .7f * Band(rill, -.1f, 1.4f, .4f));
                float habitat = dry * Mathf.Max(Mathf.Max(moist, .6f * age), Mathf.Max(island ? 1 : 0, .35f * Band(a, .2f, .9f)));
                float patch = Smooth((Noise(local, 9, 17.3f) - .36f) / .3f), clumps = Smooth((Noise(local, 2.6f, 23.1f) - .4f) / .22f);
                float sprigs = .3f * Smooth((Noise(local, 1.2f, 5.3f) - .5f) / .3f) * Smooth((Noise(local, 13, 2.9f) - .45f) / .3f);
                maps[(int)LakebedDetail.DryTall][v, u] = Mathf.RoundToInt(160 * Frayed(habitat * patch * clumps, local, 23.9f) * clumps);
                // Shorter dry grass rings the clumps and scatters thinly in sprigs between them.
                maps[(int)LakebedDetail.DryGrass][v, u] = Mathf.RoundToInt(130 * Mathf.Max(Frayed(habitat * patch * clumps, local, 31.3f), habitat * sprigs));
                maps[(int)LakebedDetail.GreenGrass][v, u] = Mathf.RoundToInt(255 * GreenDensity(local) * Frayed(GreenShape(local, a, c, .24f) * Smooth((tuft - .25f) / .12f), local, 7.7f));
                float clump2 = Noise(local, 6, 4.4f), scatter = Noise(local, 2.3f, 9.7f);
                float shore = Band(a, -.2f, .25f), bank = Band(c, -.25f, .8f, .25f) * (1 - bar), damp = Band(c, .3f, 3.2f, .4f);
                // Reeds stand in a few colonies along the water's edge, long rather than round, dense in
                // their cores and thinning to single stems; never on the bare bars. Rushes grow as tussock
                // colonies on damp ground only (channel banks, rills, the lowest shore), and the weeds in
                // loose groups on the drier flats: single plants spaced evenly across open ground read as
                // planted (user, 2026-10-09).
                float colony = Smooth((Noise(local, 7, 11.3f) - .52f) / .14f) * Smooth((Noise(local, 2.2f, 3.1f) - .2f) / .45f);
                maps[(int)LakebedDetail.Reeds][v, u] = Mathf.RoundToInt(210 * Mathf.Max(shore, bank) * colony);
                float wetGround = Mathf.Max(Mathf.Max(damp * (1 - bar), Band(a, .15f, .5f)), Band(rill, -.3f, .8f, .3f));
                maps[(int)LakebedDetail.Rushes][v, u] = Mathf.RoundToInt(150 * Frayed(dry * wetGround * Smooth((Noise(local, 5, 4.4f) - .5f) / .15f), local, 13.9f));
                maps[(int)LakebedDetail.Feather][v, u] = Mathf.RoundToInt(70 * dry * Band(a, .45f, 1.3f) * Smooth((c - 2) / 1)
                    * Smooth((Noise(local, 8, 6.6f) - .55f) / .12f) * Smooth((clump2 - .5f) / .2f));
                // Pebbles lie where water sorted them: channel beds, the bars, rill floors and broken strand
                // lines along old waterlines, and only a stray few on the flats.
                float strand = Smooth((Noise(local, 6, 8.8f) - .4f) / .2f);
                float pebbles = Mathf.Max(Mathf.Max(Smooth((.2f - c) / .4f) * (.5f + .5f * scatter), bar * (.4f + .6f * scatter)),
                    Mathf.Max(Band(a, -.15f, .55f) * strand * (.3f + .7f * scatter), Mathf.Max(.6f * Smooth((-.1f - rill) / .3f) * scatter, .3f * Smooth((scatter - .8f) / .06f))));
                int kind = (int)LakebedDetail.Pebble1 + Mathf.FloorToInt(Noise(local, 1.7f, 5.5f) * 2.999f);
                maps[kind][v, u] = Mathf.RoundToInt(130 * pebbles);
                // Drift twigs collect on the bars and along the old waterlines.
                float drift = Band(a, .2f, .55f) * strand + .8f * bar * Smooth((.9f - a) / .4f);
                maps[(int)LakebedDetail.Twigs][v, u] = Mathf.RoundToInt(80 * Mathf.Clamp01(drift) * Smooth((Noise(local, 5, 2.2f) - .62f) / .1f));
            }
            for (int l = 0; l < count; l++) data.SetDetailLayer(0, 0, first + l, maps[l]);
        }

        // Stranded stones where the water left them: a lag of cobbles in the channel beds and against
        // the cut banks of bends, a few on the bars and along broken old waterlines, and only a rare lone
        // stone out on the flats; the odd slumped heap of rubble at a cut bank or the old shore. Stones
        // lie on their broad side well bedded in the mud, mostly small with the odd larger one, and wear
        // the silt-coated bare rock. Pale stones standing on end in evenly spaced clusters read as
        // scattered props (user, 2026-10-09). Only larger stones keep colliders.
        private static void ScatterDebris(Section s, Transform environment, Rect[] stations)
        {
            var parent = new GameObject("Lakebed debris").transform;
            parent.SetParent(environment, false);
            var random = new System.Random(1789);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            bool Allowed(Vector2 p) => SiteLayout.BeyondOpening(p) > DressingClearance + 1 && s.Sample(s.Drained, DemoPoint(p)) > .5f
                && !stations.Any(r => r.Contains(p));
            void Put(string prefabName, Vector2 p, float scale, Quaternion rotation, float embed, bool stranded = true, float flatten = 1)
            {
                // flatten scales the local x axis, which Settled turns upright.
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VendorPrefabs + prefabName + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing Highlands prefab " + prefabName + ".");
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                WithoutStaticBatching(item);
                item.transform.SetPositionAndRotation(new Vector3(p.x, 0, p.y), rotation);
                item.transform.localScale = new Vector3(scale * flatten, scale, scale);
                var bounds = WorldBounds(item.transform);
                // Sit on the lowest ground under the footprint so no edge floats over a bank or bed;
                // clusters spread across uneven ground are left out.
                var (low, high) = Footprint(bounds, xz => s.Sample(s.After, DemoPoint(xz)) - SiteInDemo.y);
                if (high - low > (prefabName.StartsWith("Rubble") ? .2f : Mathf.Max(.25f, bounds.size.y * .5f)))
                {
                    UnityEngine.Object.DestroyImmediate(item);
                    return;
                }
                item.transform.position += Vector3.up * (low - bounds.min.y - embed * bounds.size.y);
                // The player walks over small stones instead of snagging on them.
                if (bounds.size.magnitude < 1.6f) foreach (var collider in item.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                if (stranded) Bare(item);
                else foreach (var renderer in item.GetComponentsInChildren<Renderer>()) UseMeadowRock(renderer);
            }
            Quaternion Yaw() => Quaternion.Euler(0, Range(0, 360), 0);
            // Water-worn stones settle on their broad side, a little tilted, never on end: the pack's rocks
            // are modelled standing, their narrowest axis local x, so that axis is turned upright.
            Quaternion Settled() => Quaternion.Euler(Range(-7, 7), Range(0, 360), Range(-7, 7)) * Quaternion.Euler(0, 0, 90 + Range(-12, 12));
            // Mostly small, the odd larger one.
            float Size(float min, float max) => Mathf.Lerp(min, max, Mathf.Pow((float)random.NextDouble(), 2.2f));
            void Stones(Vector2 centre, int count, float spread, float minScale, float maxScale)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = centre + new Vector2(Range(-spread, spread), Range(-spread, spread));
                    if (Allowed(p)) Put("Rocks/Rock_" + random.Next(4), p, Size(minScale, maxScale), Settled(), Range(.4f, .55f), true, Range(.7f, .95f));
                }
            }
            foreach (var stream in s.Streams)
            {
                var points = stream.Points;
                // Wet channels carry the most stone, gullies a little, rills a stray pebble.
                int gapMin = stream.Wet ? 5 : stream.Flow == Flow.Gully ? 10 : 18, gapMax = gapMin * 2 + 4;
                for (int i = random.Next(4, 10); i < points.Length - 1; i += random.Next(gapMin, gapMax))
                {
                    if (random.NextDouble() < .3) continue;
                    var tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                    var left = new Vector2(-tangent.y, tangent.x);
                    float half = stream.HalfWidth[i], turn = stream.Turn[i];
                    if (stream.Flow == Flow.Rill)
                    {
                        Stones(points[i] + left * Range(-half, half) * .5f, 1, .2f, .08f, .2f);
                        continue;
                    }
                    if (Mathf.Abs(turn) > .3f)
                    {
                        // Against the cut bank outside a bend, where the current drops its heaviest stones;
                        // now and then a heap where the bank slumped.
                        var outside = left * -Mathf.Sign(turn);
                        if (stream.Wet && Mathf.Abs(turn) > .7f && random.NextDouble() < .2)
                        {
                            var heap = points[i] + outside * (half + Range(.3f, .9f));
                            if (Allowed(heap)) Put("Rubble/RubbleSparse_" + (1 + random.Next(3)), heap, Range(.5f, .8f), Yaw(), .25f);
                        }
                        else Stones(points[i] + outside * half * Range(.5f, 1.05f), random.Next(1, 4), .5f, .1f, stream.Wet ? .4f : .25f);
                        // A few on the bar opposite.
                        if (stream.Wet && random.NextDouble() < .45)
                            Stones(points[i] - outside * (half + Range(0, 1.2f)), random.Next(1, 3), .7f, .07f, .2f);
                    }
                    else Stones(points[i] + left * half * Range(-.8f, .8f), random.Next(1, 3), .45f, .08f, stream.Wet ? .3f : .2f);
                }
            }
            // The retreating lake left stones along broken strand lines at its old waterlines; a rare
            // larger stone lies alone out on the flats.
            int shoreline = 0, flats = 0;
            for (int attempt = 0; attempt < 6000 && (shoreline < 14 || flats < 6); attempt++)
            {
                var p = new Vector2(Range(DrainedCentre.x - DrainedRadii.x, DrainedCentre.x + DrainedRadii.x), Range(DrainedCentre.y - DrainedRadii.y, DrainedCentre.y + DrainedRadii.y));
                if (!Allowed(p)) continue;
                float a = s.Sample(s.After, DemoPoint(p)) - WaterLevel;
                if (shoreline < 14 && a > .15f && a < .7f && Noise(p, 6, 8.8f) > .5f)
                {
                    shoreline++;
                    if (random.NextDouble() < .2) Put("Rubble/Rubble" + (random.NextDouble() < .6 ? "Sparse_" : "Dense_") + (1 + random.Next(3)), p, Range(.5f, .85f), Yaw(), .2f);
                    else Stones(p, random.Next(2, 6), 1.6f, .08f, .3f);
                }
                else if (flats < 6 && a > .9f && random.NextDouble() < .15)
                {
                    flats++;
                    Stones(p, 1, 0, .3f, .6f);
                }
            }
            // Grass-topped boulders crown the larger islands, as on the old lake's shoals.
            foreach (var (centre, radius) in Islands.Where(i => i.radius >= 6))
                Put("Rocks/Rock_" + random.Next(4), centre + new Vector2(Range(-1.5f, 1.5f), Range(-1.5f, 1.5f)), Range(1.1f, 1.5f),
                    Quaternion.Euler(Range(78, 96), Range(0, 360), Range(-10, 10)), .35f, false);
        }

        public const string DryGrassMaterialPath = Folder + "/DryGrass.mat";
        public const string RushMaterialPath = Folder + "/DryRushes.mat";
        public const string ReedMaterialPath = Folder + "/DryReeds.mat";

        // The canyon's living plants in warm, muted meadow colours: the packs paint them a vivid cool
        // green that read bluish against the warm mud (user, 2026-10-02). One project copy per pack
        // material, so each keeps its own texture.
        private static readonly Dictionary<string, string> MeadowMaterials = new Dictionary<string, string>
        {
            { "_Grass", Folder + "/MeadowGrass.mat" }, { "SwirlyFern", Folder + "/MeadowFern.mat" },
            { "SwirlyShrub", Folder + "/MeadowShrub.mat" }, { "GrassMountainShrub", Folder + "/MeadowFeather.mat" },
        };
        public static readonly Color MeadowLight = new Color(.5f, .48f, .28f), MeadowDark = new Color(.4f, .39f, .22f);

        private static GameObject MeadowVariant(GameObject source)
        {
            string material = source.GetComponentInChildren<Renderer>().sharedMaterial.name;
            return MeadowMaterials.TryGetValue(material, out string path) ? DryVariant(source, path, MeadowLight, MeadowDark, "Meadow") : source;
        }

        // The canyon's own detail prototypes with its green plants swapped for their meadow variants, no
        // taller than knee to hip height: the pack's 3.5x meadow cards swayed far more than everything
        // around them, since its wind grows with a plant's height (user, 2026-10-02).
        private const float MeadowMinHeight = .6f, MeadowMaxHeight = 1.2f;

        private static DetailPrototype[] CanyonDetails(DetailPrototype[] source) => source.Select(p =>
        {
            if (p.prototype == null) return p;
            var variant = MeadowVariant(p.prototype);
            if (variant == p.prototype) return p;
            return new DetailPrototype(p)
            {
                prototype = variant, minHeight = Mathf.Min(p.minHeight, MeadowMinHeight), maxHeight = Mathf.Min(p.maxHeight, MeadowMaxHeight)
            };
        }).ToArray();

        // Project prefab variant of a pack plant drawn with a recoloured copy of its material (muted and
        // sun-dried, or meadow). Terrain details ignore prototype colours with the pack's grass shader.
        // An existing material keeps its Inspector tuning.
        private static GameObject DryVariant(GameObject source, string materialPath, Color light, Color dark, string suffix = "Dry")
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(source.GetComponentInChildren<Renderer>().sharedMaterial)
                    { name = System.IO.Path.GetFileNameWithoutExtension(materialPath) };
                material.SetColor("_Color01", light);
                material.SetColor("_Color02", dark);
                // Some pack grass is fully glossy, which glares lime under the noon sun.
                material.SetFloat("_Smoothness", .1f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string path = TreesFolder + "/" + source.name + " " + suffix + ".prefab";
            if (!AssetDatabase.IsValidFolder(TreesFolder)) AssetDatabase.CreateFolder(Folder, "Trees");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        // Swaps the pack rock material for the bare lakebed copy on every renderer of an object.
        private static void Bare(GameObject item)
        {
            var vendor = AssetDatabase.LoadAssetAtPath<Material>(VendorRockMaterial);
            var bare = BareRockMaterial();
            foreach (var renderer in item.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                if (!materials.Contains(vendor)) continue;
                renderer.sharedMaterials = materials.Select(m => m == vendor ? bare : m).ToArray();
            }
        }

        // Rock that sat in the lake is a shade darker than the sunlit cliffs, a neutral tint (a warm one
        // turned salmon under the grade), and grows nothing: the pack's grass layer still showed lime
        // blotches on flat faces at its weakest setting, so its opacity is zero. Pale bare stones on the
        // mud read as props (user, 2026-10-09). Rejected: a top-down coat of the canyon mud as dried silt,
        // which read as rust-red or burnt patches at every tint tried.
        public static readonly Color LakebedRockTint = new Color(.7f, .7f, .71f, 1);

        // Stranded stones and boulders on the old bed read as bare, water-darkened lakebed rock.
        private static Material BareRockMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BareRockMaterialPath);
            if (material == null)
            {
                var vendor = AssetDatabase.LoadAssetAtPath<Material>(VendorRockMaterial);
                if (vendor == null) throw new InvalidOperationException("Missing approved Highlands rock material.");
                material = new Material(vendor) { name = "LakebedRock" };
                material.SetFloat("_LayerPower", 0);
                AssetDatabase.CreateAsset(material, BareRockMaterialPath);
            }
            material.SetColor("_Color", LakebedRockTint);
            material.SetColor("_2ndColor", new Color(1, 1, 1, 0));
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
