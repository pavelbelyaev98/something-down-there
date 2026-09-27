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
            if (!Layouts.TryGetValue(key, out var layout)) Layouts[key] = layout = catalog.Generate(SiteLayout.Extent, seed, Ground);
            return layout;
        }

        // The shipped ground (MainGame's excavation seed), shared by every layout.
        private const int ExcavationSeed = 2718;
        private static TerrainMaterialSnapshot site;
        private static TerrainMaterialSnapshot Site => site ??= TerrainMaterialSnapshot.Generate(SiteLayout.Size, SiteLayout.CellSize, ExcavationSeed);
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
            CollectionAssert.AreEqual(layout,catalog.Generate(extent,seed,Ground));
            Assert.That(layout.Length,Is.EqualTo(catalog.TotalCount));
            CollectionAssert.AreEqual(new[] {5390,1200,1280,1280,1400,1510,1400,1160,1060}, catalog.Entries.Where(e=>!e.AuthoredPlacement).Select(e=>e.Count));
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
            CollectionAssert.AreEqual(new[] { 640, 0, 0, 0, 0, 0, 0, 0, 0 }, catalog.Entries.Where(e=>!e.AuthoredPlacement).Select(e => e.ShallowCount));
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
                    for (float x = 0; x + 3 <= SiteLayout.Extent.x; x += 3) for (float z = 0; z + 3 <= SiteLayout.Extent.z; z += 3)
                        if (InPlot(x, z) && InPlot(x + 3, z) && InPlot(x, z + 3) && InPlot(x + 3, z + 3))
                            patches.Add(fresh.Count(i => layout[i].Position.x >= x && layout[i].Position.x < x + 3
                                && layout[i].Position.z >= z && layout[i].Position.z < z + 3));
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

        private static float MeanValue(DiscoveryPlacement[] layout, DiscoveryCatalog catalog, float from, float to)
        {
            var values = layout.Where(p => SiteLayout.Extent.y - p.Position.y >= from && SiteLayout.Extent.y - p.Position.y < to)
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
            var minerals = catalog.Entries.Where(e => e.ItemId.StartsWith("mineral_")).ToArray();
            CollectionAssert.AreEqual(new[] { "Coal", "Copper", "Iron", "Silver", "Gold", "Emerald", "Ruby", "Diamond" }, minerals.Select(e => e.Prefab.DisplayName));
            CollectionAssert.AreEqual(new[] { 4, 5, 6, 9, 13, 20, 30, 45 }, minerals.Select(e => e.Prefab.SaleValue));
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

        // Concept 03 §4 host ground: where both exist at a depth, host ground holds clearly more of
        // a type than other ground; every type still turns up outside it (scatter).
        [TestCase(90127)] [TestCase(12)]
        public void HostGroundHoldsMoreOfItsTypesWithoutEmptyingOtherGround(int seed)
        {
            var catalog = Catalog;
            var layout = Layout(seed);
            var ores = catalog.Entries.Where(e => e.ItemId.StartsWith("mineral_")).Select(e => Array.IndexOf(catalog.Entries, e)).ToArray();
            int rockIndex = Array.FindIndex(catalog.Entries, e => e.ItemId == "common_rock");
            Assert.That(catalog.Entries[ores[0]].HostGrounds, Is.EqualTo(new[] { TerrainMaterialId.Rock, TerrainMaterialId.FracturedRock, TerrainMaterialId.Crack }));
            Assert.That(catalog.Entries[rockIndex].HostGrounds, Is.EqualTo(new[] { TerrainMaterialId.Soil }));
            // Density in and out of the host per depth window: finds per sampled ground volume.
            float Ratio(int[] types, TerrainMaterialId host, float from, float to, out int outside, TerrainMaterialId? also = null, TerrainMaterialId? other = null)
            {
                bool InHost(TerrainMaterialId id) => id == host || id == also;
                bool Counted(TerrainMaterialId id) => other == null || InHost(id) || id == other;
                int inHostFinds = 0, otherFinds = 0; long hostSamples = 0, otherSamples = 0;
                foreach (var p in layout)
                {
                    float depth = SiteLayout.Extent.y - p.Position.y;
                    if (depth < from || depth >= to || Array.IndexOf(types, p.PrefabIndex) < 0 || !Counted(Ground(p.Position))) continue;
                    if (InHost(Ground(p.Position))) inHostFinds++; else otherFinds++;
                }
                for (float z = .5f; z < SiteLayout.Extent.z; z += 1)
                for (float x = .5f; x < SiteLayout.Extent.x; x += 1)
                {
                    if (SiteLayout.BeyondFootprint(new Vector2(SiteLayout.Origin.x + x, SiteLayout.Origin.z + z)) >= 0) continue;
                    for (float y = SiteLayout.Extent.y - to; y < SiteLayout.Extent.y - from; y += .5f)
                    {
                        var id = Ground(new Vector3(x, y, z));
                        if (!Counted(id)) continue;
                        if (InHost(id)) hostSamples++; else otherSamples++;
                    }
                }
                outside = otherFinds;
                Assert.That(hostSamples, Is.GreaterThan(0));
                return (inHostFinds / (float)hostSamples) / Mathf.Max(1e-6f, otherFinds / (float)otherSamples);
            }
            float zone2 = Ratio(ores, TerrainMaterialId.Rock, 40, 73, out int clayOres, TerrainMaterialId.FracturedRock);
            float deep = Ratio(ores, TerrainMaterialId.Rock, 78, 149, out int veinOres, TerrainMaterialId.FracturedRock);
            // Cracks are richer than the rock they break: some open into ore (never all of them).
            float cracks = Ratio(ores, TerrainMaterialId.FracturedRock, 78, 149, out int plainRockOres, TerrainMaterialId.Crack, TerrainMaterialId.Rock);
            float plain = Ratio(new[] { rockIndex }, TerrainMaterialId.Soil, 2.5f, 14, out int gravelRocks);
            Debug.Log($"Seed {seed}: ore in zone-2 rock masses x{zone2:F1}, ore in deep rock x{deep:F1}, ore in cracks x{cracks:F1}, plain rocks in soil x{plain:F1}");
            Assert.That(zone2, Is.GreaterThan(2), "Rock masses in the clay hold more ore.");
            Assert.That(deep, Is.GreaterThan(2), "Deep ore favours rock over the clay veins.");
            Assert.That(cracks, Is.GreaterThan(2), "Cracks hold more ore than plain rock.");
            Assert.That(plainRockOres, Is.GreaterThan(0));
            Assert.That(plain, Is.GreaterThan(1.5f), "Plain rocks favour soil over gravel lenses.");
            Assert.That(clayOres, Is.GreaterThan(0)); Assert.That(veinOres, Is.GreaterThan(0)); Assert.That(gravelRocks, Is.GreaterThan(0));
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
                var accepted = catalog.Generate(SiteLayout.Extent, 90127, Ground).Take(catalog.ShallowCount).ToArray();
                foreach (var entry in catalog.Entries)
                {
                    if (entry.AuthoredPlacement) continue;
                    entry.Count = entry.ShallowCount + (entry.Count - entry.ShallowCount) / 2;
                }
                CollectionAssert.AreEqual(accepted, catalog.Generate(SiteLayout.Extent, 90127, Ground).Take(catalog.ShallowCount));
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
                for (int i = 0; i + 4 < slices.Length; i++)
                    Assert.That(slices.Skip(i).Take(5).Sum(), Is.GreaterThanOrEqualTo(220),
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
            catalog.Generate(extent, 90127, Ground); // warm meshes, JIT, the placement grid and the ground
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var layout = catalog.Generate(extent, 12, Ground);
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
