using System;
using System.IO;
using UnityEngine;

namespace SomethingDownThere
{
    public enum TerrainMaterialId : byte { Soil, Clay, Rock }

    // Immutable identities share the density lattice, including samples excavated into air.
    // Captures can share this object with the save worker without copying the world.
    public sealed class TerrainMaterialSnapshot
    {
        private readonly byte[] samples;
        public int Length => samples.Length;
        public TerrainMaterialId this[int index] => (TerrainMaterialId)samples[index];
        private TerrainMaterialSnapshot(byte[] ownedSamples) => samples = ownedSamples;

        public static TerrainMaterialSnapshot Uniform(int length, TerrainMaterialId material = TerrainMaterialId.Soil)
        {
            if (length < 0 || length > WorldSaveCodec.MaximumSamples) throw new ArgumentOutOfRangeException(nameof(length));
            if (material > TerrainMaterialId.Rock) throw new ArgumentOutOfRangeException(nameof(material));
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
                WorldSnapshot.Require(value <= (byte)TerrainMaterialId.Rock, "Unknown ground material.");
        }

        public static TerrainMaterialSnapshot Generate(Vector3Int size, float cellSize, int seed)
        {
            ExcavationGrid.ValidateDimensions(size, cellSize);
            int stride = size.x + 1;
            var values = new byte[stride * (size.y + 1) * (size.z + 1)];
            var warps = new float[stride];
            var thicknesses = new float[stride];
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
                }
                for (int y = 0; y <= size.y; y++)
                {
                    float depth = (size.y - y) * cellSize;
                    for (int x = 0; x <= size.x; x++)
                    {
                        float layer = Mathf.Repeat(depth + warps[x], 8f);
                        values[index++] = (byte)(depth < 1.1f ? TerrainMaterialId.Soil
                            : layer >= 4f && layer < 4f + thicknesses[x] ? TerrainMaterialId.Rock
                            : layer >= 1.8f && layer < 4f ? TerrainMaterialId.Clay : TerrainMaterialId.Soil);
                    }
                }
            }
            return new TerrainMaterialSnapshot(values);
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
