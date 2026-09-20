using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class StarterFindCatalogTests
    {
        private static DiscoveryCatalog Catalog => AssetDatabase.LoadAssetAtPath<DiscoveryCatalog>("Assets/Content/StarterFinds/StarterFindCatalog.asset");

        [TestCase(90127)] [TestCase(12)] [TestCase(991)]
        public void TrialPopulationIsReproducibleWithExactQuotasAndBuriedClearance(int seed)
        {
            var catalog = Catalog; catalog.Validate();
            var extent = SiteLayout.Extent; var layout = catalog.Generate(extent,seed);
            CollectionAssert.AreEqual(layout,catalog.Generate(extent,seed));
            Assert.That(layout.Length,Is.EqualTo(12484));
            CollectionAssert.AreEqual(new[] {5500,1200,458,436,438,1038,1270,1142,1002}, catalog.Entries.Select(e=>e.Count));
            for(int index=0;index<catalog.Entries.Length;index++)
            {
                var entry=catalog.Entries[index];
                Assert.That(layout.Count(p=>p.PrefabIndex==index),Is.EqualTo(entry.Count));
                Assert.That(layout.Take(catalog.ShallowCount).Count(p=>p.PrefabIndex==index),Is.EqualTo(entry.ShallowCount));
                Assert.That(entry.Prefab.DetectorEligible,Is.False);
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
                var renderer=entry.Prefab.GetComponent<MeshRenderer>();
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"),Is.Not.Null);
                Assert.That(renderer.sharedMaterial.GetTexture("_BumpMap"),Is.Not.Null);
                bool buried = true;
                foreach(var placement in layout.Where(p=>p.PrefabIndex==index))
                {
                    // The prefab's authored shrink moves real soil clearance with it.
                    var appearance = entry.Appearance(placement.AppearanceIndex);
                    var scale = appearance.transform.localScale;
                    foreach(var vertex in appearance.GetComponent<MeshFilter>().sharedMesh.vertices)
                    {
                        var world=placement.Position+placement.Rotation*Vector3.Scale(vertex, scale);
                        buried &= world.x >= 0 && world.x <= extent.x && world.y >= 0 && world.y <= extent.y-.01f && world.z >= 0 && world.z <= extent.z;
                    }
                }
                Assert.That(buried, Is.True, entry.ItemId + ": every rotated mesh vertex must start inside soil.");
            }
            AssertSeparated(layout, catalog.Entries.Select(e => e.PlacementRadius).ToArray(), seed);
        }

        [Test]
        public void ShallowEncountersCoverTheTopAndStartingRimAcrossSeeds()
        {
            var catalog = Catalog;
            var radii = catalog.Entries.Select(e => e.PlacementRadius).ToArray();
            Assert.That(catalog.ShallowCount, Is.EqualTo(750));
            CollectionAssert.AreEqual(new[] { 750, 0, 0, 0, 0, 0, 0, 0, 0 }, catalog.Entries.Select(e => e.ShallowCount));
            for (int seed = 0; seed < 100; seed++)
            {
                var layout = catalog.Generate(SiteLayout.Extent, seed);
                var top = layout.Take(catalog.ShallowCount).ToArray();
                // The source catalog owns the cover above the real mesh envelope.
                Assert.That(top.All(p => SiteLayout.Extent.y - p.Position.y >= radii[p.PrefabIndex] + catalog.Entries[p.PrefabIndex].ShallowMinCover - .0001f
                    && SiteLayout.Extent.y - p.Position.y <= radii[p.PrefabIndex] + catalog.Entries[p.PrefabIndex].ShallowMaxCover + .0001f), Is.True);
                Assert.That(top.Count(p => p.Position.z <= 6), Is.GreaterThanOrEqualTo(50));
                Assert.That(layout.Skip(catalog.ShallowCount).Count(), Is.EqualTo(11734));
                Assert.That(layout.Count(p => p.Position.y < 8.5f), Is.GreaterThanOrEqualTo(100));
                // Sample walkable excavation locations, including lateral/back areas. This is a
                // spatial bound on empty topsoil, not a claim about every player's encounter time.
                // The rug is bucketed so the sweep stays linear as the entry layer grows.
                var rug = new System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<Vector2>>();
                foreach (var p in top)
                {
                    var cell = (Mathf.FloorToInt(p.Position.x / 2f), Mathf.FloorToInt(p.Position.z / 2f));
                    if (!rug.TryGetValue(cell, out var list)) rug[cell] = list = new System.Collections.Generic.List<Vector2>();
                    list.Add(new Vector2(p.Position.x, p.Position.z));
                }
                for (float x = .8f; x <= 23.2f; x += .5f)
                    for (float z = .8f; z <= 23.2f; z += .5f)
                    {
                        int cx = Mathf.FloorToInt(x / 2f), cz = Mathf.FloorToInt(z / 2f);
                        float distance = float.MaxValue;
                        for (int ox = -1; ox <= 1; ox++)
                        for (int oz = -1; oz <= 1; oz++)
                            if (rug.TryGetValue((cx + ox, cz + oz), out var list))
                                foreach (var p in list) distance = Mathf.Min(distance, Vector2.Distance(new Vector2(x, z), p));
                        Assert.That(distance, Is.LessThanOrEqualTo(1.5f), $"Seed {seed}, topsoil at {x}, {z}");
                    }
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
            var top = catalog.Generate(SiteLayout.Extent, seed).Take(catalog.ShallowCount);
            int reachable = 0;
            foreach (var placement in top)
            {
                float highest = hulls[placement.AppearanceIndex].Max(v => (placement.Rotation * v).y);
                float soilCover = SiteLayout.Extent.y - placement.Position.y - highest;
                Assert.That(soilCover, Is.GreaterThanOrEqualTo(.01f), "Nothing should poke through untouched turf.");
                if (soilCover <= .2f) reachable++;
            }
            Assert.That(reachable, Is.GreaterThanOrEqualTo(700),
                "The first shallow scrape must reach most of the denser rock layer without shrinking models.");
            Assert.That(rock.Prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.GreaterThan(.4f));
        }

        private static void AssertSeparated(DiscoveryPlacement[] layout, float[] radii, int seed)
        {
            // Bucket by the largest envelope any pair can require, so a 5,000 find carpet
            // stays linear instead of thirteen million pair checks per seed.
            const float cell = 1.2f;
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
                            + (i < 750 ? DiscoveryField.SoilClearance : DiscoveryField.BandedSoilClearance) - .0001f;
                        if ((p - layout[j].Position).sqrMagnitude < required * required)
                            Assert.Fail($"Seed {seed}: placements {i} and {j} overlap their soil envelopes.");
                    }
                }
            }
        }

        [Test]
        public void FreshDigFacesInTheFirstMetresApproachTheAcceptedShallowDensity()
        {
            var catalog = Catalog;
            var hulls = catalog.Entries.Select(e => Enumerable.Range(0, e.AppearanceCount).Select(i =>
                e.Appearance(i).GetComponent<MeshCollider>().sharedMesh.vertices.Select(v =>
                    Vector3.Scale(v, e.Appearance(i).transform.localScale)).ToArray()).ToArray()).ToArray();
            float[] floors = { 1, 1.5f, 2, 2.5f, 3, 4 };
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = catalog.Generate(SiteLayout.Extent, seed);
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
                    // Blind, fixed-area patches measure player-scale encounters. Fallen
                    // objects cannot enter this count: positions are the original generation.
                    var patches = new System.Collections.Generic.List<int>();
                    for (float x = 1; x <= 19; x += 3) for (float z = 1; z <= 19; z += 3)
                        patches.Add(fresh.Count(i => layout[i].Position.x >= x && layout[i].Position.x < x + 3
                            && layout[i].Position.z >= z && layout[i].Position.z < z + 3));
                    // Random patches may vary; require no empty patch and keep even the
                    // sparse decile useful, rather than treating one low sample as the mean.
                    patches.Sort();
                    Assert.That(patches[0], Is.GreaterThanOrEqualTo(1), $"Seed {seed}, floor {floor}: empty local dig patch.");
                    Assert.That(patches[patches.Count / 10], Is.GreaterThanOrEqualTo(6),
                        $"Seed {seed}, floor {floor}: too many sparse local patches.");
                    Assert.That(patches.Average(), Is.GreaterThanOrEqualTo(8), $"Seed {seed}, floor {floor}: encounters are too sparse.");
                }
            }
        }

        [Test]
        public void DepthMixSlidesFromJunkToValueAndKeepsScatteredOutliers()
        {
            var catalog = Catalog;
            var cheap = new System.Collections.Generic.HashSet<string> { "common_rock", "mineral_coal" };
            var rich = new System.Collections.Generic.HashSet<string> { "mineral_gold", "mineral_emerald", "mineral_ruby", "mineral_diamond" };
            int outlierSeeds = 0, deepCheapTotal = 0, highRichTotal = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = catalog.Generate(SiteLayout.Extent, seed);
                Assert.That(DepthShare(layout, catalog, 2, 6, cheap), Is.GreaterThanOrEqualTo(.45f), $"Seed {seed}: the top layers must stay junk-heavy.");
                Assert.That(DepthShare(layout, catalog, 20, 31, cheap), Is.LessThanOrEqualTo(.05f), $"Seed {seed}: junk must not dominate deep ground.");
                Assert.That(DepthShare(layout, catalog, 2, 8, rich), Is.LessThanOrEqualTo(.15f), $"Seed {seed}: rich finds must stay rare near the surface.");
                Assert.That(DepthShare(layout, catalog, 20, 31, rich), Is.GreaterThanOrEqualTo(.8f), $"Seed {seed}: deep ground must be worth digging.");
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

        [Test]
        public void MineralBandsHaveIncreasingValuesAndLateralCoverageAcrossSeeds()
        {
            var catalog = Catalog;
            var minerals = catalog.Entries.Where(e => e.ItemId.StartsWith("mineral_")).ToArray();
            CollectionAssert.AreEqual(new[] { "Coal", "Copper", "Iron", "Silver", "Gold", "Emerald", "Ruby", "Diamond" }, minerals.Select(e => e.Prefab.DisplayName));
            CollectionAssert.AreEqual(new[] { 4, 5, 6, 9, 13, 20, 30, 45 }, minerals.Select(e => e.Prefab.SaleValue));
            for (int seed = 0; seed < 20; seed++)
            {
                var layout = catalog.Generate(SiteLayout.Extent, seed);
                foreach (var entry in minerals)
                {
                    int index = Array.IndexOf(catalog.Entries, entry);
                    var placements = layout.Where(p => p.PrefabIndex == index).ToArray();
                    var deep = placements.Where(p => entry.DeepCount > 0 && SiteLayout.Extent.y - p.Position.y >= entry.DeepMinDepth).ToArray();
                    Assert.That(deep.Length, Is.EqualTo(entry.DeepCount), entry.ItemId + ": deep quota");
                    Assert.That(deep.All(p => SiteLayout.Extent.y - p.Position.y <= entry.DeepMaxDepth + .0001f), Is.True);
                    var banded = placements.Except(deep).ToArray();
                    Assert.That(banded.All(p => SiteLayout.Extent.y - p.Position.y >= entry.MinDepth - .0001f && SiteLayout.Extent.y - p.Position.y <= entry.MaxDepth + .0001f), Is.True, entry.ItemId);
                    // Most of a type still sits in its core: the wide band only scatters outliers.
                    int core = banded.Count(p => SiteLayout.Extent.y - p.Position.y >= entry.CoreMinDepth - .0001f && SiteLayout.Extent.y - p.Position.y <= entry.CoreMaxDepth + .0001f);
                    Assert.That(core, Is.GreaterThanOrEqualTo(Mathf.FloorToInt(banded.Length * entry.CoreShare * .9f)), $"{seed}: {entry.ItemId} core share");
                    for (int quadrant = 0; quadrant < 4; quadrant++)
                        Assert.That(placements.Count(p => (p.Position.x < 12 ? 0 : 1) + (p.Position.z < 12 ? 0 : 2) == quadrant),
                            Is.GreaterThanOrEqualTo(entry.Count / 10), $"Seed {seed}, {entry.ItemId}, quadrant {quadrant}");
                }
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
                var accepted = catalog.Generate(SiteLayout.Extent, 90127).Take(catalog.ShallowCount).ToArray();
                foreach (var entry in catalog.Entries)
                {
                    entry.Count = entry.ShallowCount + (entry.Count - entry.ShallowCount - entry.DeepCount) / 2;
                    entry.DeepCount = 0;
                }
                CollectionAssert.AreEqual(accepted, catalog.Generate(SiteLayout.Extent, 90127).Take(catalog.ShallowCount));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void LowerReservoirHasFindsInEveryMetreAcrossSeeds()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var slices = new int[67];
                foreach (var placement in Catalog.Generate(SiteLayout.Extent, seed))
                {
                    int index = Mathf.FloorToInt(SiteLayout.Extent.y - placement.Position.y) - 32;
                    if (index >= 0 && index < slices.Length) slices[index]++;
                }
                Assert.That(slices.Min(), Is.GreaterThanOrEqualTo(20), $"Seed {seed}: sparse lower reservoir");
                for (int i = 0; i + 4 < slices.Length; i++)
                    Assert.That(slices.Skip(i).Take(5).Sum(), Is.GreaterThanOrEqualTo(180),
                        $"Seed {seed}: dry stretch below layer {i}");
            }
        }

        [Test]
        public void DeepAllocationRejectsInvalidCountsAndBands()
        {
            var catalog = UnityEngine.Object.Instantiate(Catalog);
            try
            {
                var entry = catalog.Entries.Single(e => e.ItemId == "mineral_gold");
                int count = entry.DeepCount;
                float min = entry.DeepMinDepth, max = entry.DeepMaxDepth;
                foreach (int invalid in new[] { -1, entry.Count + 1 })
                {
                    entry.DeepCount = invalid;
                    Assert.Throws<InvalidDataException>(() => catalog.Validate());
                }
                entry.DeepCount = count;
                entry.DeepMinDepth = entry.MaxDepth - 1;
                Assert.Throws<InvalidDataException>(() => catalog.Validate());
                entry.DeepMinDepth = min;
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, min })
                {
                    entry.DeepMaxDepth = invalid;
                    Assert.Throws<InvalidDataException>(() => catalog.Validate());
                }
                entry.DeepMaxDepth = max;
                catalog.Validate();
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void FullPopulationGeneratesInsideTheWarmTimeBudget()
        {
            var catalog = Catalog;
            var extent = SiteLayout.Extent;
            catalog.Generate(extent, 90127); // warm meshes, JIT and the placement grid
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var layout = catalog.Generate(extent, 12);
            watch.Stop();
            Debug.Log($"Full population placement: {watch.Elapsed.TotalMilliseconds:F0} ms for {layout.Length} finds.");
            Assert.That(layout.Length, Is.EqualTo(12484));
            Assert.That(watch.Elapsed.TotalSeconds, Is.LessThan(1.0), "Placement must stay clear of the old all-pairs scan.");
        }

        [Test]
        public void PreviousThousandFindPopulationStillResolvesWithoutReroll()
        {
            var catalog = Catalog;
            var counts = new[] { 96, 240, 160, 128, 112, 96, 80, 64, 48 };
            int index = 0;
            for (int e = 0; e < catalog.Entries.Length; e++)
                for (int n = 0; n < counts[e]; n++)
                {
                    var state = new FindSnapshot { ContentId = catalog.Entries[e].Prefab.SaveContentId,
                        Item = new ItemSnapshot { Id = "legacy-" + index, Name = "Historical item", Value = 5 },
                        Position = new Vector3(1 + index * .001f, 12, 3), Rotation = Quaternion.Euler(0, index % 360, 0),
                        Scale = Vector3.one, Collected = index % 5 == 0 };
                    Assert.That(catalog.Resolve(state.ContentId, out bool legacy), Is.SameAs(catalog.Entries[e].Prefab));
                    Assert.That(legacy, Is.False);
                    var restored = catalog.PrepareRestore(state);
                    Assert.That(restored.ContentId, Is.EqualTo(state.ContentId));
                    Assert.That(restored.Position, Is.EqualTo(state.Position));
                    Assert.That(restored.Rotation, Is.EqualTo(state.Rotation));
                    Assert.That(restored.Item.Id, Is.EqualTo(state.Item.Id));
                    Assert.That(restored.Item.Value, Is.EqualTo(state.Item.Value));
                    Assert.That(restored.Collected, Is.EqualTo(state.Collected));
                    index++;
                }
            Assert.That(index, Is.EqualTo(1024));
            Assert.That(index, Is.LessThanOrEqualTo(DiscoveryField.MaximumPopulation));
        }

        [Test]
        public void RockOwnsTheShallowLayerWithThreeSavedAppearancesAndFullTiltOrientations()
        {
            var catalog = Catalog;
            var rock = catalog.Entries.Single(e => e.ItemId == "common_rock");
            // Rocks are the shallow layer: every saved appearance must resolve and restore.
            Assert.That(rock.Count, Is.EqualTo(5500));
            Assert.That(rock.ShallowCount, Is.EqualTo(750));
            Assert.That(rock.AppearanceCount, Is.EqualTo(3));
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < rock.AppearanceCount; i++)
            {
                var prefab = rock.Appearance(i);
                seen.Add(prefab.SaveContentId);
                Assert.That(prefab.DisplayName, Is.EqualTo("Rock"));
                Assert.That(prefab.SaleValue, Is.EqualTo(2));
                Assert.That(prefab.Size, Is.EqualTo(FindSize.Large));
                Assert.That(prefab.RequiredExposure, Is.EqualTo(.6f));
                Assert.That(prefab.DetectorEligible, Is.False);
                Assert.That(catalog.Resolve(prefab.SaveContentId, out bool legacy), Is.SameAs(prefab));
                Assert.That(legacy, Is.False);
                var state = new FindSnapshot { ContentId = prefab.SaveContentId, Item = new ItemSnapshot { Id = "saved-rock", Name = "Rock", Value = 7 },
                    Position = new Vector3(3, 20, 4), Rotation = Quaternion.Euler(11, 22, 33), Scale = Vector3.one, PhysicsReleased = true };
                var restored = catalog.PrepareRestore(state);
                Assert.That(restored.ContentId, Is.EqualTo(state.ContentId));
                Assert.That(restored.Rotation, Is.EqualTo(state.Rotation));
                Assert.That(restored.Item.Value, Is.EqualTo(7), "Historical value survives tuning.");
                Assert.That(restored.PhysicsReleased, Is.True);
            }
            Assert.That(seen.Count, Is.EqualTo(3));
            // Shipped types seed full tilt, not just yaw, across the rock population.
            bool tipped = false, inverted = false;
            foreach (int seed in new[] { 90127, 12, 991 })
                foreach (var placement in catalog.Generate(SiteLayout.Extent, seed))
                {
                    if (catalog.Entries[placement.PrefabIndex].ItemId != "common_rock") continue;
                    float up = Vector3.Dot(placement.Rotation * Vector3.up, Vector3.up);
                    tipped |= Mathf.Abs(up) < .4f; inverted |= up < -.5f;
                }
            Assert.That(tipped && inverted, Is.True, "Rotations must include full tilt, not just yaw.");
        }

        [Test]
        public void LegacyPopulationMigratesWithoutRerollOrChangesToCarriedRecordsAndMigrationIsIdempotent()
        {
            var catalog=Catalog;
            var layout=DiscoveryField.Generate(new Vector3(24,12,24),96,90127);
            for(int i=0;i<96;i++)
            {
                var alias=catalog.LegacyAliases[i%catalog.LegacyAliases.Length]; bool collected=i%5==0;
                var saved=new FindSnapshot { ContentId=alias.OldId,Item=new ItemSnapshot {Id="old-"+i,Name="Historical item",Value=5+(i%3)*3},
                    Position=layout[i].Position,Rotation=layout[i].Rotation,Scale=Vector3.one*.8f,Collected=collected };
                var migrated=catalog.PrepareRestore(saved); var repeated=catalog.PrepareRestore(migrated);
                Assert.That(migrated.ContentId,Is.EqualTo(alias.CurrentId)); Assert.That(migrated.Position,Is.EqualTo(saved.Position));
                Assert.That(migrated.Rotation,Is.EqualTo(saved.Rotation)); Assert.That(migrated.Scale,Is.EqualTo(Vector3.one));
                Assert.That(migrated.Item.Id,Is.EqualTo(saved.Item.Id)); Assert.That(migrated.Item.Value,Is.EqualTo(saved.Item.Value));
                Assert.That(migrated.Collected,Is.EqualTo(collected)); Assert.That(saved.ContentId,Is.EqualTo(alias.OldId));
                Assert.That(saved.Scale,Is.EqualTo(Vector3.one*.8f)); Assert.That(saved.Item.Name,Is.EqualTo("Historical item"));
                Assert.That(migrated.Item.Name,collected?Is.EqualTo(saved.Item.Name):Is.Not.EqualTo(saved.Item.Name));
                Assert.That(repeated.ContentId,Is.EqualTo(migrated.ContentId)); Assert.That(repeated.Item.Name,Is.EqualTo(migrated.Item.Name));
                Assert.That(repeated.Item.Value,Is.EqualTo(migrated.Item.Value)); Assert.That(repeated.Scale,Is.EqualTo(migrated.Scale));
            }
            Assert.Throws<InvalidDataException>(()=>catalog.Resolve("missing-content",out _));
        }

        [Test]
        public void ShallowRockAndCoalPopulationFundsTheExistingShovelTrackWithoutClearingTheWholeMap()
        {
            var catalog = Catalog;
            var rock = catalog.Entries.Single(e => e.ItemId == "common_rock");
            int totalCost = StationTrade.DefaultPrices().Sum();
            Assert.That(catalog.Entries.Sum(e => e.ShallowCount * e.Prefab.SaleValue), Is.GreaterThan(totalCost),
                "Shallow play should fund every existing shovel upgrade without mandatory deep-map clearance.");
            Assert.That(5 * rock.Prefab.SaleValue, Is.EqualTo(StationTrade.DefaultPrices()[0]));
        }

        [Test]
        public void RetiredEntriesRemainRestorableWithHistoricalIdentityPoseAndValue()
        {
            var catalog = UnityEngine.Object.Instantiate(Catalog);
            try
            {
                // Simulate retiring the junk types again: a zero-count entry must keep
                // resolving for existing saves without ever spawning in a new game.
                foreach (bool collected in new[] { false, true })
                    foreach (var entry in catalog.Entries.Where(e => e.ItemId.StartsWith("common_bottle_")))
                    {
                        entry.Count = 0; entry.ShallowCount = 0;
                        catalog.Validate();
                        var saved = new FindSnapshot { ContentId = entry.Prefab.SaveContentId,
                            Item = new ItemSnapshot { Id = "old-bottle", Name = "Glass Bottle", Value = 17 },
                            Position = new Vector3(2, -1, 3), Rotation = Quaternion.Euler(45, 90, 12),
                            Scale = Vector3.one, PhysicsReleased = true, Collected = collected };
                        var restored = catalog.PrepareRestore(saved);
                        Assert.That(catalog.Resolve(saved.ContentId, out bool legacy), Is.SameAs(entry.Prefab));
                        Assert.That(legacy, Is.False);
                        Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(saved)));
                    }
                // Historical ids replaced by different art keep resolving to the current type.
                var legacySaved = new FindSnapshot { ContentId = "common_can_intact",
                    Item = new ItemSnapshot { Id = "old-can", Name = "Food/Drink Can", Value = 1 },
                    Position = new Vector3(4, -2, 5), Rotation = Quaternion.Euler(10, 20, 30),
                    Scale = Vector3.one * .7f, PhysicsReleased = true };
                var prefab = catalog.Resolve(legacySaved.ContentId, out bool replaced);
                var legacyRestored = catalog.PrepareRestore(legacySaved);
                Assert.That(replaced, Is.True);
                Assert.That(prefab, Is.Not.Null);
                Assert.That(legacyRestored.ContentId, Is.EqualTo(prefab.SaveContentId));
                Assert.That(legacyRestored.Item.Id, Is.EqualTo("old-can"));
                Assert.That(legacyRestored.Item.Value, Is.EqualTo(1));
                Assert.That(legacyRestored.Position, Is.EqualTo(legacySaved.Position));
                Assert.That(legacyRestored.Rotation, Is.EqualTo(legacySaved.Rotation));
                catalog.Entries[0].Count = -1;
                Assert.Throws<InvalidDataException>(() => catalog.Validate());
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void CurrentItemIdentityAndSavedValueSurviveChangingItsPrefabArt()
        {
            var original=Catalog;var catalog=UnityEngine.Object.Instantiate(original);
            var replacement=UnityEngine.Object.Instantiate(original.Entries[0].Prefab.gameObject);
            try
            {
                replacement.GetComponent<MeshFilter>().sharedMesh=original.Entries[1].Prefab.GetComponent<MeshFilter>().sharedMesh;
                replacement.GetComponent<MeshCollider>().sharedMesh=original.Entries[1].Prefab.GetComponent<MeshCollider>().sharedMesh;
                catalog.Entries[0].Prefab=replacement.GetComponent<BuriedFind>();
                catalog.Validate();
                var saved=new FindSnapshot {ContentId=original.Entries[0].Prefab.SaveContentId,Item=new ItemSnapshot {Id="same-item",Name="Glass Bottle",Value=17},Scale=Vector3.one,Rotation=Quaternion.identity};
                var restored=catalog.PrepareRestore(saved);
                Assert.That(catalog.Resolve(saved.ContentId,out bool legacy),Is.SameAs(replacement.GetComponent<BuriedFind>()));
                Assert.That(legacy,Is.False);Assert.That(restored.Item.Id,Is.EqualTo("same-item"));Assert.That(restored.Item.Value,Is.EqualTo(17));
            }
            finally {UnityEngine.Object.DestroyImmediate(replacement);UnityEngine.Object.DestroyImmediate(catalog);}
        }
    }
}
