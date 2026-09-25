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
        private const float TrickleDepth = .15f, BankWidth = 2.4f, MouthLift = .005f, MaxBedGrade = .08f;
        // Metres beyond the plot outline: channels leave a walkable margin around it.
        public const float ChannelClearance = 6f;
        public const string TrickleMeshPath = Folder + "/Trickles.asset";

        // Site-local courses to the remaining lake. One stream rises at the east cliff foot, wraps
        // the plot's north and west sides and splits at its mouth; another rises from a spring west
        // of the boulders south of the camp. A dry gully joins from the cliff; dry gullies end on a
        // wet channel.
        private static readonly (Vector2[] course, bool wet, float seed)[] Courses =
        {
            (new[] { new Vector2(27, 31), new Vector2(17, 24), new Vector2(5, 22.5f), new Vector2(-9, 23.5f), new Vector2(-22, 21), new Vector2(-30, 10),
                new Vector2(-32, -2), new Vector2(-39, -6), new Vector2(-50, -6), new Vector2(-62, -9) }, true, 1.3f),
            (new[] { new Vector2(-32, -2), new Vector2(-38, 7), new Vector2(-46, 11), new Vector2(-60, 13) }, true, 6.2f),
            (new[] { new Vector2(11, -38), new Vector2(6, -31), new Vector2(-6, -28), new Vector2(-18, -24), new Vector2(-30, -26),
                new Vector2(-42, -22), new Vector2(-52, -23), new Vector2(-64, -26) }, true, 4.1f),
            (new[] { new Vector2(33, 11), new Vector2(26, 17), new Vector2(18, 23.5f) }, false, 2.9f),
        };

        private sealed class Stream
        {
            public Vector2[] Points;  // site-local centreline, upstream first
            public float[] Bed;       // demo-space bed height, never rising downstream
            public float[] HalfWidth; // bed half-width
            public bool Wet;
        }

        private static float Band(float value, float low, float high, float soft = .15f) =>
            Smooth((value - low) / soft) * Smooth((high - value) / soft);

        private static Vector3 DemoPoint(Vector2 local) => new Vector3(local.x + SiteInDemo.x, 0, local.y + SiteInDemo.z);

        private static void CarveChannels(Section s)
        {
            int n = s.Samples;
            s.Uncarved = (float[,])s.After.Clone();
            s.Channel = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) s.Channel[z, x] = float.MaxValue;
            // Dry gullies first: a wet channel then follows their mouths instead of a gully
            // cutting a hole beneath the running water.
            foreach (var (course, wet, seed) in Courses.OrderBy(c => c.wet))
            {
                var points = Around(Meander(course, .5f, seed), s.Rocks);
                var stream = new Stream { Points = points, Wet = wet, Bed = new float[points.Length], HalfWidth = new float[points.Length] };
                float total = 0;
                for (int i = 1; i < points.Length; i++) total += Vector2.Distance(points[i], points[i - 1]);
                float along = 0, lowest = float.MaxValue;
                bool reachedLake = false;
                for (int i = 0; i < points.Length; i++)
                {
                    if (i > 0) along += Vector2.Distance(points[i], points[i - 1]);
                    float t = along / total;
                    // Occasional pools widen and deepen the wet channels.
                    float pool = wet ? Mathf.Pow(Mathf.PerlinNoise(along / 13, seed * 3.1f), 3) * 1.6f : 0;
                    float depth = wet ? Mathf.Lerp(.2f, .5f, Smooth(t / .35f)) + .12f * pool : Mathf.Lerp(.08f, .28f, Smooth(t / .5f));
                    float half = (wet ? Mathf.Lerp(1.3f, 2.6f, t) * (1 + .6f * pool) : Mathf.Lerp(.5f, .9f, t))
                        * Mathf.Lerp(.35f, 1, Smooth(along / 5)); // Seeps emerge narrow from the bank.
                    // The bed is cut below the lowest ground across the whole section, so on a side
                    // slope both banks still rise above the water instead of it spilling downhill.
                    float ground = SectionFloor(s, s.Uncarved, points, i, half + BankWidth);
                    // Beds stay just above the lake plane until the course reaches open lake water,
                    // so no inland stretch dips into a pool the lake would show through; from there
                    // the bed follows the ground down at the gentle grade below.
                    reachedLake |= s.Sample(s.Uncarved, DemoPoint(points[i])) < WaterLevel - .05f;
                    float floor = reachedLake ? float.MinValue : WaterLevel + .03f;
                    lowest = Mathf.Max(Mathf.Min(lowest, ground - depth), floor);
                    // A bed falls at most a gentle grade per metre: a sudden drop leaves a straight ledge
                    // across the channel that the water outlines as a polygon.
                    if (i > 0) lowest = Mathf.Max(lowest, stream.Bed[i - 1] - MaxBedGrade * Vector2.Distance(points[i], points[i - 1]));
                    stream.Bed[i] = lowest;
                    stream.HalfWidth[i] = half;
                }
                Carve(s, stream);
                s.Streams.Add(stream);
            }
            s.StreamLevel = StreamLevels(s);
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

        // Catmull-Rom through the authored course, with small meanders faded out at both ends
        // so seeps and junctions stay where they were authored.
        private static Vector2[] Meander(Vector2[] controls, float spacing, float seed)
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
            float total = along[dense.Count - 1];
            var result = new Vector2[dense.Count];
            for (int i = 0; i < dense.Count; i++)
            {
                var tangent = (dense[Mathf.Min(i + 1, dense.Count - 1)] - dense[Mathf.Max(i - 1, 0)]).normalized;
                float wiggle = (.9f * Mathf.Sin(along[i] / 6.5f + seed) + 1.4f * (Mathf.PerlinNoise(along[i] / 9, seed) - .5f))
                    * Smooth(along[i] / 6) * Smooth((total - along[i]) / 6);
                result[i] = dense[i] + new Vector2(-tangent.y, tangent.x) * wiggle;
            }
            return result;
        }

        private static void Carve(Section s, Stream stream)
        {
            float reach = stream.HalfWidth.Max() + BankWidth;
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
                float ground = s.After[z, x], cut = ground, edge = float.MaxValue;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var ab = points[i + 1] - points[i];
                    float u = Mathf.Clamp01(Vector2.Dot(p - points[i], ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    float distance = Vector2.Distance(points[i] + ab * u, p);
                    float half = Mathf.Lerp(stream.HalfWidth[i], stream.HalfWidth[i + 1], u);
                    if (distance > half + BankWidth) continue;
                    float bed = Mathf.Lerp(stream.Bed[i], stream.Bed[i + 1], u), across = distance / half;
                    cut = Mathf.Min(cut, distance < half ? bed + .06f * across * across
                        : Mathf.Lerp(bed + .06f, ground, Smooth((distance - half) / BankWidth)));
                    edge = Mathf.Min(edge, distance - half);
                }
                if (edge == float.MaxValue) continue;
                s.After[z, x] = Mathf.Min(ground, Mathf.Lerp(ground, cut, Smooth(keep / 2)));
                s.Channel[z, x] = Mathf.Min(s.Channel[z, x], edge);
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
                float reach = stream.HalfWidth.Max() + BankWidth;
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
                        float span = Mathf.Lerp(stream.HalfWidth[i], stream.HalfWidth[i + 1], u) + BankWidth;
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
            // The canyon's own grass, sun-dried to muted olive on the exposed bed, and dusty rushes and reeds.
            var dryGrass = From("Grass_3", DryVariant(Plant(canyon, "Grass_3"), DryGrassMaterialPath, new Color(.4f, .44f, .25f), new Color(.32f, .37f, .2f)), canyon);
            var dryTall = From("Grass_4", DryVariant(Plant(canyon, "Grass_4"), DryGrassMaterialPath, new Color(.4f, .44f, .25f), new Color(.32f, .37f, .2f)), canyon);
            var rushes = From("GrassMountain2", DryVariant(Plant(mountain, "GrassMountain2"), RushMaterialPath, new Color(.6f, .64f, .46f), new Color(.5f, .55f, .38f)));
            // Knee-high tufts, not the canyon's tall meadow cards.
            dryTall.minWidth = .9f; dryTall.maxWidth = 1.5f; dryTall.minHeight = .6f; dryTall.maxHeight = 1.25f;
            dryGrass.minHeight = .5f; dryGrass.maxHeight = 1;
            // Order is the detail layer order after the demo's prototypes; see LakebedDetail.
            return new[] { reeds, rushes, From("GrassMountain4"), From("Pebble1"), From("Pebble2"), From("Pebble3"), From("Branchs"), dryGrass, dryTall };
        }

        private enum LakebedDetail { Reeds, Rushes, Feather, Pebble1, Pebble2, Pebble3, Twigs, DryGrass, DryTall }

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
                float a = s.After[z, x] - WaterLevel, c = s.Channel[z, x];
                bool island = s.Drained[z, x] <= 0 && IslandHeight(local) > WaterLevel + .05f;
                bool region = s.Drained[z, x] > .3f || island || (s.Lake[z, x] && a > -.3f && a < 1.2f && local.magnitude < 120);
                if (!region || SiteLayout.BeyondOpening(local) < DressingClearance || stations.Any(r => r.Contains(local))) continue;
                float tuft = Noise(local, 3.2f, 21.7f), field = Noise(local, 11, 17.3f);
                // Grasses keep their roots dry: nothing but reeds and pebbles stands in the water.
                float stream = s.StreamLevel[z, x];
                float dry = Smooth((a - .05f) / .1f) * (float.IsNaN(stream) ? 1 : Smooth((s.After[z, x] - stream - .03f) / .08f));
                // Dense tufts on banks, strand lines and islands; a few across the flats.
                float habitat = dry * Mathf.Max(Band(c, .2f, 2.8f, .4f), Band(a, .15f, 1f), island ? 1 : 0, .5f * Smooth((field - .56f) / .1f));
                float clumped = habitat * Smooth((tuft - .56f) / .06f);
                maps[(int)LakebedDetail.DryTall][v, u] = Mathf.RoundToInt(150 * clumped);
                // Shorter dry grass rings the clumps and scatters thinly across the higher flats.
                float flats = .35f * Smooth((a - 1.2f) / .4f) * Smooth((Noise(local, 14, 5.3f) - .7f) / .06f) * Smooth((tuft - .45f) / .1f);
                maps[(int)LakebedDetail.DryGrass][v, u] = Mathf.RoundToInt(130 * Mathf.Max(habitat * Smooth((tuft - .5f) / .06f), flats));
                float clump = Noise(local, 3.5f, 11.3f), clump2 = Noise(local, 6, 4.4f), scatter = Noise(local, 2.3f, 9.7f);
                float shore = Band(a, -.2f, .25f), bank = Band(c, -.25f, .8f, .25f), damp = Band(c, .3f, 3.2f, .4f);
                // Reeds grow in a few dense stands right at the water; rushes clump along damp banks.
                maps[(int)LakebedDetail.Reeds][v, u] = Mathf.RoundToInt(170 * Mathf.Max(shore, bank) * Smooth((clump - .63f) / .07f));
                maps[(int)LakebedDetail.Rushes][v, u] = Mathf.RoundToInt(100 * dry * Mathf.Max(damp, Band(a, .2f, .9f)) * Smooth((clump2 - .6f) / .08f));
                maps[(int)LakebedDetail.Feather][v, u] = Mathf.RoundToInt(90 * dry * Band(a, .45f, 1.3f) * Smooth((clump2 - .68f) / .08f) * Smooth((c - 2) / 1));
                float pebbles = Mathf.Max(Smooth((.2f - c) / .4f), Band(a, -.15f, .55f) * (.4f + .6f * scatter), .45f * Smooth((scatter - .78f) / .06f));
                int kind = (int)LakebedDetail.Pebble1 + Mathf.FloorToInt(Noise(local, 1.7f, 5.5f) * 2.999f);
                maps[kind][v, u] = Mathf.RoundToInt(130 * pebbles);
                float strand = Band(a, .2f, .55f) + .8f * Band(c, -.2f, 1.5f) * Smooth((.7f - a) / .3f);
                maps[(int)LakebedDetail.Twigs][v, u] = Mathf.RoundToInt(80 * Mathf.Clamp01(strand) * Smooth((Noise(local, 5, 2.2f) - .66f) / .08f));
            }
            for (int l = 0; l < count; l++) data.SetDetailLayer(0, 0, first + l, maps[l]);
        }

        // Rubble clusters and bare stranded stones along the channels, the retreating shoreline
        // and a few on the flats. Only larger stones keep colliders.
        private static void ScatterDebris(Section s, Transform environment, Rect[] stations)
        {
            var parent = new GameObject("Lakebed debris").transform;
            parent.SetParent(environment, false);
            var random = new System.Random(1789);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            bool Allowed(Vector2 p) => SiteLayout.BeyondOpening(p) > DressingClearance + 1 && s.Sample(s.Drained, DemoPoint(p)) > .5f
                && !stations.Any(r => r.Contains(p));
            void Put(string prefabName, Vector2 p, float scale, Quaternion rotation, float embed, bool stranded = true)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VendorPrefabs + prefabName + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing Highlands prefab " + prefabName + ".");
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                WithoutStaticBatching(item);
                item.transform.SetPositionAndRotation(new Vector3(p.x, 0, p.y), rotation);
                item.transform.localScale = Vector3.one * scale;
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
            }
            Quaternion Yaw() => Quaternion.Euler(0, Range(0, 360), 0);
            Quaternion Tumble() => Quaternion.Euler(Range(-25, 25), Range(0, 360), Range(-25, 25));
            void Stones(Vector2 centre, int count, float spread, float minScale, float maxScale)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = centre + new Vector2(Range(-spread, spread), Range(-spread, spread));
                    if (Allowed(p)) Put("Rocks/Rock_" + random.Next(4), p, Range(minScale, maxScale), Tumble(), .35f);
                }
            }
            foreach (var stream in s.Streams)
                for (int i = random.Next(6, 14); i < stream.Points.Length; i += random.Next(12, 24))
                {
                    var tangent = (stream.Points[Mathf.Min(i + 1, stream.Points.Length - 1)] - stream.Points[Mathf.Max(i - 1, 0)]).normalized;
                    var side = new Vector2(-tangent.y, tangent.x) * (random.NextDouble() < .5 ? -1 : 1);
                    var p = stream.Points[i] + side * (stream.HalfWidth[i] + Range(0, BankWidth));
                    if (!Allowed(p)) continue;
                    if (random.NextDouble() < .5) Put("Rubble/RubbleSparse_" + (1 + random.Next(3)), p, Range(.6f, 1.05f), Yaw(), .05f);
                    else Stones(p, random.Next(2, 5), 1.4f, .12f, .38f);
                }
            // The retreating lake leaves stones along its old waterlines; a few lie out on the flats.
            int shoreline = 0, flats = 0;
            for (int attempt = 0; attempt < 4000 && (shoreline < 16 || flats < 9); attempt++)
            {
                var p = new Vector2(Range(DrainedCentre.x - DrainedRadii.x, DrainedCentre.x + DrainedRadii.x), Range(DrainedCentre.y - DrainedRadii.y, DrainedCentre.y + DrainedRadii.y));
                if (!Allowed(p)) continue;
                float a = s.Sample(s.After, DemoPoint(p)) - WaterLevel;
                if (shoreline < 16 && a > .15f && a < .75f)
                {
                    shoreline++;
                    if (random.NextDouble() < .4) Put("Rubble/Rubble" + (random.NextDouble() < .5 ? "Sparse_" : "Dense_") + (1 + random.Next(3)), p, Range(.6f, 1), Yaw(), .05f);
                    else Stones(p, random.Next(2, 6), 1.8f, .1f, .32f);
                }
                else if (flats < 9 && a > .9f && random.NextDouble() < .3)
                {
                    flats++;
                    Stones(p, random.Next(1, 3), 1.2f, .25f, .55f);
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

        // Project prefab variant of a pack plant drawn with a muted, sun-dried copy of its
        // material. Terrain details ignore prototype colours with the pack's grass shader.
        private static GameObject DryVariant(GameObject source, string materialPath, Color light, Color dark)
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
            string path = TreesFolder + "/" + source.name + " Dry.prefab";
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

        // Stranded stones read as bare lakebed rock, without the pack's grown-over tops.
        private static Material BareRockMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BareRockMaterialPath);
            if (material != null) return material; // Retain Inspector tuning.
            var vendor = AssetDatabase.LoadAssetAtPath<Material>(VendorRockMaterial);
            if (vendor == null) throw new InvalidOperationException("Missing approved Highlands rock material.");
            material = new Material(vendor) { name = "LakebedRock" };
            material.SetFloat("_LayerPower", 0);
            AssetDatabase.CreateAsset(material, BareRockMaterialPath);
            return material;
        }
    }
}
