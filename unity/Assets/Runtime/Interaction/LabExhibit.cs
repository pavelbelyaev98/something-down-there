using UnityEngine;

namespace SomethingDownThere
{
    // A Ground Lab gallery piece (user, 2026-10-06): a copy of one look of a find, set out on the surface to be looked at.
    // Solid scenery that names itself when aimed at; it is never taken.
    [DisallowMultipleComponent]
    public sealed class LabExhibit : MonoBehaviour, IInteractionTarget
    {
        private string label = "";

        public void Describe(string text) => label = text;
        public string GetPrompt(FpsPlayer player) => label;
        public bool TryInteract(FpsPlayer player) => false;
    }
}
