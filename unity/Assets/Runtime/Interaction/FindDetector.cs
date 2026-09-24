using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    // Only a population replacement scans the common population. Steady sampling
    // visits authored signal sources, with no allocations or raycasts.
    public sealed class FindDetector
    {
        private readonly FpsPlayer player;
        private readonly DetectorTargeting targeting = new DetectorTargeting();
        private readonly List<BuriedFind> sources = new List<BuriedFind>();
        private readonly List<DetectorCandidate> candidates = new List<DetectorCandidate>();
        private DiscoveryField field;
        private long populationRevision = -1;
        private BuriedFind target;
        public BuriedFind Target => Eligible(target) ? target : null;
        public int SourceCount => sources.Count;
        public int SignalLevel => Target == null ? 0 : targeting.SignalLevel;

        public FindDetector(FpsPlayer player) { this.player = player; }
        public void Reset()
        {
            targeting.Reset(); sources.Clear(); candidates.Clear();
            target = null; field = null; populationRevision = -1;
        }

        public static bool Eligible(BuriedFind find) => find != null && find.isActiveAndEnabled
            && find.DetectorEligible && find.Item != null && find.State == FindState.World && !find.IsHeld
            && find.Exposure <= 0 && !find.DepthRecorded;

        public void Tick()
        {
            if (!player.GameplayActive) return;
            var currentField = player.Discoveries;
            if (currentField == null || !currentField.isActiveAndEnabled || !currentField.Initialized)
            { Reset(); return; }
            bool changed = field != currentField || populationRevision != currentField.PopulationRevision;
            if (changed)
            {
                Reset(); field = currentField; populationRevision = field.PopulationRevision;
                foreach (var find in field.Finds)
                    if (find != null && find.DetectorEligible) sources.Add(find);
            }
            using var profile = SampleMarker.Auto();
            candidates.Clear();
            foreach (var find in sources)
                if (Eligible(find)) candidates.Add(new DetectorCandidate(find.Item.InstanceId, find.WorldBounds.center));
            targeting.Step(candidates, player.ViewCamera.transform.position, player.ViewCamera.transform.forward);
            target = null;
            // Resolve within the small signal cache, never DiscoveryField.Find's dense list.
            foreach (var find in sources)
                if (Eligible(find) && find.Item.InstanceId == targeting.TargetId) { target = find; break; }
        }

        private static readonly Unity.Profiling.ProfilerMarker SampleMarker = new Unity.Profiling.ProfilerMarker("Detector.Sample");
    }
}
