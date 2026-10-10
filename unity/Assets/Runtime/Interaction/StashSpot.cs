using UnityEngine;

namespace SomethingDownThere
{
    // A carry unique's own place in the camp workshop (119): a glowing see-through copy of that unique stands there,
    // pulsing, while the player carries it, and aiming at the copy offers to set the unique down there for good.
    // Configure Camp Workshop builds one per carry unique from art/stash-uniques/catalog.json; its transform is where the
    // unique's base stands.
    public sealed class StashSpot : MonoBehaviour, IInteractionTarget
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        [SerializeField] private string contentId;
        [SerializeField] private DiscoveryField discoveries;
        [SerializeField] private Renderer ghost;
        [SerializeField] private BoxCollider aim;
        [SerializeField] private Color glow = new Color(1f, .86f, .62f) * .9f;
        [SerializeField, Min(0)] private float pulsePerSecond = .7f;
        private long seenStash = -1, seenPopulation = -1;
        private BuriedFind find;
        private MaterialPropertyBlock block;

        public string ContentId => contentId;
        public bool Waiting { get; private set; }

        private void Update()
        {
            if (discoveries == null) return;
            if (discoveries.StashRevision != seenStash || discoveries.PopulationRevision != seenPopulation)
            {
                seenStash = discoveries.StashRevision; seenPopulation = discoveries.PopulationRevision;
                find = discoveries.FindUnique(contentId);
                Waiting = find != null && find.State == FindState.Carried;
                ghost.enabled = aim.enabled = Waiting;
            }
            if (!Waiting) return;
            block ??= new MaterialPropertyBlock();
            float pulse = .6f + .4f * Mathf.Sin(Time.time * pulsePerSecond * Mathf.PI * 2);
            block.SetColor(EmissionId, glow * pulse);
            ghost.SetPropertyBlock(block);
        }

        public string GetPrompt(FpsPlayer player) => Waiting && find != null
            ? $"{find.DisplayName}  |  {player.InputSettings.Display(PlayerBinding.Interact)} to place" : "";

        public bool TryInteract(FpsPlayer player)
        {
            if (!Waiting || find == null || player == null || !player.GameplayActive) return false;
            if (!find.PlaceAt(transform.position, transform.rotation)) return false;
            player.ShowFeedback($"{find.DisplayName} has its place in the workshop");
            return true;
        }
    }
}
