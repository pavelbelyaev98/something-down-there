using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere.Editor
{
    // Markers along the dig plot outline, on the permanent collar just outside where digging starts.
    // Three comparison options, one shown at a time (DigBoundaryMarkers): a continuous ring of pack
    // stones, survey stakes with barrier tape (open where the winch cable enters) and a low edging
    // of weathered logs. None has colliders, so digging, aiming and the winch cable pass through.
    public static partial class LakebedSiteSetup
    {
        public const string BoundaryMeshFolder = Folder + "/Boundary";
        public const string BoundaryWoodPath = Folder + "/BoundaryWood.mat";
        public const string BoundaryTapeRedPath = Folder + "/BoundaryTapeRed.mat";
        public const string BoundaryTapeWhitePath = Folder + "/BoundaryTapeWhite.mat";
        // Metres beyond the outline: the markers stand on the collar's flat top.
        public const float StoneRingOffset = .55f, TapeOffset = .55f, EdgingOffset = .45f;
        private const float TapeGapHalfArc = 13;

        private static void BuildDigBoundary(Transform environment)
        {
            var parent = new GameObject("Dig boundary").transform;
            parent.SetParent(environment, false);
            if (!AssetDatabase.IsValidFolder(BoundaryMeshFolder)) AssetDatabase.CreateFolder(Folder, "Boundary");
            var wood = BoundaryMaterial(BoundaryWoodPath, "BoundaryWood", new Color(1, .96f, .9f), true);
            var options = new[]
            {
                StoneRing(parent),
                SurveyTape(parent, wood, BoundaryMaterial(BoundaryTapeRedPath, "BoundaryTapeRed", new Color(.78f, .08f, .06f), false),
                    BoundaryMaterial(BoundaryTapeWhitePath, "BoundaryTapeWhite", new Color(.9f, .9f, .88f), false)),
                TimberEdging(parent, wood),
            };
            parent.gameObject.AddComponent<DigBoundaryMarkers>().Configure(options);
        }

        // Points along the plot outline at a fixed offset from one bearing to another, with the
        // distance walked so far.
        private static List<(Vector2 point, float along)> OutlinePath(float offset, float from, float to)
        {
            var path = new List<(Vector2, float)>();
            float along = 0;
            Vector2 previous = default;
            for (float compass = from; compass <= to + 1e-3f; compass += .25f)
            {
                var point = SiteLayout.OpeningPoint(Mathf.Repeat(compass, 360), offset);
                if (path.Count > 0) along += Vector2.Distance(point, previous);
                path.Add((point, along));
                previous = point;
            }
            return path;
        }

        private static (Vector2 point, Vector2 tangent) PathAt(List<(Vector2 point, float along)> path, float distance)
        {
            int i = 0;
            while (i < path.Count - 2 && path[i + 1].along < distance) i++;
            var (a, da) = path[i];
            var (b, db) = path[i + 1];
            return (Vector2.Lerp(a, b, Mathf.InverseLerp(da, db, distance)), (b - a).normalized);
        }

        private static Vector3 Ground(Vector2 point, float height = 0) => new Vector3(point.x, SiteLayout.RimTop + height, point.y);

        // Rounded pack stones laid shoulder to shoulder, long side along the outline, half sunk. They
        // merge into one mesh at the pack's middle detail, a single draw for the whole ring.
        private static GameObject StoneRing(Transform parent)
        {
            var stones = new[] { "Stone1b", "Stone2b", "Stone3b" }.Select(name =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPrefabs + "Rocks/" + name + ".prefab");
                var lods = prefab != null && prefab.TryGetComponent<LODGroup>(out var group) ? group.GetLODs() : null;
                if (lods == null || lods.Length < 2) throw new InvalidOperationException("Missing approved Mountains stone " + name + ".");
                var renderer = lods[1].renderers[0];
                return (mesh: renderer.GetComponent<MeshFilter>().sharedMesh, material: renderer.sharedMaterial,
                    local: prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix);
            }).ToArray();
            if (stones.Any(stone => stone.material != stones[0].material))
                throw new InvalidOperationException("The ring's pack stones no longer share one material.");
            var random = new System.Random(4242);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var path = OutlinePath(StoneRingOffset, 0, 360);
            float total = path[path.Count - 1].along, along = 0;
            var parts = new List<CombineInstance>();
            while (along < total - .3f)
            {
                var stone = stones[random.Next(stones.Length)];
                var shape = Transformed(stone.mesh.bounds, stone.local);
                float length = Range(.5f, .75f), scale = length / Mathf.Max(shape.size.x, shape.size.z);
                var (point, tangent) = PathAt(path, along);
                // The stone's long horizontal axis follows the outline.
                var facing = Quaternion.LookRotation(new Vector3(tangent.x, 0, tangent.y)) *
                    (shape.size.x > shape.size.z ? Quaternion.Euler(0, 90, 0) : Quaternion.identity);
                var rotation = Quaternion.Euler(Range(-6, 6), Range(-9, 9), Range(-6, 6)) * facing;
                var placed = Matrix4x4.TRS(Ground(point), rotation, Vector3.one * scale) * stone.local;
                var bounds = Transformed(stone.mesh.bounds, placed);
                placed = Matrix4x4.Translate(Vector3.up * (SiteLayout.RimTop - bounds.min.y - Range(.35f, .45f) * bounds.size.y)) * placed;
                parts.Add(new CombineInstance { mesh = stone.mesh, transform = placed });
                // Overlap each neighbour a little: no gaps along the ring.
                along += length * .72f;
            }
            var ring = new Mesh { name = "DigBoundaryStones", indexFormat = IndexFormat.UInt32 };
            ring.CombineMeshes(parts.ToArray(), true, true);
            ring.RecalculateBounds();
            return BoundaryObject(parent, "Stone ring", SaveMesh(ring, BoundaryMeshFolder + "/DigBoundaryStones.asset"), stones[0].material);
        }

        private static Bounds Transformed(Bounds local, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.min), Vector3.zero);
            for (int i = 1; i < 8; i++)
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z)));
            return result;
        }

        // Timber stakes with red-and-white barrier tape along the outline, open where the camp's
        // winch cable enters the plot.
        private static GameObject SurveyTape(Transform parent, Material wood, Material red, Material white)
        {
            var random = new System.Random(77);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var path = OutlinePath(TapeOffset, SiteLayout.CampCompass + TapeGapHalfArc, SiteLayout.CampCompass + 360 - TapeGapHalfArc);
            float total = path[path.Count - 1].along;
            int count = Mathf.CeilToInt(total / 2.4f);
            var stakes = new List<(Vector3 foot, Vector3 top)>();
            for (int i = 0; i <= count; i++)
            {
                var (point, _) = PathAt(path, total * i / count);
                var foot = Ground(point, -.15f);
                stakes.Add((foot, foot + Quaternion.Euler(Range(-2, 2), 0, Range(-2, 2)) * Vector3.up * Range(1.1f, 1.2f)));
            }
            var mesh = new ProceduralMesh(3);
            foreach (var (foot, top) in stakes) mesh.Box(0, foot, top, .05f);
            for (int i = 1; i < stakes.Count; i++)
            {
                // A slightly sagging ribbon at knee height, in 25 cm stripes.
                Vector3 From(int s) => Vector3.Lerp(stakes[s].foot, stakes[s].top, .97f / Vector3.Distance(stakes[s].foot, stakes[s].top));
                Vector3 a = From(i - 1), b = From(i);
                int pieces = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / .25f));
                for (int k = 0; k < pieces; k++)
                {
                    float t0 = k / (float)pieces, t1 = (k + 1) / (float)pieces;
                    Vector3 p0 = Vector3.Lerp(a, b, t0) + Vector3.down * (.06f * 4 * t0 * (1 - t0));
                    Vector3 p1 = Vector3.Lerp(a, b, t1) + Vector3.down * (.06f * 4 * t1 * (1 - t1));
                    mesh.Ribbon(k % 2 == 0 ? 1 : 2, p0, p1, .06f);
                }
            }
            return BoundaryObject(parent, "Survey tape", mesh.Save("DigBoundaryTape"), wood, red, white);
        }

        // Weathered logs laid end to end along the outline, their lower third sunk into the ground.
        private static GameObject TimberEdging(Transform parent, Material wood)
        {
            var random = new System.Random(311);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var path = OutlinePath(EdgingOffset, 0, 360);
            float total = path[path.Count - 1].along, along = 0;
            var mesh = new ProceduralMesh(1);
            while (along < total - .2f)
            {
                float length = Mathf.Min(Range(1.3f, 1.9f), total - along + .1f), radius = Range(.13f, .17f);
                var (a, _) = PathAt(path, along);
                var (b, _) = PathAt(path, Mathf.Min(along + length, total));
                float lift = radius * .35f;
                mesh.Cylinder(0, Ground(a, lift), Ground(b, lift), radius, 9, Range(0, 1));
                along += length - .12f; // Overlapping ends close the joints on curves.
            }
            return BoundaryObject(parent, "Timber edging", mesh.Save("DigBoundaryEdging"), wood);
        }

        private static GameObject BoundaryObject(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return item;
        }

        // Weathered pack ash bark for timber, or a plain colour for tape; Inspector tuning is kept.
        private static Material BoundaryMaterial(string path, string name, Color colour, bool bark)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            if (bark)
            {
                const string ash = "Assets/BK/PureNature_Highlands/Models/Trees/Ash/AshTrunk_";
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(ash + "a.tga");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ash + "n.tga");
                if (albedo == null || normal == null) throw new InvalidOperationException("Missing approved Highlands ash bark.");
                material.SetTexture("_BaseMap", albedo);
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", bark ? .12f : .25f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Small generated meshes: boxes, open ribbons and capped cylinders, one submesh per material.
        private sealed class ProceduralMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int>[] triangles;

            public ProceduralMesh(int submeshes) =>
                triangles = Enumerable.Range(0, submeshes).Select(_ => new List<int>()).ToArray();

            private int Add(Vector3 position, Vector3 normal, Vector2 texcoord)
            {
                vertices.Add(position); normals.Add(normal); uv.Add(texcoord);
                return vertices.Count - 1;
            }

            private void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 size)
            {
                var normal = Vector3.Cross(b - a, d - a).normalized;
                int i0 = Add(a, normal, Vector2.zero), i1 = Add(b, normal, new Vector2(size.x, 0));
                int i2 = Add(c, normal, size), i3 = Add(d, normal, new Vector2(0, size.y));
                triangles[submesh].AddRange(new[] { i0, i2, i1, i0, i3, i2 });
            }

            public void Box(int submesh, Vector3 foot, Vector3 top, float width)
            {
                var up = (top - foot).normalized;
                var side = Vector3.Cross(up, Vector3.forward).normalized * width * .5f;
                var front = Vector3.Cross(side, up).normalized * width * .5f;
                Vector3[] ring = { -side - front, side - front, side + front, -side + front };
                float height = Vector3.Distance(foot, top);
                for (int i = 0; i < 4; i++)
                {
                    Vector3 a = ring[i], b = ring[(i + 1) % 4];
                    Quad(submesh, foot + a, foot + b, top + b, top + a, new Vector2(width, height));
                }
                Quad(submesh, top + ring[3], top + ring[2], top + ring[1], top + ring[0], Vector2.one * width);
            }

            // A thin vertical ribbon between two points, faces on both sides.
            public void Ribbon(int submesh, Vector3 from, Vector3 to, float height)
            {
                var up = Vector3.up * height;
                Quad(submesh, from, to, to + up, from + up, new Vector2(1, 1));
                Quad(submesh, to, from, from + up, to + up, new Vector2(1, 1));
            }

            public void Cylinder(int submesh, Vector3 from, Vector3 to, float radius, int sides, float phase)
            {
                var axis = (to - from).normalized;
                var side = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.forward : Vector3.up).normalized;
                var up = Vector3.Cross(side, axis);
                float length = Vector3.Distance(from, to);
                int first = vertices.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    var normal = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    // Slightly irregular timber around its axis.
                    float r = radius * (1 + .08f * Mathf.Sin(angle * 3 + phase * 6));
                    foreach (float t in new[] { 0f, 1f })
                        // The bark wraps once around; it is four circumferences tall.
                        Add(Vector3.Lerp(from, to, t) + normal * r, normal, new Vector2(i / (float)sides, phase + t * length / (radius * 2 * Mathf.PI * 4)));
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = first + i * 2;
                    triangles[submesh].AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
                }
                foreach (var (end, direction) in new[] { (from, -axis), (to, axis) })
                {
                    int centre = Add(end, direction, new Vector2(.5f, .5f));
                    for (int i = 0; i <= sides; i++)
                    {
                        float angle = i * Mathf.PI * 2 / sides;
                        Add(end + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius, direction,
                            new Vector2(.5f + .5f * Mathf.Cos(angle), .5f + .5f * Mathf.Sin(angle)));
                    }
                    for (int i = 0; i < sides; i++)
                        triangles[submesh].AddRange(direction == axis
                            ? new[] { centre, centre + 2 + i, centre + 1 + i }
                            : new[] { centre, centre + 1 + i, centre + 2 + i });
                }
            }

            public Mesh Save(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, subMeshCount = triangles.Length };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv);
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateTangents(); mesh.RecalculateBounds();
                return SaveMesh(mesh, BoundaryMeshFolder + "/" + name + ".asset");
            }
        }
    }
}
