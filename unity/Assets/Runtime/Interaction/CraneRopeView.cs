using UnityEngine;

namespace SomethingDownThere
{
    // The salvage crane's cable, one rope from the trolley's sheave to the hook block (the pack's own
    // straight cable is stripped from its mesh). Hanging, it runs straight down to the block. During a
    // recovery it is the smart rope: down the hole mouth and along the dug route, with the crane's hook
    // riding its end into the lifting eye on the load.
    public sealed class CraneRopeView : MonoBehaviour, IRopeCollision
    {
        [SerializeField] private LineRenderer rope;
        [SerializeField] private TowerCraneRig rig;
        private readonly RopeDynamics dynamics = new RopeDynamics();
        private readonly Vector3[] seed = new Vector3[ExtractionSnapshot.MaximumWaypoints + 2];
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private TerrainVolume terrain;
        private SalvageRopeSettings settings;
        private Vector3[] route;
        private Matrix4x4 routeToWorld;
        private int reelSegment;
        private Vector3 reel, attachment;
        private Vector3 previousTip, currentTip, previousDirection = Vector3.up, currentDirection = Vector3.up;
        private float paidLength;
        private bool attached;
        private Collider loadCollider;
        public bool Configured => rope != null && rig != null;
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
            if (terrain.TryGetComponent<ExcavationDaylight>(out var lighting)) lighting.Register(rope);
            Hide();
            rope.enabled = true;
        }

        // Back to the hanging cable.
        public void Hide() { dynamics.Clear(); route = null; }

        // One physics step of the released hook riding the rope's free end: its seat at `tip`, its block
        // toward the rope along `direction`, eased so a hand-off from the hanging hook does not jump.
        public void Carry(Vector3 tip, Vector3 direction, float dt, bool snap)
        {
            direction = direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.up;
            float follow = snap ? 1 : 1 - Mathf.Exp(-14 * dt);
            previousTip = snap ? tip : currentTip; previousDirection = snap ? direction : currentDirection;
            currentTip = Vector3.Lerp(previousTip, tip, follow);
            currentDirection = Vector3.Slerp(previousDirection, direction, follow).normalized;
        }

        // The simulated rope pays out from the route at `anchorDistance` to `end`: the rope's free end
        // behind the riding hook, or the lifting eye once it has hooked on. Above that point it is drawn
        // straight up the route and the shaft to the trolley.
        public void Draw(Transform world, Vector3[] path, float distance, float anchorDistance, Vector3 end, bool attachedToLoad)
        {
            if (path == null || path.Length < 2) { Hide(); return; }
            Vector3 point = world.TransformPoint(ExtractionSnapshot.Point(path, distance, out int segment));
            reel = world.TransformPoint(ExtractionSnapshot.Point(path, anchorDistance, out int anchorSegment));
            routeToWorld = world.localToWorldMatrix; reelSegment = anchorSegment;
            attachment = end;
            attached = attachedToLoad;
            // The free end can sit off the route (the hook block beyond its tip): that span is paid out too.
            float available = Mathf.Max(0, anchorDistance - distance) + (attached ? 0 : Vector3.Distance(point, end));
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
            }
            rope.enabled = true;
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

        // Draws the cable and puts a released hook where it rides; the crane calls this after its late update.
        public void Render()
        {
            if (rope == null || rig == null) return;
            if (dynamics.Count < 2)
            {
                rope.positionCount = 2;
                rope.SetPosition(0, rig.Sheave); rope.SetPosition(1, rig.RopeEntry);
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
            rig.PlaceHook(tip, direction);
            HookDirection = direction;
            int above = route.Length - 1 - reelSegment;
            rope.positionCount = above + last + 2;
            rope.SetPosition(0, rig.Sheave);
            for (int i = 0; i < above; i++) rope.SetPosition(i + 1, routeToWorld.MultiplyPoint3x4(route[route.Length - 1 - i]));
            for (int i = 0; i < last; i++) rope.SetPosition(above + i + 1, dynamics.RenderPosition(i, alpha));
            rope.SetPosition(above + last + 1, rig.RopeEntry);
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
