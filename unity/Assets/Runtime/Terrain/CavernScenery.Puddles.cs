using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Standing water in the caves' basins (user, 2026-10-08: "add water inside the caves like puddles ... when digging they
    // could disappear", then "make the puddles deeper"): each basin the generator sinks into a cave's floor
    // (TerrainGround.Cavern.Basins) holds a puddle filled PuddleFill of its depth, found from the cave's own shape
    // (TerrainGround.CavernHollow), so it is the same wherever the layout is generated and saves need nothing. See-through,
    // glossy water that the lamps and crystals glint in, darker over its deeper parts. Digging into it keeps it, as a hole
    // dug under water fills, and a cut opened to it below its surface fills too (Retrace) (user: puddles "immediately
    // disappear"); it goes only when a cut lets the water out (Escaped), and then at once, never animated (user: shrinking
    // "is horrible"); a reload finds the same and draws the same.
    // Planned on a worker thread (the shape's noise is pure maths), so a new layout never hitches. The Developer admin hides
    // them all to compare (PuddlesShown).
    public sealed partial class CavernScenery
    {
        // The water's cells, how far a puddle reaches from its lowest point at most, how much of its basin's depth the water
        // fills, and the least water worth showing (m2).
        private const float PuddleCell = .1f, PuddleReach = 2.6f, PuddleFill = .75f, PuddleLeast = .3f;
        // How far under its surface the water is followed out through a cut, how far under it dug ground must lie for the
        // water to fill it, how much further than its reach it may spread before it counts as gone, and how far below its
        // bed a hole beside it must go to swallow it.
        private const float PuddleSeep = .04f, PuddleFills = .1f, PuddleSpread = 1f, PuddleSink = .1f;
        // How far up the floor the water's edge is drawn past the shoreline, under the stone, so the rendered floor (a
        // smoothed sampling of the same shape) always meets the water rather than leaving a gap.
        private const float PuddleShore = .015f;
        // Deeper water darker: a faint dark see-through layer just under the surface (DeepStep apart) over the parts deeper
        // than each of these, so a puddle's middle reads deep from above (two strong layers drew hard rings).
        private static readonly float[] DeepWater = { .1f, .2f, .3f, .4f, .5f };
        private const float DeepStep = .004f;
        private static readonly Color DeepColour = new Color(.012f, .01f, .008f, .12f);
        // Session-only (Developer admin): whether the puddles show.
        public static bool PuddlesShown = true;
        // The water's ripples drift this far (texture repeats a second) on a copy of the material, so lamps and crystals
        // glint and shimmer in it the way water does.
        private static readonly Vector2 RippleDrift = new Vector2(.013f, .008f);
        private static readonly int2[] Steps = { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) };
        private Material puddleWater, puddleDepths;

        [SerializeField] private Material puddleMaterial;
        private Transform puddleRoot;
        private TerrainGround.GroundLayout puddleLayout;
        private Task<List<PuddlePlan>> planning;
        private readonly List<Puddle> puddles = new List<Puddle>();
        private bool puddlesShown = true;
        public int PuddleCount { get { int n = 0; foreach (var puddle in puddles) if (!puddle.Drained) n++; return n; } }

        // A puddle as planned (grid-local metres): its water level, its bed's lowest point and centre, its cells (round
        // Origin, PuddleCell apart; Bottom the lowest), and its water traced along the floor: the surface's triangles and
        // the dark layers' under it (x, depth below the surface, z).
        private sealed class PuddlePlan
        {
            public float Level, Floor;
            public float2 Centre, Origin;
            public int2 Bottom;
            public List<int2> Wet;
            public readonly List<float3> Surface = new List<float3>();
            public readonly List<float3> Depths = new List<float3>();
        }

        private sealed class Puddle
        {
            public Renderer Body;
            public Bounds Bounds;
            public PuddlePlan Plan;
            public HashSet<int2> Wet;
            // The cells beyond its own that cuts opened to its water, now under it.
            public HashSet<int2> Spread = new HashSet<int2>();
            public bool Drained;
        }

        private void OnEnable() { if (terrain != null) terrain.Changed += CheckPuddles; }
        private void OnDisable() { if (terrain != null) terrain.Changed -= CheckPuddles; }

        private void UpdatePuddles(TerrainGround.GroundLayout layout)
        {
            if (!ReferenceEquals(layout, puddleLayout))
            {
                puddleLayout = layout;
                ClearPuddles();
                planning = puddleMaterial != null && layout.Caverns.Length > 0 ? Task.Run(() => Plan(layout)) : null;
            }
            if (planning != null && planning.IsCompleted)
            {
                if (planning.Status == TaskStatus.RanToCompletion) BuildPuddles(planning.Result);
                else Debug.LogWarning("Cave puddles: " + planning.Exception);
                planning = null;
            }
            if (puddleWater != null) puddleWater.mainTextureOffset = RippleDrift * Time.time;
            if (puddlesShown != PuddlesShown)
            {
                puddlesShown = PuddlesShown;
                foreach (var puddle in puddles) if (!puddle.Drained) puddle.Body.enabled = puddlesShown;
            }
        }

        private void ClearPuddles()
        {
            foreach (var puddle in puddles)
                if (puddle.Body != null) { Destroy(puddle.Body.GetComponent<MeshFilter>().sharedMesh); Destroy(puddle.Body.gameObject); }
            puddles.Clear();
        }

        // A cut that lets a puddle's water out takes it away at once; one that opens a dead end to it fills; a restored
        // checkpoint without the cut brings it back as it was.
        private void CheckPuddles(Bounds changed)
        {
            changed.Expand(.5f);
            var spread = new HashSet<int2>();
            foreach (var puddle in puddles)
            {
                if (!puddle.Bounds.Intersects(changed)) continue;
                Settle(puddle, spread);
                puddle.Body.enabled = puddlesShown && !puddle.Drained;
            }
        }

        private void Settle(Puddle puddle, HashSet<int2> spread)
        {
            puddle.Drained = Escaped(puddle, spread);
            if (puddle.Drained || spread.SetEquals(puddle.Spread)) return;
            puddle.Spread = new HashSet<int2>(spread);
            Retrace(puddle);
        }

        // Whether a cut has let the water out: followed from just under its surface through the air beyond its own cells, it
        // reaches ground lower than its bed (a hole dug beside it, a lower floor) or spreads well past its reach. A hole dug
        // in its own bed only fills. `spread` gets the dug cells beyond its own that it fills.
        private bool Escaped(Puddle puddle, HashSet<int2> spread)
        {
            spread.Clear();
            var plan = puddle.Plan;
            float seep = plan.Level - PuddleSeep, sink = plan.Floor - PuddleSink;
            int limit = (int)math.ceil((PuddleReach + PuddleSpread) / PuddleCell);
            var queue = new Queue<int2>();
            var seen = new HashSet<int2>();
            foreach (var c in plan.Wet) if (Air(plan, c, seep)) { seen.Add(c); queue.Enqueue(c); }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var step in Steps)
                {
                    var n = c + step;
                    if (puddle.Wet.Contains(n) || !seen.Add(n) || !Air(plan, n, seep)) continue;
                    if (math.lengthsq(n - plan.Bottom) > limit * limit || Air(plan, n, sink)) return true;
                    if (Air(plan, n, plan.Level - PuddleFills)) spread.Add(n);
                    queue.Enqueue(n);
                }
            }
            return false;
        }

        private bool Air(PuddlePlan plan, int2 c, float y)
        {
            var xz = plan.Origin + (float2)c * PuddleCell;
            return !terrain.IsSolid(terrain.transform.TransformPoint(new Vector3(xz.x, y, xz.y)));
        }

        // The water drawn again over its own cells and those cuts opened to it, along the dug ground's own floor, so a notch
        // dug through its rim below the water fills instead of standing dry beside a wall of water.
        private void Retrace(Puddle puddle)
        {
            var plan = puddle.Plan;
            var cells = new List<int2>(plan.Wet);
            cells.AddRange(puddle.Spread);
            var floors = new Dictionary<int2, float>();
            float Height(int2 c)
            {
                if (floors.TryGetValue(c, out float h)) return h;
                h = DugFloor(plan, c);
                floors.Add(c, h);
                return h;
            }
            var surface = new List<float3>(); var depths = new List<float3>();
            Shoreline(surface, cells, c => Height(c) - (plan.Level + PuddleShore), plan.Origin, 0);
            for (int k = 0; k < DeepWater.Length; k++)
            {
                float deep = plan.Level - DeepWater[k];
                Shoreline(depths, cells, c => Height(c) - deep, plan.Origin, DeepStep * (k + 1));
            }
            var filter = puddle.Body.GetComponent<MeshFilter>();
            Destroy(filter.sharedMesh);
            filter.sharedMesh = WaterMesh(plan.Centre, depths, surface);
        }

        // The ground's floor (as dug) under a cell near a puddle: from air over the water down to stone, halved to under a
        // centimetre; NaN where stone stands over the water.
        private float DugFloor(PuddlePlan plan, int2 c)
        {
            var xz = plan.Origin + (float2)c * PuddleCell;
            bool Solid(float y) => terrain.IsSolid(terrain.transform.TransformPoint(new Vector3(xz.x, y, xz.y)));
            float low = plan.Floor - 1.5f, high = plan.Level + .3f;
            if (Solid(high)) return float.NaN;
            if (!Solid(low)) return low;
            for (int i = 0; i < 8; i++)
            {
                float mid = (low + high) * .5f;
                if (Solid(mid)) low = mid; else high = mid;
            }
            return (low + high) * .5f;
        }

        private void BuildPuddles(List<PuddlePlan> plans)
        {
            if (puddleRoot == null) puddleRoot = new GameObject("Cave puddles").transform;
            if (puddleWater == null)
            {
                puddleWater = new Material(puddleMaterial) { name = "Cave puddle water", hideFlags = HideFlags.DontSave };
                // The same see-through Lit (so the same shader variant the build keeps), dark and matte: it only shades.
                puddleDepths = new Material(puddleMaterial) { name = "Cave puddle depths", hideFlags = HideFlags.DontSave };
                puddleDepths.SetColor("_BaseColor", DeepColour); puddleDepths.SetColor("_Color", DeepColour);
                puddleDepths.SetFloat("_Smoothness", 0);
            }
            foreach (var plan in plans)
            {
                var centre = new float3(plan.Centre.x, plan.Level, plan.Centre.y);
                var go = new GameObject("Cave puddle", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(puddleRoot, false);
                go.transform.SetPositionAndRotation(terrain.transform.TransformPoint((Vector3)centre), terrain.transform.rotation);
                go.GetComponent<MeshFilter>().sharedMesh = WaterMesh(plan.Centre, plan.Depths, plan.Surface);
                var body = go.GetComponent<MeshRenderer>();
                // The dark layers first, then the surface over them.
                body.sharedMaterials = new[] { puddleDepths, puddleWater };
                body.shadowCastingMode = ShadowCastingMode.Off;
                // Lit by the crystals, whose lights reach only the ground's layer. Its material is the excavation's Lit already
                // (CavernSetup), so the daylight needs no adapted copy.
                body.renderingLayerMask |= TerrainVolume.LampShadowLayer;
                var puddle = new Puddle { Body = body, Bounds = body.bounds, Plan = plan, Wet = new HashSet<int2>(plan.Wet) };
                float spread = 2 * (PuddleSpread + .3f);
                puddle.Bounds.Expand(new Vector3(spread, 2 * (plan.Level - plan.Floor + PuddleSink + .3f), spread));
                Settle(puddle, new HashSet<int2>());
                body.enabled = PuddlesShown && !puddle.Drained;
                puddles.Add(puddle);
            }
            puddlesShown = PuddlesShown;
        }

        // The water: the planned triangles (three points to a triangle) in the puddle's own frame round its centre, the dark
        // layers one submesh and the surface the next; UVs in metres for the ripple map, so every tangent runs along x (the
        // map's v along z).
        private static Mesh WaterMesh(float2 centre, List<float3> depths, List<float3> surface)
        {
            var vertices = new List<Vector3>(depths.Count + surface.Count);
            var uvs = new List<Vector2>(vertices.Capacity);
            int[] Add(List<float3> points)
            {
                var triangles = new int[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    triangles[i] = vertices.Count;
                    vertices.Add(new Vector3(point.x - centre.x, -point.y, point.z - centre.y));
                    uvs.Add(new Vector2(point.x, point.z));
                }
                return triangles;
            }
            var under = Add(depths);
            var over = Add(surface);
            var mesh = new Mesh { name = "Cave puddle", subMeshCount = 2 };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(under, 0); mesh.SetTriangles(over, 1);
            var up = new Vector3[vertices.Count]; var along = new Vector4[vertices.Count];
            for (int i = 0; i < up.Length; i++) { up[i] = Vector3.up; along[i] = new Vector4(1, 0, 0, -1); }
            mesh.normals = up;
            mesh.tangents = along;
            mesh.RecalculateBounds();
            return mesh;
        }

        // Every cave's puddles (worker thread): one in each basin.
        private static List<PuddlePlan> Plan(TerrainGround.GroundLayout layout)
        {
            var plans = new List<PuddlePlan>();
            foreach (var cave in layout.Caverns)
                for (int i = 0; i < cave.Basins.Length; i++)
                {
                    var basin = cave.Basins[i];
                    float floor = Floor(cave, basin.x, basin.y);
                    if (float.IsNaN(floor)) continue;
                    var puddle = Flood(cave, new float3(basin.x, floor, basin.y), basin.w * PuddleFill);
                    if (puddle != null) plans.Add(puddle);
                }
            return plans;
        }

        // The water round a low spot: the floor's lowest point near it, then the cells joined to it whose floor lies below
        // the water, `fill` over that point or less, so the water stays within PuddleReach of it.
        private static PuddlePlan Flood(TerrainGround.Cavern cave, float3 spot, float fill)
        {
            var heights = new Dictionary<int2, float>();
            var origin = spot.xz;
            // Stone above the water and a little over counts as a wall.
            float above = fill + .3f;
            float Height(int2 c)
            {
                if (heights.TryGetValue(c, out float h)) return h;
                var xz = origin + (float2)c * PuddleCell;
                h = Floor(cave, xz.x, xz.y, spot.y, above);
                heights.Add(c, h);
                return h;
            }
            // Walk down to the lowest floor near the spot.
            var bottom = int2.zero;
            for (int step = 0; step < 40; step++)
            {
                var next = bottom;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var c = bottom + new int2(dx, dz);
                    float h = Height(c);
                    if (!float.IsNaN(h) && !float.IsInfinity(h) && h < Height(next)) next = c;
                }
                if (next.Equals(bottom)) break;
                bottom = next;
            }
            float floor = Height(bottom);
            if (float.IsNaN(floor)) return null;
            int reach = (int)math.ceil(PuddleReach / PuddleCell);
            for (float depth = fill; depth > .015f; depth *= .85f)
            {
                float level = floor + depth;
                var region = new List<int2>();
                var seen = new HashSet<int2> { bottom };
                var queue = new Queue<int2>();
                queue.Enqueue(bottom);
                bool spills = false;
                while (queue.Count > 0 && !spills)
                {
                    var c = queue.Dequeue();
                    region.Add(c);
                    if (math.lengthsq(c - bottom) > reach * reach) { spills = true; break; }
                    foreach (var step in Steps)
                    {
                        var n = c + step;
                        if (!seen.Add(n)) continue;
                        float h = Height(n);
                        // A sudden drop: the water would run off into it.
                        if (float.IsNegativeInfinity(h)) { spills = true; break; }
                        if (!float.IsNaN(h) && h < level) queue.Enqueue(n);
                    }
                }
                // The ring of water drawn round the cells (WaterMesh) must not hang over a drop either.
                foreach (var c in region)
                    for (int dz = -1; dz <= 1 && !spills; dz++)
                    for (int dx = -1; dx <= 1 && !spills; dx++)
                        spills = float.IsNegativeInfinity(Height(c + new int2(dx, dz)));
                if (spills) continue;
                if (region.Count * PuddleCell * PuddleCell < PuddleLeast) return null;
                var plan = new PuddlePlan { Level = level, Floor = floor, Origin = origin, Bottom = bottom, Wet = region };
                float2 sum = 0;
                foreach (var c in region) sum += origin + (float2)c * PuddleCell;
                plan.Centre = sum / region.Count;
                Shoreline(plan.Surface, region, c => Height(c) - (level + PuddleShore), origin, 0);
                for (int k = 0; k < DeepWater.Length; k++)
                {
                    float deep = level - DeepWater[k];
                    Shoreline(plan.Depths, region, c => Height(c) - deep, origin, DeepStep * (k + 1));
                }
                return plan;
            }
            return null;
        }

        // The water's outline (marching squares): in every square of nodes beside the flooded ones, the part where the floor
        // lies below the water (depth below zero; stone that rises over it counts as dry), its edge where the floor meets
        // the water, so the drawn edge follows the floor's own contour instead of the grid. Added as triangles `drop` under
        // the surface.
        private static readonly int2[] SquareCorners = { new int2(0, 0), new int2(1, 0), new int2(1, 1), new int2(0, 1) };

        private static void Shoreline(List<float3> into, List<int2> region, System.Func<int2, float> depth, float2 origin, float drop)
        {
            var squares = new HashSet<int2>();
            foreach (var c in region)
                for (int dz = -1; dz <= 0; dz++)
                for (int dx = -1; dx <= 0; dx++) squares.Add(c + new int2(dx, dz));
            var values = new float[4];
            var polygon = new List<float2>(8);
            foreach (var square in squares)
            {
                bool wet = false;
                for (int k = 0; k < 4; k++)
                {
                    float v = depth(square + SquareCorners[k]);
                    values[k] = float.IsNaN(v) ? float.PositiveInfinity : v;
                    wet |= values[k] < 0;
                }
                if (!wet) continue;
                polygon.Clear();
                for (int k = 0; k < 4; k++)
                {
                    int next = (k + 1) & 3;
                    var a = origin + (float2)(square + SquareCorners[k]) * PuddleCell;
                    var b = origin + (float2)(square + SquareCorners[next]) * PuddleCell;
                    if (values[k] < 0) polygon.Add(a);
                    if ((values[k] < 0) != (values[next] < 0))
                        polygon.Add(math.lerp(a, b, math.saturate(values[k] / (values[k] - values[next]))));
                }
                // Facing up: the corners run anticlockwise seen from above, so each fan triangle is taken the other way round.
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    into.Add(new float3(polygon[0].x, drop, polygon[0].y));
                    into.Add(new float3(polygon[i + 1].x, drop, polygon[i + 1].y));
                    into.Add(new float3(polygon[i].x, drop, polygon[i].y));
                }
            }
        }

        // The floor's height under a grid-local x/z near a height (the cave's own floor when none is given): from solid
        // stone below to air above, halved to a few millimetres. NaN where no floor stands there (a pillar, outside, stone
        // standing `above` over the height or more); negative infinity where it falls well below it.
        private static float Floor(in TerrainGround.Cavern cave, float x, float z, float near = float.NaN, float above = .45f)
        {
            float low, high;
            if (float.IsNaN(near))
            {
                var (floor, roof) = TerrainGround.CavernSpan(cave, x, z);
                if (float.IsNaN(floor) || roof - floor < .6f) return float.NaN;
                low = floor - .1f; high = floor + .05f;
            }
            else { low = near - .45f; high = near + above; }
            if (TerrainGround.CavernHollow(cave, new float3(x, high, z)) >= 0) return float.NaN;
            if (TerrainGround.CavernHollow(cave, new float3(x, low, z)) < 0) return float.IsNaN(near) ? float.NaN : float.NegativeInfinity;
            for (int i = 0; i < 10; i++)
            {
                float mid = (low + high) * .5f;
                if (TerrainGround.CavernHollow(cave, new float3(x, mid, z)) < 0) high = mid; else low = mid;
            }
            return (low + high) * .5f;
        }
    }
}
