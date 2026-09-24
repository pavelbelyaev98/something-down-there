using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SomethingDownThere
{
    // Data only: captured on the main thread, then owned by the writer until commit.
    // Future progression extends this whole-world boundary, never a separate wallet file.
    public sealed class WorldSnapshot
    {
        public long Sequence, UtcTicks;
        public string SiteId = "main-site-v1";
        public GridSnapshot Terrain;
        public Vector3 TerrainPosition;
        public Quaternion TerrainRotation;
        public int ExcavationSeed, DiscoverySeed;
        public FindSnapshot[] Finds;
        public ExtractionSnapshot Extraction;
        public WorksiteSnapshot Worksite = new WorksiteSnapshot();
        public ItemSnapshot[] Inventory;
        public int InventoryCapacity, Credits, ShovelLevel, SuccessfulStrokes;
        public int InventoryLevel = 1, FuelLevel = 1;
        public float BatteryCapacity, BatteryCharge, Pitch, VerticalSpeed;
        public float CrouchAmount;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;

        public void ValidateTerrain(Vector3Int size, float cellSize, Vector3 position, Quaternion rotation)
        {
            Require(Terrain.Size == size && Terrain.CellSize == cellSize
                && Vector3.Distance(TerrainPosition, position) < .001f && Quaternion.Angle(TerrainRotation, rotation) < .001f,
                "The saved excavation layout does not match this game. Start a new game.");
        }

        public void Validate()
        {
            Require(Sequence > 0 && UtcTicks > 0 && UtcTicks <= DateTime.MaxValue.Ticks, "Invalid checkpoint identity.");
            Require(SiteId == "main-site-v1", "Unknown excavation site.");
            Require(Terrain != null, "Missing excavation.");
            Terrain.Validate();
            Require(Worksite != null, "Missing worksite equipment.");
            Worksite.Validate();
            Require(Valid(TerrainPosition) && Valid(TerrainRotation) && Valid(PlayerPosition) && Valid(PlayerRotation), "Invalid world position.");
            Require(Finite(Pitch) && Math.Abs(Pitch) < 90 && Finite(VerticalSpeed), "Invalid player movement.");
            Require(Finite(CrouchAmount) && CrouchAmount >= 0f && CrouchAmount <= 1f, "Invalid saved crouch stance.");
            Require(InventoryCapacity > 0 && InventoryCapacity <= 256 && Credits >= 0 && ShovelLevel >= 1
                && ShovelLevel <= EquipmentProgression.LevelCount && SuccessfulStrokes >= 0, "Invalid progression.");
            Require(InventoryLevel >= 1 && InventoryLevel <= EquipmentProgression.LevelCount
                && FuelLevel >= 1 && FuelLevel <= EquipmentProgression.LevelCount, "Invalid capacity upgrades.");
            Require(Finite(BatteryCapacity) && BatteryCapacity > 0 && Finite(BatteryCharge)
                && BatteryCharge >= 0 && BatteryCharge <= BatteryCapacity, "Invalid battery.");
            Require(Finds != null && Finds.Length <= DiscoveryField.MaximumPopulation && Inventory != null && Inventory.Length <= InventoryCapacity, "Invalid discovery population.");
            var population = new Dictionary<string, FindSnapshot>(StringComparer.Ordinal);
            foreach (var find in Finds)
            {
                Require(find != null && find.Item != null, "Missing discovery record.");
                find.Item.Validate();
                Require(!string.IsNullOrWhiteSpace(find.ContentId) && Valid(find.Position) && Valid(find.Rotation)
                    && Valid(find.Scale) && find.Scale.x > 0 && find.Scale.y > 0 && find.Scale.z > 0, "Invalid discovery placement.");
                Require(!population.ContainsKey(find.Item.Id), "Duplicate discovery identity.");
                population.Add(find.Item.Id, find);
            }
            var uniques = new HashSet<string>(StringComparer.Ordinal);
            var sockets = new HashSet<string>(StringComparer.Ordinal);
            int extracting = 0;
            foreach (var find in Finds)
            {
                Require(Enum.IsDefined(typeof(FindState),find.State), "Unknown find state.");
                Require(Finite(find.DiscoveryDepth) && find.DiscoveryDepth>=0 && find.DiscoveryDepth<=Terrain.Size.y*Terrain.CellSize,
                    "Invalid discovery depth.");
                Require(find.DisplaySocket!=null && find.DisplaySocket.Length<=128, "Invalid display socket.");
                if(find.Item.Kind==DiscoveryKind.Unique)
                {
                    Require(uniques.Add(find.ContentId) && find.State!=FindState.Collected,"Invalid unique ownership.");
                    Require(!find.PhysicsReleased || find.State==FindState.World || find.State==FindState.Extracting,"Stored unique cannot be dynamic.");
                    Require(find.State==FindState.World || find.DepthRecorded,"Recovered unique has no discovery record.");
                    if(find.State==FindState.Extracting) extracting++;
                    Require(find.State!=FindState.Displayed || !string.IsNullOrEmpty(find.DisplaySocket) && sockets.Add(find.DisplaySocket),"Duplicate exhibit socket.");
                    Require(find.State==FindState.Displayed || find.DisplaySocket.Length==0,"Undisplayed find has a socket.");
                }
                else Require((find.State==FindState.World || find.State==FindState.Collected) && !find.DepthRecorded && find.DisplaySocket.Length==0,
                    "Ordinary find has unique state.");
            }
            Require(extracting==(Extraction==null?0:1),"Extraction ownership is inconsistent.");
            Extraction?.Validate(population,Terrain);
            var carried = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Inventory)
            {
                Require(item != null, "Missing carried item.");
                item.Validate();
                Require(item.Kind == DiscoveryKind.Common && carried.Add(item.Id) && population.TryGetValue(item.Id, out var find) && find.State == FindState.Collected
                    && item.Name == find.Item.Name && item.Value == find.Item.Value, "Carried item does not match a collected discovery.");
            }
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool Valid(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        internal static bool Valid(Quaternion q) => Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w)
            && Math.Abs(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w - 1) < 0.001f;
        internal static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }
    }

    public sealed class GridSnapshot
    {
        public Vector3Int Size;
        public float CellSize, RemovedVolume;
        public int Revision, LowestCarvedY;
        public DensitySnapshot Density;
        public TerrainMaterialSnapshot Materials;

        public int SampleCount
        {
            get
            {
                WorldSnapshot.Require(Size.x > 0 && Size.y > 0 && Size.z > 0
                    && Size.x <= ExcavationGrid.MaximumCellsPerAxis && Size.y <= ExcavationGrid.MaximumCellsPerAxis
                    && Size.z <= ExcavationGrid.MaximumCellsPerAxis, "Invalid terrain dimensions.");
                long count = (long)(Size.x + 1) * (Size.y + 1) * (Size.z + 1);
                WorldSnapshot.Require(count <= WorldSaveCodec.MaximumSamples, "Terrain exceeds the supported sample budget.");
                return (int)count;
            }
        }

        public void Validate()
        {
            WorldSnapshot.Require(Size.x > 0 && Size.y > 0 && Size.z > 0
                && Size.x <= ExcavationGrid.MaximumCellsPerAxis && Size.y <= ExcavationGrid.MaximumCellsPerAxis
                && Size.z <= ExcavationGrid.MaximumCellsPerAxis
                && WorldSnapshot.Finite(CellSize) && CellSize > 0 && CellSize <= 10, "Invalid terrain dimensions.");
            WorldSnapshot.Require(Density != null && Density.Length == SampleCount
                && Materials != null && Materials.Length == Density.Length
                && Revision >= 0 && LowestCarvedY >= 0 && LowestCarvedY <= Size.y
                && WorldSnapshot.Finite(RemovedVolume) && RemovedVolume >= 0
                && RemovedVolume <= Size.x * (double)Size.y * Size.z * CellSize * CellSize * CellSize + 1, "Invalid terrain state.");
            Density.Validate(CellSize * 2);
        }
    }

    public sealed class FindSnapshot
    {
        public string ContentId;
        public ItemSnapshot Item;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public FindState State;
        public bool Collected => State == FindState.Collected || State == FindState.Stored || State == FindState.Displayed;
        public bool PhysicsReleased, DepthRecorded;
        public float DiscoveryDepth;
        public string DisplaySocket = "";
    }

    public sealed class ItemSnapshot
    {
        public string Id, Name;
        public int Value;
        public DiscoveryKind Kind;
        public static ItemSnapshot Capture(InventoryItem item) => new ItemSnapshot { Id = item.InstanceId, Name = item.DisplayName, Value = item.SaleValue, Kind = item.Kind };
        public InventoryItem Restore() => new InventoryItem(Id, Name, Value, Kind);
        internal void Validate() => WorldSnapshot.Require(!string.IsNullOrWhiteSpace(Id) && Id.Length <= 128
            && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 256 && Value >= 0 && (Kind == DiscoveryKind.Common || Kind == DiscoveryKind.Unique && Value == 0), "Invalid item record.");
    }
}
