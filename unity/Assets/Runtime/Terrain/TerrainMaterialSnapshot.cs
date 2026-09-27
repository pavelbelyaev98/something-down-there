using System;
using System.IO;
using UnityEngine;

namespace SomethingDownThere
{
    // Saved as bytes: append new families, never renumber. Hardness order is
    // EquipmentProgression.HardnessOrder, not the declaration order.
    public enum TerrainMaterialId : byte { Soil, Clay, Rock, Gravel, Concrete }

    // Immutable identities share the density lattice, including samples excavated into air.
    // Captures can share this object with the save worker without copying the world.
    public sealed class TerrainMaterialSnapshot
    {
        public const TerrainMaterialId Last = TerrainMaterialId.Concrete;
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

        public static TerrainMaterialSnapshot Generate(Vector3Int size, float cellSize, int seed)
        {
            ExcavationGrid.ValidateDimensions(size, cellSize);
            int stride = size.x + 1;
            var values = new byte[stride * (size.y + 1) * (size.z + 1)];
            var warps = new float[stride];
            var thicknesses = new float[stride];
            var gravel = new float[stride];
            int cycles = Mathf.CeilToInt(size.y * cellSize / LayerCycle) + 1;
            var slabTop = new float[stride * cycles]; var slabBottom = new float[stride * cycles];
            // Seed offsets, not UnityEngine.Random. Only column-scale noise is evaluated;
            // the large inner loop writes contiguous bytes using simple layer arithmetic.
            uint hash = unchecked((uint)seed * 747796405u + 2891336453u);
            float offsetX = (hash & 65535) * .017f, offsetZ = (hash >> 16) * .019f;
            int index = 0;
            for (int z = 0; z <= size.z; z++)
            {
                for (int x = 0; x <= size.x; x++)
                {
                    warps[x] = (Mathf.PerlinNoise(x * cellSize * .19f + offsetX, z * cellSize * .19f + offsetZ) - .5f) * 2.4f;
                    thicknesses[x] = 1.3f + Mathf.PerlinNoise(x * cellSize * .31f + offsetZ, z * cellSize * .31f + offsetX) * .9f;
                    // Gravel lenses: lens thickness grows with this second field; zero leaves soil.
                    gravel[x] = Mathf.Max(0, Mathf.PerlinNoise(x * cellSize * .23f + offsetZ * 1.7f, z * cellSize * .23f + offsetX * 1.3f) - .52f) * 5f;
                    for (int cycle = 0; cycle < cycles; cycle++)
                        ConcreteSlab(x * cellSize, z * cellSize, cycle, hash, out slabTop[x * cycles + cycle], out slabBottom[x * cycles + cycle]);
                }
                for (int y = 0; y <= size.y; y++)
                {
                    float depth = (size.y - y) * cellSize;
                    int cycle = Mathf.Min(cycles - 1, (int)(depth / LayerCycle));
                    for (int x = 0; x <= size.x; x++)
                    {
                        float layer = Mathf.Repeat(depth + warps[x], LayerCycle);
                        int slab = x * cycles + cycle;
                        values[index++] = (byte)(depth < 1.1f ? TerrainMaterialId.Soil
                            : depth >= slabTop[slab] && depth < slabBottom[slab] ? TerrainMaterialId.Concrete
                            : layer >= 4f && layer < 4f + thicknesses[x] ? TerrainMaterialId.Rock
                            : layer >= 1.8f && layer < 4f ? TerrainMaterialId.Clay
                            : layer >= 1.8f - gravel[x] && layer < 1.8f ? TerrainMaterialId.Gravel : TerrainMaterialId.Soil);
                    }
                }
            }
            return new TerrainMaterialSnapshot(values);
        }

        private const float LayerCycle = 8f, SlabCell = 6f, SlabChance = .22f;

        // Buried concrete: rare flat slabs, one chance per 6 m cell and 8 m depth cycle, never
        // in the first cycle. Top/bottom depths, or +inf/-inf when this column misses the slab.
        private static void ConcreteSlab(float x, float z, int cycle, uint seed, out float top, out float bottom)
        {
            top = float.PositiveInfinity; bottom = float.NegativeInfinity;
            if (cycle == 0) return;
            int cx = Mathf.FloorToInt(x / SlabCell), cz = Mathf.FloorToInt(z / SlabCell);
            uint h = unchecked(seed ^ (uint)cx * 73856093u ^ (uint)cz * 19349663u ^ (uint)cycle * 83492791u);
            if (Next(ref h) >= SlabChance) return;
            float width = 2f + Next(ref h) * 2.5f, length = 2f + Next(ref h) * 2.5f;
            float left = cx * SlabCell + Next(ref h) * (SlabCell - width), near = cz * SlabCell + Next(ref h) * (SlabCell - length);
            if (x < left || x >= left + width || z < near || z >= near + length) return;
            top = cycle * LayerCycle + 1f + Next(ref h) * 5f;
            bottom = top + .6f + Next(ref h) * .4f;
        }

        private static float Next(ref uint state)
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
