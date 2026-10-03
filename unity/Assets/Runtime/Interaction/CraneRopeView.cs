using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // The salvage crane's cable: the pack's two falls from the trolley's sheaves to the hook block, drawn
    // as the pack's own strands (its straight cable is stripped from the jib mesh). Hanging, they run
    // straight down to the block. During a recovery the cable is the smart rope: down the hole mouth and
    // along the dug route, with the crane's hook riding its end into the lifting eye on the load; both
    // falls follow it, drawn together through the hole and spreading again into the block.
    public sealed class CraneRopeView : MonoBehaviour, IRopeCollision
    {
        // Sides of a strand's cross-section, as on the pack's cable.
        private const int Sides = 6;
        // Where the cable is pulled off the straight line from the trolley to the hook (by the hole's rim or
        // a tunnel's corner) the falls draw together, almost touching (their centres this many strand radii
        // from the rope's), fully once it is DeflectionTogether metres off that line; they fan out to the
        // block's rope entries over this last length (metres), and along the rope their spacing changes at
        // most this fast (metres per metre).
        private const float TogetherRadii = 1.5f, DeflectionTogether = .5f, FanLength = .6f, SpacingSlope = .4f;
        // Rope points closer than this (metres) are one point.
        private const float MergeDistance = .03f;
        [SerializeField] private MeshFilter strands;
        [SerializeField] private TowerCraneRig rig;
        // The pack's cable texture strip: u from the sheave to the hook block, v around a strand.
        [SerializeField] private Vector2 uvAlong, uvAround;
        private readonly RopeDynamics dynamics = new RopeDynamics();
        private readonly Vector3[] seed = new Vector3[ExtractionSnapshot.MaximumWaypoints + 2];
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly List<Vector3> centre = new List<Vector3>(), sides = new List<Vector3>(), strand = new List<Vector3>();
        private readonly List<float> lengths = new List<float>(), spacings = new List<float>();
        private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        private readonly List<Vector4> tangents = new List<Vector4>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private Mesh mesh;
        private int builtFrame = -1;
        private TerrainVolume terrain;
        private SalvageRopeSettings settings;
        private Vector3[] route;
        private Matrix4x4 routeToWorld;
        private int reelSegment;
        private Vector3 reel, attachment;
        private Vector3 previousTip, currentTip, previousDirection = Vector3.up, currentDirection = Vector3.up;
        // Where the falls arrive at the released hook block: its rope entries turn to lie along it.
        private Vector3 across;
        private float paidLength;
        private bool attached;
        private Collider loadCollider;
        public bool Configured => strands != null && rig != null && uvAlong != Vector2.zero;
        public bool Initialized => terrain != null && settings != null;
        public int ParticleCount => dynamics.Count;
        public Vector3 ParticlePosition(int index) => dynamics.Position(index);
        public double LastSimulationMilliseconds { get; private set; }
        // The rope's pull at the hook on the last render, from its tip toward the rope.
        public Vector3 HookDirection { get; private set; } = Vector3.up;

        public void Initialize(TerrainVolume volume, SalvageRopeSettings tuning)
        {
            if (terrain == volume && settings == tuning) return;
            terrain = volume; settings = tuning;
            if (terrain.TryGetComponent<ExcavationDaylight>(out var lighting)) lighting.Register(strands.GetComponent<Renderer>());
            Hide();
        }

        private void OnDestroy() { if (mesh != null) Destroy(mesh); }

        // Back to the hanging cable.
        public void Hide() { dynamics.Clear(); route = null; }

        // One physics step of the released hook riding the rope's free end: its seat at `tip`, its block
        // toward the rope along `direction`. It sits exactly at the rope's end (a lagging hook left the
        // cable doubling back into it); frames between physics steps interpolate like the rope.
        public void Carry(Vector3 tip, Vector3 direction, bool snap)
        {
            direction = direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.up;
            previousTip = snap ? tip : currentTip; previousDirection = snap ? direction : currentDirection;
            currentTip = tip; currentDirection = direction;
        }

        // The simulated rope pays out from the route at `anchorDistance` to `end`: the rope's free end
        // behind the riding hook, or the lifting eye once it has hooked on. Above that point it is drawn
        // straight up the route and the shaft to the trolley. While the hook is still riding down the
        // straight shaft (the anchor at or above its end) nothing is simulated: the rope hangs straight.
        public void Draw(Transform world, Vector3[] path, float distance, float anchorDistance, Vector3 end, bool attachedToLoad)
        {
            if (path == null || path.Length < 2) { Hide(); return; }
            bool straight = anchorDistance <= distance + .001f;
            Vector3 point = world.TransformPoint(ExtractionSnapshot.Point(path, distance, out int segment));
            Vector3 anchor = world.TransformPoint(ExtractionSnapshot.Point(path, anchorDistance, out int anchorSegment));
            reel = straight ? end : anchor;
            routeToWorld = world.localToWorldMatrix; reelSegment = anchorSegment;
            attachment = end;
            attached = attachedToLoad;
            // The free end can sit off the route (the hook block beyond its tip): that span is paid out too.
            float available = straight ? 0 : Mathf.Max(0, anchorDistance - distance) + (attached ? 0 : Vector3.Distance(point, end));
            // The reel takes cable in even while the load is wedged. Adding the
            // guide/load gap here paid the spring's extension straight back out,
            // so the visible cable stayed slack while an invisible spring hauled.
            paidLength = Mathf.Max(Vector3.Distance(reel, attachment), available);
            if (route != path || dynamics.Count == 0)
            {
                int count = 0;
                seed[count++] = reel;
                for (int i = anchorSegment; i > segment; i--) seed[count++] = world.TransformPoint(path[i]);
                seed[count++] = attachment;
                dynamics.Seed(seed, count, settings.RopeSegmentLength);
                route = path;
                across = rig.EntrySpread;
            }
            Render();
        }

        public void Simulate(float dt, Collider payload, float tension)
        {
            if (dynamics.Count < 2 || terrain == null || settings == null || terrain.IsRestoring) return;
            using var profile = SimulationMarker.Auto();
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            loadCollider = payload;
            float slack = attached ? settings.RopeSlack * (1 - Mathf.Clamp01(tension)) : settings.RopeSlack;
            float length = paidLength * (1 + slack);
            // Cleared corners can shorten the supported cable path before the
            // route guide passes them. Reel that spare live curve in as well.
            if (attached) length = Mathf.Min(length, dynamics.Length - settings.RopeSpeed * dt * Mathf.Clamp01(tension));
            dynamics.Step(dt, reel, attachment, length, settings.RopeSegmentLength,
                settings.RopeDamping, settings.RopeIterations, settings.RopeRadius, this, attached ? tension : 0);
            LastSimulationMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            Render();
        }

        // Puts a released hook where it rides and draws the cable (once per rendered frame); the crane
        // calls this after its late update.
        public void Render()
        {
            if (strands == null || rig == null) return;
            bool build = !Time.inFixedTimeStep && builtFrame != Time.frameCount;
            if (dynamics.Count < 2)
            {
                if (!build) return;
                // Hanging: each fall straight from its sheave to its entry into the block, as in the pack.
                centre.Clear(); centre.Add(rig.Sheave); centre.Add(rig.RopeEntry);
                Build();
                return;
            }
            float alpha = Time.inFixedTimeStep ? 1 : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            int last = dynamics.Count - 1;
            Vector3 tip, direction;
            if (attached)
            {
                // The hook hangs in the lifting eye along the rope; the cable inside its length is hidden.
                float length = rig.HookLength + RecoveryMarkView.HookReach;
                while (last > 1 && Vector3.Distance(dynamics.RenderPosition(last - 1, alpha), attachment) < length) last--;
                Vector3 toward = (last > 1 ? dynamics.RenderPosition(last - 1, alpha) : reel) - attachment;
                direction = toward.sqrMagnitude > .00001f ? toward.normalized : Vector3.up;
                tip = attachment + direction * RecoveryMarkView.HookReach;
            }
            else
            {
                tip = Vector3.Lerp(previousTip, currentTip, alpha);
                direction = Vector3.Slerp(previousDirection, currentDirection, alpha).normalized;
            }
            rig.PlaceHook(tip, direction, across);
            HookDirection = direction;
            if (!build) return;
            int above = route.Length - 1 - reelSegment;
            centre.Clear(); centre.Add(rig.Sheave);
            // Nothing in mid-air holds a bend: from the trolley the cable runs straight to the first route
            // point at or under the ground.
            float ground = terrain != null ? terrain.SurfaceHeight + .05f : float.PositiveInfinity;
            for (int i = 0; i < above; i++)
            {
                Vector3 point = routeToWorld.MultiplyPoint3x4(route[route.Length - 1 - i]);
                if (point.y <= ground) AddCentre(point);
            }
            for (int i = 0; i < last; i++) AddCentre(dynamics.RenderPosition(i, alpha));
            // The rope's end can sit right at the block's entry (the hook riding down the straight
            // shaft): a near-zero last span has no direction, so the entry replaces that point.
            if (centre.Count > 1 && (rig.RopeEntry - centre[centre.Count - 1]).sqrMagnitude < MergeDistance * MergeDistance) centre[centre.Count - 1] = rig.RopeEntry;
            else centre.Add(rig.RopeEntry);
            // The two falls never twist round each other, so the block stays square to the trolley's sheaves.
            Frame();
            across = Square(rig.SheaveSpread, direction, across);
            rig.PlaceHook(tip, direction, across);
            centre[centre.Count - 1] = rig.RopeEntry;
            Build();
        }

        // A route point where the reel sits exactly on it is drawn once.
        private void AddCentre(Vector3 point)
        {
            if ((point - centre[centre.Count - 1]).sqrMagnitude > 1e-6f) centre.Add(point);
        }

        // The falls' side-by-side direction at each point of the cable's centre line: the trolley's sheave
        // spread squared to the cable there. It never flips from frame to frame, so the falls never cross.
        private void Frame()
        {
            sides.Clear();
            Vector3 side = rig.SheaveSpread, tangent = Vector3.down;
            for (int i = 0; i < centre.Count; i++)
            {
                tangent = Tangent(centre, i, tangent);
                side = Square(rig.SheaveSpread, tangent, side);
                sides.Add(side);
            }
        }

        // `spread` made square to `axis` (unit), always on the spread's own side; where the two run alongside
        // each other there is no clear square direction, so the previous side carries on.
        private static Vector3 Square(Vector3 spread, Vector3 axis, Vector3 previous)
        {
            Vector3 side = Vector3.ProjectOnPlane(spread, axis);
            if (side.sqrMagnitude < .05f * spread.sqrMagnitude) side = Vector3.ProjectOnPlane(previous, axis);
            return side.sqrMagnitude > 1e-10f ? side.normalized : Perpendicular(axis);
        }

        // Both falls as the pack's hexagonal strands, their spacing narrowing from the trolley's sheaves to
        // the block's. A straight drop keeps them apart, as when hanging. Where the cable is pulled off the
        // straight line from the trolley to the block (the hole's rim, a tunnel's corner) they draw together
        // smoothly with how far it is pulled, run on like one doubled cable and spread again into the block.
        // Snapping them together past a bend angle flickered as the simulated rope wobbled.
        private void Build()
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "Crane cable", hideFlags = HideFlags.DontSave };
                mesh.MarkDynamic();
                strands.sharedMesh = mesh;
            }
            builtFrame = Time.frameCount;
            int count = centre.Count;
            lengths.Clear(); lengths.Add(0);
            for (int i = 1; i < count; i++) lengths.Add(lengths[i - 1] + Vector3.Distance(centre[i - 1], centre[i]));
            float total = Mathf.Max(lengths[count - 1], .0001f);
            Vector3 top = rig.SheaveSpread, bottom = rig.EntrySpread;
            // Each fall enters the block on its own side: a block turned the other way round swaps entries.
            if (count > 2 && Vector3.Dot(sides[count - 2], bottom) < 0) bottom = -bottom;
            float radius = rig.StrandRadius, together = radius * TogetherRadii;
            Vector3 start = centre[0], end = centre[count - 1];
            spacings.Clear();
            for (int i = 0; i < count; i++)
            {
                float hanging = Mathf.Lerp(top.magnitude, bottom.magnitude, lengths[i] / total);
                Vector3 nearest = start + Vector3.Project(centre[i] - start, end - start);
                float pulled = Mathf.Clamp01(Vector3.Distance(centre[i], nearest) / DeflectionTogether);
                float spacing = Mathf.Lerp(hanging, together, pulled * pulled * (3 - 2 * pulled));
                spacings.Add(Mathf.Lerp(bottom.magnitude, spacing, Mathf.Clamp01((total - lengths[i]) / FanLength)));
            }
            for (int i = 2; i < count - 1; i++) spacings[i] = Mathf.Min(spacings[i], spacings[i - 1] + SpacingSlope * (lengths[i] - lengths[i - 1]));
            for (int i = count - 3; i > 0; i--) spacings[i] = Mathf.Min(spacings[i], spacings[i + 1] + SpacingSlope * (lengths[i + 1] - lengths[i]));
            vertices.Clear(); normals.Clear(); tangents.Clear(); uvs.Clear(); triangles.Clear();
            var toLocal = transform.worldToLocalMatrix;
            for (int fall = 0; fall < 2; fall++)
            {
                float sign = fall == 0 ? 1 : -1;
                strand.Clear();
                strand.Add(centre[0] + top * sign);
                for (int i = 1; i < count - 1; i++) strand.Add(centre[i] + sides[i] * (spacings[i] * sign));
                strand.Add(centre[count - 1] + bottom * sign);
                Tube(total, radius, toLocal);
            }
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
        }

        // One strand along `strand`, its rings carried along without twisting.
        private void Tube(float total, float radius, Matrix4x4 toLocal)
        {
            int start = vertices.Count, count = strand.Count;
            Vector3 forward = Tangent(strand, 0, Vector3.down);
            Vector3 across0 = Perpendicular(forward);
            for (int i = 0; i < count; i++)
            {
                Vector3 next = Tangent(strand, i, forward);
                across0 = Vector3.ProjectOnPlane(Quaternion.FromToRotation(forward, next) * across0, next);
                across0 = across0.sqrMagnitude > 1e-8f ? across0.normalized : Perpendicular(next);
                forward = next;
                Vector3 up = Vector3.Cross(forward, across0);
                float u = Mathf.Lerp(uvAlong.x, uvAlong.y, lengths[i] / total);
                for (int k = 0; k <= Sides; k++)
                {
                    float angle = k * Mathf.PI * 2 / Sides;
                    Vector3 radial = across0 * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    vertices.Add(toLocal.MultiplyPoint3x4(strand[i] + radial * radius));
                    normals.Add(toLocal.MultiplyVector(radial));
                    Vector3 along = toLocal.MultiplyVector(forward);
                    tangents.Add(new Vector4(along.x, along.y, along.z, 1));
                    uvs.Add(new Vector2(u, Mathf.Lerp(uvAround.x, uvAround.y, k / (float)Sides)));
                }
                if (i == 0) continue;
                int ring = start + i * (Sides + 1), previous = ring - (Sides + 1);
                for (int k = 0; k < Sides; k++)
                {
                    triangles.Add(previous + k); triangles.Add(previous + k + 1); triangles.Add(ring + k);
                    triangles.Add(previous + k + 1); triangles.Add(ring + k + 1); triangles.Add(ring + k);
                }
            }
        }

        private static Vector3 Tangent(List<Vector3> line, int i, Vector3 fallback)
        {
            Vector3 d = line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)];
            return d.sqrMagnitude > 1e-6f ? d.normalized : fallback;
        }

        private static Vector3 Perpendicular(Vector3 direction)
        {
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < .9f ? Vector3.up : Vector3.right);
            return side.normalized;
        }

        public Vector3 Project(Vector3 from, Vector3 point, float radius)
        {
            Vector3 motion = point - from;
            float length = motion.magnitude;
            if (length > .003f)
            {
                int count = Physics.SphereCastNonAlloc(from, radius, motion / length, hits, length,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float nearest = length;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (hit.collider == loadCollider || hit.collider is CharacterController) continue;
                    if (hit.distance <= 0 || hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    point = hit.point + hit.normal * (radius + .003f);
                }
            }
            // Density updates immediately with digging and resolves tiny overlaps
            // that a mesh sweep can miss. It never removes soil for the rope.
            float sample = terrain.CellSize * .35f;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float density = terrain.SignedDensity(point);
                if (density <= -radius) break;
                Vector3 gradient = new Vector3(
                    terrain.SignedDensity(point + Vector3.right * sample) - terrain.SignedDensity(point - Vector3.right * sample),
                    terrain.SignedDensity(point + Vector3.up * sample) - terrain.SignedDensity(point - Vector3.up * sample),
                    terrain.SignedDensity(point + Vector3.forward * sample) - terrain.SignedDensity(point - Vector3.forward * sample)) / (2 * sample);
                float slope = gradient.magnitude;
                if (slope < .05f) { if (terrain.SignedDensity(from) < -radius) point = from; break; }
                point -= gradient / slope * Mathf.Min(terrain.CellSize, (density + radius + .003f) / slope);
            }
            return point;
        }

        private static readonly Unity.Profiling.ProfilerMarker SimulationMarker = new Unity.Profiling.ProfilerMarker("Crane.Rope");
    }
}
