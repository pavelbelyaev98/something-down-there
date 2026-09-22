namespace SomethingDownThere
{
    public enum EquipmentKind { Shovel, Inventory, Fuel }

    // Authored capacity increments also preserve unusual capacities in older saves.
    public static class EquipmentProgression
    {
        public const int LevelCount = 5;
        public const float FuelPerCredit = 100f;
        public const float ShavingIntervalScale = 0.1f;
        public const float ShavingDepthRatio = 0.05f;
        // One ladder for every track: tier 2 costs the same whether it buys bite, bag or
        // tank. The shovel runs one tier deeper, so it consumes the last entry too.
        public static readonly int[] TierPrices = { 10, 25, 55, 100, 180 };
        private static readonly int[] Slots = { 5, 5, 10, 10 };
        private static readonly float[] Fuel = { 50, 50, 100, 100 };
        public static int Price(int ownedLevel) => TierPrices[ownedLevel - 1];
        public static int InventoryIncrease(int ownedLevel) => Slots[ownedLevel - 1];
        public static float FuelIncrease(int ownedLevel) => Fuel[ownedLevel - 1];
        public static string Name(EquipmentKind kind) => kind == EquipmentKind.Inventory ? "Backpack"
            : kind == EquipmentKind.Fuel ? "Fuel tank" : "Shovel";
    }
}
