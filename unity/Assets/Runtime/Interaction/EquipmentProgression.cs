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
        public const int LevelCount = 10;
        public const int DrillLevel = 7;
        public const float FuelPerCredit = 100f;
        public const float ShavingIntervalScale = 0.1f;
        public const float ShavingDepthRatio = 0.12f;
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
        // Every track pays the same for the same next level. No scene-owned copies.
        private static readonly int[] TierPrices = { 10, 25, 55, 100, 180, 300, 480, 750, 1100 };
        private static readonly int[] Slots = { 5, 5, 10, 10, 15, 20, 25, 30, 40 };
        private static readonly float[] Fuel = { 50, 50, 100, 100, 150, 200, 250, 300, 400 };
        public static ShovelProfile[] ToolProfiles() => new[]
        {
            new ShovelProfile(.230000f, 1.60f, 0f), new ShovelProfile(.264000f, 1.50f, .2f),
            new ShovelProfile(.302000f, 1.40f, .4f), new ShovelProfile(.345807f, 1.30f, .6f),
            new ShovelProfile(.440000f, 1.20f, .8f), new ShovelProfile(.560000f, 1.10f, 1f),
            new ShovelProfile(.680000f, 1.00f, 1.2f), new ShovelProfile(.800000f, .90f, 1.4f),
            new ShovelProfile(.940000f, .80f, 1.6f), new ShovelProfile(1.100000f, .70f, 1.8f)
        };
        public static bool UsesDrill(int level) => level >= DrillLevel;
        public static string ToolName(int level) => UsesDrill(level) ? "Drill" : "Shovel";
        public static int Price(int ownedLevel) => TierPrices[UpgradeIndex(ownedLevel)];
        public static int InventoryIncrease(int ownedLevel) => Slots[UpgradeIndex(ownedLevel)];
        public static float FuelIncrease(int ownedLevel) => Fuel[UpgradeIndex(ownedLevel)];
        private static int UpgradeIndex(int ownedLevel) => ownedLevel >= 1 && ownedLevel < LevelCount
            ? ownedLevel - 1 : throw new System.ArgumentOutOfRangeException(nameof(ownedLevel));
        public static string Name(EquipmentKind kind) => kind == EquipmentKind.Inventory ? "Backpack"
            : kind == EquipmentKind.Fuel ? "Fuel tank" : "Tool";
    }
}
