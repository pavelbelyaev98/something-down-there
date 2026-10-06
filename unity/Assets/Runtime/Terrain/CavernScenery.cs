using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace SomethingDownThere
{
    // The caverns' scenery (115): solid props from CavernDressing set on each cavern's floor and walls whenever the
    // ground's layout is made (new game, load, Ground Lab), seeded from the cavern, so it is the same every time and saves
    // nothing. It is lit like the dig (ExcavationDaylight): dark until lamps reach it, except the crystal cavern's
    // glowing crystals and the light each throws in its colour (the demo's look, user 2026-10-06). A bloom fades in while
    // the view is inside the crystal cavern. Props stand where they were set; digging under one leaves it standing.
    public sealed class CavernScenery : MonoBehaviour
    {
        // Boulders and rubble in every cavern; formations and glowing crystals in the crystal cavern. Sink: the share of a
        // prop's height set into the floor or wall; Tries: positions tried per prop before it is left out.
        private const int Boulders = 4, Rubble = 3, CrystalBoulders = 5, CrystalRubble = 4, Formations = 6, GlowCrystals = 9, Tries = 12;
        private const float FloorSink = .18f, WallSink = .12f, BloomFade = 1.5f;
        // Sizes in metres (a boulder's, rubble patch's or crystal's longest side, a formation's height): the demo's
        // pieces are cave-sized, from 2 m spikes to 12 m columns. A glowing crystal shows its own texture in its colour
        // (GlowBase of it lit, GlowIntensity of it glowing), with a light of LightRange and LightIntensity.
        private static readonly Vector2 BoulderSize = new Vector2(.5f, 1.1f), RubbleSize = new Vector2(1.4f, 2.4f),
            FormationSize = new Vector2(.9f, 2.4f), CrystalSize = new Vector2(.7f, 2f);
        private const float GlowBase = .3f, GlowIntensity = .75f, LightRange = 5.5f, LightIntensity = 2.2f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private TerrainVolume terrain;
        [SerializeField] private CavernDressing dressing;
        private TerrainGround.GroundLayout dressed;
        private readonly List<GameObject> props = new List<GameObject>();
        private readonly Dictionary<GameObject, Bounds> sizes = new Dictionary<GameObject, Bounds>();
        private Volume bloom;
        private FpsPlayer viewer;
        public int PropCount => props.Count;
        public int LightCount { get; private set; }
        public float BloomWeight => bloom != null ? bloom.weight : 0;

        private void LateUpdate()
        {
            if (terrain == null || dressing == null) return;
            var layout = terrain.GroundLayout;
            if (!ReferenceEquals(layout, dressed)) Dress(layout);
            UpdateBloom();
        }

        private void Dress(TerrainGround.GroundLayout layout)
        {
            foreach (var prop in props) if (prop != null) Destroy(prop);
            props.Clear();
            LightCount = 0;
            dressed = layout;
            var daylight = terrain.GetComponent<ExcavationDaylight>();
            foreach (var cavern in layout.Caverns) DressCavern(cavern, daylight);
        }

        private void DressCavern(TerrainGround.Cavern cavern, ExcavationDaylight daylight)
        {
            var random = new System.Random((int)math.hash(cavern.Seed));
            var taken = new List<(Vector3 centre, float radius)>();
            Scatter(cavern, dressing.Boulders, cavern.Crystal ? CrystalBoulders : Boulders, BoulderSize, false, false, random, taken, daylight, null);
            Scatter(cavern, dressing.Rubble, cavern.Crystal ? CrystalRubble : Rubble, RubbleSize, false, false, random, taken, daylight, null);
            if (!cavern.Crystal) return;
            Scatter(cavern, dressing.Formations, Formations, FormationSize, true, false, random, taken, daylight, null);
            Scatter(cavern, dressing.Crystals, GlowCrystals, CrystalSize, false, true, random, taken, daylight, dressing.Glow);
        }

        // count props of the kinds, each sized within sizes (its height if byHeight, else its longest side), on the floor
        // (standing up) or on the walls and roof (pointing into the air); glow: their colours, which they shine in, with a
        // light each.
        private void Scatter(TerrainGround.Cavern cavern, GameObject[] kinds, int count, Vector2 sizes, bool byHeight, bool onWalls,
            System.Random random, List<(Vector3 centre, float radius)> taken, ExcavationDaylight daylight, Color[] glow)
        {
            if (kinds == null || kinds.Length == 0) return;
            for (int n = 0; n < count; n++)
            {
                var kind = kinds[random.Next(kinds.Length)];
                if (kind == null) continue;
                var size = Size(kind);
                float measure = byHeight ? size.size.y : Mathf.Max(size.size.x, Mathf.Max(size.size.y, size.size.z));
                float s = Mathf.Lerp(sizes.x, sizes.y, (float)random.NextDouble()) / Mathf.Max(measure, .01f);
                float height = size.size.y * s, reach = Mathf.Max(size.extents.x, size.extents.z) * s;
                for (int attempt = 0; attempt < Tries; attempt++)
                {
                    int chamber = random.Next(cavern.Centres.Length);
                    var heart = TerrainGround.CavernHeart(cavern, chamber);
                    var radii = cavern.Radii[chamber];
                    Vector3 surface, up;
                    if (onWalls)
                    {
                        float around = (float)random.NextDouble() * 360f, elevation = Mathf.Lerp(-20f, 70f, (float)random.NextDouble());
                        var (face, outward) = TerrainGround.CavernFace(cavern, heart, Quaternion.Euler(-elevation, around, 0) * Vector3.forward);
                        surface = face; up = -(Vector3)outward;
                    }
                    else
                    {
                        float angle = (float)random.NextDouble() * Mathf.PI * 2, distance = Mathf.Sqrt((float)random.NextDouble()) * .7f;
                        var start = heart + new float3(Mathf.Cos(angle) * radii.x, 0, Mathf.Sin(angle) * radii.z) * distance;
                        if (TerrainGround.CavernHollow(cavern, start) >= -.2f) continue;
                        var (face, _) = TerrainGround.CavernFace(cavern, start, new float3(0, -1, 0));
                        surface = face; up = Vector3.up;
                    }
                    var centre = surface + up * (height * .5f);
                    bool clear = true;
                    foreach (var (other, radius) in taken) clear &= Vector3.Distance(other, centre) > radius + reach;
                    if (!clear) continue;
                    taken.Add((centre, reach));
                    var turn = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0);
                    var foot = surface - up * (height * (onWalls ? WallSink : FloorSink)) - turn * (size.center - Vector3.up * size.extents.y) * s;
                    var prop = Instantiate(kind, terrain.transform.TransformPoint(foot), terrain.transform.rotation * turn, transform);
                    prop.transform.localScale = kind.transform.localScale * s;
                    props.Add(prop);
                    Color colour = glow != null && glow.Length > 0 ? glow[random.Next(glow.Length)] : default;
                    foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                    {
                        if (daylight != null) daylight.Register(renderer);
                        if (glow == null) continue;
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        block.SetColor(BaseColorId, colour * GlowBase);
                        block.SetColor(EmissionColorId, colour * GlowIntensity);
                        renderer.SetPropertyBlock(block);
                    }
                    if (glow != null) AddLight(prop.transform, terrain.transform.TransformPoint(surface + up * (height * .55f + .35f)), colour);
                    break;
                }
            }
        }

        private void AddLight(Transform parent, Vector3 position, Color colour)
        {
            var light = new GameObject("Crystal light").AddComponent<Light>();
            light.transform.SetParent(parent, true);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = colour;
            light.range = LightRange;
            light.intensity = LightIntensity;
            light.shadows = LightShadows.None;
            LightCount++;
        }

        // A prefab's bounds in its own frame, unscaled by its root.
        private Bounds Size(GameObject kind)
        {
            if (sizes.TryGetValue(kind, out var bounds)) return bounds;
            bool any = false;
            var root = kind.transform.worldToLocalMatrix;
            foreach (var filter in kind.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                var matrix = root * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(corner, Vector3.zero); any = true; } else bounds.Encapsulate(corner);
                }
            }
            sizes[kind] = bounds;
            return bounds;
        }

        private void UpdateBloom()
        {
            if (dressing.Bloom == null) return;
            if (bloom == null)
            {
                var go = new GameObject("Crystal cavern bloom");
                go.transform.SetParent(transform, false);
                bloom = go.AddComponent<Volume>();
                bloom.isGlobal = true; bloom.priority = 20; bloom.sharedProfile = dressing.Bloom; bloom.weight = 0;
            }
            if (viewer == null) viewer = FindAnyObjectByType<FpsPlayer>();
            float target = 0;
            if (viewer != null && viewer.ViewCamera != null && dressed != null)
            {
                var p = (float3)terrain.transform.InverseTransformPoint(viewer.ViewCamera.transform.position);
                foreach (var cavern in dressed.Caverns)
                    if (cavern.Crystal && math.all(p > cavern.Min) && math.all(p < cavern.Max) && TerrainGround.CavernHollow(cavern, p) < .3f) target = 1;
            }
            bloom.weight = Mathf.MoveTowards(bloom.weight, target, Time.unscaledDeltaTime * BloomFade);
        }
    }
}
