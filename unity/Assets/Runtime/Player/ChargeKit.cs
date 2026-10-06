namespace SomethingDownThere
{
    // The player's C4 (026): the track level (blast radius, pack size, charge price; EquipmentProgression.C4) and the
    // charges owned, carried or armed, like lamps. A charge leaves the kit only when it goes off.
    public sealed class ChargeKit
    {
        public int Level { get; private set; }
        public int Owned { get; private set; }
        public long Revision { get; private set; }
        public C4Profile Current => EquipmentProgression.C4(Level);
        public bool Full => Owned >= Current.PackSize;

        public ChargeKit(int level = 1, int owned = 0)
        {
            if (level < 1 || level > EquipmentProgression.LevelCount) throw new System.ArgumentOutOfRangeException(nameof(level));
            if (owned < 0 || owned > EquipmentProgression.C4(level).PackSize) throw new System.ArgumentOutOfRangeException(nameof(owned));
            Level = level; Owned = owned;
        }

        internal bool TryUpgradeTo(int level)
        {
            if (level != Level + 1 || level > EquipmentProgression.LevelCount) return false;
            Level = level; Revision++;
            return true;
        }

        internal bool TryAdd()
        {
            if (Full) return false;
            Owned++; Revision++;
            return true;
        }

        // Charges that went off.
        internal void Spend(int count)
        {
            if (count < 0 || count > Owned) throw new System.ArgumentOutOfRangeException(nameof(count));
            Owned -= count; Revision++;
        }

        // Developer admin: a full pack.
        internal void Fill() { if (!Full) { Owned = Current.PackSize; Revision++; } }
    }
}
