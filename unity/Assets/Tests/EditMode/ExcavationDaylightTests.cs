using System;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class ExcavationDaylightTests
    {
        private static readonly Vector3 Extent = new Vector3(6, 6, 6);
        private static readonly Bounds All = new Bounds(Extent * 0.5f, Extent);
        private static void Rebuild(ExcavationDaylightGrid grid, Func<Vector3, bool> air, Bounds? bounds = null)
        {
            var work = grid.Rebuild(bounds ?? All, air);
            while (work.MoveNext()) { }
        }

        [Test]
        public void DaylightFadesDownTheShaftAndAroundALateralBend()
        {
            var extent = new Vector3(6, 12, 6);
            var grid = new ExcavationDaylightGrid(extent);
            Rebuild(grid, p => p.y >= 12 || Mathf.Abs(p.x - 2) < .6f && Mathf.Abs(p.z - 3) < .6f
                || p.y >= 7.5f && p.y <= 8.5f && p.x >= 2 && p.x <= 5.5f && Mathf.Abs(p.z - 3) < .6f,
                new Bounds(extent * .5f, extent));
            float shallow = grid.Sample(new Vector3(2, 11, 3));
            float middle = grid.Sample(new Vector3(2, 8, 3));
            float deep = grid.Sample(new Vector3(2, 2, 3));
            float bend = grid.Sample(new Vector3(5, 8, 3));
            Assert.That(shallow, Is.InRange(.97f, .995f), "The first metre should retain daylight.");
            Assert.That(middle, Is.InRange(.88f, .94f), "An open four-metre shaft should remain comfortably readable.");
            Assert.That(deep, Is.InRange(.68f, .8f), "A ten-metre shaft should retain most of its daylight.");
            Assert.That(bend, Is.LessThan(middle * .9f), "Lateral enclosure still attenuates daylight.");
            Assert.That(bend, Is.GreaterThan(.65f), "A short branch must remain comfortably readable.");
            float previous = 1;
            for (float depth = .5f; depth <= 11; depth += .5f)
            {
                float current = grid.Sample(new Vector3(2, 12 - depth, 3));
                Assert.That(current, Is.LessThanOrEqualTo(previous));
                Assert.That(previous - current, Is.LessThan(.1f), "No abrupt darkness step while descending.");
                previous = current;
            }
            Assert.That(grid.Sample(new Vector3(2, 12.2f, 3)), Is.EqualTo(1));
        }

        [Test]
        public void DeepShaftAndLongShallowBranchReachDarkness()
        {
            var extent = new Vector3(44, 100, 4);
            var grid = new ExcavationDaylightGrid(extent);
            Rebuild(grid, p => p.y >= extent.y
                || Mathf.Abs(p.x - 2) < .6f && Mathf.Abs(p.z - 2) < .6f
                || p.y >= 95.5f && p.y <= 96.5f && p.x >= 2 && p.x <= 43 && Mathf.Abs(p.z - 2) < .6f,
                new Bounds(extent * .5f, extent));

            Assert.That(grid.Sample(new Vector3(2, 99, 2)), Is.GreaterThan(.8f));
            Assert.That(grid.Sample(new Vector3(2, 80, 2)), Is.InRange(.35f, .5f));
            Assert.That(grid.Sample(new Vector3(2, 60, 2)), Is.InRange(.05f, .1f));
            Assert.That(grid.Sample(new Vector3(2, 50, 2)), Is.InRange(.015f, .03f));
            Assert.That(grid.Sample(new Vector3(2, 30, 2)), Is.Zero);
            Assert.That(grid.Sample(new Vector3(2, 1, 2)), Is.Zero);
            Assert.That(grid.Sample(new Vector3(12, 96, 2)), Is.InRange(.28f, .4f));
            Assert.That(grid.Sample(new Vector3(22, 96, 2)), Is.InRange(.03f, .07f));
            Assert.That(grid.Sample(new Vector3(42, 96, 2)), Is.Zero,
                "A sufficiently long sideways route still reaches darkness.");
        }

        [Test]
        public void ThinEarthPartitionStopsTransportBetweenOpenSamples()
        {
            var grid = new ExcavationDaylightGrid(Extent);
            Rebuild(grid, p => p.y >= 6 || Mathf.Abs(p.x - 3) < .6f && Mathf.Abs(p.z - 3) < .6f
                && (p.y > 3.35f || p.y < 3.2f));
            Assert.That(grid.Sample(new Vector3(3, 4, 3)), Is.GreaterThan(.3f));
            Assert.That(grid.Sample(new Vector3(3, 2, 3)), Is.Zero);
        }

        [Test]
        public void SideBoundaryAndSealedChambersDoNotAdmitSky()
        {
            var grid = new ExcavationDaylightGrid(Extent);
            Rebuild(grid, p => p.y >= 6 || p.y <= 3);
            Assert.That(grid.Sample(new Vector3(0, 2, 3)), Is.Zero);
            Assert.That(grid.Sample(new Vector3(3, 2, 3)), Is.Zero);
        }

        [Test]
        public void OpeningAndClosingTheRoofMatchesACompleteRestoreRebuild()
        {
            bool open = false;
            Func<Vector3, bool> air = p => p.y >= 6 || Mathf.Abs(p.x - 3) < .6f
                && Mathf.Abs(p.z - 3) < .6f && (p.y <= 4.5f || open);
            var grid = new ExcavationDaylightGrid(Extent);
            Rebuild(grid, air);
            Assert.That(grid.Sample(new Vector3(3, 2, 3)), Is.Zero);
            var roof = new Bounds(new Vector3(3, 5.25f, 3), new Vector3(1.3f, 1.5f, 1.3f));
            open = true;
            Rebuild(grid, air, roof);
            Assert.That(grid.Sample(new Vector3(3, 2, 3)), Is.GreaterThan(.1f));
            var restored = new ExcavationDaylightGrid(Extent);
            Rebuild(restored, air);
            Assert.That(grid.Light, Is.EqualTo(restored.Light));
            open = false;
            Rebuild(grid, air, roof);
            Assert.That(grid.Sample(new Vector3(3, 2, 3)), Is.Zero);
        }
    }
}
