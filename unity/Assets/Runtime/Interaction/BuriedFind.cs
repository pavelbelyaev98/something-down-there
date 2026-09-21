using UnityEngine;

namespace SomethingDownThere
{
    public enum FindSize { Small, Large }

    // Authored meshes carry real surface samples. Ellipsoid fallback is legacy fixture support.
    [DisallowMultipleComponent, RequireComponent(typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class BuriedFind : MonoBehaviour
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
        private TerrainVolume terrain;
        private MeshCollider hitCollider;
        private MeshRenderer visual;
        private FindPhysics physical;
        private readonly RaycastHit[] coveringHits = new RaycastHit[32];
        public InventoryItem Item { get; private set; }
        public float Exposure { get; private set; }
        public bool Collected { get; private set; }
        public FindSize Size => size;
        // Visibility/range are checked against the actual collider when collecting.
        public float RequiredExposure => Mathf.Clamp(collectionThreshold, 0.1f, 1f);
        public bool IsHeld => physical != null && physical.Held;
        public bool IsReleased => physical != null && physical.Released;
        public bool Collectible => Item != null && !Collected && !IsHeld && Exposure >= RequiredExposure;
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
            hitCollider = GetComponent<MeshCollider>();
            visual = GetComponent<MeshRenderer>();
            if (owner.TryGetComponent<ExcavationDaylight>(out var daylight)) daylight.Register(visual);
            physical = GetComponent<FindPhysics>();
            Item = new InventoryItem(identity, displayName, saleValue);
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
            if (physical != null) physical.Initialize(owner, population);
            RefreshExposure();
        }

        private bool UsesPhysicalPose => physical != null && !Collected && physical.Released && !physical.Body.isKinematic;
        public FindSnapshot Capture() => new FindSnapshot { ContentId = saveContentId, Item = ItemSnapshot.Capture(Item),
            // Inactive collected bodies no longer have a live PhysX pose; preserve their transform history.
            Position = terrain.transform.InverseTransformPoint(UsesPhysicalPose ? physical.Body.position : transform.position),
            Rotation = Quaternion.Inverse(terrain.transform.rotation) * (UsesPhysicalPose ? physical.Body.rotation : transform.rotation),
            Scale = transform.localScale, Collected = Collected, PhysicsReleased = physical != null && physical.Released };

        public void Restore(FindSnapshot state)
        {
            Item = state.Item.Restore();
            transform.SetPositionAndRotation(terrain.transform.TransformPoint(state.Position), terrain.transform.rotation * state.Rotation);
            transform.localScale = state.Scale;
            if (physical != null) physical.Restore(state.PhysicsReleased);
            Collected = state.Collected;
            visual.enabled = hitCollider.enabled = !Collected;
            gameObject.SetActive(!Collected);
            RefreshExposure();
        }

        public void RefreshExposure(bool terrainChanged = true)
        {
            if (Collected || terrain == null) return;
            int clear = 0;
            foreach (Vector3 sample in exposureSamples)
                if (!terrain.IsSolid(transform.TransformPoint(sample))) clear++;
            Exposure = clear / (float)exposureSamples.Length;
            // Soil occlusion is not GPU occlusion: submitting every buried high-detail
            // mesh still costs vertex/shadow work. Keep the original collider targetable
            // and wake rendering conservatively as excavation approaches the whole bounds.
            visual.enabled = Exposure > 0 || IsHeld || terrain.MayExpose(WorldBounds);
            // Keep the real surface targetable even when a newly visible sliver falls
            // between exposure samples. The nearest terrain collider still occludes it.
            if (!hitCollider.enabled) hitCollider.enabled = true;
            if (terrainChanged && physical != null) physical.TerrainChanged();
        }

        internal bool HasSoilAttachment(float surfaceTolerance = .005f)
        {
            if (terrain.IsSolid(transform.position)) return true;
            foreach (var sample in exposureSamples)
                if (terrain.SignedDensity(transform.TransformPoint(sample)) > surfaceTolerance
                    || terrain.SignedDensity(transform.TransformPoint(sample * .65f)) > .005f) return true;
            return false;
        }

        internal bool CanReleaseFromSoil()
        {
            if (terrain == null || terrain.IsRestoring || terrain.IsSolid(transform.position)) return false;
            bool nearlyFree = Exposure >= .9f;
            float tolerance = nearlyFree ? terrain.CellSize * .35f : .005f;
            foreach (var sample in exposureSamples)
                if (terrain.SignedDensity(transform.TransformPoint(sample)) > tolerance
                    || terrain.SignedDensity(transform.TransformPoint(sample * .65f)) > .005f) return false;
            return true;
        }

        public string GetPrompt(FpsPlayer player)
        {
            if (Collected || !isActiveAndEnabled) return "";
            if (!Collectible) return $"Uncover more  |  {Mathf.RoundToInt(Exposure * 100)}% / {Mathf.RoundToInt(RequiredExposure * 100)}% exposed";
            string collect = player.Inventory.IsFull ? "Inventory full"
                : $"{(player.InputSettings.ToggleDig ? "Toggle" : "Hold")} {player.InputSettings.Display(PlayerBinding.Dig)} to collect";
            string lift = CanLift(player) ? $"  |  {player.InputSettings.Display(PlayerBinding.Grab)} to lift" : "";
            return $"{Item.DisplayName}  |  {collect}{lift}";
        }

        internal bool TryGetCoveringSoil(FpsPlayer player, int worldMask, out RaycastHit soil)
        {
            soil = default;
            if (Collectible || Collected || IsHeld || terrain == null || !terrain.CanDig) return false;
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
            Collected = true;
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
    }
}
