using UnityEngine;

namespace SomethingDownThere
{
    // Tuning of the salvage crane's smart rope: marking, route search, payout, haul and ruptures.
    [CreateAssetMenu(menuName = "Something Down There/Salvage rope settings")]
    public sealed class SalvageRopeSettings : ScriptableObject
    {
        [Min(.1f)] public float MarkSeconds = 1.1f;
        [Min(.1f)] public float RopeSpeed = 3f;
        [Min(.1f)] public float HaulSpeed = 2.25f;
        [Min(.1f)] public float AttachSeconds = .6f;
        [Range(.01f, .15f)] public float Clearance = .055f;
        [Range(.01f, .05f)] public float RopeRadius = .018f;
        [Range(.12f, .4f)] public float RopeSegmentLength = .22f;
        [Range(0, .12f)] public float RopeSlack = .045f;
        [Range(.2f, 5f)] public float RopeDamping = 1.4f;
        [Range(8, 24)] public int RopeIterations = 12;
        [Range(.02f, .25f)] public float MaximumMoveStep = .16f;
        [Range(30f, 200f)] public float SpringAcceleration = 100f;
        [Range(4f, 30f)] public float SpringDamping = 9f;
        [Range(.2f, 1f)] public float GuideLead = .7f;
        [Range(1f, 5f)] public float MaximumLoadSpeed = 4f;
        [Tooltip("Reeling speed multiplier reached while the load is jammed; carried into release.")]
        [Range(1f, 4f)] public float LoadedHaulMultiplier = 3f;
        [Tooltip("Extra rope stretch permitted during a jam, still following the accepted route bends.")]
        [Range(.2f, 1.5f)] public float LoadedGuideLead = 1.35f;
        [Range(1f, 8f)] public float MaximumBurstSpeed = 8f;
        [Range(1f, 5f)] public float MaximumSpin = 5f;
        [Tooltip("Continuous blocked contact under rope tension before soil can break.")]
        [Range(.2f, 2f)] public float ContactStallSeconds = .22f;
        [Tooltip("Shorter physical retry when a chip leaves the same load wedged. Progress or lost contact resets it.")]
        [Range(.08f, .5f)] public float FollowupContactSeconds = .09f;
        [Tooltip("A load advancing slower than this share of the haul speed is still jammed: creeping along the walls does not reset a wind-up.")]
        [Range(0f, .6f)] public float CreepShare = .3f;
        [Tooltip("Spring strength builds to this multiplier during a blocked physical contact.")]
        [Range(1f, 4f)] public float BlockedPullMultiplier = 3.8f;
        [Tooltip("Without forward progress, automatically build a stronger pull; never wait for player intervention.")]
        [Range(.4f, 3f)] public float RetensionSeconds = .55f;
        [Range(3f, 6f)] public float RetensionPullMultiplier = 6f;
        [Tooltip("Ease the loaded spring back to normal after release, preserving its physical recoil.")]
        [Range(.1f, 2f)] public float TensionReleaseSeconds = 1.1f;
        [Tooltip("For every second the load is stuck (wedged, or crawling far slower than the reel) the machine's drive rises by this much: it pulls harder, reels faster, tears bigger chunks, and the next jam winds up sooner.")]
        [Range(0f, 3f)] public float DriveGrowthPerSecond = .5f;
        [Tooltip("Highest drive the machine builds up over a haul.")]
        [Range(1f, 5f)] public float MaximumDrive = 2.5f;
        [Tooltip("While the load runs free the drive eases back toward normal with this time constant.")]
        [Range(.5f, 20f)] public float DriveRelaxSeconds = 6f;
        [Tooltip("Half width of one local break at the actual terrain contact.")]
        [Range(.15f, .5f)] public float ContactBreakRadius = .38f;
        [Tooltip("Depth of one local break into the contacted soil.")]
        [Range(.05f, .3f)] public float ContactBreakDepth = .24f;
        [Tooltip("Opposing impact speed needed to break dirt immediately; slow contact must wind up first.")]
        [Range(2f, 6f)] public float ImpactBreakSpeed = 3f;
        [Tooltip("Extra local rupture size at full tension or a hard impact.")]
        [Range(1f, 2f)] public float RuptureSizeMultiplier = 1.4f;
        [Range(0f, 1f)] public float BreakParticleIntensity = 1f;
        [Range(32, 2048)] public int SearchNodesPerFrame = 256;
        [Range(1000, 500000)] public int MaximumSearchNodes = 150000;
        public bool Valid => float.IsFinite(MarkSeconds) && MarkSeconds > 0
            && float.IsFinite(RopeSpeed) && RopeSpeed > 0 && float.IsFinite(HaulSpeed) && HaulSpeed > 0
            && float.IsFinite(AttachSeconds) && AttachSeconds > 0 && AttachSeconds <= 60
            && Clearance >= .01f && Clearance <= .15f && RopeRadius >= .01f && RopeRadius <= .05f
            && RopeSegmentLength >= .12f && RopeSegmentLength <= .4f && RopeSlack >= 0 && RopeSlack <= .12f
            && RopeDamping >= .2f && RopeDamping <= 5 && RopeIterations >= 8 && RopeIterations <= 24
            && MaximumMoveStep >= .02f && MaximumMoveStep <= .25f
            && SpringAcceleration >= 30 && SpringAcceleration <= 200 && SpringDamping >= 4 && SpringDamping <= 30
            && GuideLead >= .2f && GuideLead <= 1 && MaximumLoadSpeed >= 1 && MaximumLoadSpeed <= 5
            && LoadedHaulMultiplier >= 1 && LoadedHaulMultiplier <= 4
            && LoadedGuideLead >= GuideLead && LoadedGuideLead <= 1.5f
            && MaximumBurstSpeed >= MaximumLoadSpeed && MaximumBurstSpeed <= 8
            && MaximumSpin >= 1 && MaximumSpin <= 5
            && ContactStallSeconds >= .2f && ContactStallSeconds <= 2
            && FollowupContactSeconds >= .08f && FollowupContactSeconds <= .5f && FollowupContactSeconds <= ContactStallSeconds
            && CreepShare >= 0 && CreepShare <= .6f
            && BlockedPullMultiplier >= 1 && BlockedPullMultiplier <= 4
            && RetensionSeconds >= .4f && RetensionSeconds <= 3
            && RetensionPullMultiplier >= 3 && RetensionPullMultiplier <= 6
            && TensionReleaseSeconds >= .1f && TensionReleaseSeconds <= 2
            && DriveGrowthPerSecond >= 0 && DriveGrowthPerSecond <= 3 && MaximumDrive >= 1 && MaximumDrive <= 5
            && DriveRelaxSeconds >= .5f && DriveRelaxSeconds <= 20
            && ContactBreakRadius >= .15f && ContactBreakRadius <= .5f
            && ContactBreakDepth >= .05f && ContactBreakDepth <= .3f
            && ImpactBreakSpeed >= 2 && ImpactBreakSpeed <= 6 && ImpactBreakSpeed < MaximumBurstSpeed
            && RuptureSizeMultiplier >= 1 && RuptureSizeMultiplier <= 2
            && BreakParticleIntensity >= 0 && BreakParticleIntensity <= 1
            && SearchNodesPerFrame >= 32 && SearchNodesPerFrame <= 2048
            && MaximumSearchNodes >= SearchNodesPerFrame && MaximumSearchNodes <= 500000;
    }
}
