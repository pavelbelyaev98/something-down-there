using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // A find's mesh from a URP prop prefab (BuriedPropsSetup): its full-detail meshes (LOD0 where the prop has a LODGroup)
    // merged in its own frame and centred on their bounds, on its one URP Lit material; the find's own detail levels
    // come from the shared discovery import. Its hull is a box, or the convex hull of the mesh within the discovery
    // hull budget (ConvexHull).
    internal static class PropBake
    {
        // A convex hull of at most HullPoints corners has at most 2 x HullPoints - 4 triangles: the discovery hull
        // budget of 220.
        private const int HullPoints = 112;

        // trophy: a great cave's crystal trophy (116), seated by the generator rather than buried loose, so no size cap.
        internal static (Mesh mesh, Mesh hull, Material material) Bake(string prefabPath, string id, string meshFolder, float scale, bool boxHull,
            bool trophy = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidDataException("Missing prop " + prefabPath);
            var lods = prefab.GetComponentInChildren<LODGroup>(true)?.GetLODs();
            var detail = lods != null ? lods[0].renderers.Select(r => r.GetComponent<MeshFilter>()).ToArray() : prefab.GetComponentsInChildren<MeshFilter>(true);
            var materials = detail.SelectMany(f => f.GetComponent<MeshRenderer>().sharedMaterials).Distinct().ToArray();
            if (materials.Length != 1 || materials[0] == null || materials[0].shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidDataException(id + ": a prop find needs one URP Lit material (run Configure Buried Props).");
            var toRoot = prefab.transform.worldToLocalMatrix;
            var mesh = Merge(detail, toRoot, id, out var bounds);
            if (!trophy && bounds.extents.magnitude * scale > DiscoveryField.MaximumLargeFindRadius) throw new InvalidDataException(id + " is too big to bury.");
            var hull = Save(boxHull ? RetroComputerSetup.BoxHull(new Bounds(Vector3.zero, bounds.size)) : ConvexHull(mesh.vertices, id),
                meshFolder + "/" + id + "_Hull.asset");
            hull.SetPreBakeCollisionMesh(isConvex: true, preBake: true);
            return (Save(mesh, meshFolder + "/" + id + ".asset"), hull, materials[0]);
        }

        // A mesh asset written in place, its GUID kept: cleared and refilled, never copied over, since an older mesh's
        // vertex layout and detail levels survive a serialized copy and scramble the new triangles.
        private static Mesh Save(Mesh source, string path)
        {
            source.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(source, path); return source; }
            existing.Clear();
            existing.indexFormat = source.indexFormat;
            existing.SetVertices(source.vertices); existing.SetNormals(source.normals); existing.SetTangents(source.tangents);
            existing.SetUVs(0, source.uv); existing.SetTriangles(source.triangles, 0); existing.RecalculateBounds();
            Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // The convex hull of the points, cut down to HullPoints corners where it has more: its six extremes (so it keeps the
        // points' bounds) and then, one by one, the corner farthest from those already kept.
        private static Mesh ConvexHull(Vector3[] source, string id)
        {
            var points = source.Distinct().ToList();
            var corners = HullCorners(points, id);
            if (corners.Count > HullPoints)
            {
                var kept = new List<Vector3>();
                for (int axis = 0; axis < 3; axis++)
                {
                    kept.Add(corners.OrderBy(p => p[axis]).First());
                    kept.Add(corners.OrderBy(p => p[axis]).Last());
                }
                kept = kept.Distinct().ToList();
                var nearest = corners.Select(p => kept.Min(k => (p - k).sqrMagnitude)).ToArray();
                while (kept.Count < HullPoints)
                {
                    int far = 0;
                    for (int i = 1; i < corners.Count; i++) if (nearest[i] > nearest[far]) far = i;
                    kept.Add(corners[far]);
                    for (int i = 0; i < corners.Count; i++) nearest[i] = Mathf.Min(nearest[i], (corners[i] - corners[far]).sqrMagnitude);
                }
                points = kept;
            }
            var faces = Hull(points, id);
            var used = faces.SelectMany(f => new[] { f.a, f.b, f.c }).Distinct().ToList();
            var mesh = new Mesh { vertices = used.Select(i => points[i]).ToArray() };
            mesh.triangles = faces.SelectMany(f => new[] { used.IndexOf(f.a), used.IndexOf(f.b), used.IndexOf(f.c) }).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Vector3> HullCorners(List<Vector3> points, string id)
        {
            var faces = Hull(points, id);
            return faces.SelectMany(f => new[] { f.a, f.b, f.c }).Distinct().Select(i => points[i]).ToList();
        }

        // Incremental convex hull: a starting tetrahedron, then each point outside replaces the faces it sees with a fan
        // from their horizon. Faces wind outwards.
        private static List<(int a, int b, int c)> Hull(List<Vector3> points, string id)
        {
            var size = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) size.Encapsulate(p);
            float eps = size.size.magnitude * 1e-5f;
            int i0 = 0;
            for (int i = 1; i < points.Count; i++) if (points[i].x < points[i0].x) i0 = i;
            int i1 = Farthest(points, p => (p - points[i0]).sqrMagnitude);
            int i2 = Farthest(points, p => Vector3.Cross(points[i1] - points[i0], p - points[i0]).sqrMagnitude);
            var baseNormal = Vector3.Cross(points[i1] - points[i0], points[i2] - points[i0]);
            int i3 = Farthest(points, p => Mathf.Abs(Vector3.Dot(baseNormal, p - points[i0])));
            if (Mathf.Abs(Vector3.Dot(baseNormal.normalized, points[i3] - points[i0])) < eps)
                throw new InvalidDataException(id + ": its mesh is flat; give it a box hull.");
            var inside = (points[i0] + points[i1] + points[i2] + points[i3]) * .25f;
            var faces = new List<(int a, int b, int c)>();
            void Add(int a, int b, int c)
            {
                var normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
                faces.Add(Vector3.Dot(normal, points[a] - inside) > 0 ? (a, b, c) : (a, c, b));
            }
            Add(i0, i1, i2); Add(i0, i1, i3); Add(i0, i2, i3); Add(i1, i2, i3);
            for (int p = 0; p < points.Count; p++)
            {
                if (p == i0 || p == i1 || p == i2 || p == i3) continue;
                var seen = faces.Where(f => Vector3.Dot(Vector3.Cross(points[f.b] - points[f.a], points[f.c] - points[f.a]).normalized,
                    points[p] - points[f.a]) > eps).ToList();
                if (seen.Count == 0) continue;
                var edges = new HashSet<(int, int)>(seen.SelectMany(f => new[] { (f.a, f.b), (f.b, f.c), (f.c, f.a) }));
                foreach (var f in seen) faces.Remove(f);
                foreach (var (a, b) in edges)
                    if (!edges.Contains((b, a))) faces.Add((a, b, p));
            }
            return faces;
        }

        private static int Farthest(List<Vector3> points, System.Func<Vector3, float> distance)
        {
            int best = 0;
            for (int i = 1; i < points.Count; i++) if (distance(points[i]) > distance(points[best])) best = i;
            return best;
        }

        // The meshes merged in the prop's frame and centred on their bounds.
        private static Mesh Merge(MeshFilter[] filters, Matrix4x4 toRoot, string id, out Bounds bounds)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
            var uvs = new List<Vector2>(); var triangles = new List<int>();
            foreach (var filter in filters)
            {
                var source = filter != null ? filter.sharedMesh : null;
                if (source == null || source.subMeshCount != 1) throw new InvalidDataException(id + ": each prop mesh needs one submesh.");
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
            bounds = new Bounds(vertices[0], Vector3.zero); foreach (var v in vertices) bounds.Encapsulate(v);
            for (int i = 0; i < vertices.Count; i++) vertices[i] -= bounds.center;
            var mesh = new Mesh { indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            if (tangents.All(t => t == new Vector4(1, 0, 0, 1))) mesh.RecalculateTangents();
            return mesh;
        }
    }
}
