using System;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    // Concept 03 §4: undercut gravel pours, undercut backfill and thin soil slump, a crack breaks
    // along its band; each release is bounded and nothing else moves.
    public sealed class GroundReleaseTests
    {
        // 8 x 6 x 8 m of one ground with a second ground wherever `inside` says so.
        private static ExcavationGrid Fixture(TerrainMaterialId ground, TerrainMaterialId feature, Func<Vector3, bool> inside)
        {
            var grid = new ExcavationGrid(new Vector3Int(64, 48, 64), .125f);
            var saved = grid.Capture();
            var ids = new byte[saved.Materials.Length];
            int i = 0;
            for (int z = 0; z <= 64; z++)
            for (int y = 0; y <= 48; y++)
            for (int x = 0; x <= 64; x++)
                ids[i++] = (byte)(inside(new Vector3(x, y, z) * .125f) ? feature : ground);
            saved.Materials = TerrainMaterialSnapshot.CopyFrom(ids);
            grid.Restore(saved);
            return grid;
        }

        // Clay with a gravel slab 1 m thick, starting slabBottom above the floor.
        private static ExcavationGrid SlabFixture(float slabBottom = 2.5f) => Fixture(TerrainMaterialId.Clay, TerrainMaterialId.Gravel,
            p => p.y >= slabBottom && p.y <= slabBottom + 1 && p.x >= 1 && p.x <= 7 && p.z >= 1 && p.z <= 7);

        [Test]
        public void UndercutGravelPoursItsSectionWithinTheReachAndNothingElse()
        {
            var grid = SlabFixture();
            // A room whose ceiling breaks into the slab's underside.
            Assert.That(grid.RemoveSphere(new Vector3(4, 1.4f, 4), 1.2f, out var cut), Is.True);
            float carved = grid.RemovedVolume;
            Assert.That(grid.TryRelease(GroundRelease.GravelPour, cut, ExcavationGrid.PourReach, out var changed, out var centre), Is.True);
            Assert.That(grid.LastRemovedVolume, Is.GreaterThan(3), "The undercut section lets go in one rush.");
            Assert.That(grid.LastRemovedVolume, Is.LessThanOrEqualTo(ExcavationGrid.MaximumSamples(GroundRelease.GravelPour) * Mathf.Pow(.125f, 3) * 1.01f));
            Assert.That(grid.RemovedVolume, Is.GreaterThan(carved));
            Assert.That(Vector3.Distance(centre, new Vector3(4, 2.5f, 4)), Is.LessThan(1));
            Assert.That(grid.IsSolid(new Vector3(4, 3, 4)), Is.False, "The gravel above the room is gone.");
            Assert.That(grid.IsSolid(new Vector3(4, 3.8f, 4)), Is.True, "Clay above the slab stays.");
            Assert.That(grid.IsSolid(new Vector3(1.2f, 3, 1.2f)), Is.True, "Gravel beyond the reach stays: the pour is bounded.");
            Assert.That(grid.IsSolid(new Vector3(4, .1f, 4)), Is.True, "The room's floor is untouched.");
            Assert.That(changed.size.x, Is.GreaterThan(0));
            // A second attempt finds nothing loose left above the room.
            Assert.That(grid.TryRelease(GroundRelease.GravelPour, cut, ExcavationGrid.PourReach, out _, out _), Is.False);
        }

        [Test]
        public void DiggingOntoGravelOrGrazingItNeverPours()
        {
            var grid = SlabFixture();
            // From above: gravel keeps solid ground beneath it.
            Assert.That(grid.RemoveSphere(new Vector3(4, 3.8f, 4), .6f, out var onto), Is.True);
            Assert.That(grid.TryRelease(GroundRelease.GravelPour, onto, ExcavationGrid.PourReach, out _, out _), Is.False);
            // A tiny nick under the slab is not a ceiling.
            grid = SlabFixture();
            Assert.That(grid.RemoveSphere(new Vector3(4, 2.45f, 4), .14f, out var nick), Is.True);
            Assert.That(grid.TryRelease(GroundRelease.GravelPour, nick, ExcavationGrid.PourReach, out _, out _), Is.False);
            // Clay undercut without gravel above never pours, and clay never slumps.
            grid = SlabFixture(slabBottom: 5);
            Assert.That(grid.RemoveSphere(new Vector3(4, 1.4f, 4), 1.2f, out var clay), Is.True);
            foreach (GroundRelease kind in Enum.GetValues(typeof(GroundRelease)))
                Assert.That(grid.TryRelease(kind, clay, 3, out _, out _), Is.False, kind.ToString());
        }

        [Test]
        public void UndercutBackfillSlumpsWithinItsReach()
        {
            // A backfill column 1 m square from 1 m to 5.5 m in clay.
            var grid = Fixture(TerrainMaterialId.Clay, TerrainMaterialId.Backfill, p => p.y >= 1 && p.y <= 5.5f && Mathf.Abs(p.x - 4) <= .5f && Mathf.Abs(p.z - 4) <= .5f);
            Assert.That(grid.RemoveSphere(new Vector3(4, .6f, 4), .7f, out var cut), Is.True);
            Assert.That(grid.TryRelease(GroundRelease.BackfillSlump, cut, ExcavationGrid.BackfillSlumpReach, out _, out _), Is.True);
            Assert.That(grid.IsSolid(new Vector3(4, 2.2f, 4)), Is.False, "The pit's fill slumps down.");
            Assert.That(grid.IsSolid(new Vector3(4, 4.8f, 4)), Is.True, "Fill beyond the reach stays.");
            Assert.That(grid.IsSolid(new Vector3(4.9f, 2.2f, 4)), Is.True, "Clay around the pit stays.");
        }

        [Test]
        public void OnlyThinSoilSlumps()
        {
            // A cavity under a 0.3 m soil roof: cutting into the roof brings the thin roof down.
            var grid = Fixture(TerrainMaterialId.Soil, TerrainMaterialId.Soil, p => false);
            Assert.That(grid.RemoveSphere(new Vector3(4, 5.1f, 4), .6f, out var roof), Is.True);
            Assert.That(grid.TryRelease(GroundRelease.SoilSlump, roof, ExcavationGrid.SoilSlumpReach, out _, out _), Is.True);
            Assert.That(grid.IsSolid(new Vector3(4, 5.85f, 4)), Is.False, "The thin roof slumps open.");
            Assert.That(grid.IsSolid(new Vector3(4, 5.85f, 6.5f)), Is.True, "Roof beyond the reach stays.");
            Assert.That(grid.IsSolid(new Vector3(4, 4.2f, 4)), Is.True, "The cavity's floor stays.");
            // A tunnel deep in soil keeps its ceiling: soil more than a metre thick never slumps.
            grid = Fixture(TerrainMaterialId.Soil, TerrainMaterialId.Soil, p => false);
            Assert.That(grid.RemoveSphere(new Vector3(4, 2, 4), .8f, out var deep), Is.True);
            Assert.That(grid.TryRelease(GroundRelease.SoilSlump, deep, ExcavationGrid.SoilSlumpReach, out _, out _), Is.False);
        }

        [Test]
        public void ACutIntoACrackBreaksItsBandAlongTheCrack()
        {
            // A vertical crack sheet through rock: a 0.5 m shattered band around x = 4.
            var grid = Fixture(TerrainMaterialId.Rock, TerrainMaterialId.FracturedRock, p => Mathf.Abs(p.x - 4) <= .25f);
            Assert.That(grid.RemoveSphere(new Vector3(4, 3, 4), .3f, out var cut), Is.True);
            float bite = grid.LastRemovedVolume;
            Assert.That(grid.TryRelease(GroundRelease.CrackBreak, cut, 1f, out _, out _), Is.True);
            Assert.That(grid.LastRemovedVolume, Is.GreaterThan(bite * 4), "Breaking along the crack clears far more than the bite.");
            Assert.That(grid.IsSolid(new Vector3(4, 3, 4.8f)), Is.False, "The band breaks along the crack.");
            Assert.That(grid.IsSolid(new Vector3(4, 3, 5.6f)), Is.True, "The break is bounded.");
            Assert.That(grid.IsSolid(new Vector3(4.6f, 3, 4)), Is.True, "Plain rock beside the band stays.");
            // A cut in plain rock away from the crack breaks nothing.
            Assert.That(grid.RemoveSphere(new Vector3(2, 3, 4), .3f, out var plain), Is.True);
            Assert.That(grid.TryRelease(GroundRelease.CrackBreak, plain, 1f, out _, out _), Is.False);
        }
    }
}
