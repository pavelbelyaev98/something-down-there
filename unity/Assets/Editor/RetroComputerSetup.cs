using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    public static class RetroComputerSetup
    {
        public const string Folder="Assets/Content/Discoveries/RetroComputer";
        [Serializable] private sealed class Source
        {
            public int schema_version;
            public string prefab;
            public Vector3 position,euler;
            public DiscoveryContentSetup.SourceEntry find;
        }
        internal static void AppendToCatalog(List<DiscoveryCatalog.Entry> entries)
        {
            var source=JsonUtility.FromJson<Source>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/retro-computer/catalog.json"))));
            var e=source?.find;
            if(source==null || source.schema_version!=1 || e==null || e.tier!="unique" || e.recovery!="rope"
                || e.instances!=1 || e.shallow_instances!=0 || e.slots!=0 || e.sale_value!=0 || !e.detector_eligible
                || string.IsNullOrWhiteSpace(e.lore) || e.required_exposure<=0 || e.required_exposure>1
                || source.prefab==SurfaceStationSetup.ComputerPrefabPath)
                throw new InvalidDataException("Invalid buried computer source.");
            foreach(string suffix in new[]{"","/Meshes","/Materials","/Prefabs"}) EnsureFolder(Folder+suffix);
            var sourcePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(source.prefab);
            if(sourcePrefab==null) throw new InvalidDataException("Missing approved computer prefab.");
            var model=UnityEngine.Object.Instantiate(sourcePrefab);
            Mesh centered;
            try
            {
                var filters=model.GetComponentsInChildren<MeshFilter>();
                if(filters.Length!=1 || filters[0].sharedMesh.subMeshCount!=1) throw new InvalidDataException("Computer must have one atlas mesh.");
                var filter=filters[0]; var sourceMesh=filter.sharedMesh;
                // A cloned imported mesh also inherits its non-readable runtime flag.
                // Own readable buffers for placement and surface sampling without changing the vendor importer.
                centered=new Mesh { indexFormat=sourceMesh.indexFormat, vertices=sourceMesh.vertices,
                    triangles=sourceMesh.triangles, normals=sourceMesh.normals, uv=sourceMesh.uv, tangents=sourceMesh.tangents };
                var matrix=filter.transform.localToWorldMatrix; var vertices=centered.vertices;
                for(int i=0;i<vertices.Length;i++) vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                var bounds=new Bounds(vertices[0],Vector3.zero); foreach(var vertex in vertices) bounds.Encapsulate(vertex);
                for(int i=0;i<vertices.Length;i++) vertices[i]-=bounds.center;
                var normals=centered.normals;
                for(int i=0;i<normals.Length;i++) normals[i]=matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
                centered.vertices=vertices; centered.normals=normals; centered.RecalculateBounds(); centered.RecalculateTangents();
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
            centered=Save(centered,Folder+"/Meshes/Computer.asset");
            var hull=Save(BoxHull(centered.bounds),Folder+"/Meshes/ComputerHull.asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Computer.mat");
            if(material==null)
            {
                material=new Material(sourcePrefab.GetComponentInChildren<Renderer>().sharedMaterial) { name="Buried computer" };
                AssetDatabase.CreateAsset(material,Folder+"/Materials/Computer.mat");
            }
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor",Color.black);
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            var prefab=DiscoveryContentSetup.UpdatePrefab(e,centered,hull,material,Folder,true,18,e.model_scale);
            entries.Add(new DiscoveryCatalog.Entry { ItemId=e.content_id, Prefab=prefab, Count=e.instances,
                AuthoredPlacement=true, AuthoredPosition=source.position, AuthoredEuler=source.euler });
        }
        internal static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            int split=path.LastIndexOf('/'); EnsureFolder(path.Substring(0,split)); AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));
        }
        private static Mesh Save(Mesh source,string path)
        {
            source.name=Path.GetFileNameWithoutExtension(path);
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) { AssetDatabase.CreateAsset(source,path); return source; }
            EditorUtility.CopySerialized(source,existing); UnityEngine.Object.DestroyImmediate(source); EditorUtility.SetDirty(existing); return existing;
        }
        private static Mesh BoxHull(Bounds bounds)
        {
            var vertices=new Vector3[8];
            for(int i=0;i<8;i++) vertices[i]=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            var mesh=new Mesh { vertices=vertices, triangles=new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,4,6,2,1,3,5,5,3,7,0,1,4,1,5,4,2,6,3,3,6,7} };
            mesh.SetPreBakeCollisionMesh(isConvex:true,preBake:true);
            mesh.RecalculateBounds(); mesh.RecalculateNormals(); return mesh;
        }
    }
}
