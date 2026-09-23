using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SomethingDownThere
{
    // One reusable workspace per terrain; dense populations do not own a native
    // buffer each. Copy only the find's local density neighbourhood, not the site.
    public sealed class TerrainExposureSampler : IDisposable
    {
        private float[] density=Array.Empty<float>();
        private NativeArray<float> nativeDensity;
        private NativeArray<Vector3> points;
        private NativeArray<int> clearCount;

        public float Measure(ExcavationGrid grid,Vector3[] samples,Bounds hull,Matrix4x4 localToTerrain)
        {
            Vector3 center=localToTerrain.MultiplyPoint3x4(hull.center);
            Vector3 extents=Abs(localToTerrain.MultiplyVector(Vector3.right*hull.extents.x))
                +Abs(localToTerrain.MultiplyVector(Vector3.up*hull.extents.y))
                +Abs(localToTerrain.MultiplyVector(Vector3.forward*hull.extents.z));
            var first=Vector3Int.FloorToInt((center-extents)/grid.CellSize)-Vector3Int.one;
            var last=Vector3Int.CeilToInt((center+extents)/grid.CellSize)+Vector3Int.one;
            var span=last-first+Vector3Int.one;
            int count=checked(span.x*span.y*span.z);
            if(density.Length<count)
            {
                density=new float[count];
                if(nativeDensity.IsCreated)nativeDensity.Dispose();
                nativeDensity=new NativeArray<float>(count,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
            }
            if(!points.IsCreated || points.Length<samples.Length)
            {
                if(points.IsCreated)points.Dispose();
                points=new NativeArray<Vector3>(samples.Length,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
            }
            if(!clearCount.IsCreated)clearCount=new NativeArray<int>(1,Allocator.Persistent);
            grid.CopySamples(first,span,density);
            NativeArray<float>.Copy(density,0,nativeDensity,0,count);
            NativeArray<Vector3>.Copy(samples,0,points,0,samples.Length);
            new ExposureJob {
                Density=nativeDensity,Points=points,ClearCount=clearCount,Count=samples.Length,
                Matrix=new float4x4(localToTerrain.GetColumn(0),localToTerrain.GetColumn(1),localToTerrain.GetColumn(2),localToTerrain.GetColumn(3)),
                First=new int3(first.x,first.y,first.z),StrideY=span.x,StrideZ=span.x*span.y,
                CellSize=grid.CellSize,Extent=grid.Extent
            }.Run();
            return clearCount[0]/(float)samples.Length;
        }

        private static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));

        public void Dispose()
        {
            if(nativeDensity.IsCreated)nativeDensity.Dispose();
            if(points.IsCreated)points.Dispose();
            if(clearCount.IsCreated)clearCount.Dispose();
        }

        [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
        private struct ExposureJob : IJob
        {
            [ReadOnly] public NativeArray<float> Density;
            [ReadOnly] public NativeArray<Vector3> Points;
            public NativeArray<int> ClearCount;
            public float4x4 Matrix;
            public int3 First;
            public int Count,StrideY,StrideZ;
            public float CellSize;
            public float3 Extent;

            public void Execute()
            {
                int clear=0;
                for(int n=0;n<Count;n++)
                {
                    float3 point=math.transform(Matrix,(float3)Points[n]);
                    if(math.any(point<0)||math.any(point>=Extent)){clear++;continue;}
                    float3 p=point/CellSize;int3 a=(int3)math.floor(p);float3 t=p-a;
                    int3 local=a-First;int i=local.x+local.y*StrideY+local.z*StrideZ,y=StrideY,z=StrideZ;
                    float low=math.lerp(math.lerp(Density[i],Density[i+1],t.x),math.lerp(Density[i+y],Density[i+y+1],t.x),t.y);
                    float high=math.lerp(math.lerp(Density[i+z],Density[i+z+1],t.x),math.lerp(Density[i+y+z],Density[i+y+z+1],t.x),t.y);
                    if(math.lerp(low,high,t.z)<=0)clear++;
                }
                ClearCount[0]=clear;
            }
        }
    }
}
