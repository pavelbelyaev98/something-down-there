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
            Require(Valid(TerrainPosition) && Valid(TerrainRotation) && Valid(PlayerPosition) && Valid(PlayerRotation), "Invalid world position.");
            Require(Finite(Pitch) && Math.Abs(Pitch) < 90 && Finite(VerticalSpeed), "Invalid player movement.");
            Require(Finite(CrouchAmount) && CrouchAmount >= 0f && CrouchAmount <= 1f, "Invalid saved crouch stance.");
            Require(InventoryCapacity > 0 && InventoryCapacity <= 256 && Credits >= 0 && ShovelLevel >= 1
                && ShovelLevel <= 6 && SuccessfulStrokes >= 0, "Invalid progression.");
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
            var carried = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Inventory)
            {
                Require(item != null, "Missing carried item.");
                item.Validate();
                Require(carried.Add(item.Id) && population.TryGetValue(item.Id, out var find) && find.Collected
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

        public void Validate()
        {
            WorldSnapshot.Require(Size.x > 0 && Size.y > 0 && Size.z > 0
                && Size.x <= ExcavationGrid.MaximumCellsPerAxis && Size.y <= ExcavationGrid.MaximumCellsPerAxis
                && Size.z <= ExcavationGrid.MaximumCellsPerAxis
                && WorldSnapshot.Finite(CellSize) && CellSize > 0 && CellSize <= 10, "Invalid terrain dimensions.");
            WorldSnapshot.Require(Density != null && Density.Length == (Size.x + 1) * (Size.y + 1) * (Size.z + 1)
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
        public bool Collected, PhysicsReleased;
    }

    public sealed class ItemSnapshot
    {
        public string Id, Name;
        public int Value;
        public static ItemSnapshot Capture(InventoryItem item) => new ItemSnapshot { Id = item.InstanceId, Name = item.DisplayName, Value = item.SaleValue };
        public InventoryItem Restore() => new InventoryItem(Id, Name, Value);
        internal void Validate() => WorldSnapshot.Require(!string.IsNullOrWhiteSpace(Id) && Id.Length <= 128
            && !string.IsNullOrWhiteSpace(Name) && Name.Length <= 256 && Value >= 0, "Invalid item record.");
    }
}
