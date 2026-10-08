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
    // glossy water that the lamps and crystals glint in. A cut into a puddle's bed takes it away at once: never an animation
    // (user: shrinking "is horrible"); a reload finds its bed dug and leaves it out. Planned on a worker thread (the shape's
    // noise is pure maths), so a new layout never hitches. The Developer admin hides them all to compare (PuddlesShown).
    public sealed partial class CavernScenery
    {
        // The water's cells, how far a puddle reaches from its lowest point at most, how much of its basin's depth the water
        // fills, the least water worth showing (m2), and how far below its bed a puddle checks for a cut.
        private const float PuddleCell = .1f, PuddleReach = 2.4f, PuddleFill = .75f, PuddleLeast = .3f, PuddleBed = .12f;
        // How far up the floor the water's edge is drawn past the shoreline, under the stone, so the rendered floor (a
        // smoothed sampling of the same shape) always meets the water rather than leaving a gap.
        private const float PuddleShore = .015f;
        // Session-only (Developer admin): whether the puddles show.
        public static bool PuddlesShown = true;
        // The water's ripples drift this far (texture repeats a second) on a copy of the material, so lamps and crystals
        // glint and shimmer in it the way water does.
        private static readonly Vector2 RippleDrift = new Vector2(.013f, .008f);
        private Material puddleWater;

        [SerializeField] private Material puddleMaterial;
        private Transform puddleRoot;
        private TerrainGround.GroundLayout puddleLayout;
        private Task<List<PuddlePlan>> planning;
        private readonly List<Puddle> puddles = new List<Puddle>();
        private bool puddlesShown = true;
        public int PuddleCount { get { int n = 0; foreach (var puddle in puddles) if (!puddle.Drained) n++; return n; } }

        // A puddle as planned: its water level and centre, its water's outline traced along the floor (triangles in the
        // puddle's own frame, level), and the bed points below it a cut has to reach to drain it.
        private sealed class PuddlePlan
        {
            public float Level;
            public float2 Centre;
            public readonly List<float2> Water = new List<float2>();
            public readonly List<float3> Bed = new List<float3>();
        }

        private sealed class Puddle
        {
            public Renderer Body;
            public Bounds Bounds;
            public float3[] Bed;
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

        // A cut that reaches a puddle's bed takes it away at once.
        private void CheckPuddles(Bounds changed)
        {
            changed.Expand(.5f);
            foreach (var puddle in puddles)
                if (!puddle.Drained && puddle.Bounds.Intersects(changed) && Dug(puddle))
                {
                    puddle.Drained = true;
                    puddle.Body.enabled = false;
                }
        }

        private bool Dug(Puddle puddle)
        {
            foreach (var bed in puddle.Bed)
                if (!terrain.IsSolid(terrain.transform.TransformPoint((Vector3)bed))) return true;
            return false;
        }

        private void BuildPuddles(List<PuddlePlan> plans)
        {
            if (puddleRoot == null) puddleRoot = new GameObject("Cave puddles").transform;
            foreach (var plan in plans)
            {
                var centre = new float3(plan.Centre.x, plan.Level, plan.Centre.y);
                var go = new GameObject("Cave puddle", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(puddleRoot, false);
                go.transform.SetPositionAndRotation(terrain.transform.TransformPoint((Vector3)centre), terrain.transform.rotation);
                var mesh = WaterMesh(plan);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var body = go.GetComponent<MeshRenderer>();
                if (puddleWater == null) puddleWater = new Material(puddleMaterial) { name = "Cave puddle water", hideFlags = HideFlags.DontSave };
                body.sharedMaterial = puddleWater;
                body.shadowCastingMode = ShadowCastingMode.Off;
                // Lit by the crystals, whose lights reach only the ground's layer. Its material is the excavation's Lit already
                // (CavernSetup), so the daylight needs no adapted copy.
                body.renderingLayerMask |= TerrainVolume.LampShadowLayer;
                var puddle = new Puddle { Body = body, Bounds = body.bounds, Bed = plan.Bed.ToArray() };
                puddle.Bounds.Expand(new Vector3(0, PuddleBed * 2 + .2f, 0));
                puddle.Drained = Dug(puddle);
                body.enabled = PuddlesShown && !puddle.Drained;
                puddles.Add(puddle);
            }
            puddlesShown = PuddlesShown;
        }

        // The water: the planned triangles (grid-local x/z, three to a triangle), level, in the puddle's own frame round its
        // centre; UVs in metres for the ripple map.
        private static Mesh WaterMesh(PuddlePlan plan)
        {
            var vertices = new List<Vector3>(plan.Water.Count); var uvs = new List<Vector2>(plan.Water.Count);
            var triangles = new int[plan.Water.Count];
            for (int i = 0; i < plan.Water.Count; i++)
            {
                var xz = plan.Water[i];
                vertices.Add(new Vector3(xz.x - plan.Centre.x, 0, xz.y - plan.Centre.y));
                uvs.Add(new Vector2(xz.x, xz.y));
                triangles[i] = i;
            }
            var mesh = new Mesh { name = "Cave puddle" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            var up = new Vector3[vertices.Count];
            for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;
            mesh.RecalculateTangents();
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
            float Height(int2 c)
            {
                if (heights.TryGetValue(c, out float h)) return h;
                var xz = origin + (float2)c * PuddleCell;
                h = Floor(cave, xz.x, xz.y, spot.y);
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
                    foreach (var step in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
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
                var plan = new PuddlePlan { Level = level };
                float2 sum = 0;
                foreach (var c in region)
                {
                    var xz = origin + (float2)c * PuddleCell;
                    sum += xz;
                    if ((c.x + c.y) % 3 == 0) plan.Bed.Add(new float3(xz.x, Height(c) - PuddleBed, xz.y));
                }
                plan.Centre = sum / region.Count;
                Shoreline(plan, region, c => Height(c) - (level + PuddleShore), origin);
                return plan;
            }
            return null;
        }

        // The water's outline (marching squares): in every square of nodes beside the flooded ones, the part where the floor
        // lies below the water (depth below zero; stone that rises over it counts as dry), its edge where the floor meets
        // the water, so the drawn edge follows the floor's own contour instead of the grid.
        private static readonly int2[] SquareCorners = { new int2(0, 0), new int2(1, 0), new int2(1, 1), new int2(0, 1) };

        private static void Shoreline(PuddlePlan plan, List<int2> region, System.Func<int2, float> depth, float2 origin)
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
                    plan.Water.Add(polygon[0]); plan.Water.Add(polygon[i + 1]); plan.Water.Add(polygon[i]);
                }
            }
        }

        // The floor's height under a grid-local x/z near a height (the cave's own floor when none is given): from solid
        // stone below to air above, halved to a few millimetres. NaN where no floor stands there (a pillar, outside, stone
        // rising well above the height); negative infinity where it falls well below it.
        private static float Floor(in TerrainGround.Cavern cave, float x, float z, float near = float.NaN)
        {
            float low, high;
            if (float.IsNaN(near))
            {
                var (floor, roof) = TerrainGround.CavernSpan(cave, x, z);
                if (float.IsNaN(floor) || roof - floor < .6f) return float.NaN;
                low = floor - .1f; high = floor + .05f;
            }
            else { low = near - .45f; high = near + .45f; }
            if (TerrainGround.CavernHollow(cave, new float3(x, high, z)) >= 0) return float.NaN;
            if (TerrainGround.CavernHollow(cave, new float3(x, low, z)) < 0) return float.IsNaN(near) ? float.NaN : float.NegativeInfinity;
            for (int i = 0; i < 9; i++)
            {
                float mid = (low + high) * .5f;
                if (TerrainGround.CavernHollow(cave, new float3(x, mid, z)) < 0) high = mid; else low = mid;
            }
            return (low + high) * .5f;
        }
    }
}
