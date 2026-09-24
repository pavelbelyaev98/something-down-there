using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    public readonly struct DetectorCandidate
    {
        public readonly string Id;
        public readonly Vector3 Position;
        public DetectorCandidate(string id, Vector3 position)
        { Id = id; Position = position; }
    }

    // Buried candidates are supplied by the discovery owner; this only measures aim.
    public sealed class DetectorTargeting
    {
        public string TargetId { get; private set; }
        public int SignalLevel { get; private set; }
        private static readonly float Weak = Cos(EquipmentProgression.DetectorWeakAngle);
        private static readonly float Medium = Cos(EquipmentProgression.DetectorMediumAngle);
        private static readonly float Strong = Cos(EquipmentProgression.DetectorStrongAngle);
        private static readonly float WeakHold = Cos(EquipmentProgression.DetectorWeakAngle + EquipmentProgression.DetectorAngleHysteresis);
        private static readonly float MediumHold = Cos(EquipmentProgression.DetectorMediumAngle + EquipmentProgression.DetectorAngleHysteresis);
        private static readonly float StrongHold = Cos(EquipmentProgression.DetectorStrongAngle + EquipmentProgression.DetectorAngleHysteresis);

        public void Reset() { TargetId = null; SignalLevel = 0; }

        public void Step(IReadOnlyList<DetectorCandidate> candidates, Vector3 origin, Vector3 forward)
        {
            if (forward.sqrMagnitude < .0001f) { Reset(); return; }
            forward.Normalize();
            int best = -1, bestLevel = 0;
            float bestAlignment = -1, bestDistance = float.PositiveInfinity;
            float acquireSquared = EquipmentProgression.DetectorRange * EquipmentProgression.DetectorRange;
            float retainSquared = EquipmentProgression.DetectorRetentionRange * EquipmentProgression.DetectorRetentionRange;
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                Vector3 offset = candidate.Position - origin;
                float distance = offset.sqrMagnitude;
                bool current = candidate.Id == TargetId;
                if (distance < .0001f || distance > (current ? retainSquared : acquireSquared)) continue;
                float alignment = Vector3.Dot(forward, offset / Mathf.Sqrt(distance));
                int level = Level(alignment, current ? SignalLevel : 0);
                if (level == 0) continue;
                if (best < 0 || alignment > bestAlignment || (alignment == bestAlignment
                    && (distance < bestDistance || (distance == bestDistance
                        && string.CompareOrdinal(candidate.Id, candidates[best].Id) < 0))))
                { best = i; bestAlignment = alignment; bestDistance = distance; bestLevel = level; }
            }
            TargetId = best < 0 ? null : candidates[best].Id;
            SignalLevel = bestLevel;
        }

        private static float Cos(float degrees) => Mathf.Cos(degrees * Mathf.Deg2Rad);

        private static int Level(float alignment, int previous) =>
            alignment >= Strong || (previous >= 3 && alignment >= StrongHold) ? 3 :
            alignment >= Medium || (previous >= 2 && alignment >= MediumHold) ? 2 :
            alignment >= Weak || (previous >= 1 && alignment >= WeakHold) ? 1 : 0;
    }
}
