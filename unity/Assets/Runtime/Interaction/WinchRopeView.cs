using UnityEngine;

namespace SomethingDownThere
{
    public sealed class WinchRopeView : MonoBehaviour, IRopeCollision
    {
        [SerializeField] private LineRenderer rope;
        [SerializeField] private Transform hook;
        [SerializeField] private Material markMaterial;
        private RecoveryMarkView mark;
        private readonly RopeDynamics dynamics = new RopeDynamics();
        private readonly Vector3[] seed = new Vector3[ExtractionSnapshot.MaximumWaypoints + 2];
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private TerrainVolume terrain;
        private SalvageWinchSettings settings;
        private Vector3[] route;
        private Vector3 reel, attachment;
        private float paidLength;
        private bool attached;
        private Collider loadCollider;
        public bool Configured => rope != null && hook != null && markMaterial != null;
        public bool Initialized => terrain != null && settings != null;
        public int ParticleCount => dynamics.Count;
        public Vector3 ParticlePosition(int index) => dynamics.Position(index);
        public double LastSimulationMilliseconds { get; private set; }

        private void LateUpdate() { if (rope != null && rope.enabled) Render(); }
        private void OnDestroy() => mark?.Dispose();

        internal void ShowMark(BuriedFind find, Vector3 localPoint, Vector3 normal, float progress, bool placed)
        {
            if (markMaterial == null) return;
            mark ??= new RecoveryMarkView(transform, markMaterial);
            mark.Show(find, localPoint, normal, progress, placed);
        }

        internal void HideMark() => mark?.Hide();

        public void Initialize(TerrainVolume volume, SalvageWinchSettings tuning)
        {
            if (terrain == volume && settings == tuning) return;
            terrain = volume; settings = tuning;
            if (terrain.TryGetComponent<ExcavationDaylight>(out var lighting))
            {
                lighting.Register(rope);
                foreach (var renderer in hook.GetComponentsInChildren<Renderer>()) lighting.Register(renderer);
            }
            Hide();
        }

        public void Hide()
        {
            if (rope != null) rope.enabled = false;
            if (hook != null) hook.gameObject.SetActive(false);
            dynamics.Clear(); route = null;
        }

        public void Draw(Transform world, Vector3[] path, float distance, int anchorIndex, Vector3? end = null)
        {
            if (path == null || path.Length < 3) { Hide(); return; }
            Vector3 point = ExtractionSnapshot.Point(path, distance, out int segment);
            reel = world.TransformPoint(path[anchorIndex]);
            attachment = end ?? world.TransformPoint(point);
            attached = end.HasValue;
            float available = segment < anchorIndex ? ExtractionSnapshot.Length(path, anchorIndex) - distance : 0;
            // The reel takes cable in even while the load is wedged. Adding the
            // guide/load gap here paid the spring's extension straight back out,
            // so the visible cable stayed slack while an invisible spring hauled.
            paidLength = Mathf.Max(Vector3.Distance(reel, attachment), available);
            if (route != path || dynamics.Count == 0)
            {
                int count = 0;
                seed[count++] = reel;
                if (segment < anchorIndex)
                    for (int i = anchorIndex - 1; i > segment; i--) seed[count++] = world.TransformPoint(path[i]);
                seed[count++] = attachment;
                dynamics.Seed(seed, count, settings.RopeSegmentLength);
                route = path;
            }
            rope.enabled = true; hook.gameObject.SetActive(true);
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

        private void Render()
        {
            if (dynamics.Count < 2) return;
            float alpha = Time.inFixedTimeStep ? 1 : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            rope.positionCount = dynamics.Count;
            for (int i = 0; i < dynamics.Count; i++) rope.SetPosition(i, dynamics.RenderPosition(i, alpha));
            Vector3 tip = attached ? attachment : dynamics.RenderPosition(dynamics.Count - 1, alpha);
            rope.SetPosition(0, reel); rope.SetPosition(dynamics.Count - 1, tip);
            hook.position = tip;
            Vector3 tangent = rope.GetPosition(dynamics.Count - 2) - tip;
            if (tangent.sqrMagnitude > .00001f) hook.up = tangent.normalized;
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

        private static readonly Unity.Profiling.ProfilerMarker SimulationMarker = new Unity.Profiling.ProfilerMarker("Winch.Rope");
    }
}
