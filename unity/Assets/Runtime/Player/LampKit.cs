namespace SomethingDownThere
{
    // Work lamps the player owns, placed or not. Bought lamps are permanent; placing and picking
    // one up never changes ownership. The count is the only state, so it doubles as the revision.
    public sealed class LampKit
    {
        public int Owned { get; private set; }
        // The Ground Lab's kit never runs out (user, 2026-10-08): as many placed as the largest kit holds; the lab never saves.
        public bool Unlimited { get; internal set; }
        public bool Full => Owned >= EquipmentProgression.MaximumLamps;

        public LampKit(int owned = EquipmentProgression.StarterLamps)
        {
            if (owned < EquipmentProgression.StarterLamps || owned > EquipmentProgression.MaximumLamps)
                throw new System.ArgumentOutOfRangeException(nameof(owned));
            Owned = owned;
        }

        internal bool TryAdd()
        {
            if (Full) return false;
            Owned++;
            return true;
        }
    }
}
