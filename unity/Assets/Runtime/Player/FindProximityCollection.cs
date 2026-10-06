using UnityEngine;

namespace SomethingDownThere
{
    // Nearby free finds collect during movement or held digging, at any height; finds taken by hand never do.
    internal sealed class FindProximityCollection
    {
        private readonly FpsPlayer player;
        private readonly CharacterController motor;
        private readonly int worldMask;
        private Collider[] overlaps = new Collider[128];
        private bool active;
        private const float Radius = 2.25f;
        private Vector3 Probe => player.ViewCamera.transform.position;

        public FindProximityCollection(FpsPlayer player, CharacterController motor, int mask)
        { this.player = player; this.motor = motor; worldMask = mask; }

        public void Clear() => active = false;

        public bool Tick(Vector3 previousFeet, bool movementRequested, bool diggingHeld)
        {
            Vector3 travel = player.FeetPosition - previousFeet; travel.y = 0;
            active = diggingHeld || (movementRequested && travel.sqrMagnitude > .000001f
                && motor.isGrounded && !player.IsJetpackActive);
            if (!active || player.Inventory.IsFull) return false;
            int count;
            do
            {
                count = Physics.OverlapSphereNonAlloc(Probe, Radius, overlaps, worldMask, QueryTriggerInteraction.Ignore);
                if (count < overlaps.Length) break;
                System.Array.Resize(ref overlaps, overlaps.Length * 2);
            } while (true);
            BuriedFind nearest = null; float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var find = overlaps[i].GetComponentInParent<BuriedFind>();
                if (find == null || !find.FullyUncovered || !CanCollect(find)) continue;
                float candidate = find.WorldBounds.SqrDistance(Probe);
                if (candidate >= distance) continue;
                nearest = find; distance = candidate;
            }
            return nearest != null && nearest.TryCollectNearby(player);
        }

        public bool CanCollect(BuriedFind find)
        {
            if (!active || find == null || !find.isActiveAndEnabled || find.HandPicked
                || player.IsMenuOpen || !player.HasGameplayFocus
                || (player.Persistence != null && player.Persistence.BlocksPlay)) return false;
            var terrain = player.ExcavationTerrain;
            if (terrain == null || terrain.IsRestoring || terrain.IsSolid(Probe)) return false;
            Vector3 delta = find.HitCollider.ClosestPoint(Probe) - Probe;
            if (delta.sqrMagnitude > Radius * Radius) return false;
            if (Vector3.Dot(player.ViewCamera.transform.forward, find.WorldBounds.center - Probe) < 0) return false;
            float length = delta.magnitude;
            if (length < .001f) return true;
            return Physics.Raycast(Probe, delta / length, out var hit, length + .015f,
                worldMask, QueryTriggerInteraction.Ignore) && hit.collider == find.HitCollider;
        }
    }
}
