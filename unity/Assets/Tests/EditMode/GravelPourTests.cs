using System;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    // Concept 03 §4 the pour: undercut gravel lets go in one bounded rush; nothing else moves.
    public sealed class GravelPourTests
    {
        // 8 x 6 x 8 m of clay with a gravel slab 1 m thick, 2.5-3.5 m below the top.
        private static ExcavationGrid SlabFixture(float slabBottom = 2.5f)
        {
            var grid = new ExcavationGrid(new Vector3Int(64, 48, 64), .125f);
            var saved = grid.Capture();
            var ids = new byte[saved.Materials.Length];
            int i = 0;
            for (int z = 0; z <= 64; z++)
            for (int y = 0; y <= 48; y++)
            for (int x = 0; x <= 64; x++)
            {
                float height = y * .125f;
                bool slab = height >= slabBottom && height <= slabBottom + 1 && x >= 8 && x <= 56 && z >= 8 && z <= 56;
                ids[i++] = (byte)(slab ? TerrainMaterialId.Gravel : TerrainMaterialId.Clay);
            }
            saved.Materials = TerrainMaterialSnapshot.CopyFrom(ids);
            grid.Restore(saved);
            return grid;
        }

        [Test]
        public void UndercutGravelPoursItsSectionWithinTheReachAndNothingElse()
        {
            var grid = SlabFixture();
            // A room whose ceiling breaks into the slab's underside.
            Assert.That(grid.RemoveSphere(new Vector3(4, 1.4f, 4), 1.2f, out var cut), Is.True);
            float carved = grid.RemovedVolume;
            Assert.That(grid.TryPourGravel(cut, out var changed, out var centre), Is.True);
            Assert.That(grid.LastRemovedVolume, Is.GreaterThan(3), "The undercut section lets go in one rush.");
            Assert.That(grid.LastRemovedVolume, Is.LessThanOrEqualTo(ExcavationGrid.PourMaximumSamples * Mathf.Pow(.125f, 3) * 1.01f));
            Assert.That(grid.RemovedVolume, Is.GreaterThan(carved));
            Assert.That(Vector3.Distance(centre, new Vector3(4, 2.5f, 4)), Is.LessThan(1));
            Assert.That(grid.IsSolid(new Vector3(4, 3, 4)), Is.False, "The gravel above the room is gone.");
            Assert.That(grid.IsSolid(new Vector3(4, 3.8f, 4)), Is.True, "Clay above the slab stays.");
            Assert.That(grid.IsSolid(new Vector3(1.2f, 3, 1.2f)), Is.True, "Gravel beyond the reach stays: the pour is bounded.");
            Assert.That(grid.IsSolid(new Vector3(4, .1f, 4)), Is.True, "The room's floor is untouched.");
            Assert.That(changed.size.x, Is.GreaterThan(0));
            // A second attempt finds nothing loose left above the room.
            Assert.That(grid.TryPourGravel(cut, out _, out _), Is.False);
        }

        [Test]
        public void DiggingOntoGravelOrGrazingItNeverPours()
        {
            var grid = SlabFixture();
            // From above: gravel keeps solid ground beneath it.
            Assert.That(grid.RemoveSphere(new Vector3(4, 3.8f, 4), .6f, out var onto), Is.True);
            Assert.That(grid.TryPourGravel(onto, out _, out _), Is.False);
            // A tiny nick under the slab is not a ceiling.
            grid = SlabFixture();
            Assert.That(grid.RemoveSphere(new Vector3(4, 2.45f, 4), .14f, out var nick), Is.True);
            Assert.That(grid.TryPourGravel(nick, out _, out _), Is.False);
            // Clay undercut without gravel above never pours either.
            grid = SlabFixture(slabBottom: 5);
            Assert.That(grid.RemoveSphere(new Vector3(4, 1.4f, 4), 1.2f, out var clay), Is.True);
            Assert.That(grid.TryPourGravel(clay, out _, out _), Is.False);
        }
    }
}
