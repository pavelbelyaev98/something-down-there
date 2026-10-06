using UnityEngine;

namespace SomethingDownThere
{
    // A burst of crystal shards (115): small bright flecks in a crystal's colour flying from a point and falling, gone in
    // about a second. A chip throws a few; a breaking crystal throws many.
    internal static class CavernShatter
    {
        private const float Life = 1.1f, Speed = 2.6f, Size = .05f, Gravity = .9f;

        internal static void Burst(Material material, Vector3 at, float radius, Color colour, int count, Transform parent)
        {
            if (material == null) return;
            var go = new GameObject("Crystal shards");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var shards = go.AddComponent<ParticleSystem>();
            shards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = shards.main;
            main.duration = .1f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(Life * .5f, Life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Speed * .4f, Speed);
            main.startSize = new ParticleSystem.MinMaxCurve(Size * .5f, Size);
            main.startColor = colour;
            main.gravityModifier = Gravity;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = shards.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = shards.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shards.Play();
        }
    }
}
