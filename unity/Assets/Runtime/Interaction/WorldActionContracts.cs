using UnityEngine;

namespace SomethingDownThere
{
    // Only return true after committing a real change. The caller charges energy afterwards.
    public interface IDigTarget
    {
        bool CanDig { get; }
        string DigPrompt { get; }
        bool TryDig(RaycastHit hit);
    }

    public interface IInteractionTarget
    {
        string GetPrompt(FpsPlayer player);
        bool TryInteract(FpsPlayer player);
    }

    // Holding Interact on it completes an action (concept 05 §3): the crane's lifting eye on a unique, a chest's rusted
    // lock. HoldInteraction owns the timing; releasing or losing the target cancels.
    public interface IHoldTarget
    {
        bool CanHold(FpsPlayer player);
        float HoldSeconds(FpsPlayer player);
        bool CompleteHold(FpsPlayer player, RaycastHit hit);
    }

    public abstract class StationTarget : MonoBehaviour, IInteractionTarget
    {
        public abstract string Title { get; }
        public abstract string Description(FpsPlayer player);
        public abstract int CommandCount { get; }
        public abstract string CommandLabel(int index, FpsPlayer player);
        public abstract bool CanExecute(int index, FpsPlayer player);
        public abstract bool TryExecute(int index, FpsPlayer player);
        public virtual void RefreshOffers(FpsPlayer player) { }
        public virtual string GetPrompt(FpsPlayer player) => player.InputSettings.Display(PlayerBinding.Interact) + " " + Title;

        public bool TryInteract(FpsPlayer player)
        {
            if (!isActiveAndEnabled || player == null || !player.GameplayActive) return false;
            player.OpenStation(this);
            return player.Station == this && player.Menu == PlayerMenu.Station;
        }
    }
}
