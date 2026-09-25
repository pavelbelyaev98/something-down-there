using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    public readonly struct DiscoveryReservation
    {
        public readonly Vector3 Position; public readonly float Radius;
        public DiscoveryReservation(Vector3 position, float radius) { Position = position; Radius = radius; }
    }

    public readonly struct DiscoveryPlacement
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly int PrefabIndex;
        public readonly int AppearanceIndex;
        public DiscoveryPlacement(Vector3 position, Quaternion rotation, int prefabIndex, int appearanceIndex = 0)
        { Position = position; Rotation = rotation; PrefabIndex = prefabIndex; AppearanceIndex = appearanceIndex; }
    }

    [DisallowMultipleComponent]
    public sealed class DiscoveryField : MonoBehaviour
    {
        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private BuriedFind[] prefabs;
        [SerializeField] private DiscoveryCatalog catalog;
        public DiscoveryCatalog Catalog => catalog;
        [SerializeField] private int seed = 90127;
        [SerializeField, Range(1, MaximumPopulation)] private int count = 96;
        [Tooltip("Keep the user-requested simple review shapes out of release gameplay until final-art acceptance.")]
        [SerializeField] private bool developmentContent = true;
        private readonly List<BuriedFind> finds = new List<BuriedFind>();
        private bool initialized;
        private bool generationDeferred;
        private Camera xrayCamera;
        private readonly Collider[] changedFinds = new Collider[256];
        public IReadOnlyList<BuriedFind> Finds => finds;
        public int Seed => seed;
        public long MotionRevision { get; private set; }
        public long PopulationRevision { get; private set; }
        public Collider PlayerCollider { get; private set; }
        internal void NotifyMotion() => MotionRevision++;
        public bool Initialized => initialized || (developmentContent && !FpsPlayer.AdminBuild);
        public void DeferGeneration() => generationDeferred = true;
        public BuriedFind Find(string id) => finds.Find(f => f.Item.InstanceId == id);
        public BuriedFind StoredUnique => finds.Find(f => f.State == FindState.Stored);

        public FindSnapshot[] Capture()
        {
            var states = new FindSnapshot[finds.Count];
            for (int i = 0; i < states.Length; i++) states[i] = finds[i].Capture();
            return states;
        }

        public void ValidateRestore(FindSnapshot[] states)
        {
            if (catalog != null)
            {
                catalog.Validate();
                foreach (var state in states)
                {
                    var source = catalog.Resolve(state.ContentId);
                    if(source.Kind!=state.Item.Kind)
                        throw new System.IO.InvalidDataException("Saved find policy differs from current content.");
                }
                foreach(var entry in catalog.Entries)
                    if(entry.Prefab.Kind==DiscoveryKind.Unique && Array.FindAll(states,s=>s.ContentId==entry.Prefab.SaveContentId).Length!=1)
                        throw new System.IO.InvalidDataException("The saved discoveries differ from current content. Start a New Game.");
                return;
            }
            if (developmentContent && !FpsPlayer.AdminBuild && states.Length > 0)
                throw new System.IO.InvalidDataException("This save uses development discoveries unavailable in this build.");
            foreach (var state in states)
                if (Array.Find(prefabs, p => p != null && p.SaveContentId == state.ContentId) == null)
                    throw new System.IO.InvalidDataException("This save needs discovery content missing from this game version.");
        }

        public void Restore(FindSnapshot[] states, int savedSeed)
        {
            ValidateRestore(states);
            foreach (var find in finds) { find.gameObject.SetActive(false); Destroy(find.gameObject); }
            finds.Clear();
            seed = savedSeed;
            foreach (var state in states)
            {
                var prefab = catalog != null ? catalog.Resolve(state.ContentId) : Array.Find(prefabs, p => p.SaveContentId == state.ContentId);
                var find = Instantiate(prefab, transform);
                find.Initialize(terrain, state.Item.Id, this);
                find.Restore(state);
                find.name = state.Item.Name + " " + state.Item.Id;
                finds.Add(find);
            }
            initialized = true;
            PopulationRevision++;
        }

        private void OnEnable()
        {
            PlayerCollider = transform.root.GetComponentInChildren<CharacterController>();
            if (terrain != null) terrain.Changed += HandleExcavationChanged;
            if (initialized) foreach (var find in finds) find.RefreshExposure();
        }

        private void OnDisable()
        {
            if (terrain != null) terrain.Changed -= HandleExcavationChanged;
            SetXray(false, null);
        }

        public void SetXray(bool enabled, Camera camera)
        {
            xrayCamera = enabled ? camera : null;
            if (terrain != null) terrain.SetXray(xrayCamera != null);
            UpdateXrayVisibility();
        }

        private void LateUpdate()
        {
            if (xrayCamera != null) UpdateXrayVisibility();
        }

        private void UpdateXrayVisibility()
        {
            // Keep the debug view local, like the former markers: rendering the
            // entire deep population would submit thousands of detailed meshes.
            Vector3 eye = xrayCamera != null ? xrayCamera.transform.position : Vector3.zero;
            foreach (var find in finds)
                if (find != null) find.SetXrayVisible(xrayCamera != null && (find.transform.position - eye).sqrMagnitude <= 18f * 18f);
        }

        private void Start()
        {
            if (!generationDeferred) InitializePopulation();
        }

        public void InitializePopulation()
        {
            if (initialized) return;
            if (developmentContent && !FpsPlayer.AdminBuild) { gameObject.SetActive(false); return; }
            if (terrain == null || (catalog == null && (prefabs == null || prefabs.Length != 3 || Array.Exists(prefabs, p => p == null))))
            {
                Debug.LogError("Discoveries require terrain and three find prefabs.", this);
                enabled = false;
                return;
            }
            var extent = (Vector3)terrain.Dimensions * terrain.CellSize;
            var placements = catalog != null ? catalog.Generate(extent, seed) : Generate(extent, count, seed);
            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                var source = catalog != null ? catalog.Entries[placement.PrefabIndex].Appearance(placement.AppearanceIndex) : prefabs[placement.PrefabIndex];
                var find = Instantiate(source, terrain.transform.TransformPoint(placement.Position),
                    terrain.transform.rotation * placement.Rotation, transform);
                find.Initialize(terrain, $"find-{seed}-{i:D3}", this);
                find.name = find.Item.DisplayName + " " + i;
                finds.Add(find);
            }
            initialized = true;
            PopulationRevision++;
        }

        private void HandleExcavationChanged(Bounds changed)
        {
            using var profile = ExposureMarker.Auto();
            // Every world find retains its collider even while soil hides its mesh.
            // Reuse PhysX's spatial index instead of reading thousands of renderer
            // bounds for every local cut. Synchronize moved/restored finds first.
            int count;
            using (QueryMarker.Auto())
            {
                Physics.SyncTransforms();
                count = Physics.OverlapBoxNonAlloc(changed.center, changed.extents, changedFinds,
                    Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Collide);
            }
            if (count == changedFinds.Length)
            {
                // Full terrain reset/restore can cover the whole population. Never
                // silently lose notifications when a bounded local query overflows.
                foreach (var find in finds)
                    if (!find.Collected && changed.Intersects(find.WorldBounds)) find.RefreshExposure();
                return;
            }
            for (int i = 0; i < count; i++)
                if (changedFinds[i].TryGetComponent<BuriedFind>(out var find) && find.GetComponentInParent<DiscoveryField>() == this
                    && !find.Collected) find.RefreshExposure();
        }

        private static readonly Unity.Profiling.ProfilerMarker ExposureMarker = new Unity.Profiling.ProfilerMarker("Discovery.TerrainChanged");
        private static readonly Unity.Profiling.ProfilerMarker QueryMarker = new Unity.Profiling.ProfilerMarker("Discovery.BoundsQuery");

        // Separate deterministic stream from excavation. The enlarged starter forms
        // fit within 0.5 m of their centers in every rotation, with soil between them.
        public const float MinimumSpacing = 1.15f;
        public const float MaximumFindRadius = 0.5f;
        public const float SoilClearance = 0.10f;
        // Denser buried layers retain a gap between full-size enclosing spheres.
        // The accepted turf layout keeps its original clearance and random stream.
        public const float BandedSoilClearance = 0.03f;
        // Safety bound for saves, snapshot validation and the serialized count range.
        // The authored site population lives in the discovery catalog.
        public const int MaximumPopulation = 16384;
        public static DiscoveryPlacement[] Generate(Vector3 extent, int total, int placementSeed)
            => Generate(extent, total, placementSeed, Math.Min(total, 24));

        public static DiscoveryPlacement[] Generate(Vector3 extent, int total, int placementSeed, int shallowCount)
            => Generate(extent, total, placementSeed, shallowCount, null);

        // Catalog placements use the enclosing radius of each approved mesh. A small
        // bottle does not need the same empty soil envelope as a large rock.
        public static DiscoveryPlacement[] Generate(Vector3 extent, int total, int placementSeed, int shallowCount, float[] radii)
            => Generate(extent, total, placementSeed, shallowCount, radii, null);

        public static DiscoveryPlacement[] Generate(Vector3 extent, int total, int placementSeed, int shallowCount, float[] radii, Vector2[] depthBands)
            => Generate(extent, total, placementSeed, shallowCount, radii, depthBands, null);

        // A footprint (grid-local XZ) limits candidates to where the player can dig; see SiteLayout.
        public static DiscoveryPlacement[] Generate(Vector3 extent, int total, int placementSeed, int shallowCount, float[] radii, Vector2[] depthBands, Vector2[] shallowCovers, DiscoveryReservation[] reserved = null, Func<Vector2, bool> footprint = null)
        {
            if (!ExcavationGrid.Finite(extent.x) || !ExcavationGrid.Finite(extent.y) || !ExcavationGrid.Finite(extent.z)
                || extent.x < 8 || extent.y < 4 || extent.z < 8 || total < 1 || total > MaximumPopulation
                || shallowCount < 0 || shallowCount > total)
                throw new ArgumentOutOfRangeException(nameof(total), "Use a site at least 8 x 4 x 8 m and a supported discovery population.");
            if (radii != null && (radii.Length != total || Array.Exists(radii, r => !ExcavationGrid.Finite(r) || r <= 0 || r > MaximumFindRadius)))
                throw new ArgumentOutOfRangeException(nameof(radii));
            if (depthBands != null && (depthBands.Length != total || Array.Exists(depthBands, b =>
                !ExcavationGrid.Finite(b.x) || !ExcavationGrid.Finite(b.y) || b.x < 0 || b.y < 0
                || (b.x > 0 && b.y == 0) || (b.y > 0 && b.y <= b.x))))
                throw new ArgumentOutOfRangeException(nameof(depthBands));
            if (shallowCovers != null && (shallowCovers.Length != total || Array.Exists(shallowCovers, b =>
                !ExcavationGrid.Finite(b.x) || !ExcavationGrid.Finite(b.y) || b.x < 0 || b.y < 0
                || (b.y == 0 && b.x != 0) || (b.y > 0 && (b.x < .01f || b.y <= b.x)))))
                throw new ArgumentOutOfRangeException(nameof(shallowCovers));
            var random = new System.Random(placementSeed);
            var result = new DiscoveryPlacement[total];
            var targetDepths = DepthTargets(depthBands, shallowCount, extent.y, placementSeed);
            // Neighbourhood index: clearance is answered from the immediate cells, the
            // best-candidate spread metric from the closest occupied shell around them.
            var grid = new PlacementGrid(extent, MinimumSpacing + .001f, total + (reserved?.Length ?? 0));
            if (reserved != null) for (int r = 0; r < reserved.Length; r++) grid.Add(total + r, reserved[r].Position, reserved[r].Radius);
            float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            for (int i = 0; i < total; i++)
            {
                bool placed = false;
                bool shallow = i < shallowCount;
                bool banded = !shallow && depthBands != null && depthBands[i].y > 0;
                float minDepth = banded ? depthBands[i].x : 0;
                float maxDepth = banded ? Mathf.Min(depthBands[i].y, extent.y - .8f) : 0;
                if (banded && (minDepth < (radii == null ? MaximumFindRadius : radii[i]) + SoilClearance || maxDepth <= minDepth))
                    throw new ArgumentOutOfRangeException(nameof(depthBands), "The mineral depth band does not fit inside this site.");
                float bestDistance = -1;
                Vector3 best = default;
                // Choose a target depth before ranking lateral coverage. A small local
                // window lets dense layers fit without pushing finds metres down a band.
                float bandDepth = banded ? targetDepths[i] : 0;
                // Best of 64 candidates spreads encounters without rows or a fixed route.
                // Reserve the required soil envelope from the first placement; spending
                // extra clearance early can leave the final shallow finds without room.
                int maxAttempts = radii == null ? 2000 : 4000;
                // A packed entry carpet accepts the best of a few clear candidates; wide
                // banded types keep the 64-candidate spread so no band reads as a recipe.
                int refinement = shallow ? 8 : 64;
                int outside = 0;
                for (int attempt = 0; attempt < maxAttempts && (!placed || ((shallow || banded) && attempt < refinement)); attempt++)
                {
                    // The first finds lie a few metres in from the camp side, measured from the site centre.
                    float x = i < 6 ? Range(extent.x * 0.5f - 2.5f, extent.x * 0.5f + 2.5f) : Range(0.8f, extent.x - 0.8f);
                    float near = Mathf.Max(1, extent.z * .5f - 11);
                    // The catalog covers the whole layer; the legacy three-prefab
                    // validation field keeps its small entrance allocation.
                    float z = i < 6 ? Range(near, near + 2.5f) : radii == null && i < Math.Min(shallowCount, 48)
                        ? Range(0.8f, 6) : Range(0.8f, extent.z - 0.8f);
                    // Candidates outside the footprint are redrawn rather than spent as attempts.
                    if (footprint != null && !footprint(new Vector2(x, z)))
                    {
                        if (++outside < maxAttempts * 8) attempt--;
                        continue;
                    }
                    // Authored cover puts the entry layer just under the turf, measured
                    // above the true mesh envelope. Legacy catalogs keep their two tiers.
                    float radius = radii == null ? MaximumFindRadius : radii[i];
                    float envelope = radius + SoilClearance;
                    bool covered = shallow && shallowCovers != null && shallowCovers[i].y > 0;
                    float depth = covered ? radius + Range(shallowCovers[i].x, shallowCovers[i].y)
                        : shallow ? (random.NextDouble() < .8 ? Range(envelope + .02f, envelope + .2f)
                        : Range(envelope + .2f, envelope + .4f))
                        : banded ? Range(Mathf.Max(minDepth, bandDepth - .15f), Mathf.Min(maxDepth, bandDepth + .15f))
                        : i < shallowCount + 36 ? Range(1.2f, Mathf.Min(3.5f, extent.y - 0.8f))
                        : Range(2.5f, extent.y - 0.8f);
                    var position = new Vector3(x, extent.y - depth, z);
                    grid.Evaluate(position, radii == null ? -1f : radii[i], banded, out bool clear, out float nearest);
                    if (!clear || nearest <= bestDistance) continue;
                    placed = true;
                    bestDistance = nearest;
                    best = position;
                }
                if (!placed) throw new InvalidOperationException($"The discovery density is too high for this site (placement {i + 1}/{total}, seed {placementSeed}, depth {bandDepth:F2}).");
                grid.Add(i, best, radii == null ? MaximumFindRadius : radii[i]);
                result[i] = new DiscoveryPlacement(best, Quaternion.Euler(Range(0, 360), Range(0, 360), Range(0, 360)), i % 3);
            }
            return result;
        }

        private static float[] DepthTargets(Vector2[] bands, int shallow, float height, int seed)
        {
            if (bands == null) return null;
            var targets = new float[bands.Length];
            var groups = new Dictionary<Vector2, List<int>>();
            var order = new List<Vector2>();
            for (int i = shallow; i < bands.Length; i++)
            {
                if (bands[i].y <= 0) continue;
                if (!groups.TryGetValue(bands[i], out var group))
                {
                    groups.Add(bands[i], group = new List<int>());
                    order.Add(bands[i]);
                }
                group.Add(i);
            }
            // A shuffled stratified sample keeps depth quotas evenly spread without
            // creating visible rows. Its own stream leaves the accepted turf untouched.
            var random = new System.Random(unchecked(seed ^ 0x2DA341B));
            foreach (var band in order)
            {
                var indices = groups[band];
                for (int i = indices.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1), value = indices[i];
                    indices[i] = indices[j]; indices[j] = value;
                }
                for (int i = 0; i < indices.Count; i++)
                    targets[indices[i]] = Mathf.Lerp(band.x, Mathf.Min(band.y, height - .8f),
                        (i + (float)random.NextDouble()) / indices.Count);
            }
            return targets;
        }

        // Deterministic uniform buckets replace the old all-pairs scan. The cell edge
        // covers the largest soil envelope any two finds can require, so a clearance
        // conflict is always inside the +-1 ring; ranking only needs a local window.
        private sealed class PlacementGrid
        {
            // Nearest-neighbour search stops after this many cells: every candidate
            // with no neighbour inside the window scores the same distant value.
            private const int MaximumRing = 5;
            private readonly Vector3[] positions;
            private readonly float[] radii;
            private readonly int[] head, next;
            private readonly int width, height, depth;
            private readonly float cell, distantSquared;
            private float largestRadius;
            private readonly List<int> largeReservations = new List<int>();

            public PlacementGrid(Vector3 extent, float cellSize, int capacity)
            {
                cell = cellSize;
                distantSquared = (cellSize * MaximumRing) * (cellSize * MaximumRing);
                width = Mathf.Max(1, Mathf.CeilToInt(extent.x / cellSize));
                height = Mathf.Max(1, Mathf.CeilToInt(extent.y / cellSize));
                depth = Mathf.Max(1, Mathf.CeilToInt(extent.z / cellSize));
                head = new int[width * height * depth];
                for (int i = 0; i < head.Length; i++) head[i] = -1;
                next = new int[capacity];
                positions = new Vector3[capacity];
                radii = new float[capacity];
            }

            public void Add(int index, Vector3 position, float radius)
            {
                positions[index] = position;
                radii[index] = radius;
                // A rare authored load must not enlarge every common-find bucket scan.
                if (radius > MaximumFindRadius) { largeReservations.Add(index); return; }
                largestRadius = Mathf.Max(largestRadius, radius);
                int slot = Slot(position);
                next[index] = head[slot];
                head[slot] = index;
            }

            // radius < 0 marks the legacy uniform three-prefab path. The nearest
            // neighbour decides how spread out the candidate sits; the lowest cell
            // shells hold both the soil-envelope conflict test and, in dense ground,
            // the true nearest find, so the metric tracks the old all-pairs scan.
            public void Evaluate(Vector3 position, float radius, bool banded, out bool clear, out float nearest)
            {
                clear = true;
                nearest = distantSquared;
                foreach (int other in largeReservations)
                {
                    float spacing = Mathf.Max(0, radius) + radii[other] + (banded ? BandedSoilClearance : SoilClearance);
                    if ((positions[other] - position).sqrMagnitude < spacing * spacing) { clear = false; return; }
                }
                int cx = Mathf.Clamp((int)(position.x / cell), 0, width - 1);
                int cy = Mathf.Clamp((int)(position.y / cell), 0, height - 1);
                int cz = Mathf.Clamp((int)(position.z / cell), 0, depth - 1);
                int clearanceRing = Mathf.Max(1, Mathf.CeilToInt((Mathf.Max(0, radius) + largestRadius + SoilClearance) / cell));
                float best = Scan(position, radius, banded, cx, cy, cz, clearanceRing, 0, out bool found, out clear);
                if (!clear) return;
                if (found) { nearest = best; return; }
                for (int ring = clearanceRing + 1; ring <= MaximumRing; ring++)
                {
                    best = Scan(position, radius, banded, cx, cy, cz, ring, ring, out found, out clear);
                    if (!clear) return;
                    if (found) { nearest = best; return; }
                }
            }

            // Visits the cells whose Chebyshev distance from the candidate sits in
            // [inner, outer]; returns the nearest metric found in that shell.
            private float Scan(Vector3 position, float radius, bool banded, int cx, int cy, int cz,
                int outer, int inner, out bool found, out bool clear)
            {
                float best = distantSquared;
                found = false;
                clear = true;
                for (int x = Mathf.Max(0, cx - outer); x <= Mathf.Min(width - 1, cx + outer); x++)
                for (int y = Mathf.Max(0, cy - outer); y <= Mathf.Min(height - 1, cy + outer); y++)
                for (int z = Mathf.Max(0, cz - outer); z <= Mathf.Min(depth - 1, cz + outer); z++)
                {
                    int distance = Mathf.Max(Mathf.Abs(x - cx), Mathf.Max(Mathf.Abs(y - cy), Mathf.Abs(z - cz)));
                    if (distance < inner) continue;
                    for (int other = head[(x * height + y) * depth + z]; other >= 0; other = next[other])
                    {
                        var delta = positions[other] - position;
                        if (radius < 0 ? distance <= 1 : true)
                        {
                            float spacing = radius < 0 ? MinimumSpacing : radius + radii[other] + (banded ? BandedSoilClearance : SoilClearance);
                            if (delta.sqrMagnitude < spacing * spacing) { clear = false; return best; }
                        }
                        found = true;
                        float metric = banded ? delta.sqrMagnitude : delta.x * delta.x + delta.z * delta.z;
                        if (metric < best) best = metric;
                    }
                }
                return best;
            }

            private int Slot(Vector3 position)
            {
                int x = Mathf.Clamp((int)(position.x / cell), 0, width - 1);
                int y = Mathf.Clamp((int)(position.y / cell), 0, height - 1);
                int z = Mathf.Clamp((int)(position.z / cell), 0, depth - 1);
                return (x * height + y) * depth + z;
            }
        }
    }
}
