using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Authored clumps, instanced in small spatial batches. Grass is derived from
    // the original surface and current soil; no grass state belongs in a save.
    [DisallowMultipleComponent, RequireComponent(typeof(TerrainVolume))]
    public sealed class SurfaceGrassRenderer : MonoBehaviour
    {
        [Serializable]
        public sealed class DetailLayer
        {
            public Mesh mesh;
            public Mesh farMesh;
            public Material material;
            [Range(1, 20)] public int cellsPerPatch = 3;
            [Range(0, 1)] public float coverage = .8f;
            public Vector2 scaleRange = new Vector2(.7f, 1.3f);
            public Vector3 meshScale = Vector3.one;
            [Range(0, 1)] public float patchiness = .6f;
        }

        [SerializeField] private DetailLayer[] detailLayers = Array.Empty<DetailLayer>();
        [SerializeField] private Mesh nearMesh;
        [SerializeField] private Mesh farMesh;
        [SerializeField] private Material material;
        [SerializeField, Min(1)] private float patchSize = 2f;
        [SerializeField, Range(4, 20)] private int cellsPerPatch = 8;
        [SerializeField, Range(0f, 1f)] private float coverage = .88f;
        [SerializeField] private Vector2 scaleRange = new Vector2(.95f, 1.35f);
        [SerializeField] private Vector3 meshScale = new Vector3(.35f, 1.8f, .35f);
        [SerializeField, Min(0)] private float windPadding = .12f;
        [SerializeField, Min(0)] private float surfaceRadius;
        private const int Seed = 127;
        private sealed class Patch
        {
            public DetailLayer Layer;
            public float RootRadius;
            public Bounds Bounds;
            public Vector3[] Roots;
            public Matrix4x4[] Candidates, Near;
            public int NearCount;
            public bool Dirty = true;
        }
        private TerrainVolume terrain;
        private DetailLayer[] activeLayers;
        private readonly List<Patch> patches = new List<Patch>();
        private readonly Plane[] planes = new Plane[6];
        // Vendor shaders also upload inverse matrices: retain the conservative
        // 511-instance limit without changing their instancing declarations.
        private readonly Matrix4x4[] nearBatch = new Matrix4x4[511], farBatch = new Matrix4x4[511];
        public int PatchCount => patches.Count;
        public int SupportedClumps { get; private set; }
        public int LastRebuiltPatches { get; private set; }
        public double LastRebuildMilliseconds { get; private set; }
        public double LastSubmissionMilliseconds { get; private set; }
        public int LastDrawCalls { get; private set; }
        public int LastTriangles { get; private set; }
        public int LastVisibleClumps { get; private set; }
        public int LastFarClumps { get; private set; }
        public ulong PlacementHash { get; private set; }

        private void OnEnable()
        {
            terrain = GetComponent<TerrainVolume>();
            terrain.Changed += SoilChanged;
            RenderPipelineManager.beginCameraRendering += RenderCamera;
        }

        private void OnDisable()
        {
            if (terrain != null) terrain.Changed -= SoilChanged;
            RenderPipelineManager.beginCameraRendering -= RenderCamera;
            patches.Clear();
            activeLayers = null;
            SupportedClumps = LastRebuiltPatches = LastDrawCalls = LastTriangles = LastVisibleClumps = 0;
            PlacementHash = 0;
        }

        private void LateUpdate()
        {
            LastRebuiltPatches = 0;
            if (terrain == null || !terrain.CanDig || terrain.IsRestoring) return;
            if ((detailLayers == null || detailLayers.Length == 0) && (nearMesh == null || material == null)) return;
            var start = Stopwatch.GetTimestamp();
            if (patches.Count == 0) CreatePatches();
            foreach (var patch in patches)
            {
                if (!patch.Dirty) continue;
                SupportedClumps -= patch.NearCount;
                patch.NearCount = 0;
                for (int i = 0; i < patch.Roots.Length; i++)
                {
                    float footprintScale = Mathf.Max(patch.Candidates[i].GetColumn(0).magnitude,
                        patch.Candidates[i].GetColumn(2).magnitude);
                    if (!RootSupported(patch.Roots[i], footprintScale * patch.RootRadius + windPadding)) continue;
                    patch.Near[patch.NearCount++] = patch.Candidates[i];
                }
                SupportedClumps += patch.NearCount;
                patch.Dirty = false;
                LastRebuiltPatches++;
            }
            if (LastRebuiltPatches > 0) UpdatePlacementHash();
            LastRebuildMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private void CreatePatches()
        {
            activeLayers = detailLayers != null && detailLayers.Length > 0 ? detailLayers : new[] {
                new DetailLayer { mesh = nearMesh, farMesh = farMesh, material = material,
                    cellsPerPatch = cellsPerPatch, coverage = coverage, scaleRange = scaleRange,
                    meshScale = meshScale, patchiness = 0 }
            };
            for (int i = 0; i < activeLayers.Length; i++)
                if (activeLayers[i] != null && activeLayers[i].mesh != null && activeLayers[i].material != null)
                    CreateLayerPatches(activeLayers[i], i);
        }

        private void CreateLayerPatches(DetailLayer layer, int layerIndex)
        {
            // At most 400 instances per draw, below Unity's conservative 511 limit.
            int cells = Mathf.Clamp(layer.cellsPerPatch, 1, 20);
            float size = Mathf.Max(1, patchSize);
            var extent = (Vector3)terrain.Dimensions * terrain.CellSize;
            // Include the imported card footprint, all rotations, and wind.
            Bounds meshBounds = layer.mesh.bounds;
            if (layer.farMesh != null) meshBounds.Encapsulate(layer.farMesh.bounds);
            var reach = new Vector2(Mathf.Max(Mathf.Abs(meshBounds.min.x), Mathf.Abs(meshBounds.max.x)),
                Mathf.Max(Mathf.Abs(meshBounds.min.z), Mathf.Abs(meshBounds.max.z)));
            float rootRadius = reach.magnitude;
            Vector3 shape = Vector3.Max(Vector3.one * .01f, layer.meshScale);
            float minScale = Mathf.Max(.01f, Mathf.Min(layer.scaleRange.x, layer.scaleRange.y));
            float maxScale = Mathf.Max(minScale, Mathf.Max(layer.scaleRange.x, layer.scaleRange.y));
            float bladePadding = (Vector3.Scale(meshBounds.center, shape).magnitude
                + Vector3.Scale(meshBounds.extents, shape).magnitude) * maxScale
                + windPadding + .02f;
            var random = new System.Random(Seed + layerIndex * 7919);
            for (float z = 0; z < extent.z; z += size)
            for (float x = 0; x < extent.x; x += size)
            {
                var roots = new List<Vector3>(cells * cells);
                var matrices = new List<Matrix4x4>(cells * cells);
                for (int iz = 0; iz < cells; iz++)
                for (int ix = 0; ix < cells; ix++)
                {
                    float px = x + (ix + .5f + ((float)random.NextDouble() - .5f) * .8f) * size / cells;
                    float pz = z + (iz + .5f + ((float)random.NextDouble() - .5f) * .8f) * size / cells;
                    if (px > extent.x - .03f || pz > extent.z - .03f) continue;
                    var root = terrain.transform.TransformPoint(new Vector3(px, extent.y - .006f, pz));
                    float scaleRandom = (float)random.NextDouble();
                    float rotation = (float)random.NextDouble() * 360;
                    float keep = (float)random.NextDouble();
                    // Shared clearings keep one species from filling every gap
                    // left by another. Placement remains independent of digging.
                    float meadow = Mathf.InverseLerp(.4f, .7f,
                        Mathf.PerlinNoise(root.x * .32f + 17.2f, root.z * .32f + 71.6f));
                    float growth = Mathf.Lerp(1, Mathf.SmoothStep(0, 1.4f, meadow), layer.patchiness);
                    if (keep >= layer.coverage * growth) continue;
                    float scale = Mathf.Lerp(minScale, maxScale, scaleRandom);
                    roots.Add(root);
                    matrices.Add(Matrix4x4.TRS(root, terrain.transform.rotation *
                        Quaternion.Euler(0, rotation, 0), shape * scale));
                }
                var bounds = new Bounds(terrain.transform.TransformPoint(new Vector3(x, extent.y, z)), Vector3.zero);
                bounds.Encapsulate(terrain.transform.TransformPoint(new Vector3(Mathf.Min(x + size, extent.x), extent.y, z)));
                bounds.Encapsulate(terrain.transform.TransformPoint(new Vector3(x, extent.y, Mathf.Min(z + size, extent.z))));
                bounds.Encapsulate(terrain.transform.TransformPoint(new Vector3(Mathf.Min(x + size, extent.x), extent.y, Mathf.Min(z + size, extent.z))));
                bounds.Expand(bladePadding * 2);
                patches.Add(new Patch { Layer = layer, RootRadius = rootRadius,
                    Bounds = bounds, Roots = roots.ToArray(), Candidates = matrices.ToArray(),
                    Near = new Matrix4x4[roots.Count] });
            }
        }

        private bool RootSupported(Vector3 root, float radius)
        {
            // A handful of perimeter probes misses a small hole between the root
            // and wide leaves. Inspect every surface grid column intersecting the
            // entire canopy, including wind and the mesher's cell-sized edge halo.
            float cell = terrain.CellSize;
            Vector3 local = terrain.transform.InverseTransformPoint(root);
            Vector3 extent = (Vector3)terrain.Dimensions * cell;
            if (local.x - radius < 0 || local.z - radius < 0 ||
                local.x + radius > extent.x || local.z + radius > extent.z) return false;
            if (surfaceRadius > 0 && new Vector2(local.x - extent.x * .5f,
                local.z - extent.z * .5f).magnitude + radius > surfaceRadius) return false;
            if (!terrain.IsSolid(root - terrain.transform.up * .018f)) return false;
            float reach = radius + cell * 1.414214f;
            int firstX = Mathf.Max(0, Mathf.FloorToInt((local.x - reach) / cell));
            int lastX = Mathf.Min(terrain.Dimensions.x, Mathf.CeilToInt((local.x + reach) / cell));
            int firstZ = Mathf.Max(0, Mathf.FloorToInt((local.z - reach) / cell));
            int lastZ = Mathf.Min(terrain.Dimensions.z, Mathf.CeilToInt((local.z + reach) / cell));
            for (int z = firstZ; z <= lastZ; z++)
            for (int x = firstX; x <= lastX; x++)
            {
                float dx = x * cell - local.x, dz = z * cell - local.z;
                if (dx * dx + dz * dz > reach * reach) continue;
                var sample = terrain.transform.TransformPoint(new Vector3(x * cell, extent.y, z * cell));
                if (terrain.SignedDensity(sample) < -.00001f) return false;
            }
            return true;
        }

        private void SoilChanged(Bounds changed)
        {
            changed.Expand(.25f);
            foreach (var patch in patches)
                if (patch.Bounds.Intersects(changed)) patch.Dirty = true;
        }

        private void UpdatePlacementHash()
        {
            ulong hash = 14695981039346656037UL;
            foreach (var patch in patches)
            for (int i = 0; i < patch.NearCount; i++)
            {
                Vector4 p = patch.Near[i].GetColumn(3);
                unchecked
                {
                    hash = (hash ^ (uint)Mathf.RoundToInt(p.x * 10000)) * 1099511628211UL;
                    hash = (hash ^ (uint)Mathf.RoundToInt(p.z * 10000)) * 1099511628211UL;
                }
            }
            PlacementHash = hash;
        }

        private void RenderCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            LastDrawCalls = LastTriangles = LastVisibleClumps = LastFarClumps = 0;
            if (terrain == null || terrain.IsRestoring || patches.Count == 0 || !SystemInfo.supportsInstancing) return;
            var start = Stopwatch.GetTimestamp();
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            foreach (var layer in activeLayers)
            {
                if (layer == null || layer.mesh == null || layer.material == null) continue;
                int nearCount = 0, farCount = 0;
                Bounds nearBounds = default, farBounds = default;
                foreach (var patch in patches)
                {
                    if (patch.Layer != layer || patch.NearCount == 0 || !GeometryUtility.TestPlanesAABB(planes, patch.Bounds)) continue;
                    // Low-poly vendor cards need no far mesh. Optional authored LODs
                    // must retain the same footprint and coverage as the near mesh.
                    bool distant = layer.farMesh != null && patch.Bounds.SqrDistance(camera.transform.position) > 16f * 16f;
                    if (distant) {
                        Append(camera, layer.farMesh, patch, farBatch, ref farCount, ref farBounds);
                        LastFarClumps += patch.NearCount;
                    }
                    else Append(camera, layer.mesh, patch, nearBatch, ref nearCount, ref nearBounds);
                }
                Submit(camera, layer.mesh, layer.material, nearBatch, nearCount, nearBounds);
                Submit(camera, layer.farMesh, layer.material, farBatch, farCount, farBounds);
            }
            LastSubmissionMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private void Append(Camera camera, Mesh mesh, Patch patch, Matrix4x4[] batch, ref int count, ref Bounds bounds)
        {
            int offset = 0;
            while (offset < patch.NearCount)
            {
                int length = Mathf.Min(batch.Length - count, patch.NearCount - offset);
                if (count == 0) bounds = patch.Bounds; else bounds.Encapsulate(patch.Bounds);
                Array.Copy(patch.Near, offset, batch, count, length);
                offset += length; count += length;
                if (count == batch.Length) { Submit(camera, mesh, patch.Layer.material, batch, count, bounds); count = 0; }
            }
        }

        private void Submit(Camera camera, Mesh mesh, Material drawMaterial, Matrix4x4[] batch, int count, Bounds bounds)
        {
            if (count == 0 || mesh == null) return;
            var parameters = new RenderParams(drawMaterial) {
                camera = camera, worldBounds = bounds, layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off, motionVectorMode = MotionVectorGenerationMode.Camera
            };
            Graphics.RenderMeshInstanced(parameters, mesh, 0, batch, count);
            LastDrawCalls++; LastVisibleClumps += count;
            LastTriangles += count * (int)(mesh.GetIndexCount(0) / 3);
        }
    }
}
