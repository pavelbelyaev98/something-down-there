using UnityEngine;

namespace SomethingDownThere
{
    [CreateAssetMenu(menuName = "Something Down There/Salvage winch settings")]
    public sealed class SalvageWinchSettings : ScriptableObject
    {
        [Min(.1f)] public float MarkSeconds = 1.1f;
        [Min(.1f)] public float RopeSpeed = 3f;
        [Min(.1f)] public float HaulSpeed = 1.2f;
        [Min(.1f)] public float AttachSeconds = .6f;
        [Range(.01f, .15f)] public float Clearance = .055f;
        [Range(.01f, .05f)] public float RopeRadius = .018f;
        [Range(.02f, .25f)] public float MaximumMoveStep = .10f;
        [Range(30f, 200f)] public float SpringAcceleration = 100f;
        [Range(4f, 30f)] public float SpringDamping = 14f;
        [Range(.2f, 1f)] public float GuideLead = .55f;
        [Range(1f, 5f)] public float MaximumLoadSpeed = 2.5f;
        [Range(1f, 5f)] public float MaximumSpin = 3f;
        [Range(32, 2048)] public int SearchNodesPerFrame = 256;
        [Range(1000, 500000)] public int MaximumSearchNodes = 150000;
        public bool Valid => float.IsFinite(MarkSeconds) && MarkSeconds > 0
            && float.IsFinite(RopeSpeed) && RopeSpeed > 0 && float.IsFinite(HaulSpeed) && HaulSpeed > 0
            && float.IsFinite(AttachSeconds) && AttachSeconds > 0 && AttachSeconds <= 60
            && Clearance >= .01f && Clearance <= .15f && RopeRadius >= .01f && RopeRadius <= .05f
            && MaximumMoveStep >= .02f && MaximumMoveStep <= .25f
            && SpringAcceleration >= 30 && SpringAcceleration <= 200 && SpringDamping >= 4 && SpringDamping <= 30
            && GuideLead >= .2f && GuideLead <= 1 && MaximumLoadSpeed >= 1 && MaximumLoadSpeed <= 5
            && MaximumSpin >= 1 && MaximumSpin <= 5
            && SearchNodesPerFrame >= 32 && SearchNodesPerFrame <= 2048
            && MaximumSearchNodes >= SearchNodesPerFrame && MaximumSearchNodes <= 500000;
    }
}
