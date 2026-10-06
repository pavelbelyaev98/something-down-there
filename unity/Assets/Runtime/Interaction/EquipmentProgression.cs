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
        public const int LevelCount = 12;
        public const int DrillLevel = 7;
        public const float FuelPerCredit = 100f;
        public const float ShavingIntervalScale = 0.1f;
        // The drill bores tip first (user, 2026-10-05): its bit's cone is DrillBoreLengthRatio of the bite radius long,
        // and each cut pushes its tip DrillAdvanceRatio of the radius past the contact (less in harder ground), up to
        // DrillPushes such steps while it has taken less than DrillEngagedShare of a full layer, so its first moments in
        // fresh or hard ground are not a slow needle.
        public const float DrillAdvanceRatio = 0.12f, DrillBoreLengthRatio = 1.2f, DrillEngagedShare = 0.6f;
        public const int DrillPushes = 3;
        // Work lamps are not a track: New Game gives a few, every further lamp is bought once at a flat
        // price and kept for good. The cap bounds saves; only the nearest lamps shine at once.
        public const int StarterLamps = 4, MaximumLamps = 200, LampPrice = 20;
        // Relative to the owned tool: every tier retains material character and every ground remains
        // diggable. Fuel follows cadence, so a slower stroke costs proportionally more.
        private static readonly MaterialToolResponse Soil = new MaterialToolResponse(1f, 1f, 1f, 1f);
        // Backfill: loose, mixed refill; the tool suddenly sinks in (the disturbed-ground tell).
        private static readonly MaterialToolResponse Backfill = new MaterialToolResponse(1.167f, 1.109f, 1.313f, .92f);
        // Developer ground tuning: session-only replacements for the authored responses.
        private static readonly MaterialToolResponse?[] ResponseOverrides = new MaterialToolResponse?[(int)TerrainMaterialSnapshot.Last + 1];
        public static bool HasResponseOverrides => System.Array.Exists(ResponseOverrides, value => value.HasValue);
        public static void OverrideResponse(TerrainMaterialId material, MaterialToolResponse response) => ResponseOverrides[(int)material] = response;
        public static void ClearResponseOverrides() => System.Array.Clear(ResponseOverrides, 0, ResponseOverrides.Length);
        public static MaterialToolResponse MaterialResponse(TerrainMaterialId material)
            => ResponseOverrides[(int)material] ?? AuthoredResponse(material);
        // What each ground does beyond its bite (concept 03 section 4), for the developer ground table.
        public static string GroundEffect(TerrainMaterialId material) => material switch
        {
            TerrainMaterialId.Soil => "Plain ground; never collapses",
            TerrainMaterialId.Backfill => "Disturbed ground: loose fill; never collapses",
            _ => ""
        };
        public static MaterialToolResponse AuthoredResponse(TerrainMaterialId material) => material switch
        {
            TerrainMaterialId.Soil => Soil,
            TerrainMaterialId.Backfill => Backfill,
            _ => throw new System.ArgumentOutOfRangeException(nameof(material))
        };
        // Softest to hardest; each ground keeps its resistance at every tier.
        public static readonly TerrainMaterialId[] HardnessOrder = { TerrainMaterialId.Backfill, TerrainMaterialId.Soil };
        // Every track pays the same for the same next level. No scene-owned copies.
        private static readonly int[] TierPrices = { 10, 25, 55, 100, 180, 300, 480, 750, 1100, 1600, 2300 };
        private static readonly int[] Slots = { 5, 5, 10, 10, 15, 20, 25, 30, 40, 40, 40 };
        private static readonly float[] Fuel = { 50, 50, 100, 100, 150, 200, 250, 300, 400, 500, 600 };
        // User-set ends (playtest 2026-09-27): level 1 bites 0.229 m at 2.11x the stroke time, the
        // last level (12) bites 0.708 m at 2.63x with 1.42 m extra reach. Shovel levels grow evenly
        // (~1.12x), the drill (level 7) is a clear step up (~1.27x) so it out-digs the last shovel
        // even on fresh rock, then grows evenly (~1.07x, every purchase still >1.2x volume) to the
        // last level; cadence and reach grow linearly. A zone's main ground is matched two
        // purchases later.
        private static readonly float[] BiteRadii = { .229f, .2565f, .2873f, .3217f, .3603f, .4036f, .5114f, .5458f, .5824f, .6216f, .6633f, .708f };
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
        private static readonly float[] AscentSpeeds = { 8, 8.8f, 9.6f, 10.5f, 11.3f, 12.1f, 12.9f, 13.7f, 14.5f, 15.4f, 16.2f, 17 };
        private static readonly float[] AscentEnergy = { 8, 8.05f, 8.1f, 8.15f, 8.2f, 8.25f, 8.3f, 8.35f, 8.4f, 8.45f, 8.48f, 8.5f };
        public static JetpackProfile Jetpack(int level) => level >= 1 && level <= LevelCount
            ? new JetpackProfile(AscentSpeeds[level - 1], 30f + 18f * (level - 1) / (LevelCount - 1), AscentEnergy[level - 1], level >= HoverLevel)
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
