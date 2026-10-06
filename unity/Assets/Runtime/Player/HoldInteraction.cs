using UnityEngine;

namespace SomethingDownThere
{
    // Holding Interact on the aimed target until its hold completes (IHoldTarget): the crane's lifting eye on a unique, a
    // chest's rusted lock. Releasing or losing the target cancels; after a cancel the next hold needs a release first.
    public sealed class HoldInteraction
    {
        private readonly FpsPlayer player;
        private IHoldTarget target;
        private float seconds, duration = 1;
        private int bindingRevision = -1;
        private bool waitForRelease;
        public float Progress => target == null ? 0 : Mathf.Clamp01(seconds / duration);
        public IHoldTarget Target => target;
        public HoldInteraction(FpsPlayer player) => this.player = player;
        public void Reset() { seconds = 0; target = null; waitForRelease = true; }

        internal bool TryGetTarget(out IHoldTarget held, out RaycastHit hit)
        {
            held = null; hit = default;
            if (!player.GameplayActive || player.WorksiteTools != null && player.WorksiteTools.IsPlacing
                || player.BindingCapture != null && player.BindingCapture.BlocksInput
                || !player.TryGetTarget(player.Tuning.InteractReach, out hit)) return false;
            held = FpsPlayer.Contract<IHoldTarget>(hit.collider);
            return held != null && held.CanHold(player);
        }

        public bool Tick(FpsInputFrame input, float dt)
        {
            if (bindingRevision != player.InputSettings.Revision) { Reset(); bindingRevision = player.InputSettings.Revision; }
            if (!input.InteractHeld) { seconds = 0; target = null; waitForRelease = false; return false; }
            if (waitForRelease) return false;
            if (!TryGetTarget(out var held, out var hit)) { Reset(); return false; }
            if (target != null && target != held) { Reset(); return true; }
            target = held;
            duration = Mathf.Max(.01f, held.HoldSeconds(player));
            seconds += Mathf.Max(0, dt);
            if (seconds >= duration)
            {
                held.CompleteHold(player, hit);
                Reset();
            }
            return true;
        }
    }
}
