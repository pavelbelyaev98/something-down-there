using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    public sealed partial class SalvageWinch
    {
        [SerializeField] private Material soilChipsMaterial, soilDustMaterial;
        private ParticleSystem soilChips, soilDust;
        private readonly System.Random breakRandom = new System.Random(1873);

        private void InitializeBreakFeedback()
        {
            if (soilChips == null && soilChipsMaterial != null)
                soilChips = CreateBreakParticles("Recovery soil crumbs", soilChipsMaterial, false);
            if (soilDust == null && soilDustMaterial != null)
                soilDust = CreateBreakParticles("Recovery soil dust", soilDustMaterial, true);
        }

        private ParticleSystem CreateBreakParticles(string label, Material material, bool dust)
        {
            var root = new GameObject(label) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = dust ? 64 : 192;
            main.gravityModifier = dust ? .08f : .85f;
            main.startSpeed = 0;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var collision = particles.collision; collision.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(dust ? 0 : 1, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, dust ? 1.8f : .4f));
            var rotation = particles.rotationOverLifetime; rotation.enabled = !dust;
            rotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
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

        private void EmitSoilBreak(Vector3 point, Vector3 normal, float removed)
        {
            if (removed <= 0 || settings.BreakParticleIntensity <= 0) return;
            InitializeBreakFeedback();
            if (soilChips == null || soilDust == null) return;
            float amount = Mathf.Clamp(Mathf.Sqrt(removed / .08f), .5f, 1.5f) * settings.BreakParticleIntensity;
            Quaternion face = Quaternion.LookRotation(normal);
            Vector3 inherited = LoadBody.linearVelocity * .15f;
            int chips = Mathf.RoundToInt(22 * amount), puffs = Mathf.RoundToInt(7 * amount);
            for (int i = 0; i < chips + puffs; i++)
            {
                bool dust = i >= chips;
                Vector3 spread = face * new Vector3(BreakRandom(-1, 1), BreakRandom(-1, 1), 0);
                var emit = new ParticleSystem.EmitParams
                {
                    position = point + normal * .07f + spread * .18f,
                    velocity = normal * (dust ? BreakRandom(.25f, .65f) : BreakRandom(1f, 2.8f))
                        + spread * (dust ? .2f : 1.2f) + inherited,
                    startLifetime = dust ? BreakRandom(.45f, .8f) : BreakRandom(.5f, .9f),
                    startSize = dust ? BreakRandom(.18f, .36f) : BreakRandom(.025f, .065f),
                    rotation = BreakRandom(0, 360),
                    startColor = dust ? new Color(.44f, .29f, .14f, .24f)
                        : Color.Lerp(new Color(.24f, .12f, .045f), new Color(.62f, .39f, .17f), BreakRandom(0, 1))
                };
                (dust ? soilDust : soilChips).Emit(emit, 1);
            }
        }

        private float BreakRandom(float min, float max) => Mathf.Lerp(min, max, (float)breakRandom.NextDouble());

        private void ClearBreakFeedback()
        {
            if (soilChips != null) soilChips.Clear();
            if (soilDust != null) soilDust.Clear();
        }
    }
}
