using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // What a cavern crystal throws when the tool works it and when it breaks (115, user 2026-10-06: "higher quality, not
    // just cheap blocks"): faceted shards of crystal in its colour that tumble and fall, glints (the Crystal Caverns
    // pack's sparkle) that flash and fade, and a soft puff of its dust (the pack's dust flipbook). Three systems, emitted
    // into, owned by the cavern scenery. Visual only: nothing collides.
    internal sealed class CavernShatter
    {
        private const int DustFrames = 8;
        private readonly ParticleSystem shards, glints, dust;
        private readonly System.Random random = new System.Random(7741);

        internal CavernShatter(Transform parent, CavernDressing dressing)
        {
            shards = Create(parent, "Crystal shards", dressing.Shards, 400, 1.1f, true);
            glints = Create(parent, "Crystal glints", dressing.Glints, 200, 0, false);
            dust = Create(parent, "Crystal dust", dressing.Dust, 64, -.02f, false);
            var sheet = dust.textureSheetAnimation;
            sheet.enabled = true; sheet.numTilesX = DustFrames; sheet.numTilesY = DustFrames;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1));
            var drag = dust.limitVelocityOverLifetime; drag.enabled = true; drag.limit = 100; drag.drag = 3;
            var fade = glints.colorOverLifetime; fade.enabled = true;
            var curve = new Gradient();
            curve.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) });
            fade.color = curve;
            var shrink = glints.sizeOverLifetime; shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .6f, 1, .1f));
            var spin = shards.rotationOverLifetime; spin.enabled = true; spin.separateAxes = true;
            spin.x = spin.y = spin.z = new ParticleSystem.MinMaxCurve(-6, 6);
            var thin = shards.sizeOverLifetime; thin.enabled = true;
            thin.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.8f, 1), new Keyframe(1, 0)));
        }

        private static ParticleSystem Create(Transform parent, string label, Material material, int capacity, float gravity, bool mesh)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false; main.loop = true; main.startSpeed = 0;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = capacity;
            main.gravityModifier = gravity;
            main.startRotation3D = mesh; main.startSize3D = mesh;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (mesh)
            {
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.SetMeshes(new[] { ShardMesh(4, 1.8f, 11), ShardMesh(6, 2.6f, 23), ShardMesh(5, 1.4f, 37) });
                renderer.alignment = ParticleSystemRenderSpace.World;
            }
            else renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            system.Play();
            return system;
        }

        // A crystal splinter: a prism of `sides` faces tapering to a point at each end, `length` times as long as wide,
        // its faces flat-shaded so each catches the light; a seeded twist makes each shape its own.
        private static Mesh ShardMesh(int sides, float length, int seed)
        {
            var lumps = new System.Random(seed);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var ring = new Vector3[sides];
            float half = length * .5f;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides + (float)lumps.NextDouble() * .3f, r = .5f * (.75f + .5f * (float)lumps.NextDouble());
                ring[i] = new Vector3(Mathf.Cos(angle) * r, (float)(lumps.NextDouble() - .5) * .3f, Mathf.Sin(angle) * r);
            }
            var top = new Vector3(0, half, 0) + new Vector3((float)lumps.NextDouble() - .5f, 0, (float)lumps.NextDouble() - .5f) * .2f;
            var bottom = new Vector3(0, -half * .6f, 0);
            var triangles = new System.Collections.Generic.List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c) { vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(vertices.Count - 3); triangles.Add(vertices.Count - 2); triangles.Add(vertices.Count - 1); }
            for (int i = 0; i < sides; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % sides];
                Face(a, top, b);
                Face(b, bottom, a);
            }
            var shard = new Mesh { name = "Crystal shard", hideFlags = HideFlags.DontSave };
            shard.SetVertices(vertices); shard.SetTriangles(triangles, 0);
            shard.RecalculateNormals(); shard.RecalculateBounds();
            return shard;
        }

        private float Range(float a, float b) => a + (b - a) * (float)random.NextDouble();
        private Vector3 Spray(Vector3 normal, float spread)
        {
            var away = Random.onUnitSphere;
            if (Vector3.Dot(away, normal) < 0) away = -away;
            return Vector3.Slerp(normal, away, spread).normalized;
        }

        // A burst at a point on a crystal: count shards and a few glints flying out round normal, and a puff of dust;
        // radius spreads where they start (a breaking crystal throws from all of it).
        internal void Burst(Vector3 at, Vector3 normal, float radius, Color colour, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float size = Range(.02f, .07f);
                shards.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * radius,
                    velocity = Spray(normal, .7f) * Range(1.2f, 3.6f),
                    startSize3D = new Vector3(size, size * Range(.8f, 1.3f), size),
                    rotation3D = new Vector3(Range(0, 360), Range(0, 360), Range(0, 360)),
                    startColor = Color.Lerp(colour, Color.white, Range(0, .35f)),
                    startLifetime = Range(.9f, 1.6f),
                }, 1);
            }
            for (int i = 0; i < 2 + count / 4; i++)
                glints.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * radius * 1.1f,
                    velocity = Spray(normal, .9f) * Range(.1f, .5f),
                    startSize = Range(.12f, .3f) * (1 + radius),
                    rotation = Range(0, 360),
                    startColor = Color.Lerp(colour, Color.white, .5f),
                    startLifetime = Range(.35f, .8f),
                }, 1);
            for (int i = 0; i < 1 + count / 20; i++)
                dust.Emit(new ParticleSystem.EmitParams
                {
                    position = at + Random.insideUnitSphere * radius * .6f,
                    velocity = Spray(normal, .5f) * Range(.3f, .9f),
                    startSize = Range(.5f, .9f) * (1 + radius),
                    rotation = Range(0, 360),
                    startColor = Color.Lerp(colour, new Color(.75f, .75f, .78f), .6f),
                    startLifetime = Range(1.2f, 2f),
                }, 1);
        }
    }
}
