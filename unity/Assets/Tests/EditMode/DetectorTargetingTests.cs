using NUnit.Framework;
using UnityEngine;

namespace SomethingDownThere.Tests
{
    public sealed class DetectorTargetingTests
    {
        private static DetectorCandidate At(string id, float distance, float yaw = 0)
            => new DetectorCandidate(id, Quaternion.Euler(0, yaw, 0) * Vector3.forward * distance);

        [TestCase(40, 0, 1)]
        [TestCase(20, 0, 2)]
        [TestCase(0, 0, 3)]
        [TestCase(-20, 0, 2)]
        [TestCase(0, 20, 2)]
        [TestCase(0, -40, 1)]
        [TestCase(70, 0, 0)]
        [TestCase(180, 0, 0)]
        [TestCase(0, 90, 0)]
        public void SignalFollowsAimInBothAxes(float yaw, float pitch, int level)
        {
            var detector = new DetectorTargeting();
            detector.Step(new[] { At("find", 3) }, Vector3.zero, Quaternion.Euler(pitch, yaw, 0) * Vector3.forward);
            Assert.That(detector.SignalLevel, Is.EqualTo(level));
            Assert.That(detector.TargetId, Is.EqualTo(level == 0 ? null : "find"));
        }

        [Test]
        public void DistanceDoesNotReplaceAimStrength()
        {
            var detector = new DetectorTargeting();
            foreach (float distance in new[] { .5f, 2f, EquipmentProgression.DetectorRange })
            {
                detector.Reset();
                detector.Step(new[] { At("find", distance) }, Vector3.zero, Vector3.forward);
                Assert.That(detector.SignalLevel, Is.EqualTo(3));
                detector.Step(new[] { At("find", distance, 40) }, Vector3.zero, Vector3.forward);
                Assert.That(detector.SignalLevel, Is.EqualTo(1));
            }
        }

        [Test]
        public void AimingAtAnotherFindSwitchesImmediatelyEvenIfItIsFartherAway()
        {
            var detector = new DetectorTargeting();
            var candidates = new[] { At("near", 1, -20), At("aimed", 4, 20) };
            detector.Step(candidates, Vector3.zero, Quaternion.Euler(0, -20, 0) * Vector3.forward);
            Assert.That(detector.TargetId, Is.EqualTo("near"));
            detector.Step(candidates, Vector3.zero, Quaternion.Euler(0, 20, 0) * Vector3.forward);
            Assert.That(detector.TargetId, Is.EqualTo("aimed"));
            Assert.That(detector.SignalLevel, Is.EqualTo(3));
            detector.Step(candidates, Vector3.zero, Vector3.back);
            Assert.That(detector.SignalLevel, Is.Zero, "No timed lock may retain a signal behind the player.");
        }

        [Test]
        public void EqualBearingsUseDistanceThenStableIdentity()
        {
            var detector = new DetectorTargeting();
            detector.Step(new[] { At("z", 3), At("a", 3), At("near", 1) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.EqualTo("near"));
            detector.Reset();
            detector.Step(new[] { At("z", 3), At("a", 3) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.EqualTo("a"));
            detector.Reset();
            detector.Step(new[] { At("a", 3), At("z", 3) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.EqualTo("a"));
        }

        [Test]
        public void SmallAimChangesDoNotFlickerButLeavingEachBandLowersSignal()
        {
            var detector = new DetectorTargeting();
            var candidate = new[] { At("find", 3) };
            float margin = EquipmentProgression.DetectorAngleHysteresis;
            var thresholds = new[] { EquipmentProgression.DetectorStrongAngle, EquipmentProgression.DetectorMediumAngle, EquipmentProgression.DetectorWeakAngle };
            for (int i = 0; i < thresholds.Length; i++)
            {
                float angle = thresholds[i];
                detector.Step(candidate, Vector3.zero, Quaternion.Euler(0, angle - 1, 0) * Vector3.forward);
                Assert.That(detector.SignalLevel, Is.EqualTo(3 - i));
                detector.Step(candidate, Vector3.zero, Quaternion.Euler(0, angle + margin * .5f, 0) * Vector3.forward);
                Assert.That(detector.SignalLevel, Is.EqualTo(3 - i));
                detector.Step(candidate, Vector3.zero, Quaternion.Euler(0, angle + margin + 1, 0) * Vector3.forward);
                Assert.That(detector.SignalLevel, Is.EqualTo(2 - i));
            }
        }

        [Test]
        public void RangeHysteresisRetainsTargetButDoesNotAcquireOutsideRange()
        {
            var detector = new DetectorTargeting();
            float middle = (EquipmentProgression.DetectorRange + EquipmentProgression.DetectorRetentionRange) * .5f;
            detector.Step(new[] { At("edge", middle) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.Null);
            detector.Step(new[] { At("edge", EquipmentProgression.DetectorRange) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.SignalLevel, Is.EqualTo(3));
            detector.Step(new[] { At("edge", middle) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.EqualTo("edge"));
            detector.Step(new[] { At("edge", EquipmentProgression.DetectorRetentionRange + .01f) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.Null);
            Assert.That(detector.SignalLevel, Is.Zero);
        }

        [Test]
        public void RemovedCandidatesAndResetClearTheReading()
        {
            var detector = new DetectorTargeting();
            detector.Step(new[] { At("first", 2) }, Vector3.zero, Vector3.forward);
            detector.Step(System.Array.Empty<DetectorCandidate>(), Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.Null);
            Assert.That(detector.SignalLevel, Is.Zero);
            detector.Step(new[] { At("new", 2) }, Vector3.zero, Vector3.forward);
            Assert.That(detector.TargetId, Is.EqualTo("new"));
            detector.Reset();
            Assert.That(detector.TargetId, Is.Null);
            Assert.That(detector.SignalLevel, Is.Zero);
        }
    }
}
