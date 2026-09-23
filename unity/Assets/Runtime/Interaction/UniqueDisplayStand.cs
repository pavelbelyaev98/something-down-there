using UnityEngine;

namespace SomethingDownThere
{
    public sealed class UniqueDisplayStand : MonoBehaviour, IInteractionTarget
    {
        [SerializeField] private string socketId = "first-exhibit";
        [SerializeField] private DiscoveryField discoveries;
        [SerializeField] private Transform displayAnchor;
        public string SocketId => socketId;
        public bool Configured => discoveries!=null && displayAnchor!=null && !string.IsNullOrEmpty(socketId);
        public BuriedFind Displayed
        {
            get { foreach(var find in discoveries.Finds) if(find.State==FindState.Displayed && find.DisplaySocket==socketId) return find; return null; }
        }
        public string GetPrompt(FpsPlayer player)
        {
            var displayed=Displayed;
            if(displayed!=null) return displayed.LoreCard + $"\n{player.InputSettings.Display(PlayerBinding.Interact)} to inspect";
            var stored=discoveries.StoredUnique;
            return stored==null ? "Exhibit stand" : stored.LoreCard + $"\n{player.InputSettings.Display(PlayerBinding.Interact)} to place";
        }
        public bool TryInteract(FpsPlayer player)
        {
            if(player==null || !player.GameplayActive) return false;
            var displayed=Displayed;
            if(displayed!=null) { player.ShowFeedback(displayed.LoreCard); return true; }
            var stored=discoveries.StoredUnique;
            if(stored==null || !stored.Transition(FindState.Stored,FindState.Displayed,socketId)) return false;
            Vector3 half=Vector3.Scale(stored.LocalHull.extents,stored.transform.lossyScale);
            Vector3 center=Vector3.Scale(stored.LocalHull.center,stored.transform.lossyScale);
            stored.MoveRecovered(displayAnchor.position+Vector3.up*(half.y+.02f)-displayAnchor.rotation*center,displayAnchor.rotation);
            player.ShowFeedback(stored.LoreCard); player.Persistence?.RequestCheckpoint(); return true;
        }
    }
}
