using System;
using System.IO;
using UnityEngine;

namespace SomethingDownThere
{
    // Saved as bytes: append new grounds, never renumber. Hardness order is
    // EquipmentProgression.HardnessOrder, not the declaration order.
    // Backfill is the mixed ground of an old dug-and-refilled pit.
    public enum TerrainMaterialId : byte { Soil, Backfill, GeodeShell }

    // Immutable identities share the density lattice, including samples excavated into air.
    // Captures can share this object with the save worker without copying the world.
    public sealed class TerrainMaterialSnapshot
    {
        public const TerrainMaterialId Last = TerrainMaterialId.GeodeShell;
        private readonly byte[] samples;
        public int Length => samples.Length;
        public TerrainMaterialId this[int index] => (TerrainMaterialId)samples[index];
        private TerrainMaterialSnapshot(byte[] ownedSamples) => samples = ownedSamples;

        public static TerrainMaterialSnapshot Uniform(int length, TerrainMaterialId material = TerrainMaterialId.Soil)
        {
            if (length < 0 || length > WorldSaveCodec.MaximumSamples) throw new ArgumentOutOfRangeException(nameof(length));
            if (material > Last) throw new ArgumentOutOfRangeException(nameof(material));
            var values = new byte[length];
            if (material != TerrainMaterialId.Soil) Array.Fill(values, (byte)material);
            return new TerrainMaterialSnapshot(values);
        }

        public static TerrainMaterialSnapshot CopyFrom(byte[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            WorldSnapshot.Require(values.Length <= WorldSaveCodec.MaximumSamples, "Material field is too large.");
            var owned = (byte[])values.Clone();
            ValidateIds(owned);
            return new TerrainMaterialSnapshot(owned);
        }

        public byte[] ToArray() => (byte[])samples.Clone();
        internal void CopyTo(int source, byte[] target, int offset, int count) => Array.Copy(samples, source, target, offset, count);
        internal void Write(BinaryWriter writer) => writer.Write(samples);
        internal static TerrainMaterialSnapshot Read(BinaryReader reader, int length)
        {
            WorldSnapshot.Require(length >= 0 && length <= WorldSaveCodec.MaximumSamples, "Invalid material field length.");
            var values = reader.ReadBytes(length);
            WorldSnapshot.Require(values.Length == length, "Interrupted material field.");
            ValidateIds(values);
            return new TerrainMaterialSnapshot(values);
        }

        private static void ValidateIds(byte[] values)
        {
            foreach (byte value in values)
                WorldSnapshot.Require(value <= (byte)Last, "Unknown ground material.");
        }

        // Soil and backfill pits: see TerrainGround.
        // Odd spots: grid-local unique centres (xyz) and envelope radii (w) that pits keep clear of.
        public static TerrainMaterialSnapshot Generate(Vector3Int size, float cellSize, int seed, Vector4[] oddSpots = null,
            TerrainGround.Features features = TerrainGround.Features.All, Bounds stashPocket = default)
        {
            ExcavationGrid.ValidateDimensions(size, cellSize);
            return new TerrainMaterialSnapshot(TerrainGround.Generate(size, cellSize, seed, oddSpots, features, stashPocket));
        }

        // Seeded unit float from a hash state; never touches UnityEngine.Random.
        internal static float NextUnit(ref uint state)
        {
            state = unchecked(state * 747796405u + 2891336453u);
            uint word = unchecked(((state >> (int)((state >> 28) + 4)) ^ state) * 277803737u);
            return ((word >> 22) ^ word) * (1f / 4294967296f);
        }
    }

    public readonly struct TerrainCutFeedback
    {
        public readonly TerrainMaterialId Material;
        public readonly Vector3 Point, Normal;
        public readonly float RemovedVolume;
        public TerrainCutFeedback(TerrainMaterialId material, Vector3 point, Vector3 normal, float removedVolume)
        { Material = material; Point = point; Normal = normal; RemovedVolume = removedVolume; }
    }
}
