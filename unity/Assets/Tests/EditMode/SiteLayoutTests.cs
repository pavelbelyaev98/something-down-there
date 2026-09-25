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
            Assert.That(SiteLayout.Size, Is.EqualTo(new Vector3Int(288, 800, 208)));
            Assert.That(SiteLayout.CellSize, Is.EqualTo(.125f));
            Assert.That(SiteLayout.ChunkSize, Is.InRange(2, 24));
            Assert.That(SiteLayout.Extent, Is.EqualTo(new Vector3(36, 100, 26)));
            Assert.That(SiteLayout.Origin, Is.EqualTo(new Vector3(-18, -100, -13)));
            Assert.That(SiteLayout.Extent.y, Is.GreaterThanOrEqualTo(100f), "The reservoir is at least 100 m deep.");
            Assert.That(SiteLayout.Size.x % SiteLayout.ChunkSize, Is.Zero);
            Assert.That(SiteLayout.Size.z % SiteLayout.ChunkSize, Is.Zero);
            foreach (int cells in new[] { SiteLayout.Size.x, SiteLayout.Size.y, SiteLayout.Size.z })
                Assert.That(cells, Is.LessThanOrEqualTo(ExcavationGrid.MaximumCellsPerAxis));
            long samples = ((long)SiteLayout.Size.x + 1) * (SiteLayout.Size.y + 1) * (SiteLayout.Size.z + 1);
            Assert.That(samples, Is.LessThanOrEqualTo(WorldSaveCodec.MaximumSamples),
                "The shipped density must fit the save reader's sample bound.");
            Assert.That(samples * (sizeof(float) + sizeof(byte)), Is.LessThan(WorldSaveCodec.MaximumUnpackedBytes),
                "The shipped density and material IDs must fit the unpacked payload budget.");
        }

        // The plot outline stays inside the grid with room for the rim collar, keeps the
        // stations' 12 m camp arc and is visibly wider east-west than a circle.
        [Test]
        public void OpeningOutlineFitsTheGridAndKeepsTheCampArc()
        {
            float widest = 0, deepest = 0;
            for (float compass = 0; compass < 360; compass += .5f)
            {
                var point = SiteLayout.OpeningPoint(compass);
                Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(SiteLayout.Extent.x * .5f - 1), compass.ToString());
                Assert.That(Mathf.Abs(point.y), Is.LessThanOrEqualTo(SiteLayout.Extent.z * .5f - .9f), compass.ToString());
                widest = Mathf.Max(widest, Mathf.Abs(point.x));
                deepest = Mathf.Max(deepest, Mathf.Abs(point.y));
                if (Mathf.Abs(Mathf.DeltaAngle(compass, SiteLayout.CampCompass)) <= SiteLayout.CampHalfArc)
                    Assert.That(SiteLayout.OpeningRadius(compass), Is.EqualTo(SiteLayout.CampOpeningRadius).Within(.0001f));
                Assert.That(SiteLayout.BeyondOpening(point * .99f), Is.LessThan(0));
                Assert.That(SiteLayout.BeyondOpening(point * 1.01f), Is.GreaterThan(0));
            }
            Assert.That(widest, Is.GreaterThan(deepest + 2), "The plot is wider than it is deep.");
        }
    }
}
