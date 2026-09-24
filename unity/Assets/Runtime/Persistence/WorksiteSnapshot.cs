using System;
using System.Collections.Generic;
using UnityEngine;

namespace SomethingDownThere
{
    public enum WorldMarkKind { Arrow, Home, ReturnHere }

    public sealed class LampSnapshot
    {
        public int Slot;
        public Vector3 Position, LinearVelocity, AngularVelocity, SupportPoint, SupportNormal;
        public Quaternion Rotation;
        public bool Anchored;
    }

    public sealed class MarkSnapshot
    {
        public WorldMarkKind Kind;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    public sealed class WorksiteSnapshot
    {
        public LampSnapshot[] Lamps = Array.Empty<LampSnapshot>();
        public MarkSnapshot[] Marks = Array.Empty<MarkSnapshot>();

        public void Validate()
        {
            WorldSnapshot.Require(Lamps != null && Lamps.Length <= WorksiteTools.LampCapacity
                && Marks != null && Marks.Length <= WorksiteTools.MaximumMarks, "Invalid worksite equipment count.");
            var slots = new HashSet<int>();
            foreach (var lamp in Lamps)
                WorldSnapshot.Require(lamp != null && lamp.Slot >= 0 && lamp.Slot < WorksiteTools.LampCapacity && slots.Add(lamp.Slot)
                    && WorldSnapshot.Valid(lamp.Position) && WorldSnapshot.Valid(lamp.Rotation)
                    && WorldSnapshot.Valid(lamp.LinearVelocity) && lamp.LinearVelocity.sqrMagnitude <= 10000
                    && WorldSnapshot.Valid(lamp.AngularVelocity) && lamp.AngularVelocity.sqrMagnitude <= 10000
                    && WorldSnapshot.Valid(lamp.SupportPoint) && WorldSnapshot.Valid(lamp.SupportNormal)
                    && Mathf.Abs(lamp.SupportNormal.sqrMagnitude - 1) < .001f, "Invalid or duplicated work lamp.");
            foreach (var mark in Marks)
                WorldSnapshot.Require(mark != null && Enum.IsDefined(typeof(WorldMarkKind), mark.Kind)
                    && WorldSnapshot.Valid(mark.Position) && WorldSnapshot.Valid(mark.Rotation), "Invalid world marking.");
        }
    }
}
