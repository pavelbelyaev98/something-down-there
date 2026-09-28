using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // Developer Ground Lab (development builds, title menu): the site's grid refilled with labelled
    // 3 x 3 m bays, 12 m deep, each one ground alone or a mix that shows one behaviour (a pour, a
    // slump, a crack break, a zone change). Never saved; leaving reloads MainGame.
    public static class GroundLab
    {
        public const float BayHalf = 1.5f, BayDepth = 12f;
        private static readonly float[] Columns = { -8.75f, -5.25f, -1.75f, 1.75f, 5.25f, 8.75f };
        private static readonly float[] Rows = { -4.5f, -1f, 2.5f };

        public readonly struct Bay
        {
            public readonly string Name, Hint;
            public readonly Func<float, float, float, TerrainMaterialId> Ground;
            public readonly bool Cavity;
            public Bay(string name, string hint, Func<float, float, float, TerrainMaterialId> ground, bool cavity = false)
            { Name = name; Hint = hint; Ground = ground; Cavity = cavity; }
        }

        private static Func<float, float, float, TerrainMaterialId> Only(TerrainMaterialId ground) => (u, d, v) => ground;

        // A crack sheet: its line within 4 cm, its shattered band within 25 cm.
        private static TerrainMaterialId? Crack(float distance, TerrainMaterialId band)
            => Mathf.Abs(distance) < .04f ? TerrainMaterialId.Crack : Mathf.Abs(distance) < .25f ? band : (TerrainMaterialId?)null;

        // Rows from the spawn (south) inward: single grounds, harder grounds, then mixes.
        public static readonly Bay[] Bays =
        {
            new Bay("Soil", "loose, broad cuts", Only(TerrainMaterialId.Soil)),
            new Bay("Gravel", "grainy; pours when undercut", Only(TerrainMaterialId.Gravel)),
            new Bay("Backfill", "loose fill; slumps when undercut", Only(TerrainMaterialId.Backfill)),
            new Bay("Clay", "steady narrow shavings", Only(TerrainMaterialId.Clay)),
            new Bay("Pond clay", "smooth; easier than clay", Only(TerrainMaterialId.PondClay)),
            new Bay("Rock", "small faceted chips", Only(TerrainMaterialId.Rock)),

            new Bay("Concrete", "smallest square chips", Only(TerrainMaterialId.Concrete)),
            new Bay("Shattered rock", "a crack's band, all through", Only(TerrainMaterialId.FracturedRock)),
            new Bay("Shattered concrete", "a crack's band in concrete", Only(TerrainMaterialId.FracturedConcrete)),
            new Bay("Rock with cracks", "dig into a pale seam", (u, d, v) =>
                Crack((u + .35f * v) / 1.06f, TerrainMaterialId.FracturedRock)
                ?? Crack((.3f * u - .8f * (d - 5f) - .5f * v) / .99f, TerrainMaterialId.FracturedRock) ?? TerrainMaterialId.Rock),
            new Bay("Concrete with crack", "dig into the seam", (u, d, v) =>
                Crack((u - .3f * v) / 1.04f, TerrainMaterialId.FracturedConcrete) ?? TerrainMaterialId.Concrete),
            new Bay("Rock with clay vein", "the soft path through rock", (u, d, v) =>
                new Vector2(u - .8f * Mathf.Sin(d * .9f), v - .6f * Mathf.Cos(d * .7f)).magnitude < .55f ? TerrainMaterialId.Clay : TerrainMaterialId.Rock),

            new Bay("Gravel under soil", "gravel 1.2-2.4 m: dig under it", (u, d, v) =>
                d >= 1.2f && d <= 2.4f ? TerrainMaterialId.Gravel : TerrainMaterialId.Soil),
            new Bay("Backfill pit", "1 m pit in clay: undercut it", (u, d, v) =>
                Mathf.Abs(u) < .5f && Mathf.Abs(v) < .5f && d < 5f ? TerrainMaterialId.Backfill : TerrainMaterialId.Clay),
            new Bay("Thin soil roof", "hollow under 0.6 m: soil holds", Only(TerrainMaterialId.Soil), cavity: true),
            new Bay("Thin clay roof", "same hollow; clay holds", Only(TerrainMaterialId.Clay), cavity: true),
            new Bay("Soil, clay, rock", "zone changes at 2 m and 4 m", (u, d, v) =>
                d < 2f ? TerrainMaterialId.Soil : d < 4f ? TerrainMaterialId.Clay : TerrainMaterialId.Rock),
            new Bay("Pond-clay lens", "an odd spot in clay", (u, d, v) =>
                (u / 1.2f) * (u / 1.2f) + (v / 1.2f) * (v / 1.2f) + ((d - 1.8f) / .7f) * ((d - 1.8f) / .7f) < 1f ? TerrainMaterialId.PondClay : TerrainMaterialId.Clay),
        };

        public static Vector2 BayCentre(int bay) => new Vector2(Columns[bay % Columns.Length], Rows[bay / Columns.Length]);

        // Which bay holds a world x/z (null between bays), with the position inside it.
        private static int BayAt(float x, float z, out float u, out float v)
        {
            u = v = 0;
            for (int row = 0; row < Rows.Length; row++)
            {
                if (Mathf.Abs(z - Rows[row]) > BayHalf) continue;
                for (int column = 0; column < Columns.Length; column++)
                {
                    if (Mathf.Abs(x - Columns[column]) > BayHalf) continue;
                    u = x - Columns[column]; v = z - Rows[row];
                    return row * Columns.Length + column;
                }
            }
            return -1;
        }

        // The lab's grounds for the shipped site grid: bays in soil to 12 m, rock below.
        public static TerrainMaterialSnapshot Materials(Vector3Int size, float cellSize)
        {
            int strideY = size.x + 1, strideZ = strideY * (size.y + 1);
            var ids = new byte[strideZ * (size.z + 1)];
            var origin = SiteLayout.Origin;
            for (int z = 0; z <= size.z; z++)
            for (int y = 0; y <= size.y; y++)
            {
                int row = y * strideY + z * strideZ;
                float depth = (size.y - y) * cellSize;
                if (depth > BayDepth) { Array.Fill(ids, (byte)TerrainMaterialId.Rock, row, strideY); continue; }
                float wz = origin.z + z * cellSize;
                for (int x = 0; x <= size.x; x++)
                {
                    int bay = BayAt(origin.x + x * cellSize, wz, out float u, out float v);
                    ids[row + x] = (byte)(bay < 0 ? TerrainMaterialId.Soil : Bays[bay].Ground(u, depth, v));
                }
            }
            return TerrainMaterialSnapshot.CopyFrom(ids);
        }

        // Air boxes (grid-local metres) under the thin-roof bays: 2 x 2 m wide, 0.6-1.8 m down.
        public static List<(Vector3 min, Vector3 max)> Cavities(Vector3Int size, float cellSize)
        {
            var boxes = new List<(Vector3, Vector3)>();
            float top = size.y * cellSize;
            for (int bay = 0; bay < Bays.Length; bay++)
            {
                if (!Bays[bay].Cavity) continue;
                var centre = BayCentre(bay) - new Vector2(SiteLayout.Origin.x, SiteLayout.Origin.z);
                boxes.Add((new Vector3(centre.x - 1, top - 1.8f, centre.y - 1), new Vector3(centre.x + 1, top - .6f, centre.y + 1)));
            }
            return boxes;
        }

        // The aim prompt names the bay under the crosshair and the ground actually hit (3D text
        // would draw through the world with the built-in font shader).
        public static string Describe(Vector3 world, TerrainMaterialId hit)
        {
            int bay = BayAt(world.x, world.z, out _, out _);
            string ground = "hitting " + hit;
            return bay < 0 ? "Ground Lab  |  " + ground : $"{Bays[bay].Name}: {Bays[bay].Hint}  |  {ground}";
        }
    }
}
