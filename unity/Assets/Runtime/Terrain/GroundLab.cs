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

        // A bay: its ground at a bay-local position (u, depth, v) and its slot in the grid (row * columns + column).
        public readonly struct Bay
        {
            public readonly string Name, Hint;
            public readonly Func<float, float, float, TerrainMaterialId> Ground;
            public readonly int Slot;
            public Bay(string name, string hint, Func<float, float, float, TerrainMaterialId> ground, int slot)
            { Name = name; Hint = hint; Ground = ground; Slot = slot; }
        }

        private static Func<float, float, float, TerrainMaterialId> Only(TerrainMaterialId ground) => (u, d, v) => ground;

        // The zone borders bay (111): soil, then lake sediment from about LabBorders.x down and the riverbed from about
        // LabBorders.y, each border mixed in patches as the site's are (TerrainGround.ZoneIn); the site's broad warp would
        // only shift a 3 m bay's borders, so the bay shows the band alone.
        private static readonly Unity.Mathematics.float3 LabBorders = new Unity.Mathematics.float3(3.5f, 8f, 1000f);
        private static readonly Unity.Mathematics.float4 LabNoise = new Unity.Mathematics.float4(17, 31, 47, 59);
        private static TerrainMaterialId ZoneBorders(float u, float d, float v)
            => TerrainGround.ZoneGround(TerrainGround.ZoneIn(new Unity.Mathematics.float3(u, -d, v), d, LabBorders, LabNoise));

        // From the spawn (south) inward: single grounds, then mixes. The middle row and the north row's west half lie
        // under the find gallery, so the zone grounds take the free east slots.
        public static readonly Bay[] Bays =
        {
            new Bay("Soil", "plain ground, broad cuts", Only(TerrainMaterialId.Soil), 0),
            new Bay("Backfill", "rubble fill, three quarters of soil's speed, an old chest 3 m down", Only(TerrainMaterialId.Backfill), 1),
            new Bay("Geode shell", "geode stone to 3 m: about a quarter of soil's speed", (u, d, v) =>
                d < 3f ? TerrainMaterialId.GeodeShell : TerrainMaterialId.Soil, 2),
            new Bay("Geode", "a big geode 2 m down: dig in and break through", Only(TerrainMaterialId.Soil), 3),
            new Bay("Cave rock", "a great cave's stone to 3 m: as hard as geode shell", (u, d, v) =>
                d < 3f ? TerrainMaterialId.CaveRock : TerrainMaterialId.Soil, 4),
            new Bay("Lake sediment", "zone 2's main ground, old grey lake silt: a little firmer than soil", Only(TerrainMaterialId.LakeSediment), 5),
            new Bay("Riverbed", "zone 3's main ground, old river sand and gravel: stony, about two thirds of soil's speed",
                Only(TerrainMaterialId.Riverbed), 16),
            new Bay("Zone borders", "soil, lake sediment from about 3.5 m, riverbed from about 8 m: each border mixes over a few metres",
                ZoneBorders, 17),
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

        // The lab's caves (116): a great cave under the whole lab as the site's are made (TerrainGround.TryGreatCave), its roof
        // about 15 m down (below the bays and crane scenes), with all four crystal trophies (DiscoveryField.SpawnLabCaves),
        // and a shaft from the surface to GreatShaftLeft above its roof over the chamber nearest GreatShaftNear; then four
        // mini caves a couple of metres down, one of each cave crystal, each under a patch of its geode stone its shape
        // (MarkCave). (User, 2026-10-07: "easy to find, either pointers or anything", "big walkable caverns".)
        private const float GreatFloorDepth = 22f, GreatShaftLeft = 1f, GreatShaftRadius = .85f;
        private static readonly Vector2 GreatShaftNear = new Vector2(-12f, 0f);
        private static readonly Vector2[] MiniCaveSpots = { new Vector2(-12.6f, -4.6f), new Vector2(-9.6f, 6.8f), new Vector2(-4.2f, 7.6f), new Vector2(5.6f, -8.4f) };
        private const float MiniCentreY = -3.9f, MiniFloorY = -4.8f, CaveMarkDepth = .375f;
        public static readonly TerrainGround.Cavern[] Caves = MakeCaves(out GreatShaft);
        // The shaft down to the great cave: its axis's surface point and bottom (world).
        public static readonly (Vector3 top, Vector3 bottom) GreatShaft;

        private static TerrainGround.Cavern[] MakeCaves(out (Vector3 top, Vector3 bottom) shaft)
        {
            var caves = new List<TerrainGround.Cavern>();
            var extent = SiteLayout.Extent;
            if (!TerrainGround.TryGreatCave(extent, extent.y - GreatFloorDepth, 61, (min, max) => false, null, out var great))
                throw new InvalidOperationException("The Ground Lab's great cave did not fit.");
            caves.Add(great);
            var near = Local(new Vector3(GreatShaftNear.x, 0, GreatShaftNear.y));
            int chamber = 0;
            for (int c = 1; c < great.Centres.Length; c++)
                if (Vector2.Distance(new Vector2(great.Centres[c].x, great.Centres[c].z), new Vector2(near.x, near.z))
                    < Vector2.Distance(new Vector2(great.Centres[chamber].x, great.Centres[chamber].z), new Vector2(near.x, near.z))) chamber = c;
            var centre = great.Centres[chamber];
            var (_, roof) = TerrainGround.CavernSpan(great, centre.x, centre.z);
            var bottom = new Vector3(centre.x, roof + GreatShaftLeft, centre.z) + SiteLayout.Origin;
            shaft = (new Vector3(bottom.x, 0, bottom.z), bottom);
            for (int i = 0; i < MiniCaveSpots.Length; i++)
                caves.Add(TerrainGround.MakeCavern(new[] { Local3(MiniCaveSpots[i].x, MiniCentreY, MiniCaveSpots[i].y) },
                    new[] { new Unity.Mathematics.float3(1.75f, 1.45f, 1.6f) }, Local3(0, MiniFloorY, 0).y, .6f,
                    new Unity.Mathematics.float3(41 + i * 17, 7 + i * 5, 113 - i * 11)));
            return caves.ToArray();
        }

        // The top of the ground over a mini cave's chamber in its stone (geode shell): a patch its shape to dig down through.
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

        public static Vector2 BayCentre(int bay) => SlotCentre(Bays[bay].Slot);
        public static Vector2 SlotCentre(int slot) => new Vector2(Columns[slot % Columns.Length], Rows[slot / Columns.Length]);

        // Which bay holds a world x/z (-1 between bays and on unused slots), with the position inside it.
        private static int BayAt(float x, float z, out float u, out float v)
        {
            for (int bay = 0; bay < Bays.Length; bay++)
            {
                var centre = BayCentre(bay);
                u = x - centre.x; v = z - centre.y;
                if (Mathf.Abs(u) <= BayHalf && Mathf.Abs(v) <= BayHalf) return bay;
            }
            u = v = 0;
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
            foreach (var cave in Caves)
            {
                TerrainGround.FillCavern(ids, size, cellSize, cave);
                if (!cave.Great) MarkCave(ids, size, cellSize, cave);
            }
            foreach (var stash in Stashes(stashPocket)) TerrainGround.FillShell(ids, size, cellSize, stash, new Unity.Mathematics.float4(17, 31, 47, 59));
            return TerrainMaterialSnapshot.CopyFrom(ids);
        }

        // The lab's air (grid-local metres): the crane scenes' pockets, pits, shafts and tunnels, and the great cave's shaft.
        public static List<ExcavationGrid.LabCarve> Cavities()
        {
            var carves = new List<ExcavationGrid.LabCarve>();
            AddCraneCarves(carves);
            carves.Add(ExcavationGrid.LabCarve.Tube(Local(GreatShaft.top + Vector3.up), Local(GreatShaft.bottom), GreatShaftRadius));
            return carves;
        }

        // The aim prompt names the bay or crane scene under the crosshair and the ground actually hit
        // (3D text would draw through the world with the built-in font shader).
        // The last description, kept while the aim stays in the same half metre on the same ground: the prompt is asked for
        // every frame.
        private static (Vector3Int cell, TerrainMaterialId hit, string text) described;

        public static string Describe(Vector3 world, TerrainMaterialId hit)
        {
            var cell = Vector3Int.FloorToInt(world * 2);
            if (described.text != null && described.cell == cell && described.hit == hit) return described.text;
            described = (cell, hit, Describing(world, hit));
            return described.text;
        }

        private static string Describing(Vector3 world, TerrainMaterialId hit)
        {
            var crane = DescribeCrane(world);
            if (crane != null) return crane;
            int bay = BayAt(world.x, world.z, out _, out _);
            string ground = "hitting " + EquipmentProgression.GroundName(hit).ToLowerInvariant();
            var local = Local(world);
            if (Vector2.Distance(new Vector2(world.x, world.z), new Vector2(GreatShaft.top.x, GreatShaft.top.z)) < GreatShaftRadius + 1.2f)
                return $"Great cave: drop down the shaft ({-GreatShaft.bottom.y:0} m), dig through the last metre of rock  |  " + ground;
            foreach (var cave in Caves)
                if (!cave.Great && Over(cave, local.x, local.z))
                    return "Mini cave: a few crystals of one kind; dig down through the stone patch, about 1.5 m  |  " + ground;
            return bay < 0 ? "Ground Lab  |  " + ground : $"{Bays[bay].Name}: {Bays[bay].Hint}  |  {ground}";
        }
    }
}
