using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SomethingDownThere
{
    [DisallowMultipleComponent]
    public sealed class TerrainVolume : MonoBehaviour, IDigTarget
    {
        [SerializeField] private Vector3Int dimensions = new Vector3Int(192, 96, 192);
        [SerializeField, Min(0.1f)] private float cellSize = 0.125f;
        [SerializeField, Range(2, 24)] private int chunkSize = 16;
        // The starter bite is owned by the tool ladder, never by this scene: a stale
        // serialized copy here used to survive shovel retuning.
        private float digRadius = ShovelProfile.Defaults()[0].Radius;
        [SerializeField, Range(0f, 0.15f)] private float scoopVariation = 0.12f;
        [SerializeField, Range(0f, 0.08f)] private float scoopDepthVariation = 0.05f;
        [SerializeField] private int excavationSeed = 2718;
        [SerializeField] private Material soilMaterial;
        [SerializeField] private GameObject untouchedPreview;

        private sealed class Chunk
        {
            public Mesh Mesh;
            public MeshCollider Collider;
            public MeshRenderer Renderer;
            public readonly TerrainChunkMesh.DensityCache DensityCache = new TerrainChunkMesh.DensityCache();
            public Action BeforeMeshWrite;
            public void DetachCollider() => Collider.sharedMesh = null;
        }
        private readonly Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
        private ExcavationGrid grid;
        private readonly TerrainChunkMesh.Workspace meshing = new TerrainChunkMesh.Workspace();
        private readonly TerrainExposureSampler exposure = new TerrainExposureSampler();
        private Transform chunkRoot;
        private Material xrayMaterial;
        public bool XrayEnabled { get; private set; }

        public void SetXray(bool enabled)
        {
            if (XrayEnabled == enabled) return;
            XrayEnabled = enabled;
            if (enabled && xrayMaterial == null && soilMaterial != null)
            {
                xrayMaterial = new Material(soilMaterial) { name = "Transparent excavation ground", hideFlags = HideFlags.DontSave };
                xrayMaterial.SetFloat("_GroundOpacity", .12f);
                xrayMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                xrayMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                xrayMaterial.SetFloat("_ZWrite", 0);
                xrayMaterial.SetOverrideTag("RenderType", "Transparent");
                xrayMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                xrayMaterial.SetShaderPassEnabled("ShadowCaster", false);
                xrayMaterial.SetShaderPassEnabled("DepthOnly", false);
                xrayMaterial.SetShaderPassEnabled("DepthNormalsOnly", false);
            }
            foreach (var chunk in chunks.Values) chunk.Renderer.sharedMaterial = CurrentSoilMaterial;
            if (TryGetComponent<ExcavationDaylight>(out var daylight)) daylight.RefreshShaderState();
        }

        private Material CurrentSoilMaterial => XrayEnabled && xrayMaterial != null ? xrayMaterial : soilMaterial;
        public Vector3Int Dimensions => dimensions;
        public float CellSize => cellSize;
        public float RemovedVolume => grid?.RemovedVolume ?? 0;
        public float LastRemovedVolume => grid?.LastRemovedVolume ?? 0;
        public float LastDetachedVolume => grid?.LastDetachedVolume ?? 0;
        public int LastDetachedSamples => grid?.LastDetachedSamples ?? 0;
        public int LastSupportVisitedSamples => grid?.LastSupportVisitedSamples ?? 0;
        public int LastRemnantSamples => grid?.LastRemnantSamples ?? 0;
        public float LastRemnantVolume => grid?.LastRemnantVolume ?? 0;
        public int LastRemnantCheckedSamples => grid?.LastRemnantCheckedSamples ?? 0;
        public float SurfaceHeight => transform.TransformPoint(Vector3.up * dimensions.y * cellSize).y;
        // Kept for existing session diagnostics; volume in m3 is the smooth terrain metric.
        public int RemainingCells => dimensions.x * dimensions.y * dimensions.z
            - Mathf.RoundToInt(RemovedVolume / (cellSize * cellSize * cellSize));
        public int Revision => grid?.Revision ?? 0;
        public long StateRevision { get; private set; }
        public int ExcavationSeed => excavationSeed;
        public int ChunkCount => chunks.Count;
        // Every chunk the volume could ever own; only touched ones hold objects and meshes.
        public int ChunkKeyCount => ((dimensions.x + chunkSize - 1) / chunkSize)
            * ((dimensions.y + chunkSize - 1) / chunkSize) * ((dimensions.z + chunkSize - 1) / chunkSize);
        public int LastRebuiltChunkCount { get; private set; }
        public double LastDigMilliseconds { get; private set; }
        public double LastGridMilliseconds { get; private set; }
        public double LastMeshMilliseconds { get; private set; }
        public double LastDiscoveryMilliseconds { get; private set; }
        public long SnapshotCopiedBytes => grid?.SnapshotCopiedBytes ?? 0;
        public event Action<Bounds> Changed;
        public event Action<TerrainCutFeedback> ToolCut;
        public bool CanDig => isActiveAndEnabled && grid != null;
        public bool IsRestoring { get; private set; }
        public string DigPrompt => "";
        public float DigRadius
        {
            get => digRadius;
            set
            {
                if (!ExcavationGrid.Finite(value) || value < cellSize || value > 4f)
                    throw new ArgumentOutOfRangeException(nameof(value));
                digRadius = value;
            }
        }

        // Author before activation. Runtime state is initialized once, never on a checkpoint/enable.
        public void Configure(Vector3Int size, float metersPerCell, int cellsPerChunk, float radius,
            Material material, GameObject preview = null)
        {
            if (grid != null) throw new InvalidOperationException("Cannot reconfigure an excavation session.");
            if (cellsPerChunk < 2 || cellsPerChunk > 24) throw new ArgumentOutOfRangeException(nameof(cellsPerChunk));
            ExcavationGrid.ValidateDimensions(size, metersPerCell);
            dimensions = size;
            cellSize = metersPerCell;
            chunkSize = cellsPerChunk;
            DigRadius = radius;
            soilMaterial = material;
            untouchedPreview = preview;
        }

        private void Awake() => InitializeSession();

        public void InitializeSession()
        {
            if (grid != null) return;
            if ((transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("TerrainVolume requires unit scale; configure its dimensions instead.");
            grid = new ExcavationGrid(dimensions, cellSize, excavationSeed);
            if (untouchedPreview != null) untouchedPreview.SetActive(false);
            chunkRoot = new GameObject("Chunks").transform;
            chunkRoot.SetParent(transform, false);
            // Only the top layer owns geometry in untouched ground: the ground plane.
            // Interior chunks are created when a cut reaches them, so a 100 m volume
            // costs the same as a 32 m one.
            int surfaceLayer = (dimensions.y - 1) / chunkSize;
            for (int z = 0; z < dimensions.z; z += chunkSize)
            for (int x = 0; x < dimensions.x; x += chunkSize)
                Refresh(new Vector3Int(x / chunkSize, surfaceLayer, z / chunkSize));
        }

        public bool IsSolid(Vector3 worldPoint) => grid != null && grid.IsSolid(transform.InverseTransformPoint(worldPoint));
        public float SignedDensity(Vector3 worldPoint) => grid != null ? grid.Sample(transform.InverseTransformPoint(worldPoint)) : 0;
        public TerrainMaterialId MaterialAt(Vector3 worldPoint) => grid.MaterialAt(transform.InverseTransformPoint(worldPoint));
        public TerrainMaterialId ToolMaterialAt(RaycastHit hit)
        {
            Vector3 surface = transform.InverseTransformPoint(hit.point);
            Vector3 normal = transform.InverseTransformDirection(hit.normal).normalized;
            RefineContact(ref surface, normal);
            return grid.MaterialAt(surface - normal * (cellSize * .5f));
        }
        internal bool IsSolidLocal(Vector3 point) => grid != null && grid.IsSolid(point);
        internal float SignedDensityLocal(Vector3 point) => grid != null ? grid.Sample(point) : 0;
        internal float MeasureExposure(Vector3[] samples,Bounds hull,Matrix4x4 localToTerrain)
            => grid==null?1:exposure.Measure(grid,samples,hull,localToTerrain);

        // Conservative visibility, independent of sparse find exposure samples. Pristine
        // soil wholly enclosing a mesh cannot show it. Any nearby modified sample wakes
        // its renderer, including slivers between exposure samples and interpolation seams.
        internal bool MayExpose(Bounds worldBounds)
        {
            if (grid == null) return true;
            var local = new Bounds(transform.InverseTransformPoint(worldBounds.min), Vector3.zero);
            for (int i = 1; i < 8; i++)
                local.Encapsulate(transform.InverseTransformPoint(new Vector3(
                    (i & 1) == 0 ? worldBounds.min.x : worldBounds.max.x,
                    (i & 2) == 0 ? worldBounds.min.y : worldBounds.max.y,
                    (i & 4) == 0 ? worldBounds.min.z : worldBounds.max.z)));
            Vector3 extent = (Vector3)dimensions * cellSize;
            if (local.max.y >= extent.y || local.min.y < 0 || local.min.x < 0 || local.min.z < 0
                || local.max.x > extent.x || local.max.z > extent.z) return true;
            Vector3Int first = Vector3Int.FloorToInt(local.min / cellSize);
            Vector3Int span = Vector3Int.CeilToInt(local.max / cellSize) - first;
            return grid.AnyModified(first, Mathf.Max(span.x, Mathf.Max(span.y, span.z)));
        }

        public GridSnapshot Capture() => grid.Capture();

        public System.Collections.IEnumerator Restore(GridSnapshot snapshot, int seed)
        {
            IsRestoring = true;
            try
            {
                grid.Restore(snapshot);
                excavationSeed = seed;
                foreach (var chunk in chunks.Values) chunk.Collider.enabled = false;
                var slice = Stopwatch.StartNew();
                int surfaceLayer = (dimensions.y - 1) / chunkSize;
                var released = new List<Vector3Int>();
                for (int z = 0; z < dimensions.z; z += chunkSize)
                for (int y = 0; y < dimensions.y; y += chunkSize)
                for (int x = 0; x < dimensions.x; x += chunkSize)
                {
                    var key = new Vector3Int(x / chunkSize, y / chunkSize, z / chunkSize);
                    // The ground plane always exists; everything else only where a hole
                    // reached it. Rebuilding every key would mesh 7,200 empty chunks.
                    if (chunks.ContainsKey(key) || key.y == surfaceLayer || grid.AnyModified(key * chunkSize, chunkSize))
                    {
                        var chunk = Materialize(key);
                        Rebuild(key, chunk);
                        if (key.y != surfaceLayer && chunk.Mesh.GetIndexCount(0) == 0) released.Add(key);
                    }
                    if (slice.Elapsed.TotalMilliseconds < 8) continue;
                    yield return null;
                    slice.Restart();
                }
                // A hole that a previous session dug, but this checkpoint never had, must
                // not leave empty interior objects behind.
                foreach (var key in released) Release(key);
                Physics.SyncTransforms();
                NotifyChanged(new BoundsInt(Vector3Int.zero, dimensions));
            }
            finally { IsRestoring = false; }
        }

        public bool TryDig(RaycastHit hit) => TryDig(hit, digRadius);

        public bool TryDig(RaycastHit hit, float radius)
            => TryCut(hit, radius, 0);

        public bool TryShave(RaycastHit hit, float radius, float depth)
            => ExcavationGrid.Finite(depth) && depth > 0 && depth <= radius && TryCut(hit, radius, depth);

        public bool TryToolCut(RaycastHit hit, float radius, bool shaving)
            => TryCut(hit, radius, shaving ? radius * EquipmentProgression.ShavingDepthRatio : 0, true);

        private bool RefineContact(ref Vector3 surface, Vector3 normal)
        {
            Vector3 inside = surface - normal * cellSize * 2;
            Vector3 outside = surface + normal * cellSize * 2;
            if (grid.Sample(inside) <= 0 || grid.Sample(outside) > 0) return false;
            for (int i = 0; i < 12; i++)
            {
                Vector3 middle = (inside + outside) * .5f;
                if (grid.Sample(middle) > 0) inside = middle; else outside = middle;
            }
            surface = (inside + outside) * .5f;
            return true;
        }

        private bool TryCut(RaycastHit hit, float radius, float shaveDepth, bool adaptMaterials = false)
        {
            LastRebuiltChunkCount = 0;
            LastDigMilliseconds = 0;
            if (!CanDig || hit.collider == null || !hit.collider.enabled || hit.collider.transform.parent != chunkRoot)
                return false;
            if (!ExcavationGrid.Finite(radius) || radius < cellSize || radius > 4f) return false;
            Vector3 surface = transform.InverseTransformPoint(hit.point);
            // The net approximates the isosurface within a cell. Accept that tolerance,
            // but reject a cached hit into the air left by a previous scoop.
            if (grid.Sample(surface) < -cellSize * 0.75f) return false;
            // Removing an island can leave zero-density surface samples in empty air.
            // Recheck the actual mesh too, so its old hit cannot carve nearby soil.
            float hitTolerance = cellSize * 0.75f;
            if (!hit.collider.Raycast(new Ray(hit.point + hit.normal * hitTolerance, -hit.normal),
                out var currentHit, hitTolerance * 2)
                || (currentHit.point - hit.point).sqrMagnitude > cellSize * cellSize * 0.0001f) return false;
            int seed = unchecked(excavationSeed + grid.Revision * 486187739);
            uint depthHash = unchecked((uint)seed * 747796405u + 2891336453u);
            depthHash = unchecked(((depthHash >> (int)((depthHash >> 28) + 4)) ^ depthHash) * 277803737u);
            depthHash = (depthHash >> 22) ^ depthHash;
            float depthOffset = ((depthHash >> 8) * (1f / 16777216f) * 2 - 1) * scoopDepthVariation;
            Vector3 normal = transform.InverseTransformDirection(hit.normal).normalized;
            Vector3 point = surface - normal * (radius * (0.12f + depthOffset));
            var timer = Stopwatch.StartNew();
            BoundsInt changed;
            var material = adaptMaterials ? ToolMaterialAt(hit) : TerrainMaterialId.Soil;
            if (shaveDepth > 0)
            {
                // Surface nets approximate the isosurface. Resolve the true contact so
                // a cut shallower than a voxel keeps advancing on tilted faces too.
                if (!RefineContact(ref surface, normal)) return false;
                if (!grid.RemoveShave(surface, radius, normal, shaveDepth, out changed, adaptMaterials, adaptMaterials ? seed : 0)) return false;
            }
            else if (!grid.RemoveScoop(point, radius, normal, seed, scoopVariation, out changed, adaptMaterials)) return false;
            LastGridMilliseconds = timer.Elapsed.TotalMilliseconds;
            CommitEdit(changed);
            LastMeshMilliseconds = timer.Elapsed.TotalMilliseconds - LastGridMilliseconds;
            timer.Stop();
            LastDigMilliseconds = timer.Elapsed.TotalMilliseconds;
            LastDiscoveryMilliseconds = LastDigMilliseconds - LastGridMilliseconds - LastMeshMilliseconds;
            if (adaptMaterials) ToolCut?.Invoke(new TerrainCutFeedback(material, hit.point, hit.normal, LastRemovedVolume));
            return true;
        }

        public bool ClearLoadSweep(Vector3 from, Vector3 to, Quaternion rotation, Vector3 halfExtents)
        {
            using var profile = LoadSweepMarker.Auto();
            if (!CanDig || IsRestoring) return false;
            LastRebuiltChunkCount = 0;
            if (grid.RemoveBoxSweep(transform.InverseTransformPoint(from), transform.InverseTransformPoint(to),
                Quaternion.Inverse(transform.rotation) * rotation, halfExtents, out var changed)) CommitEdit(changed);
            return true;
        }

        private void CommitEdit(BoundsInt changed)
        {
            using var profile = CommitMarker.Auto();
            // The grid expands this region to include any detached components, even
            // beyond the brush/chunk. Rebuild visible surfaces and collision together.
            // Two cells cover vertex topology plus finite-difference normals at seams.
            Vector3Int first = Vector3Int.Max(Vector3Int.zero, changed.min - Vector3Int.one * 2);
            Vector3Int last = Vector3Int.Min(dimensions - Vector3Int.one, changed.max + Vector3Int.one * 2);
            for (int z = first.z / chunkSize; z <= last.z / chunkSize; z++)
            for (int y = first.y / chunkSize; y <= last.y / chunkSize; y++)
            for (int x = first.x / chunkSize; x <= last.x / chunkSize; x++)
            {
                var key = new Vector3Int(x, y, z);
                // Already-materialized neighbors always rebuild so shared seams and normal
                // halos stay consistent; untouched ground is only created where a cut
                // actually reached it, which keeps an empty 100 m volume cheap.
                if ((chunks.ContainsKey(key) || grid.AnyModified(key * chunkSize, chunkSize))
                    && Refresh(key)) LastRebuiltChunkCount++;
            }
            NotifyChanged(changed);
        }

        // Only the explicitly confirmed admin reset uses this. Reuse chunk objects
        // and mesh buffers so reset cannot leave old colliders or accumulate resources.
        public void ResetExcavation()
        {
            if (grid == null) return;
            grid.Reset();
            int surfaceLayer = (dimensions.y - 1) / chunkSize;
            var released = new List<Vector3Int>();
            foreach (var pair in chunks)
            {
                Rebuild(pair.Key, pair.Value);
                if (pair.Key.y != surfaceLayer && pair.Value.Mesh.GetIndexCount(0) == 0) released.Add(pair.Key);
            }
            // Reset returns the volume to untouched soil: interior chunks a previous hole
            // materialized hold empty meshes and would only be reused by another deep dig.
            foreach (var key in released) Release(key);
            LastRebuiltChunkCount = 0;
            LastDigMilliseconds = 0;
            NotifyChanged(new BoundsInt(Vector3Int.zero, dimensions));
        }

        private void NotifyChanged(BoundsInt samples)
        {
            using var profile = NotifyMarker.Auto();
            StateRevision++;
            if (Changed == null) return;
            // Include the interpolation halo and detached soil beyond the scoop.
            Vector3 min = ((Vector3)samples.min - Vector3.one * 2) * cellSize;
            Vector3 max = ((Vector3)samples.max + Vector3.one * 2) * cellSize;
            var world = new Bounds(transform.TransformPoint(min), Vector3.zero);
            for (int i = 0; i < 8; i++)
                world.Encapsulate(transform.TransformPoint(new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z)));
            Changed.Invoke(world);
        }

        private static readonly Unity.Profiling.ProfilerMarker LoadSweepMarker = new Unity.Profiling.ProfilerMarker("Excavation.LoadSweep");
        private static readonly Unity.Profiling.ProfilerMarker CommitMarker = new Unity.Profiling.ProfilerMarker("Excavation.Commit");
        private static readonly Unity.Profiling.ProfilerMarker NotifyMarker = new Unity.Profiling.ProfilerMarker("Excavation.Notify");
        private static readonly Unity.Profiling.ProfilerMarker MeshMarker = new Unity.Profiling.ProfilerMarker("Excavation.Mesh");
        private static readonly Unity.Profiling.ProfilerMarker CollisionMarker = new Unity.Profiling.ProfilerMarker("Excavation.Collision");

        private bool Rebuild(Vector3Int key, Chunk chunk)
        {
            // Detach before mutating so PhysX cannot keep the previous cooked surface.
            bool changed;
            using (MeshMarker.Auto())
                changed = TerrainChunkMesh.Rebuild(chunk.Mesh, grid, key * chunkSize, chunkSize, meshing,
                    chunk.DensityCache, chunk.BeforeMeshWrite);
            bool visible = chunk.Mesh.GetIndexCount(0) > 0;
            chunk.Renderer.enabled = visible;
            chunk.Collider.enabled = visible;
            if (changed && visible)
                using (CollisionMarker.Auto()) chunk.Collider.sharedMesh = chunk.Mesh;
            return changed;
        }

        // Chunk objects are created on demand: a 100 m volume keeps 7,200 keys but only
        // materializes the ground plane and whatever a cut has reached.
        private Chunk Materialize(Vector3Int key)
        {
            if (chunks.TryGetValue(key, out var existing)) return existing;
            var root = new GameObject($"Chunk {key.x},{key.y},{key.z}",
                typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            root.layer = gameObject.layer;
            root.transform.SetParent(chunkRoot, false);
            var chunk = new Chunk
            {
                Mesh = new Mesh { name = root.name },
                Collider = root.GetComponent<MeshCollider>(),
                Renderer = root.GetComponent<MeshRenderer>()
            };
            root.GetComponent<MeshFilter>().sharedMesh = chunk.Mesh;
            chunk.BeforeMeshWrite = chunk.DetachCollider;
            chunk.Renderer.sharedMaterial = CurrentSoilMaterial;
            chunks.Add(key, chunk);
            return chunk;
        }

        private bool Refresh(Vector3Int key) => Rebuild(key, Materialize(key));

        private void Release(Vector3Int key)
        {
            var chunk = chunks[key];
            chunks.Remove(key);
            if (Application.isPlaying)
            {
                Destroy(chunk.Mesh);
                Destroy(chunk.Collider.gameObject);
            }
            else
            {
                DestroyImmediate(chunk.Mesh);
                DestroyImmediate(chunk.Collider.gameObject);
            }
        }

        private void OnDestroy()
        {
            meshing.Dispose();
            exposure.Dispose();
            foreach (var chunk in chunks.Values)
                if (Application.isPlaying) Destroy(chunk.Mesh); else DestroyImmediate(chunk.Mesh);
            chunks.Clear();
            if (xrayMaterial != null) Destroy(xrayMaterial);
        }
    }
}
