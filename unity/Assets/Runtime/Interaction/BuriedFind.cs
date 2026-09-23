using UnityEngine;

namespace SomethingDownThere
{
    public enum FindSize { Small, Large }

    // Authored meshes carry real surface samples. Ellipsoid fallback is legacy fixture support.
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class BuriedFind : MonoBehaviour, IInteractionTarget
    {
        [SerializeField] private string saveContentId;
        public string SaveContentId => saveContentId;
        [SerializeField] private string displayName = "Blue marble";
        public string DisplayName => displayName;
        [SerializeField] private bool minor = true;
        [SerializeField] private bool detectorEligible;
        public bool DetectorEligible => !minor && detectorEligible;
        public int SurfaceSampleCount => exposureSamples == null ? 0 : exposureSamples.Length;
        [SerializeField, Min(0)] private int saleValue = 5;
        public int SaleValue => saleValue;
        [SerializeField] private FindSize size = FindSize.Small;
        [SerializeField, Range(0.1f, 1f)] private float collectionThreshold = 0.6f;
        [SerializeField] private Vector3[] exposureSamples;
        [SerializeField] private DiscoveryKind kind;
        [SerializeField] private RecoveryMethod recovery;
        [SerializeField, TextArea] private string lore = "";
        private DiscoveryField field;
        public DiscoveryKind Kind => kind;
        public RecoveryMethod Recovery => recovery;
        public bool HasLore => !string.IsNullOrWhiteSpace(lore);
        public bool RopeTarget => recovery == RecoveryMethod.Rope;
        public FindState State { get; private set; }
        public bool DepthRecorded { get; private set; }
        public float DiscoveryDepth { get; private set; }
        public string DisplaySocket { get; private set; } = "";
        public string LoreCard => $"{DisplayName}  |  Found at {DiscoveryDepth:F1} m\n{lore}";
        private TerrainVolume terrain;
        private MeshCollider hitCollider;
        private MeshRenderer visual;
        private bool xrayVisible;
        private FindPhysics physical;
        private Bounds exposureBounds;
        private readonly RaycastHit[] coveringHits = new RaycastHit[32];
        public InventoryItem Item { get; private set; }
        public float Exposure { get; private set; }
        public bool Collected => State == FindState.Collected || State == FindState.Stored || State == FindState.Displayed;
        public FindSize Size => size;
        // Visibility/range are checked against the actual collider when collecting.
        public float RequiredExposure => Mathf.Clamp(collectionThreshold, 0.1f, 1f);
        public bool IsHeld => physical != null && physical.Held;
        public bool IsReleased => physical != null && physical.Released;
        public bool ExposureReady => Item != null && State == FindState.World && !IsHeld && Exposure >= RequiredExposure;
        public bool Collectible => !RopeTarget && ExposureReady;
        public bool CanMark => RopeTarget && ExposureReady;
        public Bounds LocalHull => hitCollider.sharedMesh.bounds;
        // Released convex bodies can rest slightly inside the sampled
        // field's smooth collider. Permit only shallow contact on a free item;
        // anchored finds still require all their soil attachment to be removed.
        internal bool FullyUncovered => Collectible && terrain != null && !terrain.IsRestoring
            && !HasSoilAttachment(physical != null && physical.Released && Exposure >= .9f ? .035f : .005f);
        internal Collider HitCollider => hitCollider;
        public Bounds WorldBounds => visual.bounds;

        public void Initialize(TerrainVolume owner, string identity, DiscoveryField population = null)
        {
            terrain = owner;
            field = population;
            hitCollider = GetComponent<MeshCollider>();
            visual = GetComponent<MeshRenderer>();
            if (owner.TryGetComponent<ExcavationDaylight>(out var daylight)) daylight.Register(visual);
            physical = GetComponent<FindPhysics>();
            Item = new InventoryItem(identity, displayName, saleValue, kind);
            if (exposureSamples == null || exposureSamples.Length == 0)
            {
                exposureSamples = new Vector3[96];
                for (int i = 0; i < exposureSamples.Length; i++)
                {
                    float y = 1f - 2f * (i + 0.5f) / exposureSamples.Length;
                    float radius = Mathf.Sqrt(1f - y * y);
                    float angle = i * 2.39996323f;
                    exposureSamples[i] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius) * 0.5f;
                }
            }
            exposureBounds = new Bounds(exposureSamples[0], Vector3.zero);
            foreach (var sample in exposureSamples) exposureBounds.Encapsulate(sample);
            if (physical != null) physical.Initialize(owner, population);
            RefreshExposure();
        }

        private bool UsesPhysicalPose => physical != null && !Collected && physical.Released;
        public FindSnapshot Capture() => new FindSnapshot { ContentId = saveContentId, Item = ItemSnapshot.Capture(Item),
            // Inactive collected bodies no longer have a live PhysX pose; preserve their transform history.
            Position = terrain.transform.InverseTransformPoint(UsesPhysicalPose ? physical.Body.position : transform.position),
            Rotation = Quaternion.Inverse(terrain.transform.rotation) * (UsesPhysicalPose ? physical.Body.rotation : transform.rotation),
            Scale = transform.localScale, State = State, PhysicsReleased = physical != null && physical.Released,
            DepthRecorded = DepthRecorded, DiscoveryDepth = DiscoveryDepth, DisplaySocket = DisplaySocket };

