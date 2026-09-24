using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class SiteLayoutTests
    {
        // One guard for the whole depth axis: the shipped site must always fit the grid,
        // the save reader's sample bound and the unpacked payload budget. Raising the
        // depth is then a deliberate edit here plus a scene re-run, never a silent overflow.
        [Test]
        public void ShippedSiteFitsEveryTerrainAndSaveBound()
        {
            Assert.That(SiteLayout.Size, Is.EqualTo(new Vector3Int(192, 800, 192)));
            Assert.That(SiteLayout.CellSize, Is.EqualTo(.125f));
            Assert.That(SiteLayout.ChunkSize, Is.InRange(2, 24));
            Assert.That(SiteLayout.Extent, Is.EqualTo(new Vector3(24, 100, 24)));
            Assert.That(SiteLayout.Origin, Is.EqualTo(new Vector3(-12, -100, -12)));
            Assert.That(SiteLayout.Extent.y, Is.GreaterThanOrEqualTo(100f), "The reservoir is at least 100 m deep.");
            Assert.That(SiteLayout.Extent.x, Is.LessThanOrEqualTo(32f), "The lateral footprint stays contained.");
            Assert.That(SiteLayout.Extent.z, Is.EqualTo(SiteLayout.Extent.x));
            foreach (int cells in new[] { SiteLayout.Size.x, SiteLayout.Size.y, SiteLayout.Size.z })
                Assert.That(cells, Is.LessThanOrEqualTo(ExcavationGrid.MaximumCellsPerAxis));
            long samples = ((long)SiteLayout.Size.x + 1) * (SiteLayout.Size.y + 1) * (SiteLayout.Size.z + 1);
            Assert.That(samples, Is.LessThanOrEqualTo(WorldSaveCodec.MaximumSamples),
                "The shipped density must fit the save reader's sample bound.");
            Assert.That(samples * (sizeof(float) + sizeof(byte)), Is.LessThan(WorldSaveCodec.MaximumUnpackedBytes),
                "The shipped density and material IDs must fit the unpacked payload budget.");
        }
    }
}
