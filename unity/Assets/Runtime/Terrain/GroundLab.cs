using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // Developer Ground Lab (development builds, title menu): the site's grid refilled with labelled
    // 3 x 3 m bays, 12 m deep, each one ground alone or a mix that shows one tell, plus the crane scenes
    // around them (GroundLab.Crane).
    // Never saved; leaving reloads MainGame, restarting reloads it straight back into the lab.
    public static partial class GroundLab
    {
        public const float BayHalf = 1.5f, BayDepth = 12f;
        private static readonly float[] Columns = { -8.75f, -5.25f, -1.75f, 1.75f, 5.25f, 8.75f };
        private static readonly float[] Rows = { -4.5f, -1f, 2.5f };

        public readonly struct Bay
        {
            public readonly string Name, Hint;
            public readonly Func<float, float, float, TerrainMaterialId> Ground;
            public Bay(string name, string hint, Func<float, float, float, TerrainMaterialId> ground)
            { Name = name; Hint = hint; Ground = ground; }
        }

        private static Func<float, float, float, TerrainMaterialId> Only(TerrainMaterialId ground) => (u, d, v) => ground;

        // From the spawn (south) inward: single grounds, then mixes.
        public static readonly Bay[] Bays =
        {
            new Bay("Soil", "plain ground, broad cuts", Only(TerrainMaterialId.Soil)),
            new Bay("Backfill", "rubble fill: about three quarters of soil's speed", Only(TerrainMaterialId.Backfill)),
            new Bay("Backfill pit", "1 m rubble pit in soil, 5 m deep", (u, d, v) =>
                Mathf.Abs(u) < .5f && Mathf.Abs(v) < .5f && d < 5f ? TerrainMaterialId.Backfill : TerrainMaterialId.Soil),
            new Bay("Geode shell", "geode stone to 3 m: about a quarter of soil's speed", (u, d, v) =>
                d < 3f ? TerrainMaterialId.GeodeShell : TerrainMaterialId.Soil),
            new Bay("Geode", "a whole geode 2.6 m down: dig in and break through", Only(TerrainMaterialId.Soil)),
        };

        // The geode bay's geode (110), a small one with its crystals (DiscoveryField), as the site's are made.
        public static readonly TerrainGround.Geode Geode = TerrainGround.Make(
            (Unity.Mathematics.float3)Local(new Vector3(BayCentre(4).x, -2.6f, BayCentre(4).y)),
            new Unity.Mathematics.float3(.9f, .75f, .9f), .6f, .4f, 0);

        // The lab's seeded ground: no pits, the geode bay's geode.
        public static TerrainGround.GroundLayout Layout() => new TerrainGround.GroundLayout(
            Array.Empty<TerrainGround.Pit>(), Array.Empty<TerrainGround.Stash>(), new[] { Geode });

        public static Vector2 BayCentre(int bay) => new Vector2(Columns[bay % Columns.Length], Rows[bay / Columns.Length]);

        // Which bay holds a world x/z (-1 between bays and on unused slots), with the position inside it.
        private static int BayAt(float x, float z, out float u, out float v)
        {
            u = v = 0;
            for (int row = 0; row < Rows.Length; row++)
            {
                if (Mathf.Abs(z - Rows[row]) > BayHalf) continue;
                for (int column = 0; column < Columns.Length; column++)
                {
                    if (Mathf.Abs(x - Columns[column]) > BayHalf) continue;
                    int bay = row * Columns.Length + column;
                    if (bay >= Bays.Length) return -1;
                    u = x - Columns[column]; v = z - Rows[row];
                    return bay;
                }
            }
            return -1;
        }

        // The lab's grounds for the shipped site grid: bays in soil to 12 m, soil below.
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
                if (depth > BayDepth) continue;
                float wz = origin.z + z * cellSize;
                for (int x = 0; x <= size.x; x++)
                {
                    int bay = BayAt(origin.x + x * cellSize, wz, out float u, out float v);
                    ids[row + x] = (byte)(bay < 0 ? TerrainMaterialId.Soil : Bays[bay].Ground(u, depth, v));
                }
            }
            TerrainGround.FillGeode(ids, size, cellSize, Geode);
            return TerrainMaterialSnapshot.CopyFrom(ids);
        }

        // The lab's air (grid-local metres): the crane scenes' pockets, pits, shafts and tunnels.
        public static List<ExcavationGrid.LabCarve> Cavities()
        {
            var carves = new List<ExcavationGrid.LabCarve>();
            AddCraneCarves(carves);
            return carves;
        }

        // The aim prompt names the bay or crane scene under the crosshair and the ground actually hit
        // (3D text would draw through the world with the built-in font shader).
        public static string Describe(Vector3 world, TerrainMaterialId hit)
        {
            var crane = DescribeCrane(world);
            if (crane != null) return crane;
            int bay = BayAt(world.x, world.z, out _, out _);
            string ground = "hitting " + hit;
            return bay < 0 ? "Ground Lab  |  " + ground : $"{Bays[bay].Name}: {Bays[bay].Hint}  |  {ground}";
        }
    }
}
