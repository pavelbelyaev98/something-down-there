using System;
using UnityEngine;

namespace SomethingDownThere
{
    [Serializable]
    public sealed class ShovelProfile
    {
        [Min(0.2f)] public float Radius;
        [Min(0.1f)] public float CadenceMultiplier;
        [Min(0f)] public float ReachBonus;

        public ShovelProfile(float radius, float cadence, float reach)
        { Radius = radius; CadenceMultiplier = cadence; ReachBonus = reach; }

        public ShovelProfile Clone() => new ShovelProfile(Radius, CadenceMultiplier, ReachBonus);

    }

    // Owns progression, independent of developer overrides and future purchasing.
    public sealed class ShovelState
    {
        private readonly ShovelProfile[] profiles;
        public int Level { get; private set; } = 1;
        public int LevelCount => profiles.Length;
        public ShovelProfile Current => GetProfile(Level);

        public ShovelState(ShovelProfile[] source)
        {
            if (source == null || source.Length != EquipmentProgression.LevelCount)
                throw new ArgumentException("Provide all equipment levels.", nameof(source));
            profiles = new ShovelProfile[source.Length];
            float previousRadius = 0, previousReach = -1;
            for (int i = 0; i < source.Length; i++)
            {
                var p = source[i];
                if (p == null || !ExcavationGrid.Finite(p.Radius) || p.Radius < 0.2f || p.Radius > 4
                    || p.Radius <= previousRadius || !ExcavationGrid.Finite(p.CadenceMultiplier)
                    || p.CadenceMultiplier < 0.1f || !ExcavationGrid.Finite(p.ReachBonus) || p.ReachBonus < 0
                    || p.ReachBonus <= previousReach)
                    throw new ArgumentException("Shovel levels need increasing radii/reach and positive cadence.", nameof(source));
                profiles[i] = new ShovelProfile(p.Radius, p.CadenceMultiplier, p.ReachBonus);
                previousRadius = p.Radius;
                previousReach = p.ReachBonus;
            }
        }

        public ShovelProfile GetProfile(int level)
        {
            if (level < 1 || level > LevelCount) throw new ArgumentOutOfRangeException(nameof(level));
            return profiles[level - 1];
        }

        public bool TryUpgradeTo(int level)
        {
            if (level != Level + 1 || level > LevelCount) return false;
            Level = level;
            return true;
        }
    }
}
