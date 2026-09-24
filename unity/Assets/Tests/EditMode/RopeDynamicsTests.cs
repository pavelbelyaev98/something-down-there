using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class RopeDynamicsTests
    {
        private static void Advance(RopeDynamics rope, Vector3 a, Vector3 b, float length, int steps, IRopeCollision collision = null)
        {
            for (int i = 0; i < steps; i++) rope.Step(.02f, a, b, length, .22f, 1.4f, 12, .018f, collision);
        }

        [Test]
        public void SlackSagsUnderGravityAndReelingMakesItTaut()
        {
            var a = new Vector3(-2, 2, 0); var b = new Vector3(2, 2, 0);
            var rope = new RopeDynamics(); rope.Seed(new[] { a, b }, 2, .22f);
            Advance(rope, a, b, 4.6f, 160);
            float slackY = rope.Position(rope.Count / 2).y;
            Assert.That(slackY, Is.LessThan(1.4f), "Slack must sag through gravity, without an authored bend or wave.");
            Advance(rope, a, b, 4.01f, 160);
            Assert.That(rope.Position(rope.Count / 2).y, Is.GreaterThan(slackY + .4f), "Reeling in straightens the same simulated rope.");
            Assert.That(rope.Position(0), Is.EqualTo(a));
            Assert.That(rope.Position(rope.Count - 1), Is.EqualTo(b));
        }

        [Test]
        public void InteriorKeepsMovingAfterAttachmentStops()
        {
            var a = new Vector3(-2, 3, 0); var b = new Vector3(2, 3, 0);
            var rope = new RopeDynamics(); rope.Seed(new[] { a, b }, 2, .22f);
            Advance(rope, a, b, 4.7f, 180);
            for (int step = 0; step < 12; step++)
                Advance(rope, a, b + Vector3.forward * (step / 12f), 4.7f, 1);
            b += Vector3.forward;
            Advance(rope, a, b, 4.7f, 1);
            Vector3 before = rope.Position(rope.Count / 2);
            Advance(rope, a, b, 4.7f, 8);
            Assert.That(Vector3.Distance(rope.Position(rope.Count / 2), before), Is.GreaterThan(.025f),
                "The cable retains momentum after both endpoints stop, instead of snapping to a route line.");
        }

        private sealed class Floor : IRopeCollision
        {
            public bool Enabled = true;
            public Vector3 Project(Vector3 from, Vector3 point, float radius)
            { if (Enabled) point.y = Mathf.Max(point.y, 1 + radius); return point; }
        }

        [Test]
        public void SoilContactSupportsCableAndDiggingLetsItFall()
        {
            var a = new Vector3(-2, 2, 0); var b = new Vector3(2, 2, 0);
            var rope = new RopeDynamics(); var floor = new Floor();
            rope.Seed(new[] { a, b }, 2, .22f);
            Advance(rope, a, b, 5.5f, 220, floor);
            for (int i = 0; i < rope.Count; i++) Assert.That(rope.Position(i).y, Is.GreaterThanOrEqualTo(1.017f));
            floor.Enabled = false;
            Advance(rope, a, b, 5.5f, 80, floor);
            Assert.That(rope.Position(rope.Count / 2).y, Is.LessThan(.7f), "Removing support releases the rope without rebuilding its route.");
        }

        [Test]
        public void PayoutRetractionAndHitchesStayFiniteAndBounded()
        {
            var a = Vector3.up * 10; var b = Vector3.zero;
            var rope = new RopeDynamics(); rope.Seed(new[] { a, b }, 2, .22f);
            for (int step = 0; step < 600; step++)
            {
                b = new Vector3(Mathf.Sin(step * .03f), 5 + 4 * Mathf.Sin(step * .012f), 0);
                rope.Step(step % 30 == 0 ? .2f : .02f, a, b, Vector3.Distance(a, b) * 1.05f, .22f, 1.4f, 12, .018f, null);
                Assert.That(rope.Count, Is.InRange(2, RopeDynamics.Capacity));
                for (int i = 0; i < rope.Count; i++)
                {
                    var point = rope.Position(i);
                    Assert.That(float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z), Is.True);
                    Assert.That(Vector3.Distance(point, a), Is.LessThan(12));
                }
            }
        }
    }
}
