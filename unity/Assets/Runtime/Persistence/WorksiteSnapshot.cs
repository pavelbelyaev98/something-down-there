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

    // An armed C4 charge (026): stuck to the ground, or loose where it fell after its ground was dug away.
    public sealed class ChargeSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Stuck;
    }

    public sealed class WorksiteSnapshot
    {
        public LampSnapshot[] Lamps = Array.Empty<LampSnapshot>();
        public MarkSnapshot[] Marks = Array.Empty<MarkSnapshot>();
        public ChargeSnapshot[] Charges = Array.Empty<ChargeSnapshot>();

        public void Validate()
        {
            WorldSnapshot.Require(Lamps != null && Lamps.Length <= EquipmentProgression.MaximumLamps
                && Marks != null && Marks.Length <= WorksiteTools.MaximumMarks
                && Charges != null && Charges.Length <= EquipmentProgression.MaximumCharges, "Invalid worksite equipment count.");
            foreach (var charge in Charges)
                WorldSnapshot.Require(charge != null && WorldSnapshot.Valid(charge.Position) && WorldSnapshot.Valid(charge.Rotation), "Invalid C4 charge.");
            var slots = new HashSet<int>();
            foreach (var lamp in Lamps)
                WorldSnapshot.Require(lamp != null && lamp.Slot >= 0 && lamp.Slot < EquipmentProgression.MaximumLamps && slots.Add(lamp.Slot)
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
