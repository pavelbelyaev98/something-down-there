using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere.Editor
{
    // The dig plot outline is marked like an excavation site: timber posts with red-and-white barrier tape
    // tied round them at two heights on the permanent collar just outside where digging starts, open toward
    // the camp. Light stakes show by default; sturdier builds on 10 cm posts (in concrete, under a top rail,
    // or propped by raking braces) wait for the Developer admin to compare them (DigBoundaryStyles). Static,
    // no text. No colliders, so digging and aiming pass through.
    public static partial class LakebedSiteSetup
    {
        public const string BoundaryMeshFolder = Folder + "/Boundary";
        public const string BoundaryWoodPath = Folder + "/BoundaryWood.mat";
        public static readonly string[] BoundaryStyleNames = { "Double tape", "Heavy posts", "Post and rail", "Braced posts" };
        // Metres beyond the outline: the posts stand on the collar's flat top.
        public const float TapeOffset = .55f;
        private const float TapeGapHalfArc = 13;
        // Tape texture tile (metres along the tape); the two tapes' heights above the collar.
        private const float BandedTile = .5f;
        private static readonly float[] TapeHeights = { .48f, .95f };
        // Posts stand this deep in the collar.
        private const float PostDepth = .12f;
        // Heavy posts: 10 cm sawn timber, the tape tied just round them.
        private static readonly Vector2[] HeavySection = ChamferedSquare(.05f, .009f), HeavyLoop = ChamferedSquare(.054f, .011f);

        private static void BuildDigBoundary(Transform environment)
        {
            var parent = new GameObject("Dig boundary").transform;
            parent.SetParent(environment, false);
            if (!AssetDatabase.IsValidFolder(BoundaryMeshFolder)) AssetDatabase.CreateFolder(Folder, "Boundary");
            var wood = BoundaryMaterial(BoundaryWoodPath, "BoundaryWood", new Color(1, .96f, .9f));
            var end = PlainMaterial("BoundaryWoodEnd", new Color(.74f, .6f, .43f), .1f, 0);
            var tape = TapeMaterial("SurveyTapeBanded");
            var styles = new List<GameObject>
            {
                DoubleTape(parent, wood, end, tape),
                HeavyPosts(parent, wood, end, tape, ConcreteMaterial()),
                PostAndRail(parent, wood, end, tape),
                BracedPosts(parent, wood, end, tape),
            };
            for (int i = 1; i < styles.Count; i++) styles[i].SetActive(false);
            var switcher = new SerializedObject(parent.gameObject.AddComponent<DigBoundaryStyles>());
            var list = switcher.FindProperty("styles");
            list.arraySize = styles.Count;
            for (int i = 0; i < styles.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = styles[i];
            switcher.ApplyModifiedPropertiesWithoutUndo();
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

        // Posts for a run of tape: evenly spaced along the outline, each leaning (up to `lean` degrees) and turned
        // (up to `twist` degrees) a little, with the direction straight out from the plot's centre (the collar
        // keeps its width that way, wherever the outline wobbles).
        private static List<(Vector3 foot, Quaternion turn, float height, Vector3 outward)> StakeRun(System.Random random, float spacing,
            float minHeight, float maxHeight, float lean = 2.5f, float twist = 12)
        {
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            var path = BoundaryPath();
            float total = path[path.Count - 1].along;
            int count = Mathf.CeilToInt(total / spacing);
            var stakes = new List<(Vector3, Quaternion, float, Vector3)>();
            for (int i = 0; i <= count; i++)
            {
                var (point, tangent) = PathAt(path, total * i / count);
                float heading = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg + Range(-twist, twist);
                stakes.Add((Ground(point, -PostDepth), Quaternion.Euler(Range(-lean, lean), heading, Range(-lean, lean)), Range(minHeight, maxHeight),
                    new Vector3(point.x, 0, point.y).normalized));
            }
            return stakes;
        }

        // A run's tape tied round its posts at the given heights above the collar.
        private static void TieTape(SiteMesh mesh, List<(Vector3 foot, Quaternion turn, float height, Vector3 outward)> posts, float[] heights,
            Vector2[] loop, System.Random random)
        {
            foreach (float height in heights)
                Tape(mesh, 2, posts.Select(p => (p.foot + p.turn * Vector3.up * (height + PostDepth), p.turn)).ToList(), loop, .07f, BandedTile, random);
        }

        // Light sawn stakes (chamfered square, bark-faced, pale sawn tops), leaning and turned a little.
        private static GameObject DoubleTape(Transform parent, Material wood, Material end, Material tape)
        {
            var random = new System.Random(82);
            var stakes = StakeRun(random, 2.4f, 1.1f, 1.22f);
            var mesh = new SiteMesh(3);
            foreach (var stake in stakes) mesh.Prism(0, 1, stake.foot, stake.turn, stake.height, ChamferedSquare(.026f, .006f), .3f, .6f);
            TieTape(mesh, stakes, TapeHeights, ChamferedSquare(.029f, .007f), random);
            return BoundaryObject(parent, BoundaryStyleNames[0], mesh.Save("DigBoundaryDoubleTape"), wood, end, tape);
        }

        // Heavy posts set plumb and square to the line, for the sturdier styles.
        private static List<(Vector3 foot, Quaternion turn, float height, Vector3 outward)> HeavyRun(SiteMesh mesh, System.Random random,
            float minHeight, float maxHeight)
        {
            var posts = StakeRun(random, 2.4f, minHeight, maxHeight, .6f, 2);
            foreach (var post in posts) mesh.Prism(0, 1, post.foot, post.turn, post.height, HeavySection, .3f, .6f);
            return posts;
        }

        // Heavy posts, each cast into a round concrete footing that rises a little out of the collar.
        private static GameObject HeavyPosts(Transform parent, Material wood, Material end, Material tape, Material concrete)
        {
            var random = new System.Random(91);
            var mesh = new SiteMesh(4);
            var posts = HeavyRun(mesh, random, 1.15f, 1.19f);
            var footing = new[] { (.17f, -.06f), (.175f, .02f), (.165f, .05f), (.13f, .075f), (.07f, .085f), (0f, .086f) };
            foreach (var post in posts)
                mesh.Lathe(3, post.foot + Vector3.up * PostDepth, Quaternion.identity, footing, 20, 2, .4f);
            TieTape(mesh, posts, TapeHeights, HeavyLoop, random);
            return BoundaryObject(parent, BoundaryStyleNames[1], mesh.Save("DigBoundaryHeavyPosts"), wood, end, tape, concrete);
        }

        // Heavy posts under one continuous timber top rail laid flat on them, mitred over each post and
        // overhanging the end posts; the tapes hang lower, clear of it.
        private static GameObject PostAndRail(Transform parent, Material wood, Material end, Material tape)
        {
            var random = new System.Random(92);
            var mesh = new SiteMesh(3);
            var posts = HeavyRun(mesh, random, 1.16f, 1.18f);
            var line = posts.Select(p => p.foot + p.turn * Vector3.up * (p.height + .0225f)).ToList();
            line[0] += (line[0] - line[1]).normalized * .07f;
            line[line.Count - 1] += (line[line.Count - 1] - line[line.Count - 2]).normalized * .07f;
            mesh.Sweep(0, 1, line, ChamferedRect(.06f, .0225f, .006f), Vector3.up, .3f, .6f);
            TieTape(mesh, posts, new[] { .45f, .8f }, HeavyLoop, random);
            return BoundaryObject(parent, BoundaryStyleNames[2], mesh.Save("DigBoundaryPostAndRail"), wood, end, tape);
        }

        // Heavy posts, each propped from behind by a raking brace from three quarters of a metre up down into
        // the collar, its heel held by a short driven stake.
        private static GameObject BracedPosts(Transform parent, Material wood, Material end, Material tape)
        {
            var random = new System.Random(93);
            var mesh = new SiteMesh(3);
            var posts = HeavyRun(mesh, random, 1.15f, 1.19f);
            Vector2[] brace = ChamferedRect(.0225f, .045f, .005f);
            foreach (var post in posts)
            {
                Vector3 heel = post.foot + Vector3.up * (PostDepth - .05f) + post.outward * .45f;
                mesh.Sweep(0, 1, new[] { post.foot + post.turn * Vector3.up * (.75f + PostDepth), heel }, brace, Vector3.up, .3f, .6f);
                mesh.Prism(0, 1, heel + post.outward * .06f + Vector3.down * .07f, Quaternion.LookRotation(post.outward), .28f,
                    ChamferedSquare(.03f, .005f), .3f, .6f);
            }
            TieTape(mesh, posts, TapeHeights, HeavyLoop, random);
            return BoundaryObject(parent, BoundaryStyleNames[3], mesh.Save("DigBoundaryBracedPosts"), wood, end, tape);
        }

        // A rectangular timber section with chamfered corners, counter-clockwise.
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

        private static GameObject BoundaryObject(Transform parent, string name, Mesh mesh, params Material[] materials)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return item;
        }

        // Weathered pack ash bark for timber; Inspector tuning is kept.
        private static Material BoundaryMaterial(string path, string name, Color colour)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            const string ash = "Assets/BK/PureNature_Highlands/Models/Trees/Ash/AshTrunk_";
            return TexturedMaterial(path, name, ash + "a.tga", ash + "n.tga", colour, .12f, CullMode.Off);
        }

        // Cast concrete: the pack's rock detail (pores and hairline cracks) greyed down; Inspector tuning is kept.
        private static Material ConcreteMaterial()
        {
            string path = BoundaryMeshFolder + "/BoundaryConcrete.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            const string rock = "Assets/BK/PureNature_Highlands/Textures/Surfaces/_RockDetail1_";
            return TexturedMaterial(path, "BoundaryConcrete", rock + "a.png", rock + "n.png", new Color(.6f, .58f, .55f), .08f, CullMode.Back);
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
            material.SetTexture("_BaseMap", TapeTexture(name + "_Albedo", TextureImporterType.Default, true));
            material.SetTexture("_BumpMap", TapeTexture(name + "_Normal", TextureImporterType.NormalMap, false));
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D TapeTexture(string name, TextureImporterType type, bool colour)
        {
            string path = BoundaryMeshFolder + "/" + name + ".png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) throw new InvalidOperationException("Missing the survey tape texture " + path + " (art/survey-tape).");
            importer.textureType = type; importer.sRGBTexture = colour; importer.mipmapEnabled = true;
            importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp; importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // The boundary's mesh: smooth prisms with caps, swept beams, lathed parts and two-sided strips; one
        // submesh per material.
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

            // An upright prism of `section` (counter-clockwise from above) with a flat top cap; u runs round the
            // sides (per uTile metres), v up them (per vTile metres).
            public void Prism(int sides, int cap, Vector3 foot, Quaternion turn, float height, Vector2[] section, float uTile, float vTile)
            {
                int n = section.Length;
                Vector3 up = turn * Vector3.up;
                float perimeter = 0;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = section[i], b = section[(i + 1) % n];
                    Vector3 pa = turn * new Vector3(a.x, 0, a.y), pb = turn * new Vector3(b.x, 0, b.y);
                    Vector3 along = (pb - pa).normalized, normal = Vector3.Cross(up, along).normalized;
                    float length = (b - a).magnitude;
                    int i0 = Add(foot + pa, normal, along, new Vector2(perimeter / uTile, 0));
                    int i1 = Add(foot + pb, normal, along, new Vector2((perimeter + length) / uTile, 0));
                    int i2 = Add(foot + pb + up * height, normal, along, new Vector2((perimeter + length) / uTile, height / vTile));
                    int i3 = Add(foot + pa + up * height, normal, along, new Vector2(perimeter / uTile, height / vTile));
                    triangles[sides].AddRange(new[] { i0, i2, i1, i0, i3, i2 });
                    perimeter += length;
                }
                Vector3 top = foot + up * height, right = turn * Vector3.right;
                int centre = Add(top, up, right, new Vector2(.5f, .5f));
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = section[i], b = section[(i + 1) % n];
                    int ia = Add(top + turn * new Vector3(a.x, 0, a.y), up, right, a * 10 + Vector2.one * .5f);
                    int ib = Add(top + turn * new Vector3(b.x, 0, b.y), up, right, b * 10 + Vector2.one * .5f);
                    triangles[cap].AddRange(new[] { centre, ib, ia });
                }
            }

            // A beam of `section` (x across, y up) swept through `points` with mitred joints and capped ends, its
            // y axis toward `up`; u runs round it (per uTile metres), v along it (per vTile metres).
            public void Sweep(int sides, int ends, IList<Vector3> points, Vector2[] section, Vector3 up, float uTile, float vTile)
            {
                int count = points.Count, n = section.Length;
                var across = new Vector3[count];
                var rise = new Vector3[count];
                var along = new float[count];
                for (int i = 0; i < count; i++)
                {
                    int from = Mathf.Max(i, 1), to = Mathf.Min(i + 1, count - 1);
                    Vector3 tangent = ((points[from] - points[from - 1]).normalized + (points[to] - points[to - 1]).normalized).normalized;
                    across[i] = Vector3.Cross(up, tangent).normalized;
                    rise[i] = Vector3.Cross(tangent, across[i]);
                    if (i > 0) along[i] = along[i - 1] + Vector3.Distance(points[i], points[i - 1]);
                }
                Vector3 At(int i, Vector2 p) => points[i] + across[i] * p.x + rise[i] * p.y;
                float perimeter = 0;
                for (int e = 0; e < n; e++)
                {
                    Vector2 a = section[e], b = section[(e + 1) % n], outward = new Vector2(b.y - a.y, a.x - b.x).normalized;
                    if (Vector2.Dot(outward, a + b) < 0) outward = -outward;
                    float length = (b - a).magnitude;
                    int start = vertices.Count;
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 normal = (across[i] * outward.x + rise[i] * outward.y).normalized, round = (At(i, b) - At(i, a)).normalized;
                        Add(At(i, a), normal, round, new Vector2(perimeter / uTile, along[i] / vTile));
                        Add(At(i, b), normal, round, new Vector2((perimeter + length) / uTile, along[i] / vTile));
                    }
                    for (int i = 0; i < count - 1; i++)
                    {
                        int k = start + i * 2;
                        Triangle(sides, k, k + 1, k + 3);
                        Triangle(sides, k, k + 3, k + 2);
                    }
                    perimeter += length;
                }
                foreach (int i in new[] { 0, count - 1 })
                {
                    Vector3 normal = i == 0 ? (points[0] - points[1]).normalized : (points[count - 1] - points[count - 2]).normalized;
                    int centre = Add(points[i], normal, across[i], new Vector2(.5f, .5f));
                    for (int e = 0; e < n; e++)
                    {
                        Vector2 a = section[e], b = section[(e + 1) % n];
                        Triangle(ends, centre, Add(At(i, a), normal, across[i], a * 10 + Vector2.one * .5f),
                            Add(At(i, b), normal, across[i], b * 10 + Vector2.one * .5f));
                    }
                }
            }

            // A triangle wound to face along its first vertex's normal.
            private void Triangle(int submesh, int a, int b, int c)
            {
                bool facing = Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), normals[a]) >= 0;
                triangles[submesh].AddRange(facing ? new[] { a, b, c } : new[] { a, c, b });
            }

            // A solid of revolution about `turn`'s up axis from (radius, height) pairs, bottom to top; u runs
            // round it uTiles times, v up it vTiles times.
            public void Lathe(int submesh, Vector3 origin, Quaternion turn, (float r, float z)[] profile, int segments, float uTiles = 1, float vTiles = 1)
            {
                int start = vertices.Count;
                for (int j = 0; j < profile.Length; j++)
                {
                    var (r, z) = profile[j];
                    var (r0, z0) = profile[Mathf.Max(j - 1, 0)];
                    var (r1, z1) = profile[Mathf.Min(j + 1, profile.Length - 1)];
                    var slope = new Vector2(r1 - r0, z1 - z0).normalized;
                    for (int i = 0; i <= segments; i++)
                    {
                        float a = i * Mathf.PI * 2 / segments;
                        var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        Vector3 normal = turn * (radial * slope.y - Vector3.up * slope.x).normalized;
                        Add(origin + turn * (radial * r + Vector3.up * z), normal, turn * new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)),
                            new Vector2(i / (float)segments * uTiles, j / (float)(profile.Length - 1) * vTiles));
                    }
                }
                for (int j = 0; j < profile.Length - 1; j++)
                for (int i = 0; i < segments; i++)
                {
                    int a = start + j * (segments + 1) + i, b = a + segments + 1;
                    triangles[submesh].AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
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
