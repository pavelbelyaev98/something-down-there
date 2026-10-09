using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere.Editor
{
    // The exposed lakebed floor: two seep-fed creeks running down the shelf into the lake, packed
    // sediment on the flats, damp silt, pebble strands and sandy banks toward the water, dry grass,
    // rushes and reeds where it is damp, and stranded stones.
    public static partial class LakebedSiteSetup
    {
        public const string BareRockMaterialPath = Folder + "/LakebedRock.mat";
        private const string MountainTerrainPath = "Assets/BK/PureNature_Mountains/Models/TerrainMountain.asset";
        private const string MountainPrefabs = "Assets/BK/PureNature_Mountains/Prefabs/";
        private const string VendorRockMaterial = "Assets/BK/PureNature_Highlands/Models/Rocks/Rock/Textures/Materials/Rock.mat";
        private const float TrickleDepth = .15f, BankWidth = 1.1f, MouthLift = .005f, MaxBedGrade = .08f;
        // A bend this tight (metres of radius) or tighter has the full point bar inside it and cut bank outside.
        private const float BendRadius = 7f;
        // Each creek runs in a gentle valley this wide (metres either side of its axis) and deep, so the ground
        // falls toward it from both sides: a channel cut into level ground reads as a dug trench or a path.
        private const float ValleyHalfWidth = 9f, ValleyDepth = .3f;
        // Kinoshita meanders: wavelength in channel widths (about eleven in nature, Leopold and Wolman 1960),
        // the sinuosity that turns it into path length, the third harmonics that skew bends downstream and
        // fatten them, and how strongly (per metre) the path is pulled back toward its valley's axis.
        private const float WavelengthWidths = 11, Sinuosity = 1.25f, BendSkew = .08f, BendFlat = .03f, AxisPull = .1f;
        // Metres a creek keeps between its bank and a boulder.
        private const float RockClearance = 1.5f;
        // A creek rises in a seep, not a pool: water wells up from under stones at the slope's toe and
        // gathers into a trickle that widens and cuts in over its first HeadLength metres, from HeadWidth
        // and HeadDepth. A round pond with a ring of reeds and a dug wall behind it read as an ornamental
        // pond (user, 2026-10-09). Near the head the bed may follow the steeper toe of the slope (HeadGrade
        // per metre) instead of being cut into it as a trench.
        private const float HeadLength = 12, HeadWidth = .9f, HeadDepth = .08f, HeadGrade = .3f;
        // The wet ground round a seep: half-width at its head, how far it reaches back up the slope and
        // how far down the creek it narrows to the creek's own margin.
        private const float SeepWidth = 3.2f, SeepBack = 2.5f, SeepLength = 14;
        // How much deeper the pool in a bend's apex lies than the riffle at the bend's crossing.
        private const float PoolDip = .12f;
        // How far under the lake's surface a creek's mouth bar lies, and the near-level grade of the
        // backwater over the last Backwater metres before it.
        private const float MouthBar = .25f, BackwaterGrade = .015f, Backwater = 7;
        // The gentlest banks, a multiple of BankWidth.
        private const float MaxSoft = 2.6f;
        // Metres beyond the plot outline: channels leave a walkable margin around it.
        public const float ChannelClearance = 6f;
        public const string TrickleMeshPath = Folder + "/Trickles.asset";

        // A creek, or one of its mouths, laid along a valley axis (site-local, upstream first, ending in the lake).
        private sealed class Course
        {
            public Vector2[] Valley;
            public float Seed;
            // Channel width near the seep and lower down (metres).
            public float Width0, Width1;
            // Largest angle (radians) a bend turns away from the valley's axis.
            public float Swing;
            // A mouth leaves the course at this index ForkBack metres before that course's end; -1 for a creek's own seep.
            public int Fork = -1;
            public float ForkBack;
        }

        // Two creeks rise in seeps at the cliff foot and run down the shelf to the nearest shore, where
        // each splits into two mouths (user, 2026-10-09: streams that began in open ground, ran beside the
        // shore or round the plot, and kept one width read as artificial). The north creek rises under the
        // boulder pile where the north-east plateau's drainage reaches the shelf (the terrain's flow lines
        // converge there), runs west below the boulders south of the bay and turns down into it; the smaller
        // south creek rises among the rocks at the south-east cliff foot and runs south-west to the south shore.
        private static readonly Course[] Courses =
        {
            new Course
            {
                Valley = new[] { new Vector2(29.5f, 31.5f), new Vector2(22, 31), new Vector2(13, 30.2f), new Vector2(4, 30.5f), new Vector2(-3, 34),
                    new Vector2(-9, 40), new Vector2(-12.5f, 47), new Vector2(-14, 53), new Vector2(-15, 58.5f), new Vector2(-15.5f, 63) },
                Seed = 2.9f, Width0 = 1.3f, Width1 = 2.3f, Swing = .95f,
            },
            new Course { Valley = new[] { new Vector2(-9.5f, 55), new Vector2(-7, 59), new Vector2(-6, 63) }, Seed = 3.3f, Width0 = 1.5f, Width1 = 1.6f, Swing = .3f, Fork = 0, ForkBack = 10 },
            new Course
            {
                Valley = new[] { new Vector2(13.5f, -40), new Vector2(8, -43.5f), new Vector2(1.5f, -48), new Vector2(-4, -54), new Vector2(-8, -60.5f),
                    new Vector2(-11, -66), new Vector2(-12.5f, -71.5f), new Vector2(-13.5f, -76) },
                Seed = 9.9f, Width0 = 1.2f, Width1 = 1.9f, Swing = .8f,
            },
            new Course { Valley = new[] { new Vector2(-15, -67.5f), new Vector2(-19, -70.5f), new Vector2(-22, -73.5f) }, Seed = 7.1f, Width0 = 1.3f, Width1 = 1.4f, Swing = .3f, Fork = 2, ForkBack = 9 },
        };

        private sealed class Stream
        {
            public Vector2[] Points;  // site-local centreline, upstream first
            public float[] Along;     // metres along it from its start
            public float[] Bed;       // demo-space bed height under the centreline, never rising downstream
            public float[] Surface;   // demo-space water surface, never rising downstream
            public float[] HalfWidth; // bed half-width
            public float[] Turn;      // signed bend (+ left), 1 at BendRadius or tighter
            public float[] Pool;      // 1 in the pool at a bend's apex, 0 on the riffle at its crossing
            public float[] Soft;      // how gently the banks rise, a multiple of BankWidth
            public bool Spring;       // rises in a seep rather than branching from another creek
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

        // How wet the ground round a creek's seep is, 0-1: a patch widest at the head, reaching a little
        // back up the slope behind it and narrowing down the creek's first metres, its edge lobed so it is
        // never a circle. The ground there stays wet and grown over.
        private static float Seep(Section s, Vector2 local)
        {
            float wet = 0;
            foreach (var stream in s.Streams)
            {
                if (!stream.Spring) continue;
                var head = stream.Points[0];
                var offset = local - head;
                if (offset.sqrMagnitude > (SeepLength + 6) * (SeepLength + 6)) continue;
                // The creek's first metres barely bend, so its start sets the patch's axis.
                var down = (stream.Points[Mathf.Min(stream.Points.Length - 1, Mathf.RoundToInt(HeadLength / .25f))] - head).normalized;
                float u = Vector2.Dot(offset, down), v = Mathf.Abs(down.x * offset.y - down.y * offset.x);
                float reach = u >= 0 ? Mathf.Lerp(SeepWidth, .8f, Smooth(u / SeepLength))
                    : SeepWidth * Mathf.Sqrt(Mathf.Max(0, 1 - u * u / (SeepBack * SeepBack)));
                reach *= .7f + .6f * Noise(local, 2.5f, 31.7f);
                wet = Mathf.Max(wet, (1 - Smooth((v - reach) / 1.5f)) * (1 - Smooth((u - SeepLength) / 3)) * Smooth((u + SeepBack + 1.5f) / 1.5f));
            }
            return wet;
        }

        // Slope of the ground at a terrain sample, rise over run.
        private static float Slope(Section s, int x, int z)
        {
            int n = s.Samples - 1;
            float dx = s.After[z, Mathf.Min(x + 1, n)] - s.After[z, Mathf.Max(x - 1, 0)];
            float dz = s.After[Mathf.Min(z + 1, n), x] - s.After[Mathf.Max(z - 1, 0), x];
            return Mathf.Sqrt(dx * dx + dz * dz) / (2 * s.Cell);
        }

        private static Vector3 DemoPoint(Vector2 local) => new Vector3(local.x + SiteInDemo.x, 0, local.y + SiteInDemo.z);

        private static void CarveChannels(Section s)
        {
            int n = s.Samples;
            s.Channel = new float[n, n];
            s.Bar = new float[n, n];
            s.Valley = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) s.Channel[z, x] = float.MaxValue;
            // Paths first, each mouth from its creek's path; then the valleys, so the creek beds are cut
            // below the valley floors the water runs in.
            var paths = new List<(Vector2[] points, float[] phase)>();
            foreach (var course in Courses)
            {
                var valley = course.Fork < 0 ? course.Valley
                    : new[] { PointBack(paths[course.Fork].points, course.ForkBack) }.Concat(course.Valley).ToArray();
                paths.Add(Meander(valley, course, s.Rocks));
                if (course.Fork < 0) CarveValley(s, Densify(valley, .5f));
            }
            s.Uncarved = (float[,])s.After.Clone();
            for (int c = 0; c < Courses.Length; c++)
            {
                var course = Courses[c];
                var (points, phase) = paths[c];
                int count = points.Length;
                var stream = new Stream
                {
                    Points = points, Along = new float[count], Bed = new float[count], Surface = new float[count], HalfWidth = new float[count],
                    Pool = new float[count], Soft = new float[count], Turn = Turns(points), Spring = course.Fork < 0,
                };
                for (int i = 1; i < count; i++) stream.Along[i] = stream.Along[i - 1] + Vector2.Distance(points[i], points[i - 1]);
                float total = stream.Along[count - 1];
                // A mouth starts at its creek's bed and water level where it branches off.
                var parent = course.Fork < 0 ? null : s.Streams[course.Fork];
                int fork = parent == null ? -1 : Nearest(parent.Points, points[0]);
                float lowest = parent == null ? float.MaxValue : parent.Bed[fork];
                bool reachedLake = false;
                var water = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float along = stream.Along[i], t = along / total;
                    float width = Mathf.Lerp(course.Width0, course.Width1, Smooth(t / .6f));
                    // Pools in the bends run a little narrower and deeper, riffles at the crossings wider and
                    // shallower, once the creek has gathered below its seep.
                    float pool = Mathf.Pow(Mathf.Sin(phase[i]), 2) * Smooth(along / (stream.Spring ? HeadLength : 6)) * Smooth((total - along) / 6);
                    float half = width * .5f * Mathf.Lerp(1.12f, .9f, pool);
                    // How deep the creek has cut in and how steep its banks are wander along it: here a low
                    // swale with banks sloping gently into the water, there a short steep bank. One even
                    // bank height read as a dug ditch (user, 2026-10-09).
                    float depth = Mathf.Lerp(.25f, .4f, Smooth(t / .4f)) * Mathf.Lerp(.6f, 1.3f, Mathf.PerlinNoise(along / 11, course.Seed * 1.9f));
                    float soft = Mathf.Lerp(.8f, MaxSoft, Mathf.Pow(Mathf.PerlinNoise(along / 9, course.Seed * 2.7f + 5), 1.4f));
                    if (stream.Spring)
                    {
                        // From the seep the trickle widens and cuts in, its banks gentle at first.
                        float grow = Smooth(along / HeadLength);
                        half = Mathf.Lerp(HeadWidth * .5f, half, grow);
                        depth = Mathf.Lerp(HeadDepth, depth, grow);
                        soft = Mathf.Lerp(MaxSoft, soft, grow);
                    }
                    stream.Soft[i] = soft;
                    // Shallow water: a trickle a few centimetres deep at the seep, well inside its channel, the
                    // creek's own depth lower down.
                    water[i] = Mathf.Min(TrickleDepth, .45f * depth);
                    // The bed is cut below the lowest ground across the whole section, banks included, so
                    // on a side slope both banks still rise above the water instead of it spilling downhill.
                    float ground = SectionFloor(s, s.Uncarved, points, i, half + BankWidth * stream.Soft[i]);
                    // Beds stay just above the lake plane until the course reaches open lake water, so no
                    // inland stretch dips into a pool the lake would show through; in the lake the bed is a
                    // shallow mouth bar just under the water.
                    reachedLake |= s.Sample(s.Uncarved, DemoPoint(points[i])) < WaterLevel - .05f;
                    lowest = Mathf.Max(Mathf.Min(lowest, ground - depth), reachedLake ? WaterLevel - MouthBar : WaterLevel + .03f);
                    stream.Bed[i] = lowest;
                    stream.HalfWidth[i] = half;
                    stream.Pool[i] = pool;
                }
                // A bed falls at most a gentle grade per metre (a sudden drop leaves a straight ledge across
                // the channel that the water outlines as a polygon), and a steeper fall is taken by cutting
                // the bed down upstream of it, never by raising it below: where the old shore falls to the
                // lake, the creek has cut into it, as a drained lake's creeks incise toward its new shore,
                // and reaches the lake at the lake's level instead of standing above the shore and spilling
                // over its bank.
                // Over its last metres the lake backs up into the channel: the bed runs nearly level just
                // under the lake's surface, so the creek meets the lake at its level while the beach beside
                // it still rises above the water.
                int mouth = Array.FindIndex(stream.Bed, b => b <= WaterLevel - MouthBar + 1e-4f);
                for (int i = count - 2; i >= 0; i--)
                {
                    float fromMouth = mouth < 0 ? float.MaxValue : stream.Along[mouth] - stream.Along[i];
                    float grade = Mathf.Lerp(BackwaterGrade, MaxBedGrade, Smooth(fromMouth / Backwater));
                    if (stream.Spring) grade = Mathf.Max(grade, Mathf.Lerp(HeadGrade, MaxBedGrade, Smooth(stream.Along[i] / HeadLength)));
                    stream.Bed[i] = Mathf.Min(stream.Bed[i], stream.Bed[i + 1] + grade * Vector2.Distance(points[i], points[i + 1]));
                }
                for (int i = 0; i < count; i++) stream.Surface[i] = Mathf.Max(stream.Bed[i] + water[i], WaterLevel + MouthLift);
                if (parent != null) stream.Surface[0] = parent.Surface[fork];
                Carve(s, stream);
                s.Streams.Add(stream);
            }
            s.StreamLevel = StreamLevels(s);
        }

        // Catmull-Rom through control points at about the given spacing.
        private static Vector2[] Densify(Vector2[] controls, float spacing)
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
            return dense.ToArray();
        }

        // The point a given distance back from a path's end.
        private static Vector2 PointBack(Vector2[] path, float back)
        {
            float left = back;
            for (int i = path.Length - 1; i > 0; i--)
            {
                float step = Vector2.Distance(path[i], path[i - 1]);
                if (step >= left) return Vector2.Lerp(path[i], path[i - 1], left / step);
                left -= step;
            }
            return path[0];
        }

        private static int Nearest(Vector2[] points, Vector2 p)
        {
            int best = 0;
            for (int i = 1; i < points.Length; i++) if ((points[i] - p).sqrMagnitude < (points[best] - p).sqrMagnitude) best = i;
            return best;
        }

        // A creek's path down its valley: a Kinoshita curve, the standard model of natural meanders, whose
        // direction swings with the bend's phase, with third harmonics that skew each bend downstream and
        // fatten its apex, rather than a sine wave laid sideways. The wavelength (eleven channel widths)
        // and the swing wander along the course, so straighter runs and tighter bends take turns, and a
        // pull back toward the axis keeps the meander belt in its valley. Bends fade out at both ends, so
        // the seep, the fork and the mouth stay where they were laid out. A boulder ahead turns the creek
        // aside, as real ones bend round rocks. Returns the path's points and each point's bend phase (sin of
        // it is 1 at a bend's apex).
        private static (Vector2[] points, float[] phase) Meander(Vector2[] valley, Course course, List<(Vector2 centre, float radius)> rocks)
        {
            var axis = Densify(valley, .25f);
            var axisAlong = new float[axis.Length];
            for (int i = 1; i < axis.Length; i++) axisAlong[i] = axisAlong[i - 1] + Vector2.Distance(axis[i], axis[i - 1]);
            float length = axisAlong[axis.Length - 1];
            int segment = 1;
            (Vector2 point, Vector2 normal) AxisAt(float u)
            {
                u = Mathf.Clamp(u, 0, length);
                while (segment < axis.Length - 1 && axisAlong[segment] < u) segment++;
                while (segment > 1 && axisAlong[segment - 1] > u) segment--;
                var a = axis[segment - 1]; var b = axis[segment];
                float t = (u - axisAlong[segment - 1]) / Mathf.Max(axisAlong[segment] - axisAlong[segment - 1], 1e-5f);
                var d = (b - a).normalized;
                return (Vector2.Lerp(a, b, t), new Vector2(-d.y, d.x));
            }
            const float step = .25f;
            var points = new List<Vector2>();
            var phases = new List<float>();
            float along = 0, lateral = 0, phase = course.Seed * 2;
            while (along < length)
            {
                float t = along / length;
                float width = Mathf.Lerp(course.Width0, course.Width1, Smooth(t / .6f));
                float wavelength = WavelengthWidths * width * Mathf.Lerp(.8f, 1.25f, Mathf.PerlinNoise(along / 21, course.Seed)) * Sinuosity;
                float swing = course.Swing * Mathf.Lerp(.55f, 1.3f, Mathf.PerlinNoise(along / 17, course.Seed + 7.3f))
                    * Smooth(along / 7) * Smooth((length - along) / 7);
                float heading = swing * (Mathf.Sin(phase) + swing * swing * (BendSkew * Mathf.Cos(3 * phase) - BendFlat * Mathf.Sin(3 * phase)))
                    - AxisPull * lateral;
                var (point, normal) = AxisAt(along);
                var here = point + normal * lateral;
                var forward = new Vector2(normal.y, -normal.x) * Mathf.Cos(heading) + normal * Mathf.Sin(heading);
                foreach (var (centre, radius) in rocks)
                {
                    var toRock = centre - here;
                    float need = radius + width * .5f + RockClearance, distance = toRock.magnitude;
                    if (distance > need + 3 || Vector2.Dot(toRock, forward) < -1) continue;
                    // Steer away from the side the rock lies on, harder the closer it is.
                    float side = forward.x * toRock.y - forward.y * toRock.x >= 0 ? 1 : -1;
                    heading -= side * .9f * Smooth((need + 3 - distance) / 3);
                }
                // The path settles back onto the axis over its last metres, so it ends where it was laid out.
                points.Add(point + normal * lateral * Smooth((length - along) / 4));
                phases.Add(phase);
                along += Mathf.Cos(heading) * step;
                lateral += Mathf.Sin(heading) * step;
                phase += 2 * Mathf.PI * step / wavelength;
            }
            points.Add(axis[axis.Length - 1]);
            phases.Add(phase);
            return (points.ToArray(), phases.ToArray());
        }

        // Lowers a gentle valley round a creek's axis: deepest along it, rising smoothly to the shelf
        // ValleyHalfWidth either side. Like the channels it keeps clear of the plot, the collar and the
        // boulders. Section.Valley keeps how deep in it each sample lies (0-1), for damper ground and grass.
        private static void CarveValley(Section s, Vector2[] axis)
        {
            var min = axis.Aggregate(Vector2.positiveInfinity, Vector2.Min) - Vector2.one * ValleyHalfWidth;
            var max = axis.Aggregate(Vector2.negativeInfinity, Vector2.Max) + Vector2.one * ValleyHalfWidth;
            int Index(float local, float site, float origin) => Mathf.Clamp(Mathf.RoundToInt((local + site - origin) / s.Cell), 0, s.Samples - 1);
            for (int z = Index(min.y, SiteInDemo.z, s.Origin.y); z <= Index(max.y, SiteInDemo.z, s.Origin.y); z++)
            for (int x = Index(min.x, SiteInDemo.x, s.Origin.x); x <= Index(max.x, SiteInDemo.x, s.Origin.x); x++)
            {
                var p = s.Local(x, z);
                float distance = float.MaxValue;
                for (int i = 0; i < axis.Length - 1; i++)
                {
                    var ab = axis[i + 1] - axis[i];
                    float u = Mathf.Clamp01(Vector2.Dot(p - axis[i], ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    distance = Mathf.Min(distance, Vector2.Distance(axis[i] + ab * u, p));
                }
                if (distance >= ValleyHalfWidth) continue;
                float keep = Mathf.Min(SiteLayout.BeyondOpening(p) - ChannelClearance - 3, BeyondCollar(p) - 3);
                foreach (var (centre, radius) in s.Rocks) keep = Mathf.Min(keep, Vector2.Distance(p, centre) - radius - 1);
                if (keep <= 0) continue;
                float depth = (.5f + .5f * Mathf.Cos(Mathf.PI * distance / ValleyHalfWidth)) * Smooth(keep / 4);
                s.After[z, x] -= ValleyDepth * depth;
                s.Valley[z, x] = Mathf.Max(s.Valley[z, x], depth);
            }
        }

        // Signed bend of a centreline at each point (+ turning left), 1 at BendRadius or tighter,
        // measured over a few metres so small kinks do not count.
        private static float[] Turns(Vector2[] points)
        {
            const int reach = 8;
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

        // Bends are lopsided, as in a real channel: the deepest line swings to the outside, where the bank
        // is short and steep (the cut bank), while the inside shelves gently out of the water as a point
        // bar of gravel and sand (Section.Bar); a pool lies deepest in each bend's apex, a riffle shallow at
        // each crossing.
        private static void Carve(Section s, Stream stream)
        {
            float reach = stream.HalfWidth.Max() + BankWidth * MaxSoft * 2.8f;
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
                    float bank = BankWidth * Mathf.Lerp(stream.Soft[i], stream.Soft[i + 1], u) * (inside > 0 ? 1 + 1.8f * inside : 1 + .5f * inside);
                    if (distance > half + bank) continue;
                    float bed = Mathf.Lerp(stream.Bed[i], stream.Bed[i + 1], u) - PoolDip * Mathf.Lerp(stream.Pool[i], stream.Pool[i + 1], u);
                    float across = side * distance / half + .45f * turn, rim = side * Mathf.Min(distance / half, 1) + .45f * turn;
                    // A bank rises at once from the water's edge and rounds over onto the ground above; a bank
                    // that starts flat let shallow water spread out over it on level ground.
                    float up = Mathf.Clamp01((distance - half) / bank);
                    cut = Mathf.Min(cut, distance < half ? bed + .06f * across * across
                        : Mathf.Lerp(bed + .06f * rim * rim, ground, up * (2 - up)));
                    edge = Mathf.Min(edge, distance - half);
                    if (inside > 0)
                        bar = Mathf.Max(bar, Smooth(inside * 1.6f) * Smooth((distance - .25f * half) / (.35f * half))
                            * (1 - Smooth((distance - half - .3f * bank) / (.6f * bank))));
                }
                if (edge == float.MaxValue) continue;
                float fade = Smooth(keep / 2);
                s.After[z, x] = Mathf.Min(ground, Mathf.Lerp(ground, cut, fade));
                s.Channel[z, x] = Mathf.Min(s.Channel[z, x], edge);
                s.Bar[z, x] = Mathf.Max(s.Bar[z, x], bar * fade);
            }
        }

        // Metres outside the collar's rectangular roof, conservative at its corners.
        private static float BeyondCollar(Vector2 p) =>
            Mathf.Max(Mathf.Abs(p.x) - SiteLayout.Extent.x * .5f, Mathf.Abs(p.y) - SiteLayout.Extent.z * .5f) - CollarOverlap;

        // Water surface of the creeks at every terrain sample within reach of a bed: the course's water
        // level, blended across the nearest stretches and courses so it stays continuous at bends and
        // forks. Only stretches about as near as the nearest one count: a stretch further up the creek
        // that lies a few metres off (across a meander's neck) raised the water above its banks.
        // It follows the bed's gentle grade down to 5 mm above the lake plane. Only water joined to a
        // creek's own channel is kept: a hollow within reach whose ground lies below the creek's level but
        // is cut off from it showed as a separate puddle whose edge ran straight where the reach ended
        // (user, 2026-10-09).
        private static float[,] StreamLevels(Section s)
        {
            int n = s.Samples;
            var level = new float[n, n];
            var weight = new float[n, n];
            var nearest = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) nearest[z, x] = float.MaxValue;
            int Index(float local, float site, float origin) => Mathf.Clamp(Mathf.RoundToInt((local + site - origin) / s.Cell), 0, n - 1);
            var ends = new int[s.Streams.Count];
            // Two passes over the same stretches: the nearest distance, then the weighted levels.
            for (int pass = 0; pass < 2; pass++)
            for (int c = 0; c < s.Streams.Count; c++)
            {
                var stream = s.Streams[c];
                // The creek's own water runs on through the backwater to the open lake.
                int end = Array.FindIndex(stream.Points, q => s.Sample(s.Uncarved, DemoPoint(q)) < WaterLevel - .05f);
                ends[c] = end < 0 ? stream.Points.Length - 1 : end;
                if (ends[c] < 1) continue;
                var points = stream.Points.Take(ends[c] + 1).ToArray();
                // A point bar's gentle slope lets the water spread a little further on the inside of a bend.
                float banks = BankWidth * MaxSoft * 2.8f, reach = stream.HalfWidth.Max() + banks;
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
                        if (pass == 0) { nearest[z, x] = Mathf.Min(nearest[z, x], distance); continue; }
                        // The nearest stretches (and courses, at a fork) weigh most; the blend keeps the
                        // surface continuous where the nearest stretch would jump.
                        float off = (distance - nearest[z, x]) / .7f;
                        float w = Mathf.Exp(-off * off) + 1e-6f;
                        level[z, x] += w * Mathf.Lerp(stream.Surface[i], stream.Surface[i + 1], u);
                        weight[z, x] += w;
                    }
                }
            }
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++) level[z, x] = weight[z, x] > 0 ? level[z, x] / weight[z, x] : float.NaN;
            s.CreekLevel = (float[,])level.Clone();
            // Flood the water outward from each creek's centreline through ground below its level, within
            // the creek's carved cross-section: beyond it a film of water spread over flat shore ground.
            var joined = new bool[n, n];
            var queue = new Queue<Vector2Int>();
            bool Wet(int x, int z) => s.After[z, x] < level[z, x] && s.Channel[z, x] < float.MaxValue;
            for (int c = 0; c < s.Streams.Count; c++)
                for (int i = 0; i <= ends[c]; i++)
                {
                    var demo = DemoPoint(s.Streams[c].Points[i]);
                    int x = Mathf.Clamp(Mathf.RoundToInt((demo.x - s.Origin.x) / s.Cell), 0, n - 1);
                    int z = Mathf.Clamp(Mathf.RoundToInt((demo.z - s.Origin.y) / s.Cell), 0, n - 1);
                    if (joined[z, x] || !Wet(x, z)) continue;
                    joined[z, x] = true;
                    queue.Enqueue(new Vector2Int(x, z));
                }
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cell.x + dx, z = cell.y + dz;
                    if (x < 0 || z < 0 || x >= n || z >= n || joined[z, x] || !Wet(x, z)) continue;
                    joined[z, x] = true;
                    queue.Enqueue(new Vector2Int(x, z));
                }
            }
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++) if (!joined[z, x] && s.After[z, x] < level[z, x]) level[z, x] = float.NaN;
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
            // Stems vary in height.
            reeds.minWidth = .8f; reeds.maxWidth = 1.3f; reeds.minHeight = .7f; reeds.maxHeight = 1.7f; reeds.noiseSpread = 2; reeds.density = 1.3f;
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
            var feather = From("GrassMountain4", MeadowVariant(Plant(mountain, "GrassMountain4")));
            // Plants grow upright on a slope; the packs lean them square to the ground, so reeds and grass on a
            // steep bank stuck out sideways (user, 2026-10-09). Pebbles and twigs still lie on it.
            foreach (var plant in new[] { reeds, rushes, feather, dryGrass, dryTall, green }) plant.alignToGround = 0;
            // Order is the detail layer order after the demo's prototypes; see LakebedDetail.
            return new[] { reeds, rushes, feather, From("Pebble1", PebbleVariant(Plant(mountain, "Pebble1"))), From("Pebble2", PebbleVariant(Plant(mountain, "Pebble2"))),
                From("Pebble3", PebbleVariant(Plant(mountain, "Pebble3"))), From("Branchs"), dryGrass, dryTall, green };
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
                float a = s.After[z, x] - WaterLevel, c = s.Channel[z, x], bar = s.Bar[z, x], valley = s.Valley[z, x], seep = Seep(s, local);
                bool island = s.Drained[z, x] <= 0 && IslandHeight(local) > WaterLevel + .05f;
                bool region = s.Drained[z, x] > .3f || island || seep > 0 || (s.Lake[z, x] && a > -.3f && a < 1.2f && local.magnitude < 120);
                if (!region || SiteLayout.BeyondOpening(local) < DressingClearance || stations.Any(r => r.Contains(local))) continue;
                float tuft = Noise(local, 3.2f, 21.7f);
                // Grasses keep their roots dry: nothing but reeds and pebbles stands in the water. Reeds stand
                // at a creek's waterline, never up its banks, and tall plants keep off steep ground.
                float creek = s.CreekLevel[z, x];
                float dry = Smooth((a - .05f) / .1f) * (float.IsNaN(creek) ? 1 : Smooth((s.After[z, x] - creek - .03f) / .08f));
                float waterline = float.IsNaN(creek) ? 0 : Smooth((creek + .2f - s.After[z, x]) / .1f);
                // Upright cards on a steep bank stack into stripes, so even grass thins out there.
                float slope = Slope(s, x, z), flat = 1 - Smooth((slope - .2f) / .15f), upright = 1 - Smooth((slope - .45f) / .25f);
                float sward = 1 - Smooth((slope - .55f) / .3f);
                // Plants follow water and time rather than an even sprinkle of look-alike clumps (user,
                // 2026-10-09): ground exposed longest (higher, toward the cliff) has grown over most, the
                // band the water left last is nearly bare, the creeks' banks set back from the water, their
                // valley floors and the wet ground round a seep are greenest, and gravel bars stay bare.
                // Cover comes as broad patches of clumps with sparse sprigs between them, soft-edged, so no
                // two patches share a size.
                float age = Smooth((a - .7f) / .8f) * Mathf.Lerp(.45f, 1, Smooth((local.x + 20) / 45));
                float moist = Mathf.Max(Mathf.Max(Band(c, .6f, 3.6f, .7f) * (1 - .9f * bar), .5f * valley), seep * Smooth((c - .4f) / .6f));
                float habitat = dry * Mathf.Max(Mathf.Max(moist, .6f * age), Mathf.Max(island ? 1 : 0, .35f * Band(a, .2f, .9f)));
                float patch = Smooth((Noise(local, 9, 17.3f) - .36f) / .3f), clumps = Smooth((Noise(local, 2.6f, 23.1f) - .4f) / .22f);
                float sprigs = .3f * Smooth((Noise(local, 1.2f, 5.3f) - .5f) / .3f) * Smooth((Noise(local, 13, 2.9f) - .45f) / .3f);
                maps[(int)LakebedDetail.DryTall][v, u] = Mathf.RoundToInt(160 * Frayed(habitat * patch * clumps, local, 23.9f) * clumps * upright);
                // Shorter dry grass rings the clumps and scatters thinly in sprigs between them.
                maps[(int)LakebedDetail.DryGrass][v, u] = Mathf.RoundToInt(130 * Mathf.Max(Frayed(habitat * patch * clumps, local, 31.3f), habitat * sprigs) * sward);
                // The canyon's green grass grows round the plot and lush over a seep's wet ground.
                float green = Mathf.Max(GreenDensity(local) * Frayed(GreenShape(local, a, c, .24f) * Smooth((tuft - .25f) / .12f), local, 7.7f),
                    .9f * Frayed(seep * Smooth((c - .3f) / .5f) * Smooth((tuft - .15f) / .3f), local, 19.1f));
                maps[(int)LakebedDetail.GreenGrass][v, u] = Mathf.RoundToInt(255 * green * dry * sward);
                float clump2 = Noise(local, 6, 4.4f), scatter = Noise(local, 2.3f, 9.7f);
                float shore = Band(a, -.2f, .25f), bank = Band(c, -.25f, .8f, .25f) * (1 - bar), damp = Band(c, .3f, 3.2f, .4f);
                // Reeds stand in a few colonies at the water's edge with open water between them, on gentle
                // ground only, never on the bare bars and never at a seep, where the water is only a trickle;
                // a ring of reeds round a pool and single clumps dotted evenly along a bank read as planted
                // (user, 2026-10-09). Rushes grow as tussock colonies on damp, gentle ground only (the creeks'
                // banks, a seep's margins, the lowest shore), and the weeds in loose groups on the drier flats.
                float colony = Smooth((Noise(local, 7, 11.3f) - .6f) / .05f) * Smooth((Noise(local, 2.2f, 3.1f) - .15f) / .2f);
                maps[(int)LakebedDetail.Reeds][v, u] = Mathf.RoundToInt(220 * Mathf.Max(shore, bank * waterline) * colony * flat * (1 - seep));
                float wetGround = Mathf.Max(Mathf.Max(damp * (1 - bar), Band(a, .15f, .5f)), seep * Band(c, .4f, 3, .5f));
                maps[(int)LakebedDetail.Rushes][v, u] = Mathf.RoundToInt(150 * Frayed(dry * wetGround * Smooth((Noise(local, 5, 4.4f) - .5f) / .15f), local, 13.9f)
                    * (1 - Smooth((slope - .3f) / .2f)));
                maps[(int)LakebedDetail.Feather][v, u] = Mathf.RoundToInt(70 * dry * Band(a, .45f, 1.3f) * Smooth((c - 2) / 1)
                    * Smooth((Noise(local, 8, 6.6f) - .55f) / .12f) * Smooth((clump2 - .5f) / .2f) * upright);
                // Pebbles lie where water sorted them: channel beds, the bars and broken strand lines along old
                // waterlines, and only a stray few on the flats; none out in the lake, where a creek's bed ran
                // on as a line of dark dots under the water.
                float strand = Smooth((Noise(local, 6, 8.8f) - .4f) / .2f);
                float pebbles = Mathf.Max(Mathf.Max(Smooth((-.1f - c) / .4f) * (.5f + .5f * scatter), bar * (.4f + .6f * scatter)),
                    Mathf.Max(Band(a, -.15f, .55f) * strand * (.3f + .7f * scatter), .3f * Smooth((scatter - .8f) / .06f))) * Smooth((a + .15f) / .1f);
                int kind = (int)LakebedDetail.Pebble1 + Mathf.FloorToInt(Noise(local, 1.7f, 5.5f) * 2.999f);
                maps[kind][v, u] = Mathf.RoundToInt(130 * pebbles);
                // Drift twigs collect on the bars and along the old waterlines, above the water.
                float drift = (Band(a, .2f, .55f) * strand + .8f * bar * Smooth((.9f - a) / .4f)) * dry;
                maps[(int)LakebedDetail.Twigs][v, u] = Mathf.RoundToInt(80 * Mathf.Clamp01(drift) * Smooth((Noise(local, 5, 2.2f) - .62f) / .1f));
            }
            for (int l = 0; l < count; l++) data.SetDetailLayer(0, 0, first + l, maps[l]);
        }

        // Stranded stones where the water left them: a few in the creeks' riffles and against the cut banks
        // of bends, a cluster at each seep, a few on the bars and along broken old waterlines, and
        // only a rare lone stone out on the flats; the odd slumped heap of rubble at a cut bank or the old shore. Stones
        // lie on their broad side well bedded in the mud, mostly small with the odd larger one, and wear
        // the silt-coated bare rock. Pale stones standing on end in evenly spaced clusters read as
        // scattered props (user, 2026-10-09). Only larger stones keep colliders.
        private static void ScatterDebris(Section s, Transform environment, Rect[] stations)
        {
            var parent = new GameObject("Lakebed debris").transform;
            parent.SetParent(environment, false);
            var random = new System.Random(1789);
            // Footprints of the stones placed so far: stones never grow through each other.
            var placed = new List<(Vector2 centre, float radius)>();
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
                var centre = new Vector2(bounds.center.x, bounds.center.z);
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * .8f;
                if (placed.Any(o => Vector2.Distance(o.centre, centre) < o.radius + radius))
                {
                    UnityEngine.Object.DestroyImmediate(item);
                    return;
                }
                placed.Add((centre, radius));
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
                // Riffles at the bends' crossings hold a few stones in the current; a cut bank outside a
                // bend sheds its stones at its foot, with a few pebbles on the bar across.
                for (int i = random.Next(6, 12); i < points.Length - 1; i += random.Next(10, 20))
                {
                    if (random.NextDouble() < .25 || stream.Spring && stream.Along[i] < 3) continue;
                    var tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                    var left = new Vector2(-tangent.y, tangent.x);
                    float half = stream.HalfWidth[i], turn = stream.Turn[i];
                    if (stream.Pool[i] < .35f) Stones(points[i] + left * half * Range(-.7f, .7f), random.Next(1, 4), .5f, .08f, .3f);
                    else if (Mathf.Abs(turn) > .3f)
                    {
                        var outside = left * -Mathf.Sign(turn);
                        if (Mathf.Abs(turn) > .7f && random.NextDouble() < .2)
                        {
                            var heap = points[i] + outside * (half + Range(.3f, .8f));
                            if (Allowed(heap)) Put("Rubble/RubbleSparse_" + (1 + random.Next(3)), heap, Range(.5f, .8f), Yaw(), .25f);
                        }
                        else Stones(points[i] + outside * half * Range(.6f, 1.05f), random.Next(1, 4), .5f, .1f, .4f);
                        if (random.NextDouble() < .45) Stones(points[i] - outside * (half + Range(0, 1.2f)), random.Next(1, 3), .7f, .07f, .2f);
                    }
                }
                if (!stream.Spring) continue;
                // The seep's water wells up from under a few stones bedded in the slope's toe: one or two lie
                // across the trickle's first metre, the rest beside and behind it.
                var head = points[0];
                var down = (points[Mathf.Min(8, points.Length - 1)] - head).normalized;
                var across = new Vector2(-down.y, down.x);
                void Head(Vector2 p, float min, float max)
                {
                    if (SiteLayout.BeyondOpening(p) > DressingClearance + 1 && !stations.Any(r => r.Contains(p)))
                        Put("Rocks/Rock_" + random.Next(4), p, Size(min, max), Settled(), Range(.38f, .5f), true, Range(.75f, .95f));
                }
                Head(head + down * Range(.1f, .5f) + across * Range(-.2f, .2f), .45f, .7f);
                if (random.NextDouble() < .6) Head(head + down * Range(.9f, 1.4f) + across * Range(-.4f, .4f), .3f, .5f);
                for (int k = random.Next(3, 5); k > 0; k--)
                    Head(head + down * Range(-2, 1.2f) + across * (random.NextDouble() < .5 ? -1 : 1) * Range(.7f, 2.4f), .2f, .6f);
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

        // The terrain scatters plants and pebbles without knowing about the rocks standing on it, so reeds
        // poked out of a boulder beside the water (user, 2026-10-09). Every lakebed detail cell a rock covers
        // is cleared: at a few points across the cell a ray from above meets the rock before the ground.
        // Stones without colliders get a temporary one for the test.
        private static void ClearUnderRocks(Transform environment, int first)
        {
            var terrain = environment.GetComponentInChildren<Terrain>();
            var data = terrain.terrainData;
            int cells = data.detailWidth, count = Enum.GetValues(typeof(LakebedDetail)).Length;
            float cell = data.size.x / cells;
            var corner = terrain.transform.position;
            var temporary = new List<Collider>();
            var rocks = new List<Bounds>();
            foreach (string group in new[] { "Boulders", "BigBoulders", "Rubble_dense", "Rubble_sparse", "Cliffs", "Ruins", "Lakebed debris" })
            {
                var parent = environment.Find(group);
                if (parent == null) continue;
                foreach (var renderer in parent.GetComponentsInChildren<MeshRenderer>())
                {
                    var bounds = renderer.bounds;
                    if (Mathf.Abs(bounds.center.x) > 150 || Mathf.Abs(bounds.center.z) > 170) continue;
                    rocks.Add(bounds);
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (renderer.GetComponentInParent<Collider>() == null && filter != null && filter.sharedMesh != null)
                    {
                        var collider = renderer.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = filter.sharedMesh;
                        temporary.Add(collider);
                    }
                }
            }
            Physics.SyncTransforms();
            var cleared = new HashSet<int>();
            try
            {
                foreach (var bounds in rocks)
                {
                    int u0 = Mathf.Max(0, Mathf.FloorToInt((bounds.min.x - .4f - corner.x) / cell)), u1 = Mathf.Min(cells - 1, Mathf.FloorToInt((bounds.max.x + .4f - corner.x) / cell));
                    int v0 = Mathf.Max(0, Mathf.FloorToInt((bounds.min.z - .4f - corner.z) / cell)), v1 = Mathf.Min(cells - 1, Mathf.FloorToInt((bounds.max.z + .4f - corner.z) / cell));
                    for (int v = v0; v <= v1; v++)
                    for (int u = u0; u <= u1; u++)
                    {
                        int key = v * cells + u;
                        if (cleared.Contains(key)) continue;
                        for (int k = 0; k < 5; k++)
                        {
                            float sx = k == 1 ? .15f : k == 2 ? .85f : .5f, sz = k == 3 ? .15f : k == 4 ? .85f : .5f;
                            var top = new Vector3(corner.x + (u + sx) * cell, bounds.max.y + 1, corner.z + (v + sz) * cell);
                            float ground = terrain.SampleHeight(top) + corner.y;
                            if (!Physics.Raycast(top, Vector3.down, out var hit, top.y - ground + 1, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                            if (hit.collider is TerrainCollider || hit.point.y < ground + .05f) continue;
                            cleared.Add(key);
                            break;
                        }
                    }
                }
            }
            finally
            {
                foreach (var collider in temporary) UnityEngine.Object.DestroyImmediate(collider);
            }
            for (int l = 0; l < count; l++)
            {
                var map = data.GetDetailLayer(0, 0, cells, cells, first + l);
                foreach (int key in cleared) map[key / cells, key % cells] = 0;
                data.SetDetailLayer(0, 0, first + l, map);
            }
            EditorUtility.SetDirty(data);
            Debug.Log($"Lakebed plants: cleared {cleared.Count} detail cells under rocks.");
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
            return Variant(source, material, suffix);
        }

        // The Mountains pack's pebbles are pale blue-grey granite that read as ice cubes on the warm mud
        // (user, 2026-10-09); project copies are tinted warm to sit among the lakebed's stones (a darker
        // .78/.66/.55 read as chocolate chips). An existing material keeps its Inspector tuning.
        public const string PebbleMaterialPath = Folder + "/LakebedPebble.mat";
        public static readonly Color PebbleTint = new Color(1, .9f, .8f, 1);

        private static GameObject PebbleVariant(GameObject source)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PebbleMaterialPath);
            if (material == null)
            {
                material = new Material(source.GetComponentInChildren<Renderer>().sharedMaterial) { name = "LakebedPebble" };
                material.SetColor("_Color", PebbleTint);
                AssetDatabase.CreateAsset(material, PebbleMaterialPath);
            }
            return Variant(source, material, "Lakebed");
        }

        // Project prefab variant of a pack plant or pebble drawn with a project material.
        private static GameObject Variant(GameObject source, Material material, string suffix)
        {
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
