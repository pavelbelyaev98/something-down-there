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
            new Bay("Backfill", "rubble fill, three quarters of soil's speed, an old chest 3 m down", Only(TerrainMaterialId.Backfill)),
            new Bay("Geode shell", "geode stone to 3 m: about a quarter of soil's speed", (u, d, v) =>
                d < 3f ? TerrainMaterialId.GeodeShell : TerrainMaterialId.Soil),
            new Bay("Geode", "a big geode 2 m down: dig in and break through", Only(TerrainMaterialId.Soil)),
        };

        // The geode bay's geode (110) with its crystals (DiscoveryField), as large as the site's, under the bay's centre: a
        // lobe out east under the unused slot and one north under the unused row, its hollow clear of the shell bay.
        public static readonly TerrainGround.Geode Geode = TerrainGround.Make(
            (Unity.Mathematics.float3)Local(new Vector3(BayCentre(3).x, -3.8f, BayCentre(3).y)),
            new Unity.Mathematics.float3(1.55f, 1.15f, 1.4f), .6f, 0, 0,
            new Unity.Mathematics.float3(1.25f, -.25f, .2f), new Unity.Mathematics.float3(1f, .8f, .9f),
            new Unity.Mathematics.float3(.2f, -.4f, 1.1f), new Unity.Mathematics.float3(.95f, .75f, .9f));

        // The backfill bay's old chest (user, 2026-10-06: "actual chest inside, so I test the whole flow"; the pit was too
        // tight to dig in), buried in its pocket of air and fill as the site's are, its lock to the south. pocket: the
        // chest's (size zero: no chest).
        private static readonly Vector3 LabStashAt = new Vector3(BayCentre(1).x, -3f, BayCentre(1).y);
        private static TerrainGround.Stash[] Stashes(Bounds pocket) => pocket.size == Vector3.zero ? Array.Empty<TerrainGround.Stash>()
            : new[] { TerrainGround.MakeStash((Unity.Mathematics.float3)Local(LabStashAt), Unity.Mathematics.quaternion.RotateY(Mathf.PI / 2), pocket) };

        // Four separate walkable caverns (115; user, 2026-10-07: "big walkable caverns", "easy to find"), made as the
        // site's are: three joined chambers each, about 10 m long and 3.4 m from floor to roof, the roof about 1.7 m down;
        // west, south-east, east and north of the plot, clear of the bays, crane scenes and find gallery (whose rows run
        // along z -0.8 to 3.2 from x -9); one of each cave crystal, shallow
        // to deep (DiscoveryField.SpawnLabCaves). Grey geode stone on the surface over each shows its shape (MarkCave).
        private static readonly Vector2[][] CaveChambers =
        {
            new[] { new Vector2(-13.2f, -4.6f), new Vector2(-13.4f, -1.6f), new Vector2(-13.1f, 1.4f) },
            new[] { new Vector2(8f, -6.8f), new Vector2(10.4f, -5.6f), new Vector2(11.9f, -3.8f) },
            new[] { new Vector2(3.8f, 2.6f), new Vector2(6.6f, 2.2f), new Vector2(8.8f, 2.6f) },
            new[] { new Vector2(-10f, 8.1f), new Vector2(-7f, 7.9f), new Vector2(-4f, 8.3f) },
        };
        private static readonly Unity.Mathematics.float3[] ChamberRadii =
            { new Unity.Mathematics.float3(2.2f, 2f, 2.1f), new Unity.Mathematics.float3(2.3f, 2.1f, 2.2f), new Unity.Mathematics.float3(2f, 1.9f, 2f) };
        private const float CaveCentreY = -3.8f, CaveFloorY = -5.2f, CaveMarkDepth = .375f;
        public static readonly TerrainGround.Cavern[] Caves = MakeCaves();

        private static TerrainGround.Cavern[] MakeCaves()
        {
            var caves = new TerrainGround.Cavern[CaveChambers.Length];
            for (int i = 0; i < caves.Length; i++)
                caves[i] = TerrainGround.MakeCavern(Array.ConvertAll(CaveChambers[i], c => Local3(c.x, CaveCentreY, c.y)), ChamberRadii,
                    Local3(0, CaveFloorY, 0).y, Array.Empty<Unity.Mathematics.float3>(), .6f,
                    new Unity.Mathematics.float3(41 + i * 17, 7 + i * 5, 113 - i * 11));
            return caves;
        }

        // The top of the ground over a cavern's chambers in geode stone: a grey patch the cavern's shape to dig down through.
        private static void MarkCave(byte[] ids, Vector3Int size, float cellSize, TerrainGround.Cavern cave)
        {
            int strideY = size.x + 1, strideZ = strideY * (size.y + 1), cells = Mathf.CeilToInt(CaveMarkDepth / cellSize);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cave.Min.x / cellSize)), x1 = Mathf.Min(size.x, Mathf.CeilToInt(cave.Max.x / cellSize));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(cave.Min.z / cellSize)), z1 = Mathf.Min(size.z, Mathf.CeilToInt(cave.Max.z / cellSize));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (!Over(cave, x * cellSize, z * cellSize)) continue;
                for (int y = size.y - cells; y <= size.y; y++) ids[y * strideY + z * strideZ + x] = (byte)TerrainMaterialId.GeodeShell;
            }
        }

        // Whether a grid-local x/z lies over one of a cavern's chambers.
        private static bool Over(in TerrainGround.Cavern cave, float x, float z)
        {
            for (int i = 0; i < cave.Centres.Length; i++)
            {
                float dx = (x - cave.Centres[i].x) / cave.Radii[i].x, dz = (z - cave.Centres[i].z) / cave.Radii[i].z;
                if (dx * dx + dz * dz < 1) return true;
            }
            return false;
        }

        private static Unity.Mathematics.float3 Local3(float x, float y, float z) => (Unity.Mathematics.float3)Local(new Vector3(x, y, z));

        // The lab's seeded ground: the backfill bay's chest, the geode bay's geode and the caves.
        public static TerrainGround.GroundLayout Layout(Bounds stashPocket = default) => new TerrainGround.GroundLayout(
            Array.Empty<TerrainGround.Pit>(), Stashes(stashPocket), new[] { Geode }, Caves);

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
        public static TerrainMaterialSnapshot Materials(Vector3Int size, float cellSize, Bounds stashPocket = default)
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
            foreach (var cave in Caves) { TerrainGround.FillCavern(ids, size, cellSize, cave); MarkCave(ids, size, cellSize, cave); }
            foreach (var stash in Stashes(stashPocket)) TerrainGround.FillShell(ids, size, cellSize, stash, new Unity.Mathematics.float4(17, 31, 47, 59));
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
            var local = Local(world);
            foreach (var cave in Caves)
                if (Over(cave, local.x, local.z))
                    return "Cavern: walk-in, one crystal kind; dig down through the grey stone, about 1.7 m  |  " + ground;
            return bay < 0 ? "Ground Lab  |  " + ground : $"{Bays[bay].Name}: {Bays[bay].Hint}  |  {ground}";
        }
    }
}