        public void Restore(FindSnapshot state)
        {
            Item = state.Item.Restore();
            transform.SetPositionAndRotation(terrain.transform.TransformPoint(state.Position), terrain.transform.rotation * state.Rotation);
            transform.localScale = state.Scale;
            State = state.State;
            DepthRecorded = state.DepthRecorded; DiscoveryDepth = state.DiscoveryDepth; DisplaySocket = state.DisplaySocket;
            if (physical != null) physical.Restore(state.PhysicsReleased);
            visual.enabled = hitCollider.enabled = State != FindState.Collected;
            gameObject.SetActive(State != FindState.Collected);
            RefreshExposure();
        }

        public void RefreshExposure(bool terrainChanged = true)
        {
            using var profile = ExposureMarker.Auto();
            if (terrain == null || State == FindState.Collected) return;
            if (State != FindState.World) { visual.enabled = hitCollider.enabled = true; return; }
            // Sample the local density neighbourhood in one compiled batch;
            // no per-find native allocation or full-world density copy.
            Matrix4x4 localToTerrain = terrain.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            Exposure = terrain.MeasureExposure(exposureSamples, exposureBounds, localToTerrain);
            // Soil occlusion is not GPU occlusion: submitting every buried high-detail
            // mesh still costs vertex/shadow work. Keep the original collider targetable
            // and wake rendering conservatively as excavation approaches the whole bounds.
            RefreshVisibility();
            // Keep the real surface targetable even when a newly visible sliver falls
            // between exposure samples. The nearest terrain collider still occludes it.
            if (!hitCollider.enabled) hitCollider.enabled = true;
            if (terrainChanged && physical != null) physical.TerrainChanged();
        }

        private static readonly Unity.Profiling.ProfilerMarker ExposureMarker = new Unity.Profiling.ProfilerMarker("Discovery.Exposure");

        internal void SetXrayVisible(bool visible)
        {
            if (xrayVisible == visible) return;
            xrayVisible = visible;
            RefreshVisibility();
        }

        private void RefreshVisibility() => visual.enabled = State != FindState.Collected
            && (State != FindState.World || xrayVisible || Exposure > 0 || IsHeld || terrain.MayExpose(WorldBounds));

        internal bool HasSoilAttachment(float surfaceTolerance = .005f)
        {
            Matrix4x4 localToTerrain = terrain.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            if (terrain.IsSolidLocal(localToTerrain.MultiplyPoint3x4(Vector3.zero))) return true;
            foreach (var sample in exposureSamples)
                if (terrain.SignedDensityLocal(localToTerrain.MultiplyPoint3x4(sample)) > surfaceTolerance
                    || terrain.SignedDensityLocal(localToTerrain.MultiplyPoint3x4(sample * .65f)) > .005f) return true;
            return false;
        }

        internal bool CanReleaseFromSoil()
        {
            if (State != FindState.World || terrain == null || terrain.IsRestoring) return false;
            Matrix4x4 localToTerrain = terrain.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            if (terrain.IsSolidLocal(localToTerrain.MultiplyPoint3x4(Vector3.zero))) return false;
            bool nearlyFree = Exposure >= .9f;
            float tolerance = nearlyFree ? terrain.CellSize * .35f : .005f;
            foreach (var sample in exposureSamples)
                if (terrain.SignedDensityLocal(localToTerrain.MultiplyPoint3x4(sample)) > tolerance
                    || terrain.SignedDensityLocal(localToTerrain.MultiplyPoint3x4(sample * .65f)) > .005f) return false;
            return true;
        }

        public string GetPrompt(FpsPlayer player)
        {
            if (State == FindState.Stored) return $"{DisplayName}  |  Ready for the exhibit stand";
            if (State == FindState.Displayed) return LoreCard + $"\n{player.InputSettings.Display(PlayerBinding.Interact)} to inspect";
            if (State == FindState.Extracting) return player.Winch != null ? player.Winch.Prompt : "Recovery in progress";
            if (Collected || !isActiveAndEnabled) return "";
            ObserveDiscovery();
            if (!ExposureReady) return $"Uncover more  |  {Mathf.RoundToInt(Exposure * 100)}% / {Mathf.RoundToInt(RequiredExposure * 100)}% exposed";
            if (RopeTarget) return $"{DisplayName}  |  Hold {player.InputSettings.Display(PlayerBinding.Interact)} to mark for excavation";
            string collect = player.Inventory.IsFull ? "Inventory full"
                : $"{(player.InputSettings.ToggleDig ? "Toggle" : "Hold")} {player.InputSettings.Display(PlayerBinding.Dig)} to collect";
            string lift = CanLift(player) ? $"  |  {player.InputSettings.Display(PlayerBinding.Grab)} to lift" : "";
            return $"{Item.DisplayName}  |  {collect}{lift}";
        }

