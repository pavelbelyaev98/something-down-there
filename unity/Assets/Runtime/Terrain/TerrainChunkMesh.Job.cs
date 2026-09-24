using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace SomethingDownThere
{
    public static partial class TerrainChunkMesh
    {
        // The existing surface-net algorithm runs as compiled native math. Run()
        // completes before mesh/collider publication: no stale collision frame.
        [BurstCompile(CompileSynchronously=true,FloatMode=FloatMode.Strict)]
        private struct MeshJob : IJob
        {
            public int3 Start,End,Low,Span,Size,SampleOrigin;
            public float CellSize;
            public int SampleStrideY,SampleStrideZ;
            [ReadOnly] public NativeArray<float> Samples;
            [ReadOnly] public NativeArray<byte> Materials;
            public NativeArray<int> Indices;
            public NativeArray<float> Corners;
            public NativeList<Vector3> Vertices,Normals;
            public NativeList<Vector2> UVs;
            public NativeList<Vector2> MaterialWeights;
            public NativeList<int> Triangles;

            public void Execute()
            {
                Vertices.Clear();Normals.Clear();UVs.Clear();Triangles.Clear();MaterialWeights.Clear();
                int cells=Span.x*Span.y*Span.z;
                for(int i=0;i<cells;i++)Indices[i]=-1;
                for(int z=Low.z;z<=End.z;z++)
                for(int y=Low.y;y<=End.y;y++)
                for(int x=Low.x;x<=End.x;x++)
                {
                    int mask=0,sample=SampleIndex(new int3(x,y,z));
                    for(int c=0;c<8;c++)
                    {
                        float d=Samples[sample+(c&1)+((c>>1)&1)*SampleStrideY+((c>>2)&1)*SampleStrideZ];
                        Corners[c]=d;if(d>0)mask|=1<<c;
                    }
                    if(mask==0 || mask==255)continue;
                    float3 sum=0;int crossings=0;
                    for(int c=0;c<8;c++)
                    for(int axis=0;axis<3;axis++)
                    {
                        int bit=1<<axis;if((c&bit)!=0)continue;
                        float a=Corners[c],b=Corners[c|bit];
                        if((a>0)==(b>0))continue;
                        float3 p=new float3(c&1,(c>>1)&1,(c>>2)&1);
                        p[axis]+=a/(a-b);sum+=p;crossings++;
                    }
                    float3 vertex=math.clamp((new float3(x,y,z)+sum/crossings)*CellSize,0,(float3)Size*CellSize);
                    Indices[Index(new int3(x,y,z))]=Vertices.Length;
                    Vertices.Add(vertex);Normals.Add(SurfaceNormal(vertex));UVs.Add(new Vector2(vertex.x,vertex.z));
                    MaterialWeights.Add(SurfaceMaterials(vertex));
                }
                for(int z=Start.z;z<=End.z;z++)
                for(int y=Start.y;y<=End.y;y++)
                for(int x=Start.x;x<=End.x;x++)
                {
                    if((x==End.x && End.x<Size.x)||(y==End.y && End.y<Size.y)||(z==End.z && End.z<Size.z))continue;
                    var edge=new int3(x,y,z);int sample=SampleIndex(edge);float a=Samples[sample];
                    for(int axis=0;axis<3;axis++)
                    {
                        if(edge[axis]>=Size[axis])continue;
                        int step=axis==0?1:axis==1?SampleStrideY:SampleStrideZ;
                        if((a>0)==(Samples[sample+step]>0))continue;
                        int u=(axis+1)%3,v=(axis+2)%3;
                        var q0=edge;q0[u]--;q0[v]--;
                        var q1=edge;q1[v]--;
                        var q3=edge;q3[u]--;
                        int i0=Indices[Index(q0)],i1=Indices[Index(q1)],i2=Indices[Index(edge)],i3=Indices[Index(q3)];
                        if(i0<0||i1<0||i2<0||i3<0)continue;
                        if(a<=0){int swap=i1;i1=i3;i3=swap;}
                        if(math.lengthsq((float3)Vertices[i0]-(float3)Vertices[i2])<=math.lengthsq((float3)Vertices[i1]-(float3)Vertices[i3]))
                        {Triangle(i0,i1,i2);Triangle(i0,i2,i3);}
                        else {Triangle(i0,i1,i3);Triangle(i1,i2,i3);}
                    }
                }
            }

            private int Index(int3 p) => p.x-Low.x+Span.x*(p.y-Low.y+Span.y*(p.z-Low.z));
            private int SampleIndex(int3 p) => p.x-SampleOrigin.x+(p.y-SampleOrigin.y)*SampleStrideY+(p.z-SampleOrigin.z)*SampleStrideZ;

            private float Sample(float3 point)
            {
                float3 p=point/CellSize;int3 a=(int3)math.floor(p);float3 t=p-a;
                int i=SampleIndex(a),y=SampleStrideY,z=SampleStrideZ;
                float bottom=math.lerp(math.lerp(Samples[i],Samples[i+1],t.x),math.lerp(Samples[i+y],Samples[i+y+1],t.x),t.y);
                float top=math.lerp(math.lerp(Samples[i+z],Samples[i+z+1],t.x),math.lerp(Samples[i+y+z],Samples[i+y+z+1],t.x),t.y);
                return math.lerp(bottom,top,t.z);
            }

            private float3 SurfaceNormal(float3 point)
            {
                float h=CellSize*.5f;
                var dx=new float3(h,0,0);var dy=new float3(0,h,0);var dz=new float3(0,0,h);
                float3 gradient=new float3(Sample(point+dx)-Sample(point-dx),Sample(point+dy)-Sample(point-dy),Sample(point+dz)-Sample(point-dz));
                return math.lengthsq(gradient)>1e-12f?-math.normalize(gradient):new float3(0,1,0);
            }

            private Vector2 SurfaceMaterials(float3 point)
            {
                float3 p = point / CellSize;
                int3 cell = (int3)math.floor(p);
                float3 t = p - cell;
                int index = SampleIndex(cell);
                float2 result = 0;
                for (int c = 0; c < 8; c++)
                {
                    int x = c & 1, y = (c >> 1) & 1, z = (c >> 2) & 1;
                    float weight = (x == 0 ? 1-t.x : t.x) * (y == 0 ? 1-t.y : t.y) * (z == 0 ? 1-t.z : t.z);
                    byte material = Materials[index + x + y * SampleStrideY + z * SampleStrideZ];
                    if (material == (byte)TerrainMaterialId.Clay) result.x += weight;
                    else if (material == (byte)TerrainMaterialId.Rock) result.y += weight;
                }
                result = math.saturate(result);
                result /= math.max(1f, result.x + result.y);
                return new Vector2(result.x, result.y);
            }

            private void Triangle(int a,int b,int c)
            {
                if(math.lengthsq(math.cross((float3)Vertices[b]-(float3)Vertices[a],(float3)Vertices[c]-(float3)Vertices[a]))<1e-12f)return;
                Triangles.Add(a);Triangles.Add(b);Triangles.Add(c);
            }
        }
    }
}
