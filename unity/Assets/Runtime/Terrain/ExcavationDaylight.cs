using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SomethingDownThere
{
    [DefaultExecutionOrder(500), DisallowMultipleComponent, RequireComponent(typeof(TerrainVolume))]
    public sealed class ExcavationDaylight : MonoBehaviour
    {
        [SerializeField] private Shader litShader;
        private static readonly int MapId = Shader.PropertyToID("_ExcavationDaylight");
        private static readonly int SizeId = Shader.PropertyToID("_ExcavationDaylightSize");
        private static readonly int ExtentId = Shader.PropertyToID("_ExcavationDaylightExtent");
        private static readonly int MatrixId = Shader.PropertyToID("_ExcavationDaylightWorldToLocal");
        private static readonly int EnabledId = Shader.PropertyToID("_ExcavationDaylightEnabled");
        private sealed class Receiver
        {
            public Renderer Renderer;
            public Material[] Original, Adapted;
        }
        private readonly List<Receiver> receivers = new List<Receiver>();
        private readonly HashSet<Renderer> registered = new HashSet<Renderer>();
        private readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        private readonly Stopwatch timer = new Stopwatch();
        private TerrainVolume terrain;
        private ExcavationDaylightGrid grid;
        private Texture3D texture;
        private IEnumerator rebuild;
        private Bounds pending;
        private bool dirty;
        private double accumulatedMilliseconds;
        public int PublishedRevision { get; private set; }
        public bool IsUpdating => dirty || rebuild != null;
        public double LastRebuildMilliseconds { get; private set; }
        public int CacheBytes => grid == null ? 0 : grid.Count;

        private void Awake()
        {
            if (litShader == null) litShader = Shader.Find("Something Down There/Excavation Lit");
            terrain = GetComponent<TerrainVolume>();
            grid = new ExcavationDaylightGrid((Vector3)terrain.Dimensions * terrain.CellSize);
            Vector3Int size = grid.Size;
            texture = new Texture3D(size.x, size.y, size.z, TextureFormat.R8, false)
            {
                name = "Excavation daylight cache", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            texture.SetPixelData(grid.Light, 0); texture.Apply(false, false);
        }

        private void OnEnable()
        {
            terrain.Changed += HandleExcavationChanged;
            pending = new Bounds(grid.Extent * 0.5f, grid.Extent);
            dirty = true;
            Shader.SetGlobalTexture(MapId, texture);
            Shader.SetGlobalVector(SizeId, (Vector3)grid.Size);
            Shader.SetGlobalVector(ExtentId, grid.Extent);
            Shader.SetGlobalMatrix(MatrixId, transform.worldToLocalMatrix);
            RefreshShaderState();
            foreach (var receiver in receivers)
                if (receiver.Renderer != null) receiver.Renderer.sharedMaterials = receiver.Adapted;
        }

        private void Start()
        {
            // Includes the existing held shovel and finds already initialized
            // during Start. Later checkpoint spawns register in BuriedFind.
            foreach (var renderer in transform.root.GetComponentsInChildren<Renderer>(true)) Register(renderer);
        }

        public void Register(Renderer renderer)
        {
            if (renderer == null || registered.Contains(renderer)) return;
            var original = renderer.sharedMaterials;
            var adapted = (Material[])original.Clone();
            bool changed = false;
            for (int i = 0; i < original.Length; i++)
            {
                var source = original[i];
                if (source == null || source.shader.name != "Universal Render Pipeline/Lit" || litShader == null) continue;
                if (!materials.TryGetValue(source, out var material))
                {
                    material = new Material(source) { shader = litShader, name = source.name + " (excavation daylight)", hideFlags = HideFlags.DontSave };
                    materials.Add(source, material);
                }
                adapted[i] = material;
                changed = true;
            }
            if (!changed) return;
            receivers.Add(new Receiver { Renderer = renderer, Original = original, Adapted = adapted });
            registered.Add(renderer);
            if (isActiveAndEnabled) renderer.sharedMaterials = adapted;
        }

        // OnTerrainChanged is a reserved Unity message taking an integer.
        // This is our density-volume event, not Unity Terrain's callback.
        private void HandleExcavationChanged(Bounds worldBounds)
        {
            // TerrainVolume owns a rigid, unit-scale transform. Transform all
            // corners so rotated validation fixtures receive correct dirties.
            var local = new Bounds(transform.InverseTransformPoint(worldBounds.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
                local.Encapsulate(transform.InverseTransformPoint(new Vector3(
                    (i & 1) == 0 ? worldBounds.min.x : worldBounds.max.x,
                    (i & 2) == 0 ? worldBounds.min.y : worldBounds.max.y,
                    (i & 4) == 0 ? worldBounds.min.z : worldBounds.max.z)));
            if (dirty) pending.Encapsulate(local); else pending = local;
            dirty = true;
        }

        private bool IsOpen(Vector3 local) => terrain.SignedDensity(transform.TransformPoint(local)) <= 0.001f;

        private void LateUpdate()
        {
            if (terrain.IsRestoring) return;
            if (rebuild == null && dirty)
            {
                rebuild = grid.Rebuild(pending, IsOpen);
                dirty = false;
                accumulatedMilliseconds = 0;
            }
            if (rebuild != null)
            {
                timer.Restart();
                bool more;
                do { more = rebuild.MoveNext(); }
                while (more && timer.Elapsed.TotalMilliseconds < 1.25);
                accumulatedMilliseconds += timer.Elapsed.TotalMilliseconds;
                if (more) return;
                rebuild = null;
                texture.SetPixelData(grid.Light, 0); texture.Apply(false, false);
                PublishedRevision++;
                LastRebuildMilliseconds = accumulatedMilliseconds;
                for (int i = receivers.Count - 1; i >= 0; i--)
                {
                    if (receivers[i].Renderer != null) continue;
                    registered.Remove(receivers[i].Renderer);
                    receivers.RemoveAt(i);
                }
            }
        }

        public float SampleAmbient(Vector3 worldPosition) =>
            grid.Sample(transform.InverseTransformPoint(worldPosition));

        internal void RefreshShaderState()
        {
            if (Shader.GetGlobalTexture(MapId) == texture)
                Shader.SetGlobalFloat(EnabledId, isActiveAndEnabled && !terrain.XrayEnabled ? 1 : 0);
        }

        private void OnDisable()
        {
            if (terrain != null) terrain.Changed -= HandleExcavationChanged;
            if (Shader.GetGlobalTexture(MapId) == texture) Shader.SetGlobalFloat(EnabledId, 0);
            rebuild = null;
            foreach (var receiver in receivers)
            {
                if (receiver.Renderer == null) continue;
                receiver.Renderer.sharedMaterials = receiver.Original;
            }
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
            foreach (var material in materials.Values) Destroy(material);
        }
    }
}
