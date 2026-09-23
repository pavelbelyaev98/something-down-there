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
        public float Progress => player.Winch == null ? 0 : Mathf.Clamp01(seconds / player.Winch.Settings.MarkSeconds);
        public FindExtractionInteraction(FpsPlayer player) => this.player=player;
        public void Reset() { seconds=0; target=null; waitForRelease=true; }
        public bool Tick(FpsInputFrame input,float dt)
        {
            if(bindingRevision!=player.InputSettings.Revision) { Reset(); bindingRevision=player.InputSettings.Revision; }
            if(!input.InteractHeld) { seconds=0; target=null; waitForRelease=false; return false; }
            if(waitForRelease || !player.GameplayActive || player.Winch==null || player.HeldFind!=null) return false;
            if(!player.TryGetTarget(player.Tuning.InteractReach,out var hit)) { Reset(); return false; }
            var find=hit.collider.GetComponentInParent<BuriedFind>();
            if(find==null || !find.CanMark) { Reset(); return false; }
            if(target!=null && target!=find) { Reset(); return true; }
            target=find;
            seconds+=Mathf.Max(0,dt);
            if(Progress>=1)
            {
                player.Winch.TryMark(find,hit.point,hit.normal);
                Reset();
            }
            return true;
        }
    }
}
