using UnityEngine;
namespace SomethingDownThere
{
    // Physical handling keeps the world identity and does not change inventory.
    public sealed class FindHandling
    {
        private readonly FpsPlayer player;
        private FindPhysics held;
        private Quaternion holdRotation;
        private float holdDistance;
        public BuriedFind HeldFind => held != null && held.Held && held.isActiveAndEnabled ? held.GetComponent<BuriedFind>() : null;

        public FindHandling(FpsPlayer player) => this.player = player;

        public bool TryLiftOrDrop()
        {
            if (HeldFind != null) { Release(false); return true; }
            if (!player.TryGetTarget(player.Tuning.InteractReach, out var hit)) return false;
            var find = hit.collider.GetComponentInParent<BuriedFind>();
            if (find == null || !find.CanLift(player)) return false;
            held = find.GetComponent<FindPhysics>();
            holdRotation = Quaternion.Inverse(player.ViewCamera.transform.rotation) * held.Body.rotation;
            holdDistance = Mathf.Clamp(find.WorldBounds.extents.magnitude + .7f, .9f, 1.25f);
            held.BeginHold();
            return true;
        }

        public void FixedTick(float deltaTime)
        {
            var find = HeldFind;
            if (find == null) { held = null; return; }
            var eye = player.ViewCamera.transform;
            if ((held.Body.position - eye.position).sqrMagnitude > 3.5f * 3.5f)
            {
                Release(false); player.ShowFeedback("Let go — object out of reach"); return;
            }
            Vector3 target = eye.position + eye.forward * holdDistance + eye.right * .18f - eye.up * .2f;
            if (!held.MoveHeld(target, eye.rotation * holdRotation, player.CarryVelocity, deltaTime))
            {
                held.EndHold(Vector3.zero);
                held = null; player.ShowFeedback("Object left in place");
            }
        }

        public bool Release(bool throwing)
        {
            if (HeldFind == null) { held = null; return false; }
            player.SuppressAutomaticCollection(HeldFind);
            Vector3 velocity = throwing ? (player.ViewCamera.transform.forward + Vector3.up * .12f).normalized * held.ThrowSpeed : Vector3.zero;
            held.EndHold(velocity); held = null;
            return true;
        }

        public void Suspend()
        {
            if (HeldFind != null) held.Body.linearVelocity = held.Body.angularVelocity = Vector3.zero;
        }
    }
}
