using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Surface nets: one vertex at the average interpolated edge crossing per cell.
    // Every primal edge owns one quad. A halo gives neighboring chunks identical
    // positions and density-gradient normals, independent of rebuild order.
    public static partial class TerrainChunkMesh
    {
        public sealed class DensityCache
        {
            private float[] samples;
            internal bool Matches(float[] current, int count)
            {
                if (samples == null || samples.Length != count) return false;
                for (int i = 0; i < count; i++) if (samples[i] != current[i]) return false;
                return true;
            }
            internal void Store(float[] current, int count)
            {
                if (samples == null || samples.Length != count) samples = new float[count];
                Array.Copy(current, samples, count);
            }
        }

        public sealed class Workspace : IDisposable
        {
            internal NativeList<Vector3> Vertices, Normals;
            internal NativeList<Vector2> UVs;
            internal NativeList<int> Triangles;
            internal NativeArray<float> Corners, NativeSamples;
            internal NativeArray<int> Indices;
            internal float[] Samples = Array.Empty<float>();

            internal void Prepare(int cells, int samples)
            {
                if (!Vertices.IsCreated)
                {
                    Vertices=new NativeList<Vector3>(cells,Allocator.Persistent);
                    Normals=new NativeList<Vector3>(cells,Allocator.Persistent);
                    UVs=new NativeList<Vector2>(cells,Allocator.Persistent);
                    Triangles=new NativeList<int>(cells*18,Allocator.Persistent);
                    Corners=new NativeArray<float>(8,Allocator.Persistent);
                }
                if (!Indices.IsCreated || Indices.Length<cells)
                {
                    if(Indices.IsCreated)Indices.Dispose();
                    Indices=new NativeArray<int>(cells,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                    Vertices.Capacity=Math.Max(Vertices.Capacity,cells);
                    Normals.Capacity=Math.Max(Normals.Capacity,cells);
                    UVs.Capacity=Math.Max(UVs.Capacity,cells);
                    Triangles.Capacity=Math.Max(Triangles.Capacity,cells*18);
                }
                if(Samples.Length<samples)
                {
                    Samples=new float[samples];
                    if(NativeSamples.IsCreated)NativeSamples.Dispose();
                    NativeSamples=new NativeArray<float>(samples,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                }
            }

            public void Dispose()
            {
                if(Vertices.IsCreated)Vertices.Dispose();
                if(Normals.IsCreated)Normals.Dispose();
                if(UVs.IsCreated)UVs.Dispose();
                if(Triangles.IsCreated)Triangles.Dispose();
                if(Corners.IsCreated)Corners.Dispose();
                if(Indices.IsCreated)Indices.Dispose();
                if(NativeSamples.IsCreated)NativeSamples.Dispose();
            }
        }

        public static bool Rebuild(Mesh mesh, ExcavationGrid grid, Vector3Int start, int chunkSize,
            Workspace workspace = null, DensityCache cache = null, Action beforeWrite = null)
        {
            using var temporary = workspace == null ? new Workspace() : null;
            var w = workspace ?? temporary;
            Vector3Int end = Vector3Int.Min(grid.Size, start + Vector3Int.one * chunkSize);
            Vector3Int low = start - Vector3Int.one;
            Vector3Int span = end - low + Vector3Int.one;
            Vector3Int sampleOrigin = low - Vector3Int.one;
            Vector3Int sampleSpan = span + Vector3Int.one * 3;
            int samples = sampleSpan.x * sampleSpan.y * sampleSpan.z;
            w.Prepare(span.x * span.y * span.z, samples);
            grid.CopySamples(sampleOrigin, sampleSpan, w.Samples);
            if (cache != null && cache.Matches(w.Samples, samples)) return false;
            NativeArray<float>.Copy(w.Samples, 0, w.NativeSamples, 0, samples);
            new MeshJob {
                Start = new int3(start.x,start.y,start.z), End = new int3(end.x,end.y,end.z),
                Low = new int3(low.x,low.y,low.z), Span = new int3(span.x,span.y,span.z),
                Size = new int3(grid.Size.x,grid.Size.y,grid.Size.z), CellSize = grid.CellSize,
                SampleOrigin = new int3(sampleOrigin.x,sampleOrigin.y,sampleOrigin.z),
                SampleStrideY = sampleSpan.x, SampleStrideZ = sampleSpan.x * sampleSpan.y,
                Samples = w.NativeSamples, Indices = w.Indices, Corners = w.Corners,
                Vertices = w.Vertices, Normals = w.Normals, UVs = w.UVs, Triangles = w.Triangles
            }.Run();
            beforeWrite?.Invoke();
            mesh.Clear();
            mesh.indexFormat = w.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(w.Vertices.AsArray());
            mesh.SetNormals(w.Normals.AsArray());
            mesh.SetUVs(0, w.UVs.AsArray());
            mesh.SetIndices(w.Triangles.AsArray(), MeshTopology.Triangles, 0, false);
            mesh.RecalculateBounds();
            cache?.Store(w.Samples, samples);
            return true;
        }

    }
}