        internal bool TryGetCoveringSoil(FpsPlayer player, int worldMask, out RaycastHit soil)
        {
            soil = default;
            if (Collectible || State != FindState.World || IsHeld || terrain == null || !terrain.CanDig) return false;
            Vector3 eye = player.ViewCamera.transform.position;
            if (terrain.IsSolid(eye)) return false;
            float nearest = float.PositiveInfinity;
            float proximity = player.EffectiveShovel.Radius + terrain.CellSize;
            foreach (Vector3 sample in exposureSamples)
            {
                Vector3 covered = transform.TransformPoint(sample);
                if (!terrain.IsSolid(covered)) continue;
                Vector3 direction = (covered - eye).normalized;
                int count = Physics.RaycastNonAlloc(eye, direction, coveringHits, player.EffectiveDigReach,
                    worldMask, QueryTriggerInteraction.Ignore);
                if (count == coveringHits.Length) continue;
                RaycastHit first = default;
                float distance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    var candidate = coveringHits[i];
                    // Only the aimed find is transparent to this stroke.
                    // Other finds, walls and props remain physical blockers.
                    if (candidate.collider == hitCollider || candidate.distance >= distance) continue;
                    first = candidate;
                    distance = candidate.distance;
                }
                if (first.collider == null || first.collider.GetComponentInParent<TerrainVolume>() != terrain
                    || (first.point - covered).sqrMagnitude > proximity * proximity
                    || WorldBounds.SqrDistance(first.point) > proximity * proximity || distance >= nearest) continue;
                nearest = distance;
                soil = first;
            }
            return soil.collider != null;
        }

        public bool TryCollect(FpsPlayer player)
        {
            // Aimed and nearby pickup share one inventory transaction.
            if (!CanCollect(player)
                || terrain.IsSolid(player.ViewCamera.transform.position)
                || !player.TryGetTarget(player.PickupReach(this), out var hit) || hit.collider != hitCollider) return false;
            return CommitCollection(player);
        }

        internal bool TryCollectNearby(FpsPlayer player)
        {
            if (!CanCollect(player) || !FullyUncovered || !player.CanCollectNearby(this)) return false;
            return CommitCollection(player);
        }

        private bool CanCollect(FpsPlayer player) => player != null && !player.IsMenuOpen && player.HasGameplayFocus
            && (player.Persistence == null || !player.Persistence.BlocksPlay)
            && player.HeldFind == null && isActiveAndEnabled && Collectible && terrain != null && !terrain.IsRestoring;

        private bool CommitCollection(FpsPlayer player)
        {
            if (!player.Inventory.TryAdd(Item))
            {
                player.ShowFeedback(player.Inventory.IsFull ? "Inventory full - find left in place" : "Find already carried");
                return false;
            }
            State = FindState.Collected;
            field?.NotifyMotion();
            player.AnimateCollection(visual, GetComponent<MeshFilter>());
            hitCollider.enabled = false;
            visual.enabled = false;
            player.ShowFeedback($"Collected {Item.DisplayName}  |  Finds {player.Inventory.Count}/{player.Inventory.Capacity}");
            gameObject.SetActive(false);
            return true;
        }

        internal bool CanLift(FpsPlayer player) => physical != null && player != null && !player.IsMenuOpen
            && isActiveAndEnabled && Collectible && !terrain.IsSolid(player.ViewCamera.transform.position)
            && player.TryGetTarget(player.Tuning.InteractReach, out var hit) && hit.collider == hitCollider;

        internal void ObserveDiscovery()
        {
            if (!RopeTarget || DepthRecorded || State != FindState.World) return;
            DepthRecorded = true; DiscoveryDepth = Mathf.Max(0, terrain.SurfaceHeight - transform.position.y);
            field?.NotifyMotion();
        }

        internal bool Transition(FindState expected, FindState next, string socket = "")
        {
            if (!RopeTarget || State != expected) return false;
            bool valid = expected == FindState.World && next == FindState.Extracting
                || expected == FindState.Extracting && (next == FindState.World || next == FindState.Stored)
                || expected == FindState.Stored && next == FindState.Displayed && !string.IsNullOrEmpty(socket);
            if (!valid) return false;
            State = next; DisplaySocket = socket; field?.NotifyMotion(); RefreshExposure(false);
            return true;
        }

        internal void MoveRecovered(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (physical != null) { physical.Body.position = position; physical.Body.rotation = rotation; }
            field?.NotifyMotion();
        }

        public bool TryInteract(FpsPlayer player)
        {
            if (player == null || !player.GameplayActive) return false;
            if (State == FindState.Displayed) { player.ShowFeedback(LoreCard); return true; }
            return State == FindState.Extracting && player.Winch != null && player.Winch.Retry(this);
        }
    }
}
