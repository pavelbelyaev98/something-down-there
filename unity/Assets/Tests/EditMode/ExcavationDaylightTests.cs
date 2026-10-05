using System;
using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class ExcavationDaylightTests
    {
        private static readonly Vector3 Extent = new Vector3(6, 6, 6);
        private static readonly Bounds All = new Bounds(Extent * 0.5f, Extent);
        // Open or closed shapes as a density: closed far from air, so nodes keep to their own points.
        private static Func<Vector3, float> Density(Func<Vector3, bool> air) => p => air(p) ? -1f : 1f;

        private static void Rebuild(ExcavationDaylightGrid grid, Func<Vector3, bool> air, Bounds? bounds = null)
            => Rebuild(grid, Density(air), bounds);

        private static void Rebuild(ExcavationDaylightGrid grid, Func<Vector3, float> density, Bounds? bounds = null)
        {
            var work = grid.Rebuild(bounds ?? All, density);
            while (work.MoveNext()) { }
        }

        [Test]
        public void DepthFadesFromTheFirstMetresAndIsDarkBeyondTwenty()
        {
            Assert.That(ExcavationDaylightGrid.Daylight(0), Is.EqualTo(1));
            Assert.That(ExcavationDaylightGrid.Daylight(5), Is.InRange(.75f, .95f), "Already dimmer a few metres down.");
            Assert.That(ExcavationDaylightGrid.Daylight(10), Is.InRange(.4f, .65f), "Dim but readable at ten metres.");
            Assert.That(ExcavationDaylightGrid.Daylight(15), Is.LessThan(.35f), "Lamps are wanted from about twelve to fifteen metres.");
            Assert.That(ExcavationDaylightGrid.Daylight(25), Is.LessThan(.03f));
            Assert.That(ExcavationDaylightGrid.Daylight(32), Is.LessThan(1 / 255f));
            for (float metres = .5f; metres <= 40; metres += .5f)
            {
                float step = ExcavationDaylightGrid.Daylight(metres - .5f) - ExcavationDaylightGrid.Daylight(metres);
                Assert.That(step, Is.InRange(0f, .06f), "Darkness builds without an abrupt step.");
            }
        }

        [Test]
        public void SidewaysLosesLightFromTheFirstMetreAtAnyDepth()
        {
            Assert.That(ExcavationDaylightGrid.Daylight(6, 1), Is.LessThan(.75f));
            Assert.That(ExcavationDaylightGrid.Daylight(6, 3), Is.LessThan(ExcavationDaylightGrid.Daylight(6, 1) * .6f));
            Assert.That(ExcavationDaylightGrid.Daylight(6, 10), Is.LessThan(.05f), "A long side tunnel goes dark even near the surface.");
        }

        [Test]
        public void ShallowShaftStaysReadableAndItsShortBranchIsDarker()
        {
            var extent = new Vector3(6, 12, 6);
            var grid = new ExcavationDaylightGrid(extent);
            Rebuild(grid, p => p.y >= 12 || Mathf.Abs(p.x - 2) < .6f && Mathf.Abs(p.z - 3) < .6f
                || p.y >= 7.5f && p.y <= 8.5f && p.x >= 2 && p.x <= 5.5f && Mathf.Abs(p.z - 3) < .6f,
                new Bounds(extent * .5f, extent));
            Assert.That(grid.Sample(new Vector3(2, 11, 3)), Is.GreaterThan(.99f));
            Assert.That(grid.Sample(new Vector3(2, 2, 3)), Is.InRange(.4f, .65f), "A ten-metre shaft is dim but readable.");
            float bend = grid.Sample(new Vector3(5, 8, 3));
            Assert.That(bend, Is.LessThan(grid.Sample(new Vector3(2, 8, 3)) * .7f), "Three metres sideways is clearly darker than the shaft.");
            Assert.That(bend, Is.GreaterThan(.1f), "It is still not black so close to the shaft.");
            float previous = 1;
            for (float depth = .5f; depth <= 11; depth += .5f)
            {
                float current = grid.Sample(new Vector3(2, 12 - depth, 3));
                Assert.That(current, Is.LessThanOrEqualTo(previous));
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

            Assert.That(grid.Sample(new Vector3(2, 99, 2)), Is.GreaterThan(.99f));
            Assert.That(grid.Sample(new Vector3(2, 90, 2)), Is.InRange(.4f, .65f), "Dim but readable at ten metres.");
            Assert.That(grid.Sample(new Vector3(2, 85, 2)), Is.LessThan(.35f));
            Assert.That(grid.Sample(new Vector3(2, 75, 2)), Is.LessThan(.03f), "Dark at twenty-five metres.");
            Assert.That(grid.Sample(new Vector3(2, 60, 2)), Is.Zero);
            Assert.That(grid.Sample(new Vector3(2, 1, 2)), Is.Zero);
            // Sideways light fades from the shaft's edge whatever the depth.
            float previous = 1;
            for (float x = 3; x <= 12; x += 1)
            {
                float current = grid.Sample(new Vector3(x, 96, 2));
                Assert.That(current, Is.LessThanOrEqualTo(previous));
                previous = current;
            }
            Assert.That(grid.Sample(new Vector3(5, 96, 2)), Is.InRange(.1f, .6f));
            Assert.That(grid.Sample(new Vector3(16, 96, 2)), Is.LessThan(.05f), "Near-black fourteen metres along.");
            Assert.That(grid.Sample(new Vector3(26, 96, 2)), Is.Zero);
            Assert.That(grid.Sample(new Vector3(42, 96, 2)), Is.Zero,
                "A sufficiently long sideways route still reaches darkness.");
        }

        [Test]
        public void WalkedRampDarkensAlmostLikeAShaftNotLikeASideTunnel()
        {
            var extent = new Vector3(4, 40, 40);
            var grid = new ExcavationDaylightGrid(extent);
            // A 45 degree ramp dropping one metre per metre along z from the open top.
            Rebuild(grid, p => p.y >= extent.y || Mathf.Abs(p.x - 2) < .6f && Mathf.Abs(p.z - 2 - (extent.y - p.y)) < .8f,
                new Bounds(extent * .5f, extent));
            Assert.That(grid.Sample(new Vector3(2, extent.y - 6, 8)), Is.GreaterThan(.55f), "Six metres down a ramp is lit much like a shaft.");
            Assert.That(ExcavationDaylightGrid.Daylight(0, 6), Is.LessThan(.2f), "Six metres of flat tunnel would already be dark.");
            Assert.That(grid.Sample(new Vector3(2, extent.y - 20, 22)), Is.LessThan(.05f), "A ramp still reaches darkness.");
        }

        [Test]
        public void FreshCutIsLitBeforeTheRebuildAndTheRebuildMatchesAFullOne()
        {
            var extent = new Vector3(6, 12, 6);
            bool deeper = false;
            Func<Vector3, bool> air = p => p.y >= 12 || Mathf.Abs(p.x - 3) < .6f && Mathf.Abs(p.z - 3) < .6f && p.y >= (deeper ? 7 : 8.5f);
            var grid = new ExcavationDaylightGrid(extent);
            Rebuild(grid, air, new Bounds(extent * .5f, extent));
            Assert.That(grid.Sample(new Vector3(3, 7.5f, 3)), Is.Zero);
            deeper = true;
            var cut = new Bounds(new Vector3(3, 7.75f, 3), new Vector3(1.2f, 1.5f, 1.2f));
            grid.Patch(cut, Density(air));
            Assert.That(grid.Sample(new Vector3(3, 7.5f, 3)), Is.GreaterThan(.7f), "The fresh floor is lit at once, not after the rebuild.");
            Rebuild(grid, air, cut);
            var full = new ExcavationDaylightGrid(extent);
            Rebuild(full, air, new Bounds(extent * .5f, extent));
            Assert.That(grid.Light, Is.EqualTo(full.Light), "The partial rebuild matches a complete one.");
        }

        // A hole narrower than the node spacing, between node columns, carries daylight down it (user, 2026-10-05: a
        // narrow hole went black and lit up once widened).
        [Test]
        public void NarrowHoleBetweenNodesIsLit()
        {
            var axis = new Vector3(3.25f, 0, 3.25f);
            float Hole(Vector3 p)
            {
                if (p.y >= 6) return -1f;
                float aside = new Vector2(p.x - axis.x, p.z - axis.z).magnitude - .3f;
                return Mathf.Clamp(Mathf.Max(aside, 3.5f - p.y), -.25f, .25f);
            }
            var grid = new ExcavationDaylightGrid(Extent);
            Rebuild(grid, Hole);
            Assert.That(grid.Sample(new Vector3(axis.x + .3f, 4.5f, axis.z)), Is.GreaterThan(.5f), "Its wall 1.5 m down");
            Assert.That(grid.Sample(new Vector3(axis.x + 1.5f, 4.5f, axis.z)), Is.Zero, "Ground beside it stays dark");
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
