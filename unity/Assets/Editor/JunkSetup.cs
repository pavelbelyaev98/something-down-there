using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // Junk finds (106) from art/tv-set/catalog.json and art/big-old-tv/catalog.json: each names a URP prop prefab
    // (BuriedPropsSetup) and its find policy. The prop's meshes become one centred mesh on its one material, with a
    // box hull; the find prefab, surface samples and detail levels come from the shared discovery import.
    public static class JunkSetup
    {
        public const string Folder = "Assets/Content/Discoveries/Junk";
        private static readonly string[] Sources = { "art/tv-set/catalog.json", "art/big-old-tv/catalog.json" };
        [Serializable] private sealed class Source { public int schema_version; public JunkFind[] finds; }
        [Serializable] private sealed class JunkFind { public string prefab; public float mass_kg; public DiscoveryContentSetup.SourceEntry find; }

        internal static void AppendToCatalog(List<DiscoveryCatalog.Entry> entries)
        {
            foreach (string suffix in new[] { "", "/Meshes", "/Prefabs" }) RetroComputerSetup.EnsureFolder(Folder + suffix);
            foreach (string path in Sources)
            {
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../" + path))));
                if (source == null || source.schema_version != 1 || source.finds == null || source.finds.Length == 0)
                    throw new InvalidDataException("Invalid junk source: " + path);
                foreach (var junk in source.finds)
                {
                    var e = junk?.find;
                    if (e == null || string.IsNullOrWhiteSpace(e.content_id) || e.tier != "common" || e.recovery != "bag" || !e.junk
                        || e.instances < 1 || e.shallow_instances != 0 || e.slots != 1 || e.sale_value < 1 || e.detector_eligible
                        || e.required_exposure <= 0 || e.required_exposure > 1 || !(junk.mass_kg > 0 && junk.mass_kg <= 50))
                        throw new InvalidDataException("Invalid junk find policy: " + e?.content_id);
                    float scale = DiscoveryContentSetup.ModelScale(e);
                    var (mesh, material) = Bake(junk.prefab, e.content_id, scale);
                    var hull = RetroComputerSetup.Save(RetroComputerSetup.BoxHull(mesh.bounds), Folder + "/Meshes/" + e.content_id + "_Hull.asset");
                    var prefab = DiscoveryContentSetup.UpdatePrefab(e, mesh, hull, material, Folder, true, junk.mass_kg, scale);
                    DiscoveryContentSetup.GenerateDetailLevels(mesh);
                    entries.Add(new DiscoveryCatalog.Entry { ItemId = e.content_id, Prefab = prefab, Count = e.instances,
                        MinDepth = e.minimum_depth_m, MaxDepth = e.maximum_depth_m, Junk = true,
                        HostGrounds = DiscoveryContentSetup.HostGrounds(e.host_grounds, e.host_weights),
                        HostWeights = DiscoveryContentSetup.HostWeights(e.host_grounds, e.host_weights) });
                }
            }
        }

        // The prop's meshes in its own frame, merged and centred on their bounds; one material for all of them. Scaled,
        // it must fit the narrowest rubbish pit, where junk lies.
        private static (Mesh mesh, Material material) Bake(string prefabPath, string id, float scale)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidDataException("Missing junk prop " + prefabPath);
            var materials = prefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            if (materials.Length != 1 || materials[0] == null || materials[0].shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidDataException(id + ": a junk prop needs one URP Lit material (run Configure Buried Props).");
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
            var uvs = new List<Vector2>(); var triangles = new List<int>();
            var toRoot = prefab.transform.worldToLocalMatrix;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (source == null || source.subMeshCount != 1) throw new InvalidDataException(id + ": each junk mesh needs one submesh.");
                var matrix = toRoot * filter.transform.localToWorldMatrix; var normalMatrix = matrix.inverse.transpose;
                int offset = vertices.Count;
                vertices.AddRange(source.vertices.Select(v => matrix.MultiplyPoint3x4(v)));
                normals.AddRange(source.normals.Select(n => normalMatrix.MultiplyVector(n).normalized));
                var sourceTangents = source.tangents;
                tangents.AddRange(sourceTangents.Length == source.vertexCount
                    ? sourceTangents.Select(t => { var d = matrix.MultiplyVector(t).normalized; return new Vector4(d.x, d.y, d.z, t.w); })
                    : Enumerable.Repeat(new Vector4(1, 0, 0, 1), source.vertexCount));
                uvs.AddRange(source.uv.Length == source.vertexCount ? source.uv : new Vector2[source.vertexCount]);
                triangles.AddRange(source.triangles.Select(t => t + offset));
            }
            var bounds = new Bounds(vertices[0], Vector3.zero); foreach (var v in vertices) bounds.Encapsulate(v);
            if (bounds.extents.magnitude * scale > TerrainGround.RubbishRadius) throw new InvalidDataException(id + " is too big for a rubbish pit.");
            for (int i = 0; i < vertices.Count; i++) vertices[i] -= bounds.center;
            var mesh = new Mesh { indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            if (tangents.All(t => t == new Vector4(1, 0, 0, 1))) mesh.RecalculateTangents();
            return (RetroComputerSetup.Save(mesh, Folder + "/Meshes/" + id + ".asset"), materials[0]);
        }
    }
}
