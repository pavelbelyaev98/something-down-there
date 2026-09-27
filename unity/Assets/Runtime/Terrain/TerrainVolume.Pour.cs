using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // A tool cut that undercuts gravel lets its section pour (ExcavationGrid.TryPourGravel).
    // Debris is visual only: pooled chips and dust fall through the emptied section.
    public sealed partial class TerrainVolume
    {
        [SerializeField] private Material pourChipsMaterial, pourDustMaterial;
        private ParticleSystem pourChips, pourDust;
        private readonly System.Random pourRandom = new System.Random(4211);
        // Poured volume (m³) and world centre: audio (028) and C4 (026) listen here.
        public event Action<float, Vector3> Poured;
        public float LastPourVolume { get; private set; }

        private void PourUndercutGravel(BoundsInt cut)
        {
            LastPourVolume = 0;
            if (!grid.TryPourGravel(cut, out var changed, out var centre)) return;
            LastPourVolume = grid.LastRemovedVolume;
            CommitEdit(changed);
            EmitPour(changed, LastPourVolume);
            Poured?.Invoke(LastPourVolume, transform.TransformPoint(centre));
        }

        // Pooled debris for pours and break-ins; none without the authored materials.
        private bool EnsureDebrisParticles()
        {
            if (pourChips == null && pourChipsMaterial != null) pourChips = CreatePourParticles("Gravel pour chips", pourChipsMaterial, false);
            if (pourDust == null && pourDustMaterial != null) pourDust = CreatePourParticles("Gravel pour dust", pourDustMaterial, true);
            return pourChips != null && pourDust != null;
        }

        private void EmitPour(BoundsInt samples, float volume)
        {
            if (!EnsureDebrisParticles()) return;
            Vector3 low = (Vector3)samples.min * cellSize, high = (Vector3)samples.max * cellSize;
            int chips = Mathf.Clamp(Mathf.RoundToInt(volume * 30), 24, 220), puffs = chips / 5;
            for (int i = 0; i < chips + puffs; i++)
            {
                bool dust = i >= chips;
                var local = new Vector3(Random01(low.x, high.x), Random01(low.y, high.y), Random01(low.z, high.z));
                var emit = new ParticleSystem.EmitParams
                {
                    position = transform.TransformPoint(local),
                    velocity = new Vector3(Random01(-.5f, .5f), dust ? Random01(-.3f, .1f) : Random01(-2.4f, -.6f), Random01(-.5f, .5f)),
                    startLifetime = dust ? Random01(.8f, 1.4f) : Random01(.5f, 1.1f),
                    startSize = dust ? Random01(.25f, .5f) : Random01(.03f, .08f),
                    rotation = Random01(0, 360),
                    startColor = dust ? new Color(.42f, .38f, .32f, .22f)
                        : Color.Lerp(new Color(.22f, .2f, .18f), new Color(.6f, .55f, .47f), Random01(0, 1))
                };
                (dust ? pourDust : pourChips).Emit(emit, 1);
            }
        }

        private float Random01(float min, float max) => Mathf.Lerp(min, max, (float)pourRandom.NextDouble());

        private ParticleSystem CreatePourParticles(string label, Material material, bool dust)
        {
            var root = new GameObject(label) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = dust ? 64 : 256;
            main.gravityModifier = dust ? .06f : .9f;
            main.startSpeed = 0;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var collision = particles.collision; collision.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(dust ? 0 : 1, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, dust ? 1.8f : .5f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particles.Play();
            return particles;
        }
    }
}
