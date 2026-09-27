namespace SomethingDownThere
{
    // The owned jetpack level; stats come from EquipmentProgression.Jetpack, never the scene.
    public sealed class JetpackState
    {
        public int Level { get; private set; } = 1;
        public JetpackProfile Current => EquipmentProgression.Jetpack(Level);

        public bool TryUpgradeTo(int level)
        {
            if (level != Level + 1 || level > EquipmentProgression.LevelCount) return false;
            Level = level;
            return true;
        }
    }
}
