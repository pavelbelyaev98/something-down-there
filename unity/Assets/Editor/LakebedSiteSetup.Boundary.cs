using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere.Editor
{
    // The dig plot outline is marked like an excavation site: one red-and-white barrier tape tied round heavy
    // timber posts, each cast into a square concrete footing, on the permanent collar just outside where digging
    // starts, open toward the camp (the user's pick for feeling solid). Static, no text. No colliders, so digging and
    // aiming pass through.
    public static partial class LakebedSiteSetup
    {
        public const string BoundaryMeshFolder = Folder + "/Boundary";
        public const string BoundaryWoodPath = Folder + "/BoundaryWood.mat";
        // Metres beyond the outline: the posts stand on the collar's flat top.
        public const float TapeOffset = .55f;
        private const float TapeGapHalfArc = 13;
        // Tape texture tile (metres along the tape) and the tape's height above the collar.
        private const float BandedTile = .5f, TapeHeight = .9f;
        // Posts stand this deep in the collar.
        private const float PostDepth = .12f;
        // 10 cm sawn posts, the tape tied just round them.
        private static readonly Vector2[] PostSection = ChamferedSquare(.05f, .009f), TapeLoop = ChamferedSquare(.054f, .011f);
        // Footings: a square block tapering to a bevelled top (metres above the collar; the concrete strip maps its
        // full height, the top lies flat-mapped every TopTile metres).
        private const float TopTile = .25f;
        private const float SquareHalf = .165f, SquareBase = -.06f, SquareBlock = .14f, SquareBevel = .02f;

        private static void BuildDigBoundary(Transform environment)
        {
            if (!AssetDatabase.IsValidFolder(BoundaryMeshFolder)) AssetDatabase.CreateFolder(Folder, "Boundary");
            var wood = BoundaryMaterial(BoundaryWoodPath, "BoundaryWood", new Color(1, .96f, .9f));
            var end = PlainMaterial("BoundaryWoodEnd", new Color(.74f, .6f, .43f), .1f, 0);
            var tape = TapeMaterial("SurveyTapeBanded");
            var concrete = FootingMaterial("BoundaryFooting");
            var top = FootingMaterial("BoundaryFootingTop");
            // Posts (submeshes 0-1) cast into their footings (3 sides, 4 top), the tape (2) tied round them.
            var random = new System.Random(92);
            var mesh = new SiteMesh(5);
            var posts = PostRun(random);
            foreach (var post in posts)
            {
                SquareCast(mesh, post);
                mesh.Prism(0, 1, post.foot, post.turn, post.height, PostSection, .3f, .6f);
            }
            Tape(mesh, 2, posts.Select(p => (p.foot + p.turn * Vector3.up * (TapeHeight + PostDepth), p.turn)).ToList(), TapeLoop, .07f, BandedTile, random);
            var boundary = new GameObject("Dig boundary");
            boundary.transform.SetParent(environment, false);
            boundary.AddComponent<MeshFilter>().sharedMesh = mesh.Save("DigBoundary");
            boundary.AddComponent<MeshRenderer>().sharedMaterials = new[] { wood, end, tape, concrete, top };
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

        private static List<(Vector2 point, float along)> BoundaryPath() =>
            OutlinePath(TapeOffset, SiteLayout.CampCompass + TapeGapHalfArc, SiteLayout.CampCompass + 360 - TapeGapHalfArc);

        private static (Vector2 point, Vector2 tangent) PathAt(List<(Vector2 point, float along)> path, float distance)
        {
            int i = 0;
            while (i < path.Count - 2 && path[i + 1].along < distance) i++;
            var (a, da) = path[i];
            var (b, db) = path[i + 1];
            return (Vector2.Lerp(a, b, Mathf.InverseLerp(da, db, distance)), (b - a).normalized);
        }

        private static Vector3 Ground(Vector2 point, float height = 0) => new Vector3(point.x, SiteLayout.RimTop + height, point.y);

        // Posts for the tape: evenly spaced along the outline, set plumb and square to the line within a degree or two.
        private static List<(Vector3 foot, Quaternion turn, float height)> PostRun(System.Random random)
        {
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var path = BoundaryPath();
            float total = path[path.Count - 1].along;
            int count = Mathf.CeilToInt(total / 2.4f);
            var posts = new List<(Vector3, Quaternion, float)>();
            for (int i = 0; i <= count; i++)
            {
                var (point, tangent) = PathAt(path, total * i / count);
                float heading = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg + Range(-2, 2);
                posts.Add((Ground(point, -PostDepth), Quaternion.Euler(Range(-.6f, .6f), heading, Range(-.6f, .6f)), Range(1.15f, 1.19f)));
            }
            return posts;
        }

        // Where a post comes out of the collar, and its heading without the lean.
        private static Vector3 Collar((Vector3 foot, Quaternion turn, float height) post) => post.foot + Vector3.up * PostDepth;
        private static Quaternion Level((Vector3 foot, Quaternion turn, float height) post) => Quaternion.Euler(0, post.turn.eulerAngles.y, 0);

        // A square block, square to the line, tapering a little to a bevelled top.
        private static void SquareCast(SiteMesh mesh, (Vector3 foot, Quaternion turn, float height) post)
        {
            var block = ChamferedSquare(SquareHalf, .02f);
            float total = SquareBlock + SquareBevel, shoulder = SquareHalf * .9f;
            mesh.Prism(3, 4, Collar(post) + Vector3.up * SquareBase, Level(post), SquareBlock, block, Perimeter(block) / 3, total, .9f);
            var bevel = ChamferedSquare(shoulder, .018f);
            mesh.Prism(3, 4, Collar(post) + Vector3.up * (SquareBase + SquareBlock), Level(post), SquareBevel, bevel, Perimeter(bevel) / 3, total,
                .88f, SquareBlock, TopTile);
        }

        private static float Perimeter(Vector2[] section) => section.Select((p, i) => Vector2.Distance(p, section[(i + 1) % section.Length])).Sum();

        // A rectangular section with chamfered corners, counter-clockwise.
        private static Vector2[] ChamferedRect(float halfX, float halfY, float chamfer)
        {
            float x = halfX, y = halfY, cx = halfX - chamfer, cy = halfY - chamfer;
            return new[] { new Vector2(cx, -y), new Vector2(x, -cy), new Vector2(x, cy), new Vector2(cx, y),
                new Vector2(-cx, y), new Vector2(-x, cy), new Vector2(-x, -cy), new Vector2(-cx, -y) };
        }

        private static Vector2[] ChamferedSquare(float half, float chamfer) => ChamferedRect(half, half, chamfer);

        // Tape tied round each post (once round its section) and hanging between them: a shallow sag and a
        // twist that peaks mid-span.
        private static void Tape(SiteMesh mesh, int submesh, List<(Vector3 centre, Quaternion turn)> posts, Vector2[] section, float width, float tile,
            System.Random random)
        {
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var rows = new List<(Vector3 centre, Vector3 up)>();
            for (int i = 0; i < posts.Count; i++)
            {
                var (centre, turn) = posts[i];
                Vector3 arriving = i > 0 ? centre - rows[rows.Count - 1].centre : posts[1].centre - centre;
                arriving.y = 0; arriving.Normalize();
                var corners = section.Select(p => turn * new Vector3(p.x, 0, p.y)).ToArray();
                int first = Enumerable.Range(0, corners.Length).OrderBy(c => Vector3.Dot(corners[c], arriving)).First();
                for (int k = 0; k <= corners.Length; k++) rows.Add((centre + corners[(first + k) % corners.Length], turn * Vector3.up));
                if (i == posts.Count - 1) break;
                Vector3 from = rows[rows.Count - 1].centre, span = posts[i + 1].centre - from;
                float sag = Range(.03f, .07f), twist = Range(-25f, 25f);
                for (int k = 1; k < 16; k++)
                {
                    float t = k / 16f, bell = 4 * t * (1 - t);
                    rows.Add((from + span * t + Vector3.down * sag * bell, Quaternion.AngleAxis(twist * bell, span) * Vector3.up));
                }
            }
            float u = 0, half = width / 2;
            var strip = new List<(Vector3 bottom, Vector3 top, float u)>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) u += Vector3.Distance(rows[i].centre, rows[i - 1].centre) / tile;
                strip.Add((rows[i].centre - rows[i].up * half, rows[i].centre + rows[i].up * half, u));
            }
            mesh.Strip(submesh, strip);
        }

        // Weathered pack ash bark for timber; Inspector tuning is kept.
        private static Material BoundaryMaterial(string path, string name, Color colour)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            const string ash = "Assets/BK/PureNature_Highlands/Models/Trees/Ash/AshTrunk_";
            return TexturedMaterial(path, name, ash + "a.tga", ash + "n.tga", colour, .12f, CullMode.Off);
        }

        // Cast concrete (art/fence-footing): the sides' strip with soil soaked up from the ground, or the clean top laid
        // flat; Inspector tuning is kept.
        private static Material FootingMaterial(string name)
        {
            string path = BoundaryMeshFolder + "/" + name + ".mat";
            bool flat = name.EndsWith("Top");
            var albedo = BoundaryTexture(name + "_Albedo", TextureImporterType.Default, true, "art/fence-footing", flat);
            var normal = BoundaryTexture(name + "_Normal", TextureImporterType.NormalMap, false, "art/fence-footing", flat);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            return TexturedMaterial(path, name, AssetDatabase.GetAssetPath(albedo), AssetDatabase.GetAssetPath(normal), Color.white, .1f, CullMode.Back);
        }

        private static Material TexturedMaterial(string path, string name, string albedoPath, string normalPath, Color colour, float smoothness,
            CullMode cull)
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (albedo == null || normal == null) throw new InvalidOperationException("Missing approved Highlands texture " + albedoPath + ".");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", (float)cull);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // A plain URP Lit colour; Inspector tuning is kept.
        private static Material PlainMaterial(string name, Color colour, float smoothness, float metallic)
        {
            string path = BoundaryMeshFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Textured tape (art/survey-tape): albedo and crease normal map, a plastic sheen.
        private static Material TapeMaterial(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            string path = BoundaryMeshFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                material.SetFloat("_Smoothness", .5f);
                material.SetFloat("_BumpScale", .6f);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", BoundaryTexture(name + "_Albedo", TextureImporterType.Default, true, "art/survey-tape"));
            material.SetTexture("_BumpMap", BoundaryTexture(name + "_Normal", TextureImporterType.NormalMap, false, "art/survey-tape"));
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        // A generated boundary texture: a strip repeating along u and clamped across v, or repeating both ways.
        private static Texture2D BoundaryTexture(string name, TextureImporterType type, bool colour, string source, bool tiles = false)
        {
            string path = BoundaryMeshFolder + "/" + name + ".png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) throw new InvalidOperationException("Missing the boundary texture " + path + " (" + source + ").");
            importer.textureType = type; importer.sRGBTexture = colour; importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The boundary's mesh: smooth prisms with caps, lathed parts and two-sided strips; one submesh per material.
        private sealed class SiteMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector4> tangents = new List<Vector4>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int>[] triangles;

            public SiteMesh(int submeshes) => triangles = Enumerable.Range(0, submeshes).Select(_ => new List<int>()).ToArray();

            private int Add(Vector3 position, Vector3 normal, Vector3 tangent, Vector2 texcoord)
            {
                vertices.Add(position); normals.Add(normal); tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1));
                uv.Add(texcoord);
                return vertices.Count - 1;
            }

            // An upright prism of `section` (counter-clockwise from above), narrowing to `topScale` of it, with a flat
            // top cap; u runs round the sides (per uTile metres), v up them (per vTile metres).
            public void Prism(int sides, int cap, Vector3 foot, Quaternion turn, float height, Vector2[] section, float uTile, float vTile,
                float topScale = 1, float vStart = 0, float capTile = .1f)
            {
                int n = section.Length;
                Vector3 up = turn * Vector3.up;
                float perimeter = 0;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = section[i], b = section[(i + 1) % n];
                    Vector3 pa = turn * new Vector3(a.x, 0, a.y), pb = turn * new Vector3(b.x, 0, b.y);
                    Vector3 along = (pb - pa).normalized, rise = up * height + pa * (topScale - 1);
                    Vector3 normal = Vector3.Cross(rise, along).normalized;
                    float length = (b - a).magnitude;
                    float v0 = vStart / vTile, v1 = (vStart + height) / vTile;
                    int i0 = Add(foot + pa, normal, along, new Vector2(perimeter / uTile, v0));
                    int i1 = Add(foot + pb, normal, along, new Vector2((perimeter + length) / uTile, v0));
                    int i2 = Add(foot + pb * topScale + up * height, normal, along, new Vector2((perimeter + length) / uTile, v1));
                    int i3 = Add(foot + pa * topScale + up * height, normal, along, new Vector2(perimeter / uTile, v1));
                    triangles[sides].AddRange(new[] { i0, i2, i1, i0, i3, i2 });
                    perimeter += length;
                }
                Vector3 top = foot + up * height, right = turn * Vector3.right;
                int centre = Add(top, up, right, new Vector2(.5f, .5f));
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = section[i] * topScale, b = section[(i + 1) % n] * topScale;
                    int ia = Add(top + turn * new Vector3(a.x, 0, a.y), up, right, a / capTile + Vector2.one * .5f);
                    int ib = Add(top + turn * new Vector3(b.x, 0, b.y), up, right, b / capTile + Vector2.one * .5f);
                    triangles[cap].AddRange(new[] { centre, ib, ia });
                }
            }

            // A two-sided strip through rows of (bottom, top) points, u along it and v across.
            public void Strip(int submesh, IList<(Vector3 bottom, Vector3 top, float u)> rows)
            {
                for (int face = 0; face < 2; face++)
                {
                    int start = vertices.Count;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var (bottom, top, u) = rows[i];
                        Vector3 along = (rows[Mathf.Min(i + 1, rows.Count - 1)].bottom - rows[Mathf.Max(i - 1, 0)].bottom).normalized;
                        Vector3 normal = Vector3.Cross(along, top - bottom).normalized * (face == 0 ? 1 : -1);
                        Add(bottom, normal, along, new Vector2(u, 0));
                        Add(top, normal, along, new Vector2(u, 1));
                    }
                    for (int i = 0; i < rows.Count - 1; i++)
                    {
                        int a = start + i * 2;
                        if (face == 0) triangles[submesh].AddRange(new[] { a, a + 3, a + 1, a, a + 2, a + 3 });
                        else triangles[submesh].AddRange(new[] { a, a + 1, a + 3, a, a + 3, a + 2 });
                    }
                }
            }

            public Mesh Save(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, subMeshCount = triangles.Length };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTangents(tangents);
                mesh.SetUVs(0, uv);
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                return SaveMesh(mesh, BoundaryMeshFolder + "/" + name + ".asset");
            }
        }
    }
}
