using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // After a tool cut, ground that lets go does (ExcavationGrid.TryRelease): a crack breaks
    // along its band, undercut gravel pours, undercut backfill and thin soil slump. Debris is
    // visual only: pooled chips and dust fall through the emptied ground.
    public sealed partial class TerrainVolume
    {
        [SerializeField] private Material pourChipsMaterial, pourDustMaterial;
        private ParticleSystem pourChips, pourDust;
        private readonly System.Random pourRandom = new System.Random(4211);
        private readonly float[] releasedVolumes = new float[4];
        // Released kind, volume (m³) and world centre: audio (028) and C4 (026) listen here.
        public event Action<GroundRelease, float, Vector3> Released;
        // Volume each kind released after the last tool cut.
        public float ReleasedVolume(GroundRelease kind) => releasedVolumes[(int)kind];

        private void ReleaseGround(BoundsInt cut, float toolRadius)
        {
            System.Array.Clear(releasedVolumes, 0, releasedVolumes.Length);
            // A crack breaks further with a bigger tool; loose ground by its own reach.
            Release(GroundRelease.CrackBreak, cut, Mathf.Clamp(toolRadius * 1.5f, .4f, 1.2f));
            Release(GroundRelease.GravelPour, cut, ExcavationGrid.PourReach);
            Release(GroundRelease.BackfillSlump, cut, ExcavationGrid.BackfillSlumpReach);
            Release(GroundRelease.SoilSlump, cut, ExcavationGrid.SoilSlumpReach);
        }

        private void Release(GroundRelease kind, BoundsInt cut, float reach)
        {
            if (!grid.TryRelease(kind, cut, reach, out var changed, out var centre)) return;
            float volume = releasedVolumes[(int)kind] = grid.LastRemovedVolume;
            CommitEdit(changed);
            EmitRelease(kind, changed, volume);
            Released?.Invoke(kind, volume, transform.TransformPoint(centre));
        }

        // Pooled debris for pours and break-ins; none without the authored materials.
        private bool EnsureDebrisParticles()
        {
            if (pourChips == null && pourChipsMaterial != null) pourChips = CreatePourParticles("Gravel pour chips", pourChipsMaterial, false);
            if (pourDust == null && pourDustMaterial != null) pourDust = CreatePourParticles("Gravel pour dust", pourDustMaterial, true);
            return pourChips != null && pourDust != null;
        }

        private static readonly Color[][] ReleaseColours =
        {
            new[] { new Color(.22f, .2f, .18f), new Color(.6f, .55f, .47f), new Color(.42f, .38f, .32f, .22f) },
            new[] { new Color(.2f, .15f, .1f), new Color(.45f, .33f, .22f), new Color(.4f, .31f, .22f, .22f) },
            new[] { new Color(.25f, .17f, .1f), new Color(.5f, .36f, .22f), new Color(.45f, .35f, .25f, .22f) },
            new[] { new Color(.45f, .45f, .44f), new Color(.8f, .78f, .72f), new Color(.6f, .58f, .55f, .22f) }
        };

        private void EmitRelease(GroundRelease kind, BoundsInt samples, float volume)
        {
            if (!EnsureDebrisParticles()) return;
            var colours = ReleaseColours[(int)kind];
            float chipScale = kind == GroundRelease.CrackBreak ? 1.3f : 1f;
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
                    startSize = dust ? Random01(.25f, .5f) : Random01(.03f, .08f) * chipScale,
                    rotation = Random01(0, 360),
                    startColor = dust ? colours[2] : Color.Lerp(colours[0], colours[1], Random01(0, 1))
                };
                (dust ? pourDust : pourChips).Emit(emit, 1);
            }
        }

        // Every stroke throws a little of its ground: colour, chip size and dust differ per ground,
        // so each one reads and feels different at the tool (concept 09 section 4).
        private static readonly (Color low, Color high, float size, float dust)[] StrokeLooks =
        {
            (new Color(.26f, .18f, .11f), new Color(.46f, .33f, .2f), .035f, .25f),   // Soil
            (new Color(.5f, .28f, .14f), new Color(.72f, .42f, .22f), .06f, .08f),    // Clay: fewer, bigger clumps
            (new Color(.3f, .31f, .32f), new Color(.56f, .57f, .58f), .03f, .45f),    // Rock: shards and dust
            (new Color(.3f, .28f, .25f), new Color(.62f, .58f, .5f), .045f, .15f),    // Gravel: stones
            (new Color(.55f, .55f, .53f), new Color(.82f, .81f, .78f), .025f, .6f),   // Concrete: grit and dust
            (new Color(.36f, .42f, .46f), new Color(.56f, .63f, .67f), .05f, .05f),   // Pond clay: smooth flakes
            (new Color(.5f, .5f, .48f), new Color(.84f, .82f, .76f), .04f, .4f),      // Fractured rock
            (new Color(.55f, .55f, .53f), new Color(.85f, .84f, .8f), .035f, .5f),    // Fractured concrete
            (new Color(.5f, .5f, .48f), new Color(.84f, .82f, .76f), .04f, .4f),      // Crack
            (new Color(.18f, .14f, .1f), new Color(.55f, .4f, .26f), .045f, .2f)      // Backfill: mixed
        };

        private void EmitStroke(TerrainMaterialId material, Vector3 point, Vector3 normal, float volume)
        {
            if (!EnsureDebrisParticles()) return;
            var look = StrokeLooks[Mathf.Clamp((int)material, 0, StrokeLooks.Length - 1)];
            int chips = Mathf.Clamp(Mathf.RoundToInt(volume * 400f), 2, 16), puffs = Mathf.RoundToInt(chips * look.dust);
            for (int i = 0; i < chips + puffs; i++)
            {
                bool dust = i >= chips;
                var jitter = new Vector3(Random01(-.12f, .12f), Random01(-.12f, .12f), Random01(-.12f, .12f));
                var emit = new ParticleSystem.EmitParams
                {
                    position = point + normal * .05f + jitter,
                    velocity = normal * Random01(.4f, 1.4f) + new Vector3(Random01(-.4f, .4f), dust ? Random01(-.1f, .2f) : Random01(-.2f, .6f), Random01(-.4f, .4f)),
                    startLifetime = dust ? Random01(.6f, 1.1f) : Random01(.35f, .8f),
                    startSize = dust ? Random01(.15f, .3f) : Random01(.6f, 1.4f) * look.size,
                    rotation = Random01(0, 360),
                    startColor = dust ? new Color(look.high.r, look.high.g, look.high.b, .16f) : Color.Lerp(look.low, look.high, Random01(0, 1))
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
