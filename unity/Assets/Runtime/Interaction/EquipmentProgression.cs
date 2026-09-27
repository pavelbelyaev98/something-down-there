namespace SomethingDownThere
{
    public enum EquipmentKind { Shovel, Inventory, Fuel, Jetpack }

    // How one ground shapes the tool's bite. Hardness shows mostly as bite size (width, length,
    // depth) and a little as cadence (Interval, relative to the tool's own stroke time).
    public readonly struct MaterialToolResponse
    {
        public readonly float Width, Length, Penetration, Interval;
        public MaterialToolResponse(float width, float length, float penetration, float interval)
        { Width = width; Length = length; Penetration = penetration; Interval = interval; }
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
        // diggable. Fuel follows cadence, so a slower stroke costs proportionally more.
        private static readonly MaterialToolResponse Soil = new MaterialToolResponse(1f, 1f, 1f, 1f);
        private static readonly MaterialToolResponse Clay = new MaterialToolResponse(.98f, .745f, .831f, 1.08f);
        private static readonly MaterialToolResponse Rock = new MaterialToolResponse(.786f, .786f, .608f, 1.15f);
        // Loose stones: a broad bite whose grainy edge and floor (ExcavationGrid) leave it a little slower than soil.
        private static readonly MaterialToolResponse Gravel = new MaterialToolResponse(1.081f, .985f, .985f, 1.03f);
        // Tough but never a wall: small, shallow chips still make visible progress.
        private static readonly MaterialToolResponse Concrete = new MaterialToolResponse(.619f, .619f, .442f, 1.25f);
        // Clay basins' old pond clay: clean, smooth shavings that bite clearly easier than clay (the tell).
        private static readonly MaterialToolResponse PondClay = new MaterialToolResponse(.97f, .854f, .892f, 1.05f);
        // Beside a crack: broken rock takes ~1.5x rock's bite, and a cut into the band breaks it
        // loose along the crack (ExcavationGrid.TryRelease). Broken concrete ~2.4x concrete.
        private static readonly MaterialToolResponse FracturedRock = new MaterialToolResponse(.94f, .828f, .828f, 1.04f);
        private static readonly MaterialToolResponse FracturedConcrete = new MaterialToolResponse(.774f, .774f, .64f, 1.1f);
        // Backfill: loose, mixed refill; the tool suddenly sinks in (the disturbed-ground tell).
        private static readonly MaterialToolResponse Backfill = new MaterialToolResponse(1.167f, 1.109f, 1.313f, .92f);
        // Developer ground tuning: session-only replacements for the authored responses. A crack
        // line follows its fractured band.
        private static readonly MaterialToolResponse?[] ResponseOverrides = new MaterialToolResponse?[(int)TerrainMaterialSnapshot.Last + 1];
        public static bool HasResponseOverrides => System.Array.Exists(ResponseOverrides, value => value.HasValue);
        public static void OverrideResponse(TerrainMaterialId material, MaterialToolResponse response) => ResponseOverrides[(int)material] = response;
        public static void ClearResponseOverrides() => System.Array.Clear(ResponseOverrides, 0, ResponseOverrides.Length);
        public static MaterialToolResponse MaterialResponse(TerrainMaterialId material)
        {
            var key = material == TerrainMaterialId.Crack ? TerrainMaterialId.FracturedRock : material;
            return ResponseOverrides[(int)key] ?? AuthoredResponse(key);
        }
        // What each ground does beyond its bite (concept 03 section 4), for the developer ground table.
        public static string GroundEffect(TerrainMaterialId material) => material switch
        {
            TerrainMaterialId.Soil => "Thin roofs and shelves under 1 m slump when undercut",
            TerrainMaterialId.Gravel => "Pours when undercut (3 m section); heavy finds",
            TerrainMaterialId.Clay => "Steady narrow shavings; never collapses",
            TerrainMaterialId.PondClay => "Basin and odd-spot tell: bites easier than clay",
            TerrainMaterialId.Rock => "Small faceted chips; cracks and veins are its tells",
            TerrainMaterialId.Concrete => "Smallest square chips; cracked walls lead into rooms",
            TerrainMaterialId.FracturedRock or TerrainMaterialId.Crack => "Crack band: a cut breaks it loose along the crack",
            TerrainMaterialId.FracturedConcrete => "Cracked concrete: a cut breaks it loose along the crack",
            TerrainMaterialId.Backfill => "Disturbed ground: sinks in; slumps when undercut (2 m)",
            _ => ""
        };
        public static MaterialToolResponse AuthoredResponse(TerrainMaterialId material) => material switch
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
        // User-set ends (playtest 2026-09-27): level 1 bites 0.229 m at 2.11x the stroke time, level
        // 10 bites 0.708 m at 2.63x with 1.42 m extra reach. Shovel levels grow evenly (~1.13x),
        // the drill (level 7) is a clear step up (~1.27x) so it out-digs the last shovel even on
        // fresh rock, then grows evenly to level 10; cadence and reach grow linearly. A zone's main
        // ground is matched two purchases later.
        private static readonly float[] BiteRadii = { .229f, .2596f, .2943f, .3336f, .3782f, .4287f, .5445f, .5943f, .6487f, .708f };
        public static ShovelProfile[] ToolProfiles()
        {
            var profiles = new ShovelProfile[LevelCount];
            for (int i = 0; i < LevelCount; i++)
            {
                float t = i / (float)(LevelCount - 1);
                profiles[i] = new ShovelProfile(BiteRadii[i], (float)System.Math.Round(2.11 + (2.63 - 2.11) * t, 3), (float)System.Math.Round(1.42 * t, 3));
            }
            return profiles;
        }
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
