namespace SomethingDownThere
{
    public enum EquipmentKind { Shovel, Inventory, Fuel }

    public readonly struct MaterialToolResponse
    {
        public readonly float Width, Length, Penetration, Interval;
        public MaterialToolResponse(float width, float length, float penetration, float interval)
        { Width = width; Length = length; Penetration = penetration; Interval = interval; }
    }

    public static class EquipmentProgression
    {
        public const float DetectorRange = 4.5f;
        public const float DetectorRetentionRange = 5f;
        public const float DetectorWeakAngle = 45f;
        public const float DetectorMediumAngle = 25f;
        public const float DetectorStrongAngle = 10f;
        public const float DetectorAngleHysteresis = 2f;
        public const int LevelCount = 5;
        public const float FuelPerCredit = 100f;
        public const float ShavingIntervalScale = 0.1f;
        public const float ShavingDepthRatio = 0.05f;
        // Relative to the owned tool: every tier retains material character and all
        // families remain diggable. Fuel follows cadence, preserving powered drain.
        private static readonly MaterialToolResponse Soil = new MaterialToolResponse(1f, 1f, 1f, 1f);
        private static readonly MaterialToolResponse Clay = new MaterialToolResponse(1f, .76f, .85f, 1.15f);
        private static readonly MaterialToolResponse Rock = new MaterialToolResponse(.84f, .84f, .65f, 1.4f);
        public static MaterialToolResponse MaterialResponse(TerrainMaterialId material) => material switch
        {
            TerrainMaterialId.Soil => Soil,
            TerrainMaterialId.Clay => Clay,
            TerrainMaterialId.Rock => Rock,
            _ => throw new System.ArgumentOutOfRangeException(nameof(material))
        };
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
