namespace SomethingDownThere
{
    public enum EquipmentKind { Shovel, Inventory, Fuel, Jetpack }

    // How one ground shapes the tool's bite. Hardness shows as bite size, never as cadence: every
    // ground keeps the tool's rhythm, and harder ground takes smaller, shallower bites.
    public readonly struct MaterialToolResponse
    {
        public readonly float Width, Length, Penetration;
        public MaterialToolResponse(float width, float length, float penetration)
        { Width = width; Length = length; Penetration = penetration; }
    }

    public readonly struct JetpackProfile
    {
        public readonly float MaxAscentSpeed, Acceleration, EnergyPerSecond;
        public readonly bool HoverHold;
        public JetpackProfile(float maxAscentSpeed, float acceleration, float energyPerSecond, bool hoverHold)
        { MaxAscentSpeed = maxAscentSpeed; Acceleration = acceleration; EnergyPerSecond = energyPerSecond; HoverHold = hoverHold; }
        public float EnergyPerMetre => EnergyPerSecond / MaxAscentSpeed;
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
        // Relative to the owned tool: every tier retains material character and all families remain
        // diggable. Each stroke costs the same fuel, so hard ground costs more fuel per metre.
        private static readonly MaterialToolResponse Soil = new MaterialToolResponse(1f, 1f, 1f);
        private static readonly MaterialToolResponse Clay = new MaterialToolResponse(.955f, .726f, .81f);
        private static readonly MaterialToolResponse Rock = new MaterialToolResponse(.75f, .75f, .58f);
        // Loose stones: a broad bite whose grainy edge and floor (ExcavationGrid) leave it a little slower than soil.
        private static readonly MaterialToolResponse Gravel = new MaterialToolResponse(1.07f, .975f, .975f);
        // Tough but never a wall: small, shallow chips still make visible progress.
        private static readonly MaterialToolResponse Concrete = new MaterialToolResponse(.575f, .575f, .41f);
        // Clay basins' old pond clay: clean, smooth shavings that bite clearly easier than clay (the tell).
        private static readonly MaterialToolResponse PondClay = new MaterialToolResponse(.954f, .84f, .878f);
        // Beside a crack: broken rock crumbles ~1.5x faster than rock (the tell, felt in the dark);
        // the crack line itself cuts exactly like it. Broken concrete ~2.4x concrete.
        private static readonly MaterialToolResponse FracturedRock = new MaterialToolResponse(.928f, .817f, .817f);
        private static readonly MaterialToolResponse FracturedConcrete = new MaterialToolResponse(.75f, .75f, .62f);
        // Backfill: loose, mixed refill; the tool suddenly sinks in (the disturbed-ground tell).
        private static readonly MaterialToolResponse Backfill = new MaterialToolResponse(1.2f, 1.14f, 1.35f);
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
        // Jetpack (concept 04 section 5): every level climbs faster and cheaper per metre (1.00 -> 0.50
        // energy/m); level 1 is the starter pack. Hover hold arrives with the first purchase.
        public const int HoverLevel = 2;
        public const float HoverEnergyScale = .5f, HoverBrake = 45f, HoverGroundClearance = .5f;
        private static readonly float[] AscentSpeeds = { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 };
        private static readonly float[] AscentEnergy = { 8, 8.1f, 8.2f, 8.25f, 8.3f, 8.3f, 8.4f, 8.4f, 8.5f, 8.5f };
        public static JetpackProfile Jetpack(int level) => level >= 1 && level <= LevelCount
            ? new JetpackProfile(AscentSpeeds[level - 1], 30f + 2f * (level - 1), AscentEnergy[level - 1], level >= HoverLevel)
            : throw new System.ArgumentOutOfRangeException(nameof(level));
        public static bool UsesDrill(int level) => level >= DrillLevel;
        public static string ToolName(int level) => UsesDrill(level) ? "Drill" : "Shovel";
        public static int Price(int ownedLevel) => TierPrices[UpgradeIndex(ownedLevel)];
        public static int InventoryIncrease(int ownedLevel) => Slots[UpgradeIndex(ownedLevel)];
        public static float FuelIncrease(int ownedLevel) => Fuel[UpgradeIndex(ownedLevel)];
        private static int UpgradeIndex(int ownedLevel) => ownedLevel >= 1 && ownedLevel < LevelCount
            ? ownedLevel - 1 : throw new System.ArgumentOutOfRangeException(nameof(ownedLevel));
        public static string Name(EquipmentKind kind) => kind == EquipmentKind.Inventory ? "Backpack"
            : kind == EquipmentKind.Fuel ? "Fuel tank" : kind == EquipmentKind.Jetpack ? "Jetpack" : "Tool";
    }
}
