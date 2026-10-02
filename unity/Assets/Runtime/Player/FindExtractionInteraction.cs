using UnityEngine;

namespace SomethingDownThere
{
    public sealed class FindExtractionInteraction
    {
        private readonly FpsPlayer player;
        private BuriedFind target;
        private float seconds;
        private int bindingRevision = -1;
        private bool waitForRelease;
        public float Progress => player.Crane == null ? 0 : Mathf.Clamp01(seconds / player.Crane.Settings.MarkSeconds);
        public FindExtractionInteraction(FpsPlayer player) => this.player=player;
        public void Reset() { seconds=0; target=null; waitForRelease=true; }
        internal bool TryGetTarget(out BuriedFind find, out RaycastHit hit)
        {
            find=null; hit=default;
            if (!player.GameplayActive || player.Crane==null || !player.Crane.Configured || player.Crane.Busy
                || player.WorksiteTools != null && player.WorksiteTools.IsPlacing
                || player.BindingCapture != null && player.BindingCapture.BlocksInput
                || !player.TryGetTarget(player.Tuning.InteractReach, out hit)) return false;
            find=hit.collider.GetComponentInParent<BuriedFind>();
            return find!=null && find.isActiveAndEnabled && find.CanMark;
        }
        public bool Tick(FpsInputFrame input,float dt)
        {
            if(bindingRevision!=player.InputSettings.Revision) { Reset(); bindingRevision=player.InputSettings.Revision; }
            if(!input.InteractHeld) { seconds=0; target=null; waitForRelease=false; return false; }
            if(waitForRelease) return false;
            if(!TryGetTarget(out var find,out var hit)) { Reset(); return false; }
            if(target!=null && target!=find) { Reset(); return true; }
            target=find;
            seconds+=Mathf.Max(0,dt);
            if(Progress>=1)
            {
                player.Crane.TryMark(find,hit.point,hit.normal);
                Reset();
            }
            return true;
        }
    }
}
