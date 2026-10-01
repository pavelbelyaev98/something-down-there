using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // Soil torn out by the rope, only where ground is actually removed: tumbling clods that land on the
    // dug floor and sink away, a spray of fine crumbs, a dust cloud that hangs and drifts with the surge,
    // and a short trickle from the broken face. All are coloured by the ground there and lit like it;
    // bigger breaks and a harder-driven machine throw more. Visual only: nothing collides.
    public sealed partial class SalvageCrane
    {
        [SerializeField] private Material soilChipsMaterial, soilDustMaterial, soilClodsMaterial;
        private ParticleSystem soilChips, soilDust, soilClods, soilRest;
        private Mesh clodMesh;
        private readonly System.Random breakRandom = new System.Random(1873);
        // Particles due later: a clod coming to rest where it lands, crumbs trickling off the break.
        private readonly List<(float time, ParticleSystem system, ParticleSystem.EmitParams emit)> debrisDue
            = new List<(float, ParticleSystem, ParticleSystem.EmitParams)>();

        // Dark and light ends of each ground's broken soil, from its albedo texture and tint
        // (Content/GroundTextures): crumbs and clods show the ground they came out of.
        private static (Color dark, Color light) DebrisColors(TerrainMaterialId ground) => ground switch
        {
            TerrainMaterialId.Clay => (new Color(.37f, .22f, .14f), new Color(.62f, .4f, .27f)),
            TerrainMaterialId.PondClay => (new Color(.25f, .24f, .23f), new Color(.45f, .43f, .4f)),
            TerrainMaterialId.Gravel => (new Color(.31f, .29f, .27f), new Color(.51f, .48f, .44f)),
            TerrainMaterialId.Rock or TerrainMaterialId.FracturedRock => (new Color(.2f, .2f, .2f), new Color(.4f, .39f, .38f)),
            TerrainMaterialId.Concrete or TerrainMaterialId.FracturedConcrete => (new Color(.5f, .5f, .5f), new Color(.63f, .63f, .62f)),
            TerrainMaterialId.Backfill or TerrainMaterialId.Crack => (new Color(.25f, .19f, .13f), new Color(.45f, .36f, .27f)),
            _ => (new Color(.3f, .21f, .14f), new Color(.55f, .4f, .27f))
        };

        private void InitializeBreakFeedback()
        {
            if (soilChips == null && soilChipsMaterial != null) soilChips = CreateBreakParticles("Recovery soil crumbs", soilChipsMaterial, Debris.Crumbs);
            if (soilDust == null && soilDustMaterial != null) soilDust = CreateBreakParticles("Recovery soil dust", soilDustMaterial, Debris.Dust);
            if (soilClods == null && soilClodsMaterial != null) soilClods = CreateBreakParticles("Recovery soil clods", soilClodsMaterial, Debris.Clods);
            if (soilRest == null && soilClodsMaterial != null) soilRest = CreateBreakParticles("Recovery settling clods", soilClodsMaterial, Debris.Settling);
        }

        private enum Debris { Crumbs, Dust, Clods, Settling }

        private ParticleSystem CreateBreakParticles(string label, Material material, Debris kind)
        {
            bool clod = kind == Debris.Clods || kind == Debris.Settling;
            var root = new GameObject(label) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = kind switch { Debris.Dust => 96, Debris.Crumbs => 256, _ => 96 };
            main.gravityModifier = kind switch { Debris.Dust => -.015f, Debris.Crumbs => .85f, Debris.Clods => 1f, _ => 0f };
            main.startSpeed = 0;
            main.startRotation3D = clod;
            main.startSize3D = clod;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var collision = particles.collision; collision.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = kind != Debris.Clods;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, kind switch
            {
                // Dust swells in quickly and thins out slowly; crumbs vanish at the end of their fall.
                Debris.Dust => new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.55f, .55f), new GradientAlphaKey(0, 1) },
                Debris.Settling => new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) },
                _ => new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) }
            });
            color.color = fade;
            var size = particles.sizeOverLifetime;
            size.enabled = kind != Debris.Clods;
            size.size = kind switch
            {
                Debris.Dust => new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .45f), new Keyframe(.3f, 1.4f), new Keyframe(1, 2.4f))),
                // A landed clod holds, then sinks away into the floor.
                Debris.Settling => new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 1), new Keyframe(.55f, 1), new Keyframe(1, 0))),
                _ => new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .4f))
            };
            var rotation = particles.rotationOverLifetime; rotation.enabled = kind == Debris.Crumbs;
            rotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            if (clod)
            {
                clodMesh ??= ClodMesh();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = clodMesh;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                renderer.alignment = ParticleSystemRenderSpace.World;
            }
            else renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particles.Play();
            return particles;
        }

        // A lumpy, flattened clod of earth: a once-subdivided icosahedron with its corners pushed in and
        // out (art/soil-debris). Particles size, turn and stretch it, so one shape reads as many.
        private static Mesh ClodMesh()
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            int[] faces =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
            var midpoints = new Dictionary<long, int>();
            int Middle(int a, int b)
            {
                long key = Mathf.Min(a, b) * 100000L + Mathf.Max(a, b);
                if (midpoints.TryGetValue(key, out int index)) return index;
                vertices.Add((vertices[a] + vertices[b]) * .5f);
                return midpoints[key] = vertices.Count - 1;
            }
            var triangles = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2], ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                triangles.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            var lumps = new System.Random(4021);
            var flat = new List<Vector3>(); var flatTriangles = new List<int>();
            var radius = new float[vertices.Count];
            for (int i = 0; i < radius.Length; i++) radius[i] = .5f * (.72f + .5f * (float)lumps.NextDouble());
            Vector3 Corner(int i) { var v = vertices[i].normalized * radius[i]; v.y *= .72f; return v; }
            // Facetted: each face its own normal, so the clod reads as broken earth, not a pebble.
            for (int i = 0; i < triangles.Count; i++) { flat.Add(Corner(triangles[i])); flatTriangles.Add(i); }
            var mesh = new Mesh { name = "Soil clod", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(flat); mesh.SetTriangles(flatTriangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void EmitSoilBreak(Vector3 point, Vector3 normal, float removed)
        {
            if (removed <= 0 || settings.BreakParticleIntensity <= 0) return;
            InitializeBreakFeedback();
            if (soilChips == null || soilDust == null || soilClods == null || soilRest == null) return;
            float amount = Mathf.Clamp(Mathf.Sqrt(removed / .08f), .5f, 1.6f) * settings.BreakParticleIntensity * (1 + .4f * Throttle01);
            var (dark, light) = DebrisColors(terrain.MaterialAt(point - normal * (settings.ContactBreakDepth + terrain.CellSize)));
            // Dust is the same earth, paler and greyer as a fine cloud.
            Color dustColor = Color.Lerp(Color.Lerp(dark, light, .7f), new Color(.5f, .48f, .45f), .35f);
            Quaternion face = Quaternion.LookRotation(normal);
            Vector3 inherited = LoadBody.linearVelocity * .15f;
            float now = Time.time;

            // Clods thrown off the break, each landing where its flight first meets the ground.
            int clods = Mathf.RoundToInt(7 * amount);
            for (int i = 0; i < clods; i++)
            {
                Vector3 spread = face * new Vector3(BreakRandom(-1, 1), BreakRandom(-1, 1), 0);
                float size = BreakRandom(.04f, .11f) * Mathf.Lerp(1, 1.35f, Throttle01);
                var emit = new ParticleSystem.EmitParams
                {
                    position = point + normal * (.05f + size) + spread * .2f,
                    velocity = normal * BreakRandom(1.2f, 3f) + spread * 1.4f + Vector3.up * BreakRandom(.3f, 1.4f) + inherited,
                    startSize3D = new Vector3(size * BreakRandom(.8f, 1.25f), size * BreakRandom(.7f, 1f), size * BreakRandom(.8f, 1.25f)),
                    rotation3D = new Vector3(BreakRandom(0, 360), BreakRandom(0, 360), BreakRandom(0, 360)),
                    angularVelocity3D = new Vector3(BreakRandom(-540, 540), BreakRandom(-540, 540), BreakRandom(-540, 540)),
                    startColor = Color.Lerp(dark, light, BreakRandom(.1f, .8f))
                };
                float flight = Landing(emit.position, emit.velocity, size * .5f, out Vector3 rest, out bool floor);
                emit.startLifetime = flight;
                soilClods.Emit(emit, 1);
                if (!floor) continue;
                var settle = emit;
                settle.position = rest; settle.velocity = Vector3.down * (size * .35f);
                settle.rotation3D = emit.rotation3D + emit.angularVelocity3D * flight;
                settle.angularVelocity3D = Vector3.zero;
                settle.startLifetime = BreakRandom(1.6f, 3f);
                debrisDue.Add((now + flight, soilRest, settle));
            }

            // Fine crumbs sprayed out with the clods.
            int chips = Mathf.RoundToInt(24 * amount);
            for (int i = 0; i < chips; i++)
            {
                Vector3 spread = face * new Vector3(BreakRandom(-1, 1), BreakRandom(-1, 1), 0);
                soilChips.Emit(new ParticleSystem.EmitParams
                {
                    position = point + normal * .07f + spread * .2f,
                    velocity = normal * BreakRandom(1f, 3.2f) + spread * 1.3f + inherited,
                    startLifetime = BreakRandom(.45f, .85f),
                    startSize = BreakRandom(.025f, .06f),
                    rotation = BreakRandom(0, 360),
                    startColor = Color.Lerp(dark, light, BreakRandom(0, 1))
                }, 1);
            }

            // A dust cloud that swells off the break, hangs and drifts on with the surging load.
            int puffs = Mathf.RoundToInt(11 * amount);
            for (int i = 0; i < puffs; i++)
            {
                Vector3 spread = face * new Vector3(BreakRandom(-1, 1), BreakRandom(-1, 1), 0);
                soilDust.Emit(new ParticleSystem.EmitParams
                {
                    position = point + normal * BreakRandom(.05f, .3f) + spread * .25f,
                    velocity = normal * BreakRandom(.2f, .7f) + spread * .3f + LoadBody.linearVelocity * .25f,
                    startLifetime = BreakRandom(1.3f, 2.6f),
                    startSize = BreakRandom(.25f, .55f),
                    rotation = BreakRandom(0, 360),
                    startColor = new Color(dustColor.r, dustColor.g, dustColor.b, BreakRandom(.16f, .3f))
                }, 1);
            }

            // The broken face keeps crumbling for a moment after the chunk tears away.
            int trickle = Mathf.RoundToInt(14 * amount);
            Vector3 across = Vector3.ProjectOnPlane(Vector3.up, normal).sqrMagnitude > .01f ? Vector3.ProjectOnPlane(Vector3.up, normal).normalized : face * Vector3.up;
            Vector3 along = Vector3.Cross(normal, across);
            float reach = settings.ContactBreakRadius;
            for (int i = 0; i < trickle; i++)
            {
                debrisDue.Add((now + BreakRandom(.08f, 1f), soilChips, new ParticleSystem.EmitParams
                {
                    // From the upper part of the broken patch, falling away from the face.
                    position = point + across * (reach * BreakRandom(.1f, .9f)) + along * (reach * BreakRandom(-.8f, .8f)) - normal * BreakRandom(0, .1f),
                    velocity = normal * BreakRandom(.05f, .35f) + Vector3.down * BreakRandom(0, .3f),
                    startLifetime = BreakRandom(.45f, .8f),
                    startSize = BreakRandom(.015f, .04f),
                    rotation = BreakRandom(0, 360),
                    startColor = Color.Lerp(dark, light, BreakRandom(0, 1))
                }));
            }
        }

        // Flight time of a thrown clod until it meets the ground, where it then rests, and whether that
        // is a floor it can lie on (walls and ceilings just take it). The ground's density is marched
        // along the arc: visual only, nothing collides.
        private float Landing(Vector3 position, Vector3 velocity, float radius, out Vector3 rest, out bool floor)
        {
            const float step = .03f, longest = 1.4f;
            Vector3 gravity = Physics.gravity;
            Vector3 previous = position;
            for (float t = step; t <= longest; t += step)
            {
                Vector3 next = position + velocity * t + gravity * (.5f * t * t);
                if (terrain.SignedDensity(next) > -radius)
                {
                    rest = previous;
                    float e = terrain.CellSize * .5f;
                    floor = terrain.SignedDensity(previous + Vector3.down * (radius + e)) > terrain.SignedDensity(previous + Vector3.up * (radius + e));
                    return t - step * .5f;
                }
                previous = next;
            }
            rest = previous; floor = false;
            return longest;
        }

        // Particles that were due: landed clods and the trickle.
        private void EmitDueDebris()
        {
            if (debrisDue.Count == 0) return;
            float now = Time.time;
            for (int i = debrisDue.Count - 1; i >= 0; i--)
            {
                var due = debrisDue[i];
                if (due.time > now) continue;
                if (due.system != null) due.system.Emit(due.emit, 1);
                debrisDue.RemoveAt(i);
            }
        }

        private float BreakRandom(float min, float max) => Mathf.Lerp(min, max, (float)breakRandom.NextDouble());

        private void ClearBreakFeedback()
        {
            debrisDue.Clear();
            if (soilChips != null) soilChips.Clear();
            if (soilDust != null) soilDust.Clear();
            if (soilClods != null) soilClods.Clear();
            if (soilRest != null) soilRest.Clear();
        }
    }
}
