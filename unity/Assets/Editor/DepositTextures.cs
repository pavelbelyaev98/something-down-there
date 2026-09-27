using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // Original tileable surfaces for deposits the approved packs lack: concrete (cement mottling,
    // aggregate and pores) and packed gravel (rounded stones over dark fines). Each set is an
    // albedo, a normal map and an occlusion (G) mask. Generated once; delete a set to regenerate it.
    internal static class DepositTextures
    {
        private const int Size = 1024;

        public static Texture2D Concrete(string channel) => Load("Concrete", channel, GenerateConcrete);
        public static Texture2D Gravel(string channel) => Load("Gravel", channel, GenerateGravel);

        private static string PathOf(string kind, string channel) =>
            GroundTextureSetup.Folder + kind + "_" + (channel == "Roughness" ? "Mask" : channel) + ".png";

        private static Texture2D Load(string kind, string channel, Func<Surface> generate)
        {
            string path = PathOf(kind, channel);
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) Write(kind, generate());
            GroundTextureSetup.ConfigureImport(path, channel);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private sealed class Surface
        {
            public readonly float[] Height = new float[Size * Size];
            public readonly Color[] Colour = new Color[Size * Size];
            public readonly float[] Occlusion = new float[Size * Size];
            public float Relief, MaxSlope = float.PositiveInfinity;
        }

        private static Surface GenerateConcrete()
        {
            var random = new System.Random(7404);
            var s = new Surface { Relief = 2.2f };
            // Cement: soft periodic mottling in value and a faint warm/cool drift.
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float mottle = Fbm(x, y, 16, 4, 11) - .5f, drift = Fbm(x, y, 4, 2, 29) - .5f;
                int i = y * Size + x;
                s.Height[i] = mottle * .15f;
                float value = .6f + mottle * .09f;
                s.Colour[i] = new Color(value * (1 + drift * .04f), value, value * (1 - drift * .04f));
                s.Occlusion[i] = 1;
            }
            // Aggregate: rounded stones a few millimetres to two centimetres across, each its own tone.
            for (int n = 0; n < 1500; n++)
            {
                float cx = (float)random.NextDouble() * Size, cy = (float)random.NextDouble() * Size;
                float radius = 2.5f + (float)Math.Pow(random.NextDouble(), 2.2) * 8f;
                float tone = .42f + (float)random.NextDouble() * .3f, warmth = ((float)random.NextDouble() - .5f) * .08f;
                var shape = new Pebble(random, radius, .75f);
                var stone = new Color(tone * (1 + warmth), tone, tone * (1 - warmth));
                Stamp(cx, cy, radius + 1.5f, (i, dx, dy) =>
                {
                    if (!shape.Cover(dx, dy, out float cover, out float dome)) return;
                    s.Colour[i] = Color.Lerp(s.Colour[i], stone * (.92f + .08f * dome), cover);
                    s.Height[i] = Mathf.Max(s.Height[i], .25f + dome * .35f * Mathf.Min(1, shape.Edge / 6f));
                    // A thin cement seam darkens where each stone meets the paste.
                    s.Occlusion[i] = Mathf.Min(s.Occlusion[i], 1 - .25f * cover * (1 - dome));
                });
            }
            // Pores: small air voids read as dark pits.
            for (int n = 0; n < 1600; n++)
            {
                float cx = (float)random.NextDouble() * Size, cy = (float)random.NextDouble() * Size;
                float radius = .8f + (float)random.NextDouble() * 2.2f;
                Stamp(cx, cy, radius + 1, (i, dx, dy) =>
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy), cover = Mathf.Clamp01(radius + .5f - d);
                    if (cover <= 0) return;
                    s.Colour[i] = Color.Lerp(s.Colour[i], s.Colour[i] * .42f, cover);
                    s.Height[i] -= .3f * cover;
                    s.Occlusion[i] = Mathf.Min(s.Occlusion[i], 1 - .55f * cover);
                });
            }
            return s;
        }

        private static Surface GenerateGravel()
        {
            var random = new System.Random(7405);
            var s = new Surface { Relief = 1.6f, MaxSlope = 1.2f };
            // Fines: dark gritty sand, only visible in the gaps.
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float grit = Fbm(x, y, 64, 3, 41) - .5f;
                int i = y * Size + x;
                float value = .19f + grit * .07f;
                s.Colour[i] = new Color(value * 1.1f, value, value * .88f);
                s.Height[i] = -1 + grit * .3f;
                s.Occlusion[i] = .5f;
            }
            // Stones: rounded pebbles in a few muted rock families. Each rests on whatever lies
            // under its footprint, so later stones overlap earlier ones like a pile and every
            // stone keeps its own rounded edge.
            var families = new[]
            {
                new Color(.5f, .47f, .43f), new Color(.5f, .47f, .43f), new Color(.46f, .46f, .46f),
                new Color(.56f, .47f, .37f), new Color(.33f, .31f, .29f), new Color(.64f, .61f, .56f),
            };
            for (int n = 0; n < 7000; n++)
            {
                float cx = (float)random.NextDouble() * Size, cy = (float)random.NextDouble() * Size;
                float radius = 5 + (float)Math.Pow(random.NextDouble(), 1.6) * 12;
                var stone = families[random.Next(families.Length)] * (.85f + (float)random.NextDouble() * .25f);
                int salt = random.Next(1 << 20);
                var shape = new Pebble(random, radius, .6f);
                float rest = float.MinValue;
                Stamp(cx, cy, radius + 1.5f, (i, dx, dy) =>
                {
                    if (shape.Cover(dx, dy, out _, out _)) rest = Mathf.Max(rest, s.Height[i]);
                });
                Stamp(cx, cy, radius + 1.5f, (i, dx, dy) =>
                {
                    if (!shape.Cover(dx, dy, out float cover, out float dome)) return;
                    // Mottled grain keeps each stone from reading as a flat colour disc.
                    float grain = (Fbm(i % Size, i / Size, 128, 2, salt) - .5f) * .2f;
                    s.Colour[i] = Color.Lerp(s.Colour[i], stone * (.85f + .15f * dome + grain), cover);
                    s.Height[i] = Mathf.Lerp(s.Height[i], rest + dome * shape.Edge * .3f, cover);
                    s.Occlusion[i] = Mathf.Lerp(s.Occlusion[i], .55f + .45f * dome, cover);
                });
            }
            return s;
        }

        // An irregular, slightly flattened outline: no two pebbles repeat one shape.
        private readonly struct Pebble
        {
            private readonly float radius, lobeA, lobeB, squash, cos, sin;

            public Pebble(System.Random random, float radius, float minSquash)
            {
                this.radius = radius;
                lobeA = (float)random.NextDouble() * 6.283f;
                lobeB = (float)random.NextDouble() * 6.283f;
                squash = minSquash + (float)random.NextDouble() * (1 - minSquash);
                float turn = (float)random.NextDouble() * 6.283f;
                cos = Mathf.Cos(turn); sin = Mathf.Sin(turn);
                Edge = radius;
            }

            public float Edge { get; }

            public bool Cover(float dx, float dy, out float cover, out float dome)
            {
                float rx = dx * cos + dy * sin, ry = (dy * cos - dx * sin) / squash;
                float angle = Mathf.Atan2(ry, rx);
                float edge = radius * (1 + .1f * Mathf.Sin(angle * 2 + lobeA) + .07f * Mathf.Sin(angle * 5 + lobeB));
                float d = Mathf.Sqrt(rx * rx + ry * ry);
                cover = Mathf.Clamp01(edge + .5f - d);
                dome = Mathf.Sqrt(Mathf.Max(0, 1 - (d * d) / (edge * edge)));
                return cover > 0;
            }
        }

        private static void Stamp(float cx, float cy, float reach, Action<int, float, float> paint)
        {
            int r = Mathf.CeilToInt(reach);
            for (int oy = -r; oy <= r; oy++)
            for (int ox = -r; ox <= r; ox++)
            {
                int x = ((int)cx + ox + Size) % Size, y = ((int)cy + oy + Size) % Size;
                paint(y * Size + x, (int)cx + ox - cx + .5f, (int)cy + oy - cy + .5f);
            }
        }

        // Periodic value noise so the texture tiles: `cells` lattice cells across the image.
        private static float Fbm(int x, int y, int cells, int octaves, int salt)
        {
            float sum = 0, amplitude = .5f, total = 0;
            for (int o = 0; o < octaves; o++, cells *= 2, amplitude *= .5f)
            {
                float fx = x * cells / (float)Size, fy = y * cells / (float)Size;
                int x0 = (int)fx, y0 = (int)fy;
                float tx = Smooth(fx - x0), ty = Smooth(fy - y0);
                float a = Lattice(x0 % cells, y0 % cells, salt + o), b = Lattice((x0 + 1) % cells, y0 % cells, salt + o);
                float c = Lattice(x0 % cells, (y0 + 1) % cells, salt + o), d = Lattice((x0 + 1) % cells, (y0 + 1) % cells, salt + o);
                sum += amplitude * Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
                total += amplitude;
            }
            return sum / total;
        }

        private static float Smooth(float t) => t * t * (3 - 2 * t);

        private static float Lattice(int x, int y, int salt)
        {
            uint h = unchecked((uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 2246822519u);
            h = unchecked((h ^ (h >> 13)) * 1274126177u);
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        private static void Write(string kind, Surface s)
        {
            var normals = new Color[Size * Size];
            var masks = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // Slopes are clamped so stacked-stone steps read as bevels rather than cliffs.
                float hx = Mathf.Clamp(s.Height[y * Size + (x + 1) % Size] - s.Height[y * Size + (x + Size - 1) % Size], -s.MaxSlope, s.MaxSlope);
                float hy = Mathf.Clamp(s.Height[((y + 1) % Size) * Size + x] - s.Height[((y + Size - 1) % Size) * Size + x], -s.MaxSlope, s.MaxSlope);
                var n = new Vector3(-hx * s.Relief, -hy * s.Relief, 1).normalized;
                normals[y * Size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
                masks[y * Size + x] = new Color(1, s.Occlusion[y * Size + x], 0, 1);
            }
            WritePng(PathOf(kind, "Albedo"), s.Colour, true);
            WritePng(PathOf(kind, "Normal"), normals, false);
            WritePng(PathOf(kind, "Roughness"), masks, false);
        }

        private static void WritePng(string path, Color[] pixels, bool colour)
        {
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false, !colour);
            try
            {
                image.SetPixels(pixels);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
            AssetDatabase.ImportAsset(path);
        }
    }
}
