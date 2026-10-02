using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Developer ground X-ray: the ground turns transparent and every few cells of tell ground (cracks,
    // gravel, backfill, pond clay) and concrete get a coloured marker within reach of the camera, so
    // tells can be found and dug on purpose. Clay veins and rock masses are left out: they read in the
    // wall already and would bury the tells. Session-only; it never changes the grid.
    public sealed partial class TerrainVolume
    {
        public enum XrayGround { Cracks, Gravel, Backfill, PondClay, Concrete }
        public static readonly Color[] XrayColours =
            { new Color(1f, .12f, .08f), new Color(1f, .85f, .15f), new Color(.95f, .25f, 1f), new Color(.2f, .9f, 1f), new Color(.92f, .92f, .92f) };
        public const string XrayLegend = "red cracks, yellow gravel, magenta backfill, cyan pond clay, white concrete";
        private const float XrayRadius = 20f, XrayMarkerSize = .1f, XrayResampleDistance = 4f, XrayResampleSeconds = .5f;
        private const int XrayStep = 3;
        // Concrete stays out of the way close to the eye (a room's own walls); tells always show.
        private const float XrayContextClearance = 3f;

        [SerializeField] private Material groundXrayMarker;
        private readonly List<Matrix4x4>[] xrayMarkers = new List<Matrix4x4>[5];
        private MaterialPropertyBlock xrayBlock;
        private Mesh xrayCube;
        private Transform groundXrayEye;
        private Vector3 xraySampledAt;
        private int xraySampledRevision = -1;
        private float xrayResampleWait;
        private bool findXray;

        public bool GroundXrayEnabled => groundXrayEye != null;
        public int GroundXrayMarkerCount { get; private set; }
        public int GroundXrayMarkers(XrayGround ground) => xrayMarkers[(int)ground]?.Count ?? 0;

        public void SetXray(bool enabled)
        {
            findXray = enabled;
            ApplyXrayMaterial();
        }

        public void SetGroundXray(bool enabled, Camera eye)
        {
            groundXrayEye = enabled && eye != null ? eye.transform : null;
            xraySampledRevision = -1;
            if (!GroundXrayEnabled) foreach (var list in xrayMarkers) list?.Clear();
            ApplyXrayMaterial();
        }

        public static XrayGround? XrayClass(TerrainMaterialId material) => material switch
        {
            TerrainMaterialId.Crack or TerrainMaterialId.FracturedRock or TerrainMaterialId.FracturedConcrete => XrayGround.Cracks,
            TerrainMaterialId.Gravel => XrayGround.Gravel,
            TerrainMaterialId.Backfill => XrayGround.Backfill,
            TerrainMaterialId.PondClay => XrayGround.PondClay,
            TerrainMaterialId.Concrete => XrayGround.Concrete,
            _ => null
        };

        // Rebuilds the marker lists around the eye; plain C#, a few tens of milliseconds.
        public void SampleGroundXray()
        {
            if (!GroundXrayEnabled || grid == null) return;
            for (int i = 0; i < xrayMarkers.Length; i++) (xrayMarkers[i] ??= new List<Matrix4x4>()).Clear();
            var eye = transform.InverseTransformPoint(groundXrayEye.position);
            int reach = Mathf.CeilToInt(XrayRadius / cellSize);
            var centre = Vector3Int.RoundToInt(eye / cellSize);
            Vector3Int min = Vector3Int.Max(Vector3Int.zero, centre - Vector3Int.one * reach);
            Vector3Int max = Vector3Int.Min(dimensions, centre + Vector3Int.one * reach);
            for (int axis = 0; axis < 3; axis++) min[axis] = min[axis] / XrayStep * XrayStep;
            float reachSquared = reach * (float)reach, clearance = XrayContextClearance / cellSize;
            float clearanceSquared = clearance * clearance;
            var toWorld = transform.localToWorldMatrix;
            var scale = Vector3.one * XrayMarkerSize;
            int count = 0;
            for (int y = min.y; y <= max.y; y += XrayStep)
            {
                for (int z = min.z; z <= max.z; z += XrayStep)
                for (int x = min.x; x <= max.x; x += XrayStep)
                {
                    float dx = x - centre.x, dy = y - centre.y, dz = z - centre.z;
                    if (dx * dx + dy * dy + dz * dz > reachSquared || grid.Sample(x, y, z) <= 0f) continue;
                    var ground = XrayClass(grid.MaterialAt(x, y, z));
                    if (ground == null || (ground >= XrayGround.Concrete && dx * dx + dy * dy + dz * dz < clearanceSquared)) continue;
                    xrayMarkers[(int)ground.Value].Add(toWorld * Matrix4x4.TRS(new Vector3(x, y, z) * cellSize, Quaternion.identity, scale));
                    count++;
                }
            }
            GroundXrayMarkerCount = count;
            xraySampledAt = groundXrayEye.position;
            xraySampledRevision = grid.Revision;
            xrayResampleWait = XrayResampleSeconds;
        }

        private void LateUpdate()
        {
            if (!GroundXrayEnabled || grid == null || groundXrayMarker == null) return;
            xrayResampleWait -= Time.unscaledDeltaTime;
            if (xraySampledRevision < 0 || (xrayResampleWait <= 0f && (grid.Revision != xraySampledRevision
                || (groundXrayEye.position - xraySampledAt).sqrMagnitude > XrayResampleDistance * XrayResampleDistance)))
                SampleGroundXray();
            if (xrayCube == null) xrayCube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            xrayBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < xrayMarkers.Length; i++)
            {
                if (xrayMarkers[i] == null || xrayMarkers[i].Count == 0) continue;
                xrayBlock.SetColor("_BaseColor", XrayColours[i]);
                var parameters = new RenderParams(groundXrayMarker)
                { matProps = xrayBlock, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, layer = gameObject.layer };
                Graphics.RenderMeshInstanced(parameters, xrayCube, 0, xrayMarkers[i]);
            }
        }

        private void ApplyXrayMaterial()
        {
            bool enabled = findXray || GroundXrayEnabled;
            if (XrayEnabled == enabled) return;
            XrayEnabled = enabled;
            if (enabled && xrayMaterial == null && soilMaterial != null)
            {
                xrayMaterial = new Material(soilMaterial) { name = "Transparent excavation ground", hideFlags = HideFlags.DontSave };
                xrayMaterial.SetFloat("_GroundOpacity", .12f);
                xrayMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                xrayMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                xrayMaterial.SetFloat("_ZWrite", 0);
                xrayMaterial.SetOverrideTag("RenderType", "Transparent");
                xrayMaterial.renderQueue = (int)RenderQueue.Transparent;
                xrayMaterial.SetShaderPassEnabled("ShadowCaster", false);
                xrayMaterial.SetShaderPassEnabled("DepthOnly", false);
                xrayMaterial.SetShaderPassEnabled("DepthNormalsOnly", false);
            }
            foreach (var chunk in chunks.Values) chunk.Renderer.sharedMaterial = CurrentSoilMaterial;
            if (TryGetComponent<ExcavationDaylight>(out var daylight)) daylight.RefreshShaderState();
        }
    }
}
