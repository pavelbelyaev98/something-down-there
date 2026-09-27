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
        // Loose stones: a broad bite whose grainy edge and floor (ExcavationGrid) leave it a little slower than soil.
        private static readonly MaterialToolResponse Gravel = new MaterialToolResponse(1.1f, 1f, 1f, 1.08f);
        // Tough but never a wall: small, shallow chips at a slow cadence still make visible progress.
        private static readonly MaterialToolResponse Concrete = new MaterialToolResponse(.7f, .7f, .5f, 1.8f);
        // Clay basins' old pond clay: clean, smooth shavings that bite clearly easier than clay (the tell).
        private static readonly MaterialToolResponse PondClay = new MaterialToolResponse(1f, .88f, .92f, 1.15f);
        // Beside a crack: broken rock crumbles ~1.5x faster than rock (the tell, felt in the dark);
        // the crack line itself cuts exactly like it. Broken concrete ~3.5x concrete.
        private static readonly MaterialToolResponse FracturedRock = new MaterialToolResponse(1f, .88f, .88f, 1.25f);
        private static readonly MaterialToolResponse FracturedConcrete = new MaterialToolResponse(.85f, .85f, .7f, 1.45f);
        // Backfill: loose, mixed refill; the tool suddenly sinks in (the disturbed-ground tell).
        private static readonly MaterialToolResponse Backfill = new MaterialToolResponse(1.1f, 1.05f, 1.25f, .85f);
        public static MaterialToolResponse MaterialResponse(TerrainMaterialId material) => material switch
        {
            TerrainMaterialId.Soil => Soil,
            TerrainMaterialId.Clay => Clay,
            TerrainMaterialId.Rock => Rock,
            TerrainMaterialId.Gravel => Gravel,
            TerrainMaterialId.Concrete => Concrete,
            TerrainMaterialId.PondClay => PondClay,
            TerrainMaterialId.FracturedRock or TerrainMaterialId.Crack => FracturedRock,
            TerrainMaterialId.FracturedConcrete => FracturedConcrete,
            TerrainMaterialId.Backfill => Backfill,
            _ => throw new System.ArgumentOutOfRangeException(nameof(material))
        };
        // Softest to hardest; each family keeps its resistance at every tier. Families sharing one
        // response (fractured rock and its crack line) are one hardness class.
        public static readonly TerrainMaterialId[] HardnessOrder =
            { TerrainMaterialId.Backfill, TerrainMaterialId.Soil, TerrainMaterialId.Gravel, TerrainMaterialId.PondClay, TerrainMaterialId.FracturedRock, TerrainMaterialId.Crack,
              TerrainMaterialId.Clay, TerrainMaterialId.FracturedConcrete, TerrainMaterialId.Rock, TerrainMaterialId.Concrete };
        // Every track pays the same for the same next level. No scene-owned copies.
        private static readonly int[] TierPrices = { 10, 25, 55, 100, 180, 300, 480, 750, 1100 };
        private static readonly int[] Slots = { 5, 5, 10, 10, 15, 20, 25, 30, 40 };
        private static readonly float[] Fuel = { 50, 50, 100, 100, 150, 200, 250, 300, 400 };
        // Zone rule (concept 03): each level outpaces the next zone's main ground one level
        // down (clay at L >= soil at L-1, rock at L >= clay at L-1), so arriving in a zone after
        // one purchase never feels like a restart. Shovel levels therefore grow evenly (~1.84x).
        public static ShovelProfile[] ToolProfiles() => new[]
        {
            new ShovelProfile(.230000f, 1.60f, 0f), new ShovelProfile(.275600f, 1.50f, .2f),
            new ShovelProfile(.330000f, 1.40f, .4f), new ShovelProfile(.392000f, 1.30f, .6f),
            new ShovelProfile(.469000f, 1.20f, .8f), new ShovelProfile(.560000f, 1.10f, 1f),
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
