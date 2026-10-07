using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class DiscoveryCatalogTests
    {
        private static DiscoveryCatalog Catalog => AssetDatabase.LoadAssetAtPath<DiscoveryCatalog>("Assets/Content/Discoveries/DiscoveryCatalog.asset");
        // Seeded layouts are pure functions of the catalog, so tests share them. Keyed by the
        // catalog's serialized content: an edited catalog never reuses an old layout.
        private static readonly System.Collections.Generic.Dictionary<(int, int), DiscoveryPlacement[]> Layouts
            = new System.Collections.Generic.Dictionary<(int, int), DiscoveryPlacement[]>();
        // Default runs check the seeds the fixed-seed tests already generate; the [Explicit]
        // 20-seed sweeps run when catalog or placement code changes (tools/test-changed.ps1)
        // and with -Full.
        private const string PopulationSweep = "20-seed population sweep: runs when catalog or placement code changes, and with -Full.";
        private static readonly int[] QuickSeeds = { 12, 991, 90127 };
        private static System.Collections.Generic.IEnumerable<int> Seeds(bool sweep) => sweep ? Enumerable.Range(0, 20) : QuickSeeds;

        private static DiscoveryPlacement[] Layout(int seed)
        {
            var catalog = Catalog;
            var key = (seed, JsonUtility.ToJson(catalog).GetHashCode());
            if (!Layouts.TryGetValue(key, out var layout)) Layouts[key] = layout = catalog.Generate(SiteLayout.Extent, seed, Ground, GroundLayout);
            return layout;
        }

        // The shipped ground (MainGame's excavation seed), shared by every layout.
        private const int ExcavationSeed = 2718;
        private static TerrainMaterialSnapshot site;
        private static TerrainMaterialSnapshot Site => site ??= TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, ExcavationSeed, Catalog.OddSpots(), SiteLayout.Ground);
        private static TerrainGround.GroundLayout groundLayout;
        private static TerrainGround.GroundLayout GroundLayout => groundLayout ??= TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, ExcavationSeed, Catalog.OddSpots(), SiteLayout.Ground);
        private static TerrainMaterialId Ground(Vector3 local)
        {
            var sample = Vector3Int.Min(Vector3Int.Max(Vector3Int.RoundToInt(local / SiteLayout.CellSize), Vector3Int.zero), SiteLayout.Size);
            return Site[sample.x + (SiteLayout.Size.x + 1) * (sample.y + (SiteLayout.Size.y + 1) * sample.z)];
        }

        [TestCase(90127)] [TestCase(12)] [TestCase(991)]
        public void TrialPopulationIsReproducibleWithExactQuotasAndBuriedClearance(int seed)
        {
            var catalog = Catalog; catalog.Validate();
            var extent = SiteLayout.Extent; var layout = Layout(seed);
            CollectionAssert.AreEqual(layout,catalog.Generate(extent,seed,Ground,GroundLayout));
            Assert.That(layout.Length,Is.EqualTo(catalog.TotalCount));
            CollectionAssert.AreEqual(new[] {5390,1200,1280,1280,1400,1510,1400,1160,1060,1,1,1,1,2,13,10,7,150,10,10,20,10,20,10,20,10}, catalog.Entries.Where(e=>!e.AuthoredPlacement).Select(e=>e.Count));
            for(int index=0;index<catalog.Entries.Length;index++)
            {
                var entry=catalog.Entries[index];
                Assert.That(layout.Count(p=>p.PrefabIndex==index),Is.EqualTo(entry.Count));
                Assert.That(layout.Take(catalog.ShallowCount).Count(p=>p.PrefabIndex==index),Is.EqualTo(entry.ShallowCount));
                Assert.That(entry.Prefab.DetectorEligible,Is.EqualTo(entry.Prefab.Kind == DiscoveryKind.Unique));
                Assert.That(entry.Prefab.SurfaceSampleCount,Is.EqualTo(256));
                Assert.That(entry.Prefab.RequiredExposure,Is.EqualTo(.6f));
                Assert.That(entry.Prefab.GetComponent<FindPhysics>(),Is.Not.Null);
                Assert.That(entry.Prefab.GetComponent<MeshCollider>().convex,Is.True);
                var mesh=entry.Prefab.GetComponent<MeshFilter>().sharedMesh;
                var hull = entry.Prefab.GetComponent<MeshCollider>().sharedMesh;
                Assert.That(hull,Is.Not.SameAs(mesh));
                Assert.That(hull.triangles.Length/3,Is.LessThanOrEqualTo(220));
                Assert.That((hull.bounds.size-mesh.bounds.size).magnitude,Is.LessThan(.0001f));
                Assert.That(mesh.bounds.center.magnitude,Is.LessThan(.0001f));
                // Meshes under 300 triangles (the simplest TVs) have nothing left to reduce and cost little at any distance.
                if (mesh.GetIndexCount(0) / 3 >= 300)
                    Assert.That(mesh.lodCount,Is.GreaterThan(1),"Finds carry Mesh LOD levels, so the view-distance setting reaches them.");
                var renderer=entry.Prefab.GetComponent<MeshRenderer>();
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"),Is.Not.Null);
                if (entry.Prefab.Kind == DiscoveryKind.Common) Assert.That(renderer.sharedMaterial.GetTexture("_BumpMap"),Is.Not.Null);
                bool buried = true;
                // One seed sweeps every rotated vertex; the others share the same placement code.
                var vertices = new System.Collections.Generic.Dictionary<int, Vector3[]>();
                foreach(var placement in seed == 90127 ? layout.Where(p=>p.PrefabIndex==index) : Enumerable.Empty<DiscoveryPlacement>())
                {
                    // The prefab's authored shrink moves real soil clearance with it.
                    var appearance = entry.Appearance(placement.AppearanceIndex);
                    var scale = appearance.transform.localScale;
                    if (!vertices.TryGetValue(placement.AppearanceIndex, out var points))
                        vertices[placement.AppearanceIndex] = points = appearance.GetComponent<MeshFilter>().sharedMesh.vertices;
                    foreach(var vertex in points)
                    {
                        var world=placement.Position+placement.Rotation*Vector3.Scale(vertex, scale);
                        buried &= world.x >= 0 && world.x <= extent.x && world.y >= 0 && world.y <= extent.y-.01f && world.z >= 0 && world.z <= extent.z;
                    }
                }
                Assert.That(buried, Is.True, entry.ItemId + ": every rotated mesh vertex must start inside soil.");
            }
            AssertSeparated(layout, catalog.Entries.Select(e => e.PlacementRadius).ToArray(), seed);
        }

        [TestCase(false)] [TestCase(true, Explicit = true, Reason = PopulationSweep)]
        public void ShallowEncountersCoverTheTopAndStartingRimAcrossSeeds(bool sweep)
        {
            var catalog = Catalog;
            var radii = catalog.Entries.Select(e => e.PlacementRadius).ToArray();
            Assert.That(catalog.ShallowCount, Is.EqualTo(640));
            CollectionAssert.AreEqual(new[] { 640, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, catalog.Entries.Where(e=>!e.AuthoredPlacement).Select(e => e.ShallowCount));
            foreach (int seed in Seeds(sweep))
            {
                var layout = Layout(seed);
                var top = layout.Take(catalog.ShallowCount).ToArray();
                // The source catalog owns the cover above the real mesh envelope.
                Assert.That(top.All(p => SiteLayout.Extent.y - p.Position.y >= radii[p.PrefabIndex] + catalog.Entries[p.PrefabIndex].ShallowMinCover - .0001f
                    && SiteLayout.Extent.y - p.Position.y <= radii[p.PrefabIndex] + catalog.Entries[p.PrefabIndex].ShallowMaxCover + .0001f), Is.True);
                Assert.That(top.Count(p => p.Position.z <= 6), Is.GreaterThanOrEqualTo(50));
                Assert.That(layout.Skip(catalog.ShallowCount).Count(), Is.EqualTo(catalog.TotalCount - catalog.ShallowCount));
                Assert.That(layout.Count(p => p.Position.y < 8.5f), Is.GreaterThanOrEqualTo(100));
                // Sample the whole dig plot, including its lateral/back areas. This is a spatial
                // bound on empty topsoil, not a claim about every player's encounter time.
                // The rug is bucketed so the sweep stays linear as the entry layer grows.
                var rug = new System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<Vector2>>();
                foreach (var p in top)
                {
                    var cell = (Mathf.FloorToInt(p.Position.x / 2f), Mathf.FloorToInt(p.Position.z / 2f));
                    if (!rug.TryGetValue(cell, out var list)) rug[cell] = list = new System.Collections.Generic.List<Vector2>();
                    list.Add(new Vector2(p.Position.x, p.Position.z));
                }
                var corner = new Vector2(SiteLayout.Origin.x, SiteLayout.Origin.z);
                // One assertion per seed for the emptiest sample; thousands of NUnit calls dominated the run.
                float worst = 0; Vector2 worstAt = default;
                for (float x = .8f; x <= SiteLayout.Extent.x - .8f; x += .5f)
                    for (float z = .8f; z <= SiteLayout.Extent.z - .8f; z += .5f)
                    {
                        if (SiteLayout.BeyondFootprint(new Vector2(x, z) + corner) > 0) continue;
                        int cx = Mathf.FloorToInt(x / 2f), cz = Mathf.FloorToInt(z / 2f);
                        float distance = float.MaxValue;
                        for (int ox = -1; ox <= 1; ox++)
                        for (int oz = -1; oz <= 1; oz++)
                            if (rug.TryGetValue((cx + ox, cz + oz), out var list))
                                foreach (var p in list) distance = Mathf.Min(distance, Vector2.Distance(new Vector2(x, z), p));
                        if (distance > worst) { worst = distance; worstAt = new Vector2(x, z); }
                    }
                Assert.That(worst, Is.LessThanOrEqualTo(1.5f), $"Seed {seed}, topsoil at {worstAt.x}, {worstAt.y}");
                AssertSeparated(layout, radii, seed);
            }

        }

        [TestCase(90127)] [TestCase(12)] [TestCase(991)]
        public void FirstTwentyCentimetresReachTheShallowRockCarpet(int seed)
        {
            var catalog = Catalog;
            var rock = catalog.Entries.Single(e => e.ItemId == "common_rock");
            var hulls = Enumerable.Range(0, rock.AppearanceCount).Select(i =>
                rock.Appearance(i).GetComponent<MeshCollider>().sharedMesh.vertices.Select(v =>
                    Vector3.Scale(v, rock.Appearance(i).transform.localScale)).ToArray()).ToArray();
            var top = Layout(seed).Take(catalog.ShallowCount);
            int reachable = 0;
            foreach (var placement in top)
            {
                float highest = hulls[placement.AppearanceIndex].Max(v => (placement.Rotation * v).y);
                float soilCover = SiteLayout.Extent.y - placement.Position.y - highest;
                Assert.That(soilCover, Is.GreaterThanOrEqualTo(.01f), "Nothing should poke through untouched turf.");
                if (soilCover <= .2f) reachable++;
            }
            Assert.That(reachable, Is.GreaterThanOrEqualTo(600),
                "The first shallow scrape must reach most of the denser rock layer without shrinking models.");
            Assert.That(rock.Prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.GreaterThan(.4f));
        }

        // The stash whose chest's hollow holds this point, or -1.
        private static int InChest(Vector3 point)
        {
            var hollow = Catalog.Chest.Hollow;
            var stashes = GroundLayout.Stashes;
            for (int s = 0; s < stashes.Length; s++)
                if (hollow.Contains(Quaternion.Inverse(stashes[s].Rotation) * (point - (Vector3)stashes[s].Centre))) return s;
            return -1;
        }

        // Concept 05 §3 finds inside finds (106, 113): every stash's chest holds ChestItems of its contents (ingots and
        // crystals, taken by hand), heaped inside its hollow no higher than its rim, tipped at most ChestTip; nothing else
        // sits in a chest or its pocket of air; the treasure lies nowhere else; counts are unchanged.
        [TestCase(90127)] [TestCase(12)]
        public void EveryStashChestHoldsItsContentsAndNothingElse(int seed)
        {
            var catalog = Catalog; var layout = Layout(seed);
            var stashes = GroundLayout.Stashes;
            Assert.That(catalog.Chest, Is.Not.Null);
            Assert.That(stashes.Length, Is.EqualTo(3));
            var contents = catalog.ChestContents.Select(c => Array.FindIndex(catalog.Entries, e => e.ItemId == c.ItemId)).ToArray();
            var hollow = catalog.Chest.Hollow;
            for (int s = 0; s < stashes.Length; s++)
            {
                var held = layout.Where(p => InChest(p.Position) == s).ToArray();
                Assert.That(held.Length, Is.EqualTo(catalog.ChestItems), $"Seed {seed}: chest {s} holds its items.");
                foreach (var p in held)
                {
                    Assert.That(contents, Does.Contain(p.PrefabIndex), "Only the chest's contents lie in it.");
                    Assert.That(catalog.Entries[p.PrefabIndex].Appearance(p.AppearanceIndex).HandPicked, Is.True, "Taken by hand.");
                    Assert.That(Vector3.Angle(p.Rotation * Vector3.up, Vector3.up), Is.LessThan(DiscoveryCatalog.ChestTip + .5f), "Tipped no further than a heap.");
                    var local = Quaternion.Inverse(stashes[s].Rotation) * (p.Position - (Vector3)stashes[s].Centre);
                    Assert.That(hollow.Contains(local), Is.True, "Inside the hollow.");
                    Assert.That(local.y, Is.LessThan(catalog.Chest.Rim), "Below the rim.");
                }
            }
            Assert.That(layout.Count(p => contents.Contains(p.PrefabIndex)), Is.EqualTo(stashes.Length * catalog.ChestItems),
                $"Seed {seed}: the treasure lies only in chests.");
            var radii = catalog.Entries.Select(e => e.PlacementRadius).ToArray();
            var pocket = catalog.Chest.Pocket;
            foreach (var p in layout.Where(p => !contents.Contains(p.PrefabIndex)))
                foreach (var stash in stashes)
                {
                    var local = Quaternion.Inverse(stash.Rotation) * (p.Position - (Vector3)stash.Centre) - pocket.center;
                    var d = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)) - pocket.extents;
                    float outside = Vector3.Max(d, Vector3.zero).magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0);
                    Assert.That(outside, Is.GreaterThan(radii[p.PrefabIndex]), "Nothing else reaches into a chest's pocket.");
                }
            Assert.That(layout.Length, Is.EqualTo(catalog.TotalCount), "Chests take their contents from the population.");
        }

        // Concept 03 §5 (110): every geode holds GeodeCrystals crystals of the geode types whose band covers its depth,
        // pointing into its hollow and sunk into its shell; geode crystals lie nowhere else; counts are unchanged.
        [TestCase(90127)] [TestCase(12)]
        public void EveryGeodeIsLinedWithItsZonesCrystalsAndNothingElse(int seed)
        {
            var catalog = Catalog; var layout = Layout(seed);
            var geodes = GroundLayout.Geodes;
            Assert.That(geodes.Length, Is.EqualTo(TerrainGround.GeodesPerZone.Sum()));
            var crystals = layout.Where(p => catalog.Entries[p.PrefabIndex].Geode).ToArray();
            Assert.That(crystals.Length, Is.EqualTo(geodes.Length * DiscoveryCatalog.GeodeCrystals), "Every geode crystal lines a geode.");
            foreach (var geode in geodes)
            {
                float depth = SiteLayout.Extent.y - geode.Centre.y;
                var held = crystals.Where(p => Vector3.Distance(p.Position, geode.Centre) < geode.Reach).ToArray();
                Assert.That(held.Length, Is.EqualTo(DiscoveryCatalog.GeodeCrystals), $"Seed {seed}: a geode at {depth:F0} m holds its crystals.");
                foreach (var p in held)
                {
                    var entry = catalog.Entries[p.PrefabIndex];
                    Assert.That(depth, Is.InRange(entry.MinDepth, entry.MaxDepth), $"{entry.ItemId} belongs at this depth.");
                    var inward = ((Vector3)geode.Centre - p.Position).normalized;
                    Assert.That(Vector3.Angle(p.Rotation * Vector3.up, inward), Is.LessThan(60), "Pointing into the hollow.");
                    var foot = p.Position - p.Rotation * Vector3.up * entry.RestingHalfHeight;
                    Assert.That(TerrainGround.HollowDistance(geode, foot), Is.GreaterThan(0), "Its foot is sunk in the shell.");
                    Assert.That(TerrainGround.HollowDistance(geode, p.Position), Is.LessThan(.05f), "It stands in the hollow.");
                }
            }
            foreach (var p in layout.Where(p => !catalog.Entries[p.PrefabIndex].Geode))
                foreach (var geode in geodes)
                    Assert.That(TerrainGround.OuterDistance(geode, p.Position), Is.GreaterThan(0), "Nothing else lies in a geode.");
            Assert.That(layout.Length, Is.EqualTo(catalog.TotalCount), "Geodes take their crystals from the population.");
        }

        // The site's pits (soil plus backfill only) still keep clear of every unique's space.
        [Test]
        public void SitePitsKeepClearOfTheUniques()
        {
            var layout = TerrainGround.Layout(SiteLayout.Size, SiteLayout.CellSize, ExcavationSeed, Catalog.OddSpots(), SiteLayout.Ground);
            foreach (var entry in Catalog.Entries.Where(e => e.AuthoredPlacement))
                foreach (var pit in layout.Pits)
                {
                    var nearest = Vector3.Max((Vector3)pit.Min, Vector3.Min(entry.AuthoredPosition, (Vector3)pit.Max));
                    Assert.That(Vector3.Distance(nearest, entry.AuthoredPosition), Is.GreaterThan(entry.PlacementRadius + 1), entry.ItemId);
                }
        }

        private static void AssertSeparated(DiscoveryPlacement[] layout, float[] radii, int seed)
        {
            // Bucket by the largest envelope any pair can require, so a 5,000 find carpet
            // stays linear instead of thirteen million pair checks per seed.
            float cell = radii.Max() * 2 + DiscoveryField.SoilClearance;
            int shallowCount = Catalog.ShallowCount;
            var buckets = new System.Collections.Generic.Dictionary<(int, int, int), System.Collections.Generic.List<int>>();
            for (int i = 0; i < layout.Length; i++)
            {
                var p = layout[i].Position;
                var key = (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
                if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new System.Collections.Generic.List<int>();
                list.Add(i);
            }
            for (int i = 0; i < layout.Length; i++)
            {
                var p = layout[i].Position;
                int cx = Mathf.FloorToInt(p.x / cell), cy = Mathf.FloorToInt(p.y / cell), cz = Mathf.FloorToInt(p.z / cell);
                for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                for (int oz = -1; oz <= 1; oz++)
                {
                    if (!buckets.TryGetValue((cx + ox, cy + oy, cz + oz), out var list)) continue;
                    foreach (int j in list)
                    {
                        if (j >= i) continue;
                        // A chest's contents lie side by side on its floor (106), inside the chest's own reservation.
                        if (InChest(p) >= 0 && InChest(p) == InChest(layout[j].Position)) continue;
                        float required = radii[layout[i].PrefabIndex] + radii[layout[j].PrefabIndex]
                            + (i < shallowCount ? DiscoveryField.SoilClearance : DiscoveryField.BandedSoilClearance) - .0001f;
                        if ((p - layout[j].Position).sqrMagnitude < required * required)
                            Assert.Fail($"Seed {seed}: placements {i} and {j} overlap their soil envelopes.");
                    }
                }
            }
        }

        [TestCase(false)] [TestCase(true, Explicit = true, Reason = PopulationSweep)]
        public void FreshDigFacesInTheFirstMetresApproachTheAcceptedShallowDensity(bool sweep)
        {
            var catalog = Catalog;
            var hulls = catalog.Entries.Select(e => Enumerable.Range(0, e.AppearanceCount).Select(i =>
                e.Appearance(i).GetComponent<MeshCollider>().sharedMesh.vertices.Select(v =>
                    Vector3.Scale(v, e.Appearance(i).transform.localScale)).ToArray()).ToArray()).ToArray();
            float[] floors = { 1, 1.5f, 2, 2.5f, 3, 4 };
            foreach (int seed in Seeds(sweep))
            {
                var layout = Layout(seed);
                var tops = new float[layout.Length]; var bottoms = new float[layout.Length];
                for (int i = 0; i < layout.Length; i++)
                {
                    var p = layout[i]; float min = float.MaxValue, max = float.MinValue;
                    foreach (var v in hulls[p.PrefabIndex][p.AppearanceIndex])
                    {
                        float y = (p.Rotation * v).y;
                        min = Mathf.Min(min, y); max = Mathf.Max(max, y);
                    }
                    tops[i] = SiteLayout.Extent.y - p.Position.y - max;
                    bottoms[i] = SiteLayout.Extent.y - p.Position.y - min;
                }
                int shallow = tops.Count(d => d <= .2f);
                foreach (float floor in floors)
                {
                    var fresh = Enumerable.Range(catalog.ShallowCount, layout.Length - catalog.ShallowCount)
                        .Where(i => tops[i] <= floor && bottoms[i] >= floor).ToArray();
                    Assert.That(fresh.Length, Is.GreaterThanOrEqualTo(shallow * .65f),
                        $"Seed {seed}, floor {floor}: original buried shapes, excluding every turf find.");
                    Assert.That(fresh.Count(i => catalog.Entries[layout[i].PrefabIndex].ItemId == "mineral_coal"),
                        Is.GreaterThanOrEqualTo(15), $"Seed {seed}: coal should enter around the first metre.");
                    // Blind, fixed-area patches inside the plot measure player-scale encounters.
                    // Fallen objects cannot enter this count: positions are the original generation.
                    var patches = new System.Collections.Generic.List<int>();
                    var corner = new Vector2(SiteLayout.Origin.x, SiteLayout.Origin.z);
                    bool InPlot(float x, float z) => SiteLayout.BeyondFootprint(new Vector2(x, z) + corner) <= 0;
                    // A stash's chest pocket crossing the floor is the encounter of every patch it reaches (106, 109): its
                    // reserve keeps rocks away there.
                    bool Inside(Vector3 p, float x, float z) => p.x >= x && p.x < x + 3 && p.z >= z && p.z < z + 3;
                    float reach = new Vector2(catalog.Chest.Pocket.extents.x, catalog.Chest.Pocket.extents.z).magnitude
                        + new Vector2(catalog.Chest.Pocket.center.x, catalog.Chest.Pocket.center.z).magnitude;
                    bool Reaches(Vector3 c, float x, float z) => new Vector2(Mathf.Clamp(c.x, x, x + 3) - c.x, Mathf.Clamp(c.z, z, z + 3) - c.z).magnitude < reach;
                    var chests = GroundLayout.Stashes.Where(s => Mathf.Abs(SiteLayout.Extent.y - s.Centre.y - floor) < catalog.Chest.Radius)
                        .Select(s => (Vector3)s.Centre).ToArray();
                    for (float x = 0; x + 3 <= SiteLayout.Extent.x; x += 3) for (float z = 0; z + 3 <= SiteLayout.Extent.z; z += 3)
                        if (InPlot(x, z) && InPlot(x + 3, z) && InPlot(x, z + 3) && InPlot(x + 3, z + 3))
                            patches.Add(fresh.Count(i => Inside(layout[i].Position, x, z)) + chests.Count(c => Reaches(c, x, z)));
                    Assert.That(patches.Count, Is.GreaterThanOrEqualTo(40), "The plot holds enough whole patches to judge.");
                    // Random patches may vary; require no empty patch and keep even the
                    // sparse decile useful, rather than treating one low sample as the mean.
                    // The lobed plot packs its finds a little tighter than the old square grid,
                    // so a few seeds' sparse decile holds five rather than six.
                    patches.Sort();
                    Assert.That(patches[0], Is.GreaterThanOrEqualTo(1), $"Seed {seed}, floor {floor}: empty local dig patch.");
                    Assert.That(patches[patches.Count / 10], Is.GreaterThanOrEqualTo(5),
                        $"Seed {seed}, floor {floor}: too many sparse local patches.");
                    Assert.That(patches.Average(), Is.GreaterThanOrEqualTo(8), $"Seed {seed}, floor {floor}: encounters are too sparse.");
                }
            }
        }

        [Test]
        [Explicit(PopulationSweep)]
        public void DepthMixSlidesFromJunkToValueAndKeepsScatteredOutliers()
        {
            var catalog = Catalog;
            var cheap = new System.Collections.Generic.HashSet<string> { "common_rock", "mineral_coal" };
            var rich = new System.Collections.Generic.HashSet<string> { "mineral_gold", "mineral_emerald", "mineral_ruby", "mineral_diamond" };
            int outlierSeeds = 0, deepCheapTotal = 0, highRichTotal = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = Layout(seed);
                Assert.That(DepthShare(layout, catalog, 2, 6, cheap), Is.GreaterThanOrEqualTo(.45f), $"Seed {seed}: the top layers must stay junk-heavy.");
                Assert.That(DepthShare(layout, catalog, 20, 31, cheap), Is.LessThanOrEqualTo(.05f), $"Seed {seed}: junk must not dominate deep ground.");
                Assert.That(DepthShare(layout, catalog, 2, 8, rich), Is.LessThanOrEqualTo(.15f), $"Seed {seed}: rich finds must stay rare near the surface.");
                Assert.That(DepthShare(layout, catalog, 75, 149, rich), Is.GreaterThanOrEqualTo(.8f), $"Seed {seed}: deep ground must be worth digging.");
                // Value rises zone by zone (roughly even quarters of the site): the mix, never the price.
                float previous = 0;
                for (int zone = 0; zone < 4; zone++)
                {
                    float value = MeanValue(layout, catalog, Mathf.Max(6, zone * SiteLayout.Extent.y / 4), (zone + 1) * SiteLayout.Extent.y / 4);
                    Assert.That(value, Is.GreaterThan(previous * 1.4f), $"Seed {seed}: zone {zone + 1} must be clearly richer than the one above.");
                    previous = value;
                }
                // Scatter goes both ways: the odd lump of junk deep, the odd valuable high.
                int deepCheap = 0, highRich = 0;
                foreach (var placement in layout)
                {
                    string id = catalog.Entries[placement.PrefabIndex].ItemId;
                    if (SiteLayout.Extent.y - placement.Position.y >= 18 && cheap.Contains(id)) deepCheap++;
                    if (SiteLayout.Extent.y - placement.Position.y < 8 && rich.Contains(id)) highRich++;
                }
                // Outliers are counted per seed but asserted across the set: the entry carpet
                // reshuffles the placement stream, so one seed may carry none of a scarce
                // outlier while the set as a whole still mixes both directions.
                Assert.That(deepCheap, Is.GreaterThanOrEqualTo(1), $"Seed {seed}: deep ground needs the odd junk outlier.");
                deepCheapTotal += deepCheap; highRichTotal += highRich;
                if (deepCheap >= 2 && highRich >= 2) outlierSeeds++;
            }
            Assert.That(deepCheapTotal, Is.GreaterThanOrEqualTo(40), "Every seed set needs junk well below its band.");
            Assert.That(highRichTotal, Is.GreaterThanOrEqualTo(20), "Every seed set needs valuables high up.");
            Assert.That(outlierSeeds, Is.GreaterThanOrEqualTo(4), "The set carries outliers in both directions.");
        }

        // The ground's own mix: what chests, geodes and caverns hold is their reward, seated in them, not the ground's
        // (a zone-1 chest's ingots would otherwise outweigh the whole recent fill).
        private static float MeanValue(DiscoveryPlacement[] layout, DiscoveryCatalog catalog, float from, float to)
        {
            bool Feature(DiscoveryCatalog.Entry e) => e.Geode || e.CavernArea >= 0 || catalog.ChestContents.Any(c => c.ItemId == e.ItemId);
            var values = layout.Where(p => SiteLayout.Extent.y - p.Position.y >= from && SiteLayout.Extent.y - p.Position.y < to
                    && !Feature(catalog.Entries[p.PrefabIndex]))
                .Select(p => catalog.Entries[p.PrefabIndex].Prefab.SaleValue).ToArray();
            return values.Length == 0 ? 0 : (float)values.Average();
        }

        private static float DepthShare(DiscoveryPlacement[] layout, DiscoveryCatalog catalog, float from, float to, System.Collections.Generic.HashSet<string> ids)
        {
            int total = 0, hits = 0;
            foreach (var placement in layout)
            {
                float depth = SiteLayout.Extent.y - placement.Position.y;
                if (depth < from || depth >= to) continue;
                total++;
                if (ids.Contains(catalog.Entries[placement.PrefabIndex].ItemId)) hits++;
            }
            return total == 0 ? 0f : hits / (float)total;
        }

        [TestCase(false)] [TestCase(true, Explicit = true, Reason = PopulationSweep)]
        public void MineralBandsHaveIncreasingValuesAndLateralCoverageAcrossSeeds(bool sweep)
        {
            var catalog = Catalog;
            // Pyrite (114) sits outside the ladder: fool's gold, priced like rock.
            var minerals = catalog.Entries.Where(e => e.ItemId.StartsWith("mineral_") && e.ItemId != "mineral_pyrite").ToArray();
            CollectionAssert.AreEqual(new[] { "Coal", "Copper", "Iron", "Silver", "Gold", "Emerald", "Ruby", "Diamond" }, minerals.Select(e => e.Prefab.DisplayName));
            CollectionAssert.AreEqual(new[] { 4, 5, 8, 12, 16, 30, 45, 70 }, minerals.Select(e => e.Prefab.SaleValue));
            foreach (int seed in Seeds(sweep))
            {
                var layout = Layout(seed);
                foreach (var entry in minerals)
                {
                    int index = Array.IndexOf(catalog.Entries, entry);
                    var placements = layout.Where(p => p.PrefabIndex == index).ToArray();
                    var banded = placements;
                    Assert.That(banded.All(p => SiteLayout.Extent.y - p.Position.y >= entry.MinDepth - .0001f && SiteLayout.Extent.y - p.Position.y <= entry.MaxDepth + .0001f), Is.True, entry.ItemId);
                    // Most of a type still sits in its core: the wide band only scatters outliers.
                    int core = banded.Count(p => SiteLayout.Extent.y - p.Position.y >= entry.CoreMinDepth - .0001f && SiteLayout.Extent.y - p.Position.y <= entry.CoreMaxDepth + .0001f);
                    Assert.That(core, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(banded.Length * entry.CoreShare * .9f)), $"{seed}: {entry.ItemId} core share");
                    for (int quadrant = 0; quadrant < 4; quadrant++)
                        Assert.That(placements.Count(p => (p.Position.x < SiteLayout.Extent.x * .5f ? 0 : 1)
                            + (p.Position.z < SiteLayout.Extent.z * .5f ? 0 : 2) == quadrant),
                            Is.GreaterThanOrEqualTo(entry.Count / 10), $"Seed {seed}, {entry.ItemId}, quadrant {quadrant}");
                }
            }
        }

        // No ordinary find crowds a unique: each keeps out of the unique's space.
        [Test]
        public void NoOrdinaryFindCrowdsAUnique()
        {
            var catalog = Catalog;
            var layout = Layout(90127);
            var radii = catalog.Entries.Select(e => e.PlacementRadius).ToArray();
            var uniques = catalog.Entries.Where(e => e.AuthoredPlacement).ToArray();
            Assert.That(uniques, Is.Not.Empty);
            foreach (var entry in uniques)
            {
                var centre = entry.AuthoredPosition;
                int index = Array.IndexOf(catalog.Entries, entry);
                foreach (var p in layout)
                    if (p.PrefabIndex != index)
                        Assert.That(Vector3.Distance(p.Position, centre) - radii[p.PrefabIndex], Is.GreaterThan(entry.PlacementRadius + TerrainGround.OddSpotReach * .9f),
                            $"{entry.ItemId}: an ordinary find sits in its space.");
            }
        }

        [Test]
        public void ImpossibleShallowDensityFailsWithinABoundedSearch()
        {
            Assert.Throws<InvalidOperationException>(() => DiscoveryField.Generate(new Vector3(8, 4, 8), 256, 12, 256));
            Assert.Throws<ArgumentOutOfRangeException>(() => DiscoveryField.Generate(new Vector3(24, 12, 24), 192, 12, 193));
        }

        [Test]
        public void DeeperPopulationDoesNotChangeTheAcceptedShallowLayout()
        {
            var catalog = UnityEngine.Object.Instantiate(Catalog);
            try
            {
                var accepted = catalog.Generate(SiteLayout.Extent, 90127, Ground, GroundLayout).Take(catalog.ShallowCount).ToArray();
                foreach (var entry in catalog.Entries)
                {
                    if (entry.AuthoredPlacement) continue;
                    entry.Count = entry.ShallowCount + (entry.Count - entry.ShallowCount) / 2;
                }
                CollectionAssert.AreEqual(accepted, catalog.Generate(SiteLayout.Extent, 90127, Ground, GroundLayout).Take(catalog.ShallowCount));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        // Constant rate, rising value: below the dense entry layer every metre to the floor
        // keeps meeting finds, so the extra depth is never empty ground.
        [TestCase(false)] [TestCase(true, Explicit = true, Reason = PopulationSweep)]
        public void EveryMetreHasFindsDownToTheFloorAcrossSeeds(bool sweep)
        {
            const int top = 6;
            int bottom = Mathf.FloorToInt(SiteLayout.Extent.y - .8f);
            foreach (int seed in Seeds(sweep))
            {
                var slices = new int[bottom - top];
                foreach (var placement in Layout(seed))
                {
                    int index = Mathf.FloorToInt(SiteLayout.Extent.y - placement.Position.y) - top;
                    if (index >= 0 && index < slices.Length) slices[index]++;
                }
                Assert.That(slices.Min(), Is.GreaterThanOrEqualTo(30), $"Seed {seed}: sparse metre at {top + Array.IndexOf(slices, slices.Min())} m");
                // Each zone's cavern (115) lies under a corner of the plot, and finds keep out of it, so its depths hold a
                // little less (with its walls' minerals as encounters of their own).
                for (int i = 0; i + 4 < slices.Length; i++)
                    Assert.That(slices.Skip(i).Take(5).Sum(), Is.GreaterThanOrEqualTo(210),
                        $"Seed {seed}: dry stretch below {top + i} m");
                // Roughly constant: the deepest quarter holds nearly as many finds per metre as the mid-depths.
                float mid = (float)slices.Skip(10).Take(40).Average(), deep = (float)slices.Skip(slices.Length - 37).Average();
                Assert.That(deep, Is.GreaterThanOrEqualTo(mid * .7f), $"Seed {seed}: the bottom thins out.");
            }
        }

        [Test]
        public void FullPopulationGeneratesInsideTheWarmTimeBudget()
        {
            var catalog = Catalog;
            var extent = SiteLayout.Extent;
            catalog.Generate(extent, 90127, Ground, GroundLayout); // warm meshes, JIT, the placement grid and the ground
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var layout = catalog.Generate(extent, 12, Ground, GroundLayout);
            watch.Stop();
            Debug.Log($"Full population placement: {watch.Elapsed.TotalMilliseconds:F0} ms for {layout.Length} finds.");
            Assert.That(layout.Length, Is.EqualTo(catalog.TotalCount));
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(1.0), "Placement must stay clear of the old all-pairs scan.");
        }

        [Test]
        public void RockOwnsTheShallowLayerWithThreeSavedAppearancesAndFullTiltOrientations()
        {
            var catalog = Catalog;
            var rock = catalog.Entries.Single(e => e.ItemId == "common_rock");
            // Rocks are the shallow layer: every saved appearance must resolve and restore.
            Assert.That(rock.Count, Is.EqualTo(5390));
            Assert.That(rock.ShallowCount, Is.EqualTo(640));
            Assert.That(rock.AppearanceCount, Is.EqualTo(3));
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < rock.AppearanceCount; i++)
            {
                var prefab = rock.Appearance(i);
                seen.Add(prefab.SaveContentId);
                Assert.That(prefab.DisplayName, Is.EqualTo("Rock"));
                Assert.That(prefab.SaleValue, Is.EqualTo(2));
                Assert.That(prefab.RequiredExposure, Is.EqualTo(.6f));
                Assert.That(prefab.DetectorEligible, Is.False);
                Assert.That(catalog.Resolve(prefab.SaveContentId), Is.SameAs(prefab));

            }
            Assert.That(seen.Count, Is.EqualTo(3));
            // Shipped types seed full tilt, not just yaw, across the rock population.
            bool tipped = false, inverted = false;
            foreach (int seed in new[] { 90127, 12, 991 })
                foreach (var placement in Layout(seed))
                {
                    if (catalog.Entries[placement.PrefabIndex].ItemId != "common_rock") continue;
                    float up = Vector3.Dot(placement.Rotation * Vector3.up, Vector3.up);
                    tipped |= Mathf.Abs(up) < .4f; inverted |= up < -.5f;
                }
            Assert.That(tipped && inverted, Is.True, "Rotations must include full tilt, not just yaw.");
        }

        [TestCase("common_can_intact")]
        [TestCase("common_bottle_tall")]
        [TestCase("missing-content")]
        public void MissingContentHasNoSubstitute(string id)
        {
            Assert.Throws<InvalidDataException>(() => Catalog.Resolve(id));
        }

        [Test]
        public void StarterAllocationFundsTheShovelPhaseAndSiteFundsAllCurrentTracks()
        {
            var catalog = Catalog;
            var rock = catalog.Entries.Single(e => e.ItemId == "common_rock");
            int totalCost = Enumerable.Range(1, EquipmentProgression.LevelCount - 1).Sum(EquipmentProgression.Price);
            int starterValue = catalog.Entries.Sum(e => e.ShallowCount * e.Prefab.SaleValue);
            int shovelPhaseCost = Enumerable.Range(1, EquipmentProgression.DrillLevel - 2).Sum(EquipmentProgression.Price);
            Assert.That(starterValue, Is.GreaterThan(shovelPhaseCost));
            Assert.That(starterValue, Is.LessThan(totalCost), "Finishing the tool track should require leaving the starter allocation.");
            Assert.That(catalog.Entries.Sum(e => e.Count * e.Prefab.SaleValue), Is.GreaterThan(totalCost * 3),
                "The finite site must fund all current tracks with money left for refills.");
            Assert.That(5 * rock.Prefab.SaleValue, Is.EqualTo(EquipmentProgression.Price(1)));
        }

    }
}
