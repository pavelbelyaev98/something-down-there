using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // Rebuilds the MainGame surroundings from the east river arm of the approved
    // Pure Nature 2: Highlands demo: the river widened into a former lake, one drained
    // section of its bed around the voxel dig site, and the camp beside the opening.
    // Reruns regenerate the same terrain, objects and water from the unmodified demo.
    public static class LakebedSiteSetup
    {
        public const string Folder = "Assets/Content/Lakebed";
        public const string TerrainDataPath = Folder + "/LakebedTerrain.asset";
        public const string RimMeshPath = Folder + "/ExcavationRim.asset";
        public const string LakeMeshPath = Folder + "/LakeSurface.asset";
        public const string BorderMeshPath = Folder + "/BorderStones.asset";
        public const string MeadowLayerPath = Folder + "/RimMeadow.terrainlayer";
        // Darkens the Highlands grass layer toward the dig meadow's turf.
        private static readonly Color MeadowTint = new Color(.6f, .72f, .5f, 1);
        public const string DemoScenePath = "Assets/BK/PureNature_Highlands/Scenes/Highlands_Demo.unity";
        private const string VendorPrefabs = "Assets/BK/PureNature_Highlands/Prefabs/";

        // Demo-space authoring. The dig centre and voxel top map to the MainGame origin.
        public static readonly Vector3 SiteInDemo = new Vector3(385f, 11.2f, -360f);
        public const float WaterLevel = 10f;
        // The former lake filled the canyon floor up to this contour.
        private const float OldShore = 13f;
        // 1000 m of the demo terrain at its own sample spacing.
        private const int WindowCells = 2048;
        private static readonly Rect LakeArea = Rect.MinMaxRect(150, -520, 480, -60);
        private static readonly Vector2 LakeSeed = new Vector2(355, -300);
        // Site-local drained section against the east cliff.
        private static readonly Vector2 DrainedCentre = new Vector2(-3, -2), DrainedRadii = new Vector2(48, 76);
        private const float CampFlat = 20f, CampBlend = 30f, CampClearance = 21f, PeakRange = 1600f;
        private const float TileSize = 10f, GrassTile = 20f;
        // The walkable drained section stops this far inside its mapped edge, at the water line.
        private const float PlayAreaInset = 9f;
        // Highest point the player's feet can reach above the lakebed ground.
        public const float FlightCeiling = 16f;
        // Compass arc (clockwise from north) kept free of boundary rocks for the south-rim
        // stations and the winch rope; the camp looks north up the canyon over the opening.
        private const float CampArcStart = 128f, CampArcEnd = 212f;
        private static readonly string[] Groups = { "Cliffs", "Peaks", "BigBoulders", "Boulders", "Rubble_dense", "Rubble_sparse", "Ruins", "Trees", "Water" };

        public static Vector3 Camp(Vector3 rimLayout) => rimLayout + Vector3.up * SiteLayout.GroundTop;

        [MenuItem("Tools/Something Down There/Configure Lakebed Excavation Site")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            foreach (string name in new[] { "Environment", "Perimeter", "Clouds" }) Remove(root.Find(name));
            var surface = root.Find("Surface");
            foreach (string name in new[] { "Walking apron", "Excavation rim", "South rim", "North rim", "West rim", "East rim" })
                Remove(surface.Find(name));
            EnsureFolder();
            ArrangeCamp(root);
            var stations = StationFootprints(surface);
            var area = PlayArea();
            Section section;
            Transform environment;
            var demo = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var source = demo.GetRootGameObjects().Single(o => o.name == "Terrain").GetComponent<Terrain>();
                section = Shape(source.terrainData, source.transform.position);
                environment = new GameObject("Environment").transform;
                environment.SetParent(root, false);
                // Every scenery collider reports the permanent-boundary prompt instead of digging.
                environment.gameObject.AddComponent<PermanentTerrainBoundary>();
                BuildTerrain(source, section, environment, stations);
                CopyObjects(demo, section, environment);
                BuildWater(demo, section, environment.Find("Water"));
                CopyReflections(demo, environment);
                BuildBoundary(environment);
            }
            finally
            {
                SceneManager.SetActiveScene(scene);
                EditorSceneManager.CloseScene(demo, true);
            }
            BuildPlayArea(environment, area);
            CullHidden(environment, area);
            var soil = new SerializedObject(root.GetComponentInChildren<TerrainVolume>()).FindProperty("soilMaterial").objectReferenceValue as Material;
            BuildRim(surface, section.TerrainPosition, soil);
            float depth = SiteLayout.Extent.y;
            foreach (string side in new[] { "West", "East", "North", "South" })
            {
                var wall = root.Find("Bedrock/" + side);
                var position = wall.position; position.y = (-depth + SiteLayout.RimBottom) * .5f; wall.position = position;
                var scale = wall.localScale; scale.y = depth + SiteLayout.RimBottom; wall.localScale = scale;
            }
            var terrain = root.GetComponentInChildren<TerrainVolume>();
            var grass = new SerializedObject(terrain.GetComponent<SurfaceGrassRenderer>());
            grass.FindProperty("surfaceRadius").floatValue = SiteLayout.OpeningRadius;
            grass.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponentInChildren<Camera>().farClipPlane = 3000;
            GroundTextureSetup.ConfigureMeadowMaterials(root);
            SunPresentationSetup.Configure();
            foreach (string retired in new[] { "Assets/Content/Site/WalkingApron.asset", "Assets/Content/Site/DryGravel.mat", Folder + "/LakebedRim.mat",
                Folder + "/WadingFloor.asset", "Assets/Content/Environment/ReservoirSky.mat" })
                AssetDatabase.DeleteAsset(retired);
            if (AssetDatabase.IsValidFolder("Assets/Content/Site") && AssetDatabase.FindAssets("", new[] { "Assets/Content/Site" }).Length == 0)
                AssetDatabase.DeleteAsset("Assets/Content/Site");
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Lakebed excavation site configured from the Highlands demo.");
        }

        // Sampled demo window, heights in metres indexed [z, x].
        private sealed class Section
        {
            public int Samples => WindowCells + 1;
            public float Cell, Height;
            public int I0, J0;
            public Vector2 Origin;
            public float[,] Before, After, Shore, Drained;
            public bool[,] Lake;
            public float Extent => WindowCells * Cell;
            public Vector3 TerrainPosition => new Vector3(Origin.x - SiteInDemo.x, -SiteInDemo.y, Origin.y - SiteInDemo.z);
            public Vector2 Demo(float x, float z) => new Vector2(Origin.x + x * Cell, Origin.y + z * Cell);
            public Vector2 Local(float x, float z) => Demo(x, z) - new Vector2(SiteInDemo.x, SiteInDemo.z);

            public bool Contains(Vector3 demo) =>
                demo.x >= Origin.x && demo.z >= Origin.y && demo.x <= Origin.x + Extent && demo.z <= Origin.y + Extent;

            public float Sample(float[,] field, Vector3 demo)
            {
                float fx = Mathf.Clamp((demo.x - Origin.x) / Cell, 0, WindowCells - .001f);
                float fz = Mathf.Clamp((demo.z - Origin.y) / Cell, 0, WindowCells - .001f);
                int x = (int)fx, z = (int)fz; fx -= x; fz -= z;
                return Mathf.Lerp(Mathf.Lerp(field[z, x], field[z, x + 1], fx), Mathf.Lerp(field[z + 1, x], field[z + 1, x + 1], fx), fz);
            }

            public bool InLake(Vector3 demo)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt((demo.x - Origin.x) / Cell), 0, WindowCells);
                int z = Mathf.Clamp(Mathf.RoundToInt((demo.z - Origin.y) / Cell), 0, WindowCells);
                return Lake[z, x];
            }
        }

        private static Section Shape(TerrainData source, Vector3 sourcePosition)
        {
            int resolution = source.heightmapResolution;
            if (source.alphamapResolution != resolution - 1) throw new InvalidOperationException("Demo splat map no longer matches its heightmap.");
            var s = new Section { Cell = source.size.x / (resolution - 1), Height = source.size.y };
            int Start(float site, float origin) =>
                Mathf.Clamp(Mathf.RoundToInt((site - origin) / s.Cell) - WindowCells / 2, 0, resolution - 1 - WindowCells) & ~1;
            s.I0 = Start(SiteInDemo.x, sourcePosition.x);
            s.J0 = Start(SiteInDemo.z, sourcePosition.z);
            s.Origin = new Vector2(sourcePosition.x + s.I0 * s.Cell, sourcePosition.z + s.J0 * s.Cell);
            int n = s.Samples;
            var raw = source.GetHeights(s.I0, s.J0, n, n);
            s.Before = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) s.Before[z, x] = raw[z, x] * s.Height;
            s.Lake = FloodLake(s);
            s.Shore = ShoreDistance(s.Lake, s.Cell);
            s.After = new float[n, n];
            s.Drained = new float[n, n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float h = s.Before[z, x];
                var local = s.Local(x, z);
                if (s.Lake[z, x])
                {
                    // Exposed bathtub band below the old shoreline, then the submerged bed.
                    float d = s.Shore[z, x];
                    float bed = d < 5 ? Mathf.Lerp(OldShore, WaterLevel - 1.2f, Smooth(d / 5))
                        : Mathf.Lerp(WaterLevel - 1.2f, WaterLevel - 5.5f, Smooth((d - 5) / 13));
                    h = Mathf.Min(h, bed);
                    float drained = DrainedWeight(local);
                    s.Drained[z, x] = drained;
                    if (drained > 0) h = Mathf.Max(h, Mathf.Lerp(h, DrainedFloor(local), drained));
                }
                float r = local.magnitude;
                if (r < CampBlend) h = Mathf.Lerp(SiteInDemo.y + SiteLayout.GroundTop, h, Smooth((r - CampFlat) / (CampBlend - CampFlat)));
                s.After[z, x] = h;
            }
            return s;
        }

        private static bool[,] FloodLake(Section s)
        {
            int n = s.Samples;
            var lake = new bool[n, n];
            var queue = new Queue<Vector2Int>();
            var seed = new Vector2Int(Mathf.RoundToInt((LakeSeed.x - s.Origin.x) / s.Cell), Mathf.RoundToInt((LakeSeed.y - s.Origin.y) / s.Cell));
            if (s.Before[seed.y, seed.x] >= WaterLevel) throw new InvalidOperationException("The lake seed no longer lies in the demo river.");
            lake[seed.y, seed.x] = true; queue.Enqueue(seed);
            var steps = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var step in steps)
                {
                    var next = cell + step;
                    if (next.x < 0 || next.y < 0 || next.x >= n || next.y >= n || lake[next.y, next.x]) continue;
                    if (s.Before[next.y, next.x] >= OldShore || !LakeArea.Contains(s.Demo(next.x, next.y))) continue;
                    lake[next.y, next.x] = true; queue.Enqueue(next);
                }
            }
            return lake;
        }

        // Two-pass chamfer distance from each lake sample to the old shoreline, in metres.
        private static float[,] ShoreDistance(bool[,] lake, float cell)
        {
            int n = lake.GetLength(0);
            float diagonal = cell * 1.41421356f;
            var d = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) d[z, x] = lake[z, x] ? float.MaxValue : 0;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                if (d[z, x] == 0) continue;
                float v = d[z, x];
                if (x > 0) v = Mathf.Min(v, d[z, x - 1] + cell);
                if (z > 0) v = Mathf.Min(v, d[z - 1, x] + cell);
                if (x > 0 && z > 0) v = Mathf.Min(v, d[z - 1, x - 1] + diagonal);
                if (x < n - 1 && z > 0) v = Mathf.Min(v, d[z - 1, x + 1] + diagonal);
                d[z, x] = v;
            }
            for (int z = n - 1; z >= 0; z--)
            for (int x = n - 1; x >= 0; x--)
            {
                if (d[z, x] == 0) continue;
                float v = d[z, x];
                if (x < n - 1) v = Mathf.Min(v, d[z, x + 1] + cell);
                if (z < n - 1) v = Mathf.Min(v, d[z + 1, x] + cell);
                if (x < n - 1 && z < n - 1) v = Mathf.Min(v, d[z + 1, x + 1] + diagonal);
                if (x > 0 && z < n - 1) v = Mathf.Min(v, d[z + 1, x - 1] + diagonal);
                d[z, x] = v;
            }
            return d;
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        private static float DrainedWeight(Vector2 local)
        {
            var q = local - DrainedCentre;
            float angle = Mathf.Atan2(q.y, q.x);
            float normalized = new Vector2(q.x / DrainedRadii.x, q.y / DrainedRadii.y).magnitude;
            float edge = 1 + .09f * Mathf.Sin(3 * angle + 1.3f) + .05f * Mathf.Sin(7 * angle + .4f);
            float signedMetres = (normalized - edge) * Mathf.Min(DrainedRadii.x, DrainedRadii.y);
            return 1 - Smooth((signedMetres + 10) / 10);
        }

        // Nearly level mud, tilting gently toward the remaining lake in the west.
        private static float DrainedFloor(Vector2 local) =>
            SiteInDemo.y - .05f + .012f * local.x
            + .3f * (Mathf.PerlinNoise(local.x / 28 + 40, local.y / 28 + 12) - .5f)
            + .06f * (Mathf.PerlinNoise(local.x / 5 + 7, local.y / 5 + 91) - .5f);

        // Grass around the stone border: full at the rim, a ragged fade into the drained mud.
        private static float Meadow(Vector2 local) =>
            1 - Smooth((local.magnitude - 15f - 9f * (Noise(local, 7, 2.4f) - .5f)) / 8f);

        private static float Noise(Vector2 local, float scale, float seed) => Mathf.PerlinNoise(local.x / scale + seed, local.y / scale + seed * 1.7f);

        private static void BuildTerrain(Terrain source, Section s, Transform environment, Rect[] stations)
        {
            var from = source.terrainData;
            // Create other assets first: an asset import after CreateAsset reloads the unsaved terrain data.
            var meadow = MeadowLayer(from);
            AssetDatabase.DeleteAsset(TerrainDataPath);
            var data = new TerrainData { name = "LakebedTerrain" };
            data.heightmapResolution = s.Samples;
            data.size = new Vector3(s.Extent, s.Height, s.Extent);
            AssetDatabase.CreateAsset(data, TerrainDataPath);
            int n = s.Samples;
            var heights = new float[n, n];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++) heights[z, x] = s.After[z, x] / s.Height;
            data.SetHeights(0, 0, heights);
            data.wavingGrassAmount = from.wavingGrassAmount;
            data.wavingGrassSpeed = from.wavingGrassSpeed;
            data.wavingGrassStrength = from.wavingGrassStrength;
            data.wavingGrassTint = from.wavingGrassTint;
            data.alphamapResolution = WindowCells;
            data.terrainLayers = from.terrainLayers.Append(meadow).ToArray();
            data.SetAlphamaps(0, 0, Paint(from, s));
            data.SetDetailScatterMode(from.detailScatterMode);
            int detailScale = (from.heightmapResolution - 1) / from.detailResolution;
            data.SetDetailResolution(WindowCells / detailScale, from.detailResolutionPerPatch);
            data.detailPrototypes = from.detailPrototypes;
            CopyDetails(from, s, data, detailScale, stations);
            data.treePrototypes = from.treePrototypes;
            data.SetTreeInstances(Trees(from, source.transform.position, s), false);
            data.SetHoles(0, 0, Holes(s));
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            var terrainObject = new GameObject("Lakebed terrain");
            terrainObject.transform.SetParent(environment, false);
            terrainObject.transform.position = s.TerrainPosition;
            GameObjectUtility.SetStaticEditorFlags(terrainObject, GameObjectUtility.GetStaticEditorFlags(source.gameObject) & ~StaticEditorFlags.BatchingStatic);
            var terrain = terrainObject.AddComponent<Terrain>();
            EditorUtility.CopySerialized(source, terrain);
            terrain.terrainData = data;
            // Terrain shadow casters ignore holes and would shade the whole shaft; under the
            // near-overhead sun the cliffs and rocks still cast their own shadows.
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The demo draws at pixel error 1; 3 keeps the canyon silhouette with far fewer triangles.
            terrain.heightmapPixelError = 3;
            terrainObject.AddComponent<TerrainCollider>().terrainData = data;
        }

        // Project copy of the Highlands grass layer, tinted to meet the dig meadow at the rim.
        private static TerrainLayer MeadowLayer(TerrainData from)
        {
            var vendor = from.terrainLayers.Single(l => l.name == "Grass");
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(MeadowLayerPath);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, MeadowLayerPath); }
            EditorUtility.CopySerialized(vendor, layer);
            layer.name = "RimMeadow";
            layer.diffuseRemapMax = MeadowTint;
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssetIfDirty(layer);
            return layer;
        }

        private static float[,,] Paint(TerrainData from, Section s)
        {
            int Layer(string name) => Array.FindIndex(from.terrainLayers, l => l != null && l.name == name);
            int grassy = Layer("Mud_grass"), rubble = Layer("Mud_rubble"), mud = Layer("Mud"), sand = Layer("Sand"), gravel = Layer("Sand_rubble");
            if (new[] { grassy, rubble, mud, sand, gravel }.Any(i => i < 0)) throw new InvalidOperationException("Demo terrain layers changed.");
            var source = from.GetAlphamaps(s.I0, s.J0, WindowCells, WindowCells);
            int halo = source.GetLength(2), layers = halo + 1;
            var alpha = new float[WindowCells, WindowCells, layers];
            for (int z = 0; z < WindowCells; z++)
            for (int x = 0; x < WindowCells; x++)
            for (int l = 0; l < halo; l++) alpha[z, x, l] = source[z, x, l];
            var target = new float[layers];
            for (int z = 0; z < WindowCells; z++)
            for (int x = 0; x < WindowCells; x++)
            {
                var local = s.Local(x + .5f, z + .5f);
                float camp = 1 - Smooth((local.magnitude - 16) / 8), meadow = Meadow(local);
                float weight = Mathf.Max(s.Lake[z, x] ? Smooth(s.Shore[z, x] / 3) : 0, camp);
                if (weight <= 0) continue;
                float h = s.After[z, x];
                float patches = Noise(local, 9, 3.1f), growth = Noise(local, 14, 5.3f), grain = Noise(local, 4, 1.7f);
                Array.Clear(target, 0, layers);
                if (h < WaterLevel - .15f) { target[sand] = .75f; target[mud] = .25f; }
                else if (s.Drained[z, x] > .35f)
                {
                    target[mud] = 1;
                    target[rubble] = 1.2f * Smooth((patches - .5f) / .15f);
                    target[grassy] = 1.5f * Smooth((growth - .6f) / .12f) * Smooth((h - WaterLevel - .8f) / .6f);
                    target[sand] = 1.5f * Smooth((WaterLevel + .7f - h) / .5f);
                }
                else
                {
                    target[gravel] = .6f + .4f * patches;
                    target[mud] = .5f * grain;
                    target[sand] = 1.5f * Smooth((WaterLevel + .6f - h) / .5f);
                }
                if (camp > 0)
                {
                    for (int l = 0; l < layers; l++) target[l] *= 1 - camp;
                    target[rubble] += camp * (1 - meadow);
                    target[mud] += camp * .15f * grain;
                }
                if (meadow > 0)
                {
                    for (int l = 0; l < layers; l++) target[l] *= 1 - meadow;
                    target[halo] += meadow * (.55f + .3f * grain);
                    target[grassy] += meadow * .45f * (1 - grain);
                }
                float total = target.Sum(), mixed = 0;
                for (int l = 0; l < layers; l++) { alpha[z, x, l] = Mathf.Lerp(alpha[z, x, l], target[l] / total, weight); mixed += alpha[z, x, l]; }
                for (int l = 0; l < layers; l++) alpha[z, x, l] /= mixed;
            }
            return alpha;
        }

        private static void CopyDetails(TerrainData from, Section s, TerrainData data, int scale, Rect[] stations)
        {
            int cells = WindowCells / scale;
            for (int layer = 0; layer < from.detailPrototypes.Length; layer++)
            {
                var map = from.GetDetailLayer(s.I0 / scale, s.J0 / scale, cells, cells, layer);
                for (int v = 0; v < cells; v++)
                for (int u = 0; u < cells; u++)
                {
                    int x = u * scale, z = v * scale;
                    var local = s.Local(x, z);
                    bool changed = s.After[z, x] < s.Before[z, x] - .3f || s.After[z, x] > s.Before[z, x] + .3f;
                    bool bed = s.Lake[z, x] && s.After[z, x] < OldShore - .2f;
                    if (!changed && !bed && local.magnitude >= CampBlend - 6) continue;
                    map[v, u] = 0;
                    // Sparse grass re-colonises the high drained mud, away from the camp.
                    if (layer == 0 && s.Drained[z, x] > .35f && local.magnitude > CampBlend - 4
                        && Noise(local, 14, 5.3f) > .66f && s.After[z, x] > WaterLevel + 1) map[v, u] = 110;
                    // The meadow continues past the stone border and thins out into the mud.
                    float meadow = Meadow(local);
                    if (meadow <= 0 || local.magnitude < SiteLayout.RimOuterRadius - .2f || stations.Any(r => r.Contains(local))) continue;
                    float tufts = Noise(local, 3, 8.1f);
                    if (layer == 0) map[v, u] = Mathf.RoundToInt(150 * meadow);
                    else if (layer == 1) map[v, u] = Mathf.RoundToInt(70 * meadow * tufts);
                    else if (layer == 3 && tufts > .72f) map[v, u] = Mathf.RoundToInt(90 * meadow);
                }
                data.SetDetailLayer(0, 0, layer, map);
            }
        }

        private static TreeInstance[] Trees(TerrainData from, Vector3 sourcePosition, Section s)
        {
            var kept = new List<TreeInstance>();
            foreach (var tree in from.treeInstances)
            {
                var demo = sourcePosition + Vector3.Scale(tree.position, from.size);
                if (!s.Contains(demo) || s.InLake(demo)) continue;
                var local = new Vector2(demo.x - SiteInDemo.x, demo.z - SiteInDemo.z);
                if (local.magnitude < CampBlend || Mathf.Abs(s.Sample(s.After, demo) - s.Sample(s.Before, demo)) > .2f) continue;
                var copy = tree;
                copy.position = new Vector3((demo.x - s.Origin.x) / s.Extent, tree.position.y, (demo.z - s.Origin.y) / s.Extent);
                kept.Add(copy);
            }
            return kept.ToArray();
        }

        // The terrain opens beneath the rim collar; its staircase edge stays hidden under it.
        private static bool[,] Holes(Section s)
        {
            var solid = new bool[WindowCells, WindowCells];
            float radius = SiteLayout.RimOuterRadius - .5f;
            for (int z = 0; z < WindowCells; z++)
            for (int x = 0; x < WindowCells; x++)
                solid[z, x] = s.Local(x + .5f, z + .5f).magnitude >= radius;
            return solid;
        }

        private static void CopyObjects(Scene demo, Section s, Transform environment)
        {
            var site = SiteInDemo;
            Physics.SyncTransforms();
            // Colliding scenery that actually reaches into the camp is left out; the rest keeps its placement.
            var intruders = new HashSet<GameObject>(Physics.OverlapCapsule(site + Vector3.down * 3, site + Vector3.up * 8, CampClearance)
                .Where(c => c.gameObject.scene == demo).Select(c => PrefabUtility.GetOutermostPrefabInstanceRoot(c.gameObject) ?? c.gameObject));
            int kept = 0, skipped = 0;
            foreach (string name in Groups)
            {
                var group = demo.GetRootGameObjects().Single(o => o.name == name).transform;
                var parent = new GameObject(name).transform;
                parent.SetParent(environment, false);
                foreach (Transform item in group)
                {
                    var bounds = WorldBounds(item);
                    var pivot = item.position;
                    bool peak = name == "Peaks";
                    if (peak ? Vector2.Distance(new Vector2(pivot.x, pivot.z), new Vector2(site.x, site.z)) > PeakRange : !s.Contains(pivot)) continue;
                    if (name == "Water" && IsSeaLevelWater(item)) continue;
                    bool colliding = item.GetComponentInChildren<Collider>() != null;
                    if (colliding ? intruders.Contains(item.gameObject) : DistanceXZ(bounds, site) < CampClearance) { skipped++; continue; }
                    if (name == "Trees" && s.InLake(pivot)) { skipped++; continue; }
                    float lift = 0;
                    bool small = !peak && name != "Cliffs" && name != "Water" && Mathf.Max(bounds.size.x, bounds.size.z) < 15;
                    if (small && s.Contains(pivot))
                    {
                        lift = s.Sample(s.After, pivot) - s.Sample(s.Before, pivot);
                        if (Mathf.Abs(lift) < .05f) lift = 0;
                        else if (bounds.max.y + lift < WaterLevel + .15f) { skipped++; continue; }
                    }
                    var copy = Copy(item.gameObject, parent);
                    copy.transform.SetPositionAndRotation(pivot + Vector3.up * lift - site, item.rotation);
                    copy.transform.localScale = item.lossyScale;
                    kept++;
                }
            }
            Debug.Log($"Lakebed scenery: {kept} demo objects kept, {skipped} submerged or cleared for the camp.");
        }

        private static bool IsSeaLevelWater(Transform item)
        {
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
            return prefab != null && prefab.name == "Water" && Mathf.Abs(item.position.y - WaterLevel) < .5f;
        }

        private static GameObject Copy(GameObject source, Transform parent)
        {
            GameObject copy;
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(source);
            if (prefab != null && PrefabUtility.IsOutermostPrefabInstanceRoot(source))
            {
                copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                PrefabUtility.SetPropertyModifications(copy, PrefabUtility.GetPropertyModifications(source));
            }
            else copy = UnityEngine.Object.Instantiate(source, parent);
            copy.name = source.name;
            WithoutStaticBatching(copy);
            return copy;
        }

        // Runtime static batching would merge thousands of vendor LOD meshes into
        // gigabytes of vertex data on every scene load; the SRP batcher draws them instead.
        private static void WithoutStaticBatching(GameObject item)
        {
            foreach (var child in item.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    GameObjectUtility.GetStaticEditorFlags(child.gameObject) & ~StaticEditorFlags.BatchingStatic);
        }

        private static Bounds WorldBounds(Transform item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(item.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static float DistanceXZ(Bounds bounds, Vector3 point)
        {
            float dx = Mathf.Max(0, Mathf.Abs(point.x - bounds.center.x) - bounds.extents.x);
            float dz = Mathf.Max(0, Mathf.Abs(point.z - bounds.center.z) - bounds.extents.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // One lake surface replaces the demo's sea-level planes so no water ever crosses the dig column.
        private static void BuildWater(Scene demo, Section s, Transform water)
        {
            var template = demo.GetRootGameObjects().Single(o => o.name == "Water").transform.Cast<Transform>()
                .First(IsSeaLevelWater).GetComponent<MeshRenderer>();
            var lake = new GameObject("Lake surface").transform;
            lake.SetParent(water, false);
            lake.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh(GridMesh(s, WaterLevel, WaterLevel + .25f, "LakeSurface"), LakeMeshPath);
            var renderer = lake.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = template.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = template.receiveShadows;
        }

        private static Mesh GridMesh(Section s, float level, float below, string name)
        {
            const int step = 8;
            int cells = WindowCells / step;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var index = new Dictionary<int, int>();
            int Vertex(int gx, int gz)
            {
                int key = gz * (cells + 1) + gx;
                if (index.TryGetValue(key, out int existing)) return existing;
                var local = s.Local(gx * step, gz * step);
                vertices.Add(new Vector3(local.x, level - SiteInDemo.y, local.y));
                uv.Add(s.Demo(gx * step, gz * step) / TileSize);
                index[key] = vertices.Count - 1;
                return vertices.Count - 1;
            }
            for (int gz = 0; gz < cells; gz++)
            for (int gx = 0; gx < cells; gx++)
            {
                if (s.Local((gx + .5f) * step, (gz + .5f) * step).magnitude < CampClearance) continue;
                bool wet = false;
                for (int c = 0; c < 4 && !wet; c++) wet = s.After[(gz + (c >> 1)) * step, (gx + (c & 1)) * step] < below;
                if (!wet) continue;
                int a = Vertex(gx, gz), b = Vertex(gx + 1, gz), c0 = Vertex(gx, gz + 1), d = Vertex(gx + 1, gz + 1);
                triangles.AddRange(new[] { a, c0, d, a, d, b });
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, saved);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            return saved;
        }

        // A continuous border of small half-buried stones marks the diggable circle; a few
        // larger stones lie out in the grass, clear of the camp.
        private static void BuildBoundary(Transform environment)
        {
            var parent = new GameObject("Dig boundary").transform;
            parent.SetParent(environment, false);
            var random = new System.Random(89);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            bool InCamp(float compass) { compass = Mathf.Repeat(compass, 360); return compass > CampArcStart && compass < CampArcEnd; }
            // The border merges into one mesh per material: ~150 pebbles cost a single draw.
            var pieces = new Dictionary<Material, List<CombineInstance>>();
            float circumference = 2 * Mathf.PI * SiteLayout.OpeningRadius;
            for (float arc = 0; arc < circumference; arc += Range(.42f, .8f))
            {
                var tumble = Quaternion.Euler(Range(0, 360), Range(0, 360), Range(0, 360));
                var stone = Place(parent, "Rocks/Rock_" + random.Next(4), arc / circumference * 360, Range(12.35f, 12.75f), tumble, Range(.15f, .3f), .35f, false);
                foreach (var renderer in stone.GetComponent<LODGroup>().GetLODs()[0].renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    for (int part = 0; part < mesh.subMeshCount; part++)
                    {
                        var material = renderer.sharedMaterials[part];
                        if (!pieces.TryGetValue(material, out var list)) pieces[material] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = mesh, subMeshIndex = part, transform = renderer.localToWorldMatrix });
                    }
                }
                UnityEngine.Object.DestroyImmediate(stone);
            }
            var border = new Mesh { name = "BorderStones", indexFormat = IndexFormat.UInt32 };
            var parts = pieces.Select(entry =>
            {
                var merged = new Mesh { indexFormat = IndexFormat.UInt32 };
                merged.CombineMeshes(entry.Value.ToArray(), true, true);
                return new CombineInstance { mesh = merged, transform = Matrix4x4.identity };
            }).ToArray();
            border.CombineMeshes(parts, false, true);
            foreach (var part in parts) UnityEngine.Object.DestroyImmediate(part.mesh);
            border.RecalculateBounds();
            var stones = new GameObject("Border stones");
            stones.transform.SetParent(parent, false);
            stones.AddComponent<MeshFilter>().sharedMesh = SaveMesh(border, BorderMeshPath);
            stones.AddComponent<MeshRenderer>().sharedMaterials = pieces.Keys.ToArray();
            foreach (float cluster in new[] { 11f, 49f, 86f, 118f, 231f, 268f, 305f, 340f })
            {
                int count = 1 + random.Next(2);
                for (int i = 0; i < count; i++)
                {
                    float compass = cluster + Range(-7, 7);
                    if (InCamp(compass)) continue;
                    var tilt = Quaternion.Euler(Range(72, 98), Range(0, 360), Range(-12, 12));
                    Place(parent, "Rocks/Rock_" + random.Next(4), compass, Range(15f, 19f), tilt, Range(.5f, .9f), .35f, true);
                }
            }
        }

        private static GameObject Place(Transform parent, string prefabName, float compass, float radius, Quaternion rotation, float scale, float embed, bool solid)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VendorPrefabs + prefabName + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing Highlands prefab " + prefabName + ".");
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            WithoutStaticBatching(item);
            // Border pebbles are visual: the player walks over them without snagging.
            if (!solid) foreach (var collider in item.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            float angle = compass * Mathf.Deg2Rad;
            item.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius, rotation);
            item.transform.localScale = Vector3.one * scale;
            var bounds = WorldBounds(item.transform);
            item.transform.position += Vector3.up * (SiteLayout.GroundTop - bounds.min.y - embed * bounds.size.y);
            // Nothing loose may hang over the opening, where digging would leave it floating.
            var outward = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * .1f;
            while (DistanceXZ(WorldBounds(item.transform), Vector3.zero) < SiteLayout.OpeningRadius + .2f) item.transform.position += outward;
            return item;
        }

        // Closed collar: an exact circular lip over the terrain-hole edge and a roof over
        // the grid corners, textured to continue the surrounding lakebed ground.
        private static void BuildRim(Transform surface, Vector3 terrainPosition, Material meadow)
        {
            const int segments = 256;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                foreach (var v in new[] { a, b, c, d }) uv.Add(new Vector2(v.x - terrainPosition.x, v.z - terrainPosition.z) / GrassTile);
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            float lip = SiteLayout.RimOuterRadius + .35f;
            Vector3 top = Vector3.up * SiteLayout.RimTop, skirt = Vector3.up * (SiteLayout.GroundTop - .015f), bottom = Vector3.up * SiteLayout.RimBottom;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 Direction(float angle) => new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 da = Direction(a), db = Direction(b);
                Vector3 ia = da * SiteLayout.OpeningRadius, ib = db * SiteLayout.OpeningRadius;
                Vector3 ca = da * SiteLayout.RimOuterRadius, cb = db * SiteLayout.RimOuterRadius;
                Vector3 la = da * lip, lb = db * lip, oa = da * SiteLayout.RimRadius, ob = db * SiteLayout.RimRadius;
                Quad(ia + top, ib + top, cb + top, ca + top);
                Quad(ca + top, cb + top, lb + skirt, la + skirt);
                Quad(la + skirt, lb + skirt, ob, oa);
                Quad(ia + bottom, oa + bottom, ob + bottom, ib + bottom);
                Quad(ia + top, ia + bottom, ib + bottom, ib + top);
                Quad(oa, ob, ob + bottom, oa + bottom);
            }
            var mesh = new Mesh { name = "ExcavationRim" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            mesh.SetPreBakeCollisionMesh(false, true);
            var saved = SaveMesh(mesh, RimMeshPath);
            var rim = new GameObject("Excavation rim");
            rim.transform.SetParent(surface, false);
            rim.AddComponent<MeshFilter>().sharedMesh = saved;
            // The collar continues the dig meadow itself; the pebble border marks where digging ends.
            rim.AddComponent<MeshRenderer>().sharedMaterial = meadow;
            rim.AddComponent<MeshCollider>().sharedMesh = saved;
            rim.AddComponent<PermanentTerrainBoundary>();
        }

        // Stations keep their tested south-rim layout, lifted onto the lakebed ground.
        private static void ArrangeCamp(Transform root)
        {
            var surface = root.Find("Surface");
            void Put(Transform item, Vector3 rimLayout)
            {
                if (item == null) return;
                item.SetPositionAndRotation(Camp(rimLayout), Quaternion.identity);
                EditorUtility.SetDirty(item);
            }
            Put(surface.Find("ComputerStation"), new Vector3(-3, 0, -14));
            Put(surface.Find("RechargeZone"), new Vector3(0, 0, -14.5f));
            Put(surface.Find("ReturnAnchor"), new Vector3(0, .1f, -13.5f));
            Put(root.Find("Player"), new Vector3(0, .1f, -13));
            // Winch, pads and stands keep SalvageWinchSetup's local layout under this parent.
            Put(surface.Find("SalvageWinch"), Vector3.zero);
        }

        // Site-local footprints of the stations, kept clear of terrain grass.
        private static Rect[] StationFootprints(Transform surface)
        {
            var footprints = new List<Rect>();
            foreach (var renderer in surface.GetComponentsInChildren<Renderer>())
            {
                var bounds = renderer.bounds;
                if (bounds.size.x <= 0 || renderer is LineRenderer) continue;
                footprints.Add(Rect.MinMaxRect(bounds.min.x - .7f, bounds.min.z - .7f, bounds.max.x + .7f, bounds.max.z + .7f));
            }
            return footprints.ToArray();
        }

        // Site-local outline of the walkable drained section, just above the water line.
        public static Vector2[] PlayArea()
        {
            const int points = 72;
            var outline = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2 / points;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float edge = 1 + .09f * Mathf.Sin(3 * angle + 1.3f) + .05f * Mathf.Sin(7 * angle + .4f);
                float scale = new Vector2(direction.x / DrainedRadii.x, direction.y / DrainedRadii.y).magnitude;
                outline[i] = DrainedCentre + direction * ((edge - PlayAreaInset / Mathf.Min(DrainedRadii.x, DrainedRadii.y)) / scale);
            }
            return outline;
        }

        public static bool InPlayArea(Vector2[] outline, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
                if ((outline[i].y > point.y) != (outline[j].y > point.y)
                    && point.x < (outline[j].x - outline[i].x) * (point.y - outline[i].y) / (outline[j].y - outline[i].y) + outline[i].x)
                    inside = !inside;
            return inside;
        }

        // Invisible walls and a flight ceiling keep the player on the drained section. They use
        // the Ignore Raycast layer, so aiming, digging, lamps and the winch never hit them.
        private static void BuildPlayArea(Transform environment, Vector2[] outline)
        {
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            var parent = new GameObject("Play area bounds") { layer = ignoreRaycast }.transform;
            parent.SetParent(environment, false);
            float bottom = -4, top = FlightCeiling + 8;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length], middle = (a + b) * .5f, along = b - a;
                var wall = new GameObject("Wall " + i) { layer = ignoreRaycast }.transform;
                wall.SetParent(parent, false);
                wall.SetPositionAndRotation(new Vector3(middle.x, (bottom + top) * .5f, middle.y), Quaternion.LookRotation(new Vector3(along.x, 0, along.y)));
                wall.gameObject.AddComponent<BoxCollider>().size = new Vector3(1, top - bottom, along.magnitude + .6f);
            }
            var min = new Vector2(outline.Min(v => v.x), outline.Min(v => v.y));
            var max = new Vector2(outline.Max(v => v.x), outline.Max(v => v.y));
            var ceiling = new GameObject("Flight ceiling") { layer = ignoreRaycast }.transform;
            ceiling.SetParent(parent, false);
            // The controller is 1.8 m tall: its feet stop at FlightCeiling.
            ceiling.position = new Vector3((min.x + max.x) * .5f, FlightCeiling + 2.4f, (min.y + max.y) * .5f);
            ceiling.gameObject.AddComponent<BoxCollider>().size = new Vector3(max.x - min.x + 4, 1, max.y - min.y + 4);
        }

        // The demo lights its water with baked box-projected probes; reuse the one covering this canyon.
        private static void CopyReflections(Scene demo, Transform environment)
        {
            var source = demo.GetRootGameObjects().Single(o => o.name == "Lighting").GetComponentsInChildren<ReflectionProbe>()
                .Single(p => new Bounds(p.transform.position + p.center, p.size).Contains(SiteInDemo));
            if (source.bakedTexture == null) throw new InvalidOperationException("The demo reflection probe has no baked cubemap.");
            var probe = new GameObject("Lakebed reflections").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(environment, false);
            probe.transform.position = source.transform.position - SiteInDemo;
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = source.bakedTexture;
            probe.size = source.size; probe.center = source.center;
            probe.boxProjection = source.boxProjection; probe.blendDistance = source.blendDistance;
            probe.importance = source.importance; probe.intensity = source.intensity;
            probe.resolution = source.resolution; probe.hdr = source.hdr;
        }

        // Removes scenery and terrain trees that no viewpoint inside the play area can see.
        private static void CullHidden(Transform environment, Vector2[] outline)
        {
            var terrain = environment.GetComponentInChildren<Terrain>();
            var eyes = Viewpoints(outline, terrain);
            var candidates = Groups.Where(g => g != "Water").SelectMany(g => environment.Find(g).Cast<Transform>()).ToList();
            var seen = VisibleCandidates(environment, terrain, candidates, eyes);
            int removed = 0;
            for (int i = 0; i < candidates.Count; i++)
                if (!seen.Contains(i)) { UnityEngine.Object.DestroyImmediate(candidates[i].gameObject); removed++; }
            Physics.SyncTransforms();
            var data = terrain.terrainData;
            var ground = terrain.GetComponent<TerrainCollider>();
            var trees = data.treeInstances;
            var kept = trees.Where(tree =>
            {
                var target = terrain.transform.position + Vector3.Scale(tree.position, data.size) + Vector3.up * 4 * tree.heightScale;
                foreach (var eye in eyes)
                {
                    var line = target - eye;
                    float distance = line.magnitude;
                    if (!ground.Raycast(new Ray(eye, line / distance), out _, distance - .5f)) return true;
                }
                return false;
            }).ToArray();
            data.SetTreeInstances(kept, false);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            Debug.Log($"Lakebed visibility: removed {removed} of {candidates.Count} scenery objects and {trees.Length - kept.Length} of {trees.Length} trees unseen from {eyes.Count} viewpoints.");
        }

        // Eye positions on a grid over the play area and along its edge, at ground and ceiling height.
        private static List<Vector3> Viewpoints(Vector2[] outline, Terrain terrain)
        {
            var points = new List<Vector2>();
            var min = new Vector2(outline.Min(v => v.x), outline.Min(v => v.y));
            var max = new Vector2(outline.Max(v => v.x), outline.Max(v => v.y));
            for (float x = min.x; x <= max.x; x += 12)
            for (float z = min.y; z <= max.y; z += 12)
                if (InPlayArea(outline, new Vector2(x, z))) points.Add(new Vector2(x, z));
            var centre = outline.Aggregate(Vector2.zero, (sum, v) => sum + v) / outline.Length;
            for (int i = 0; i < outline.Length; i += 2) points.Add(Vector2.MoveTowards(outline[i], centre, 1.2f));
            var eyes = new List<Vector3>();
            foreach (var point in points)
            {
                float ground = terrain.SampleHeight(new Vector3(point.x, 0, point.y)) + terrain.transform.position.y;
                eyes.Add(new Vector3(point.x, ground + 1.7f, point.y));
                eyes.Add(new Vector3(point.x, FlightCeiling + 1.7f, point.y));
            }
            return eyes;
        }

        // Renders each candidate in a unique flat colour from every viewpoint; terrain is black.
        private static HashSet<int> VisibleCandidates(Transform environment, Terrain terrain, List<Transform> candidates, List<Vector3> eyes)
        {
            var seen = new HashSet<int>();
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var idMaterial = new Material(unlit);
            var black = new Material(unlit); black.SetColor("_BaseColor", Color.black);
            var restored = new List<(Renderer renderer, Material[] materials)>();
            var hidden = new List<Renderer>();
            var owned = new HashSet<Renderer>();
            var block = new MaterialPropertyBlock();
            var terrainMaterial = terrain.materialTemplate;
            bool instanced = terrain.drawInstanced, foliage = terrain.drawTreesAndFoliage, fog = RenderSettings.fog;
            float lodBias = QualitySettings.lodBias;
            var cameraObject = new GameObject("Visibility probe") { hideFlags = HideFlags.HideAndDontSave };
            var texture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            try
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    int id = i + 1;
                    var colour = new Color32((byte)((id & 63) * 4 + 2), (byte)(((id >> 6) & 63) * 4 + 2), (byte)(((id >> 12) & 63) * 4 + 2), 255);
                    foreach (var renderer in candidates[i].GetComponentsInChildren<Renderer>())
                    {
                        owned.Add(renderer);
                        restored.Add((renderer, renderer.sharedMaterials));
                        renderer.sharedMaterials = Enumerable.Repeat(idMaterial, renderer.sharedMaterials.Length).ToArray();
                        block.Clear(); block.SetColor("_BaseColor", colour); renderer.SetPropertyBlock(block);
                    }
                }
                foreach (var renderer in environment.root.GetComponentsInChildren<Renderer>())
                    if (!owned.Contains(renderer) && renderer.enabled) { renderer.enabled = false; hidden.Add(renderer); }
                terrain.drawInstanced = false; terrain.drawTreesAndFoliage = false; terrain.materialTemplate = black;
                RenderSettings.fog = false;
                // Sampling uses a wider view than play; keep finer LODs so nothing is dropped early.
                QualitySettings.lodBias = lodBias * 2;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.fieldOfView = 90; camera.aspect = 1; camera.nearClipPlane = .2f; camera.farClipPlane = 4000;
                camera.allowHDR = false; camera.allowMSAA = false; camera.targetTexture = texture;
                var cameraData = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = false; cameraData.renderShadows = false;
                cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                var faces = new[] { Quaternion.identity, Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 270, 0), Quaternion.Euler(-90, 0, 0), Quaternion.Euler(90, 0, 0) };
                foreach (var eye in eyes)
                foreach (var face in faces)
                {
                    camera.transform.SetPositionAndRotation(eye, face);
                    camera.Render();
                    RenderTexture.active = texture;
                    pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    RenderTexture.active = null;
                    foreach (var pixel in pixels.GetPixels32())
                    {
                        if (pixel.r < 2 && pixel.g < 2 && pixel.b < 2) continue;
                        int id = (pixel.r >> 2) | ((pixel.g >> 2) << 6) | ((pixel.b >> 2) << 12);
                        if (id >= 1 && id <= candidates.Count) seen.Add(id - 1);
                    }
                }
            }
            finally
            {
                foreach (var (renderer, materials) in restored) { renderer.sharedMaterials = materials; renderer.SetPropertyBlock(null); }
                foreach (var renderer in hidden) renderer.enabled = true;
                terrain.materialTemplate = terrainMaterial; terrain.drawInstanced = instanced; terrain.drawTreesAndFoliage = foliage;
                RenderSettings.fog = fog; QualitySettings.lodBias = lodBias;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(idMaterial); UnityEngine.Object.DestroyImmediate(black);
            }
            return seen;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "Lakebed");
        }

        private static void Remove(Transform item) { if (item != null) UnityEngine.Object.DestroyImmediate(item.gameObject); }
    }
}
