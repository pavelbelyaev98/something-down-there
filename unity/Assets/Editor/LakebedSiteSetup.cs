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
    public static partial class LakebedSiteSetup
    {
        public const string Folder = "Assets/Content/Lakebed";
        public const string TerrainDataPath = Folder + "/LakebedTerrain.asset";
        public const string RimMeshPath = Folder + "/ExcavationRim.asset";
        public const string LakeMeshPath = Folder + "/LakeSurface.asset";
        public const string SedimentLayerPath = Folder + "/PackedSediment.terrainlayer";
        public const string DryTurfLayerPath = Folder + "/DryTurf.terrainlayer";
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
        // Metres beyond the dig plot outline: level ground (it holds the camp and pads to the
        // south), then a blend back into the demo terrain.
        private const float CampFlat = 10f, CampBlend = 19f, CampClearance = 26f, PeakRange = 1600f;
        private const float TileSize = 10f, GrassTile = 20f;
        // The walkable drained section stops this far inside its mapped edge, at the water line.
        private const float PlayAreaInset = 9f;
        // Highest point the player's feet can reach above the lakebed ground.
        public const float FlightCeiling = 16f;
        // Metres beyond the plot outline: plants and debris stay this far out, and a band of
        // trampled mud surrounds the plot out to about twice that.
        public const float DressingClearance = 3f;
        // The rim collar's roof reaches this far past the grid rectangle, under the terrain.
        private const float CollarOverlap = .5f;
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
                section = Shape(source.terrainData, source.transform.position, DrainedRocks(demo));
                environment = new GameObject("Environment").transform;
                environment.SetParent(root, false);
                // Every scenery collider reports the permanent-boundary prompt instead of digging.
                environment.gameObject.AddComponent<PermanentTerrainBoundary>();
                BuildTerrain(source, section, environment, stations);
                CopyObjects(demo, section, environment);
                BuildWater(demo, section, environment.Find("Water"));
                BuildTrickles(section, environment.Find("Water"));
                CopyReflections(demo, environment);
                ScatterDebris(section, environment, stations);
            }
            finally
            {
                SceneManager.SetActiveScene(scene);
                EditorSceneManager.CloseScene(demo, true);
            }
            BuildPlayArea(environment, area);
            CullHidden(environment, area);
            var soil = new SerializedObject(root.GetComponentInChildren<TerrainVolume>()).FindProperty("soilMaterial").objectReferenceValue as Material;
            BuildRim(surface, soil);
            MainGameSceneBuilder.PlaceBedrock(root.Find("Bedrock"));
            root.GetComponentInChildren<Camera>().farClipPlane = 3000;
            GroundTextureSetup.ConfigureGroundMaterials(root);
            SunPresentationSetup.Configure();
            ConfigureWater(root);
            ConfigurePerformance(root);
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
            // Heights before channel carving: the lake follows these, the trickles own the channels.
            public float[,] Uncarved;
            // Metres from the nearest channel bed edge, negative inside a bed.
            public float[,] Channel;
            // Water surface over the wet beds (demo-space heights), NaN where dry.
            public float[,] StreamLevel;
            // Site-local footprints of the demo's boulders on the drained bed; channels bend around them.
            public List<(Vector2 centre, float radius)> Rocks = new List<(Vector2, float)>();
            public readonly List<Stream> Streams = new List<Stream>();
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

        private static Section Shape(TerrainData source, Vector3 sourcePosition, List<(Vector2, float)> rocks)
        {
            int resolution = source.heightmapResolution;
            if (source.alphamapResolution != resolution - 1) throw new InvalidOperationException("Demo splat map no longer matches its heightmap.");
            var s = new Section { Cell = source.size.x / (resolution - 1), Height = source.size.y, Rocks = rocks };
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
                    else h = Mathf.Max(h, IslandHeight(local));
                }
                float beyond = SiteLayout.BeyondOpening(local);
                if (beyond < CampBlend) h = Mathf.Lerp(SiteInDemo.y + SiteLayout.GroundTop, h, Smooth((beyond - CampFlat) / (CampBlend - CampFlat)));
                s.After[z, x] = h;
            }
            CarveChannels(s);
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

        private static float DrainedWeight(Vector2 local) => 1 - Smooth((DrainedEdge(local) + 10) / 10);

        // Approximate metres beyond the drained section's outline; negative inside it.
        private static float DrainedEdge(Vector2 local)
        {
            var q = local - DrainedCentre;
            float angle = Mathf.Atan2(q.y, q.x);
            float normalized = new Vector2(q.x / DrainedRadii.x, q.y / DrainedRadii.y).magnitude;
            float edge = 1 + .09f * Mathf.Sin(3 * angle + 1.3f) + .05f * Mathf.Sin(7 * angle + .4f);
            return (normalized - edge) * Mathf.Min(DrainedRadii.x, DrainedRadii.y);
        }

        // Footprints of the demo's colliding boulders standing on the drained bed.
        private static List<(Vector2, float)> DrainedRocks(Scene demo)
        {
            var rocks = new List<(Vector2, float)>();
            foreach (string name in new[] { "Boulders", "BigBoulders" })
            foreach (Transform item in demo.GetRootGameObjects().Single(o => o.name == name).transform)
            {
                if (item.GetComponentInChildren<Collider>() == null) continue;
                var bounds = WorldBounds(item);
                var centre = new Vector2(bounds.center.x - SiteInDemo.x, bounds.center.z - SiteInDemo.z);
                if (DrainedEdge(centre) < 5) rocks.Add((centre, Mathf.Max(bounds.extents.x, bounds.extents.z) * .9f));
            }
            return rocks;
        }

        // Nearly level mud, tilting gently toward the remaining lake in the west, with low
        // hummocks and hollows left by the retreating water.
        private static float DrainedFloor(Vector2 local) =>
            SiteInDemo.y - .05f + .012f * local.x
            + .3f * (Mathf.PerlinNoise(local.x / 28 + 40, local.y / 28 + 12) - .5f)
            + .18f * (Mathf.PerlinNoise(local.x / 9 + 3, local.y / 9 + 17) - .5f)
            + .06f * (Mathf.PerlinNoise(local.x / 5 + 7, local.y / 5 + 91) - .5f);

        // Low sand islands stranded in the remaining lake off the drained shore: broad grassy
        // tops over a narrow sandy rim that shelves into the water.
        private static readonly (Vector2 centre, float radius)[] Islands =
        {
            (new Vector2(-57, 20), 7.5f), (new Vector2(-62, -26), 6), (new Vector2(-54, 46), 5), (new Vector2(-72, 2), 5.5f)
        };

        private static float IslandHeight(Vector2 local)
        {
            float best = float.MinValue;
            foreach (var (centre, radius) in Islands)
            {
                var offset = local - centre;
                float edge = radius * (1 + .25f * (Noise(offset, 4, centre.x * .1f) - .5f) + .15f * Mathf.Sin(3 * Mathf.Atan2(offset.y, offset.x) + centre.y));
                float t = offset.magnitude / edge;
                best = Mathf.Max(best, WaterLevel + .5f - 1.3f * t * t * t - .12f * Noise(offset, 1.8f, 3.3f));
            }
            return best;
        }

        private static float Noise(Vector2 local, float scale, float seed) => Mathf.PerlinNoise(local.x / scale + seed, local.y / scale + seed * 1.7f);

        private static void BuildTerrain(Terrain source, Section s, Transform environment, Rect[] stations)
        {
            var from = source.terrainData;
            // Create other assets first: an asset import after CreateAsset reloads the unsaved terrain data.
            var sediment = SedimentLayer(s.TerrainPosition);
            var dryTurf = DryTurfLayer();
            var lakebedDetails = LakebedDetails(from);
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
            // Up to eight layers keep the terrain to two splat passes.
            data.terrainLayers = from.terrainLayers.Append(sediment).Append(dryTurf).ToArray();
            data.SetAlphamaps(0, 0, Paint(from, s));
            data.SetDetailScatterMode(from.detailScatterMode);
            int detailScale = (from.heightmapResolution - 1) / from.detailResolution;
            data.SetDetailResolution(WindowCells / detailScale, from.detailResolutionPerPatch);
            data.detailPrototypes = from.detailPrototypes.Concat(lakebedDetails).ToArray();
            CopyDetails(from, s, data, detailScale, stations);
            DressDetails(s, data, detailScale, from.detailPrototypes.Length, stations);
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

        // The pack's Mountains gravel, tinted as warm packed lakebed sediment. The dig surface
        // cap uses the same texture, tint and world-aligned tiling, so the plot has no seam.
        private static TerrainLayer SedimentLayer(Vector3 terrainPosition)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SedimentLayerPath);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, SedimentLayerPath); }
            float tile = GroundTextureSetup.PackedSedimentTileMetres;
            layer.diffuseTexture = GroundTextureSetup.PackTexture("Gravel", "Albedo");
            layer.normalMapTexture = GroundTextureSetup.PackTexture("Gravel", "Normal");
            layer.maskMapTexture = GroundTextureSetup.PackTexture("Gravel", "Roughness");
            layer.tileSize = Vector2.one * tile;
            // Terrain UVs start at its corner; this offset lines them up with world-space tiling.
            layer.tileOffset = new Vector2(Mathf.Repeat(terrainPosition.x, tile), Mathf.Repeat(terrainPosition.z, tile));
            layer.normalScale = .8f;
            layer.diffuseRemapMax = GroundTextureSetup.PackedSedimentTint;
            layer.maskMapRemapMin = new Vector4(0, .65f, 0, 0);
            layer.maskMapRemapMax = new Vector4(1, 1, 1, .15f);
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssetIfDirty(layer);
            return layer;
        }

        // The Mountains pack's natural grass, tinted toward sun-dried olive, for the exposed bed and
        // its islands; the Highlands lime is too saturated to tint. The canyon keeps its own grass.
        private static TerrainLayer DryTurfLayer()
        {
            var source = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/BK/PureNature_Mountains/Textures/Surfaces/Layers/Grass01.terrainlayer");
            if (source == null) throw new InvalidOperationException("Missing approved Mountains grass layer.");
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DryTurfLayerPath);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, DryTurfLayerPath); }
            layer.diffuseTexture = source.diffuseTexture;
            layer.normalMapTexture = source.normalMapTexture;
            layer.maskMapTexture = source.maskMapTexture;
            layer.tileSize = source.tileSize;
            layer.tileOffset = source.tileOffset;
            layer.normalScale = source.normalScale;
            layer.metallic = source.metallic;
            layer.smoothness = source.smoothness;
            layer.specular = source.specular;
            layer.maskMapRemapMin = source.maskMapRemapMin;
            layer.maskMapRemapMax = source.maskMapRemapMax;
            layer.diffuseRemapMin = source.diffuseRemapMin;
            layer.diffuseRemapMax = Vector4.Scale(source.diffuseRemapMax, new Vector4(1, .85f, .62f, 1));
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssetIfDirty(layer);
            return layer;
        }

        private static float[,,] Paint(TerrainData from, Section s)
        {
            int Layer(string name) => Array.FindIndex(from.terrainLayers, l => l != null && l.name == name);
            int rubble = Layer("Mud_rubble"), mud = Layer("Mud"), sand = Layer("Sand"), gravel = Layer("Sand_rubble");
            int lawn = Layer("Grass"), meadow = Layer("Mud_grass");
            if (new[] { rubble, mud, sand, gravel, lawn, meadow }.Any(i => i < 0)) throw new InvalidOperationException("Demo terrain layers changed.");
            var source = from.GetAlphamaps(s.I0, s.J0, WindowCells, WindowCells);
            // The project sediment and muted turf layers follow the demo's layers.
            int halo = source.GetLength(2), sediment = halo, grassy = halo + 1, layers = halo + 2;
            var alpha = new float[WindowCells, WindowCells, layers];
            for (int z = 0; z < WindowCells; z++)
            for (int x = 0; x < WindowCells; x++)
            for (int l = 0; l < halo; l++) alpha[z, x, l] = source[z, x, l];
            var target = new float[layers];
            for (int z = 0; z < WindowCells; z++)
            for (int x = 0; x < WindowCells; x++)
            {
                var local = s.Local(x + .5f, z + .5f);
                // The canyon's vivid grass fades into the muted turf on the low ground around the
                // drained section; the cliff tops keep the pack's colour.
                float mute = (1 - Smooth((DrainedEdge(local) - 8) / 30)) * Smooth((OldShore + 14 - s.After[z, x]) / 6);
                if (mute > 0)
                    foreach (int vivid in new[] { lawn, meadow })
                    {
                        float moved = alpha[z, x, vivid] * mute;
                        alpha[z, x, vivid] -= moved;
                        alpha[z, x, grassy] += moved;
                    }
                float camp = 1 - Smooth((SiteLayout.BeyondOpening(local) - CampFlat) / (CampBlend - CampFlat));
                float weight = Mathf.Max(s.Lake[z, x] ? Smooth(s.Shore[z, x] / 3) : 0, camp);
                if (weight <= 0) continue;
                float h = s.After[z, x], a = h - WaterLevel;
                float patches = Noise(local, 9, 3.1f), growth = Noise(local, 14, 5.3f), grain = Noise(local, 4, 1.7f);
                Array.Clear(target, 0, layers);
                if (a < -.15f) { target[sand] = .75f; target[mud] = .25f; }
                else if (s.Drained[z, x] > .35f)
                {
                    // Packed sediment on the flats; damp dark silt toward the water and channels;
                    // stony beds, pebble strand lines and sandy banks at the waterline.
                    float c = s.Channel[z, x];
                    float damp = Mathf.Max(Smooth((.95f - a) / .6f), Smooth((2.4f - c) / 2.2f));
                    // Broad darker mud patches break up the pale sediment, as on a real drying bed.
                    float mudPatch = Smooth((Noise(local, 16, 2.6f) - .45f) / .2f);
                    target[sediment] = (1 - damp) * (1.1f + .3f * grain) * (1 - .65f * mudPatch);
                    target[mud] = damp + (1 - damp) * (.25f * (1 - grain) + .9f * mudPatch);
                    // Channel beds are dark stony mud under the water, never pale beach sand.
                    float beach = Smooth((c - .3f) / .8f);
                    target[rubble] = Smooth((patches - .58f) / .1f) * (1 - damp) + 2.2f * Smooth((.3f - c) / .6f);
                    target[gravel] = 1.3f * Band(a, .1f, .6f) * (.5f + grain) * beach + .6f * Band(c, .3f, 1.6f, .3f);
                    // Green only returns on the higher ground toward the cliffs.
                    target[grassy] = .9f * Smooth((growth - .72f) / .08f) * Smooth((a - 1.3f) / .5f) * (1 - damp);
                    target[sand] = 1.6f * Smooth((.4f - a) / .3f) * beach + .7f * Band(c, .2f, 1.2f, .3f) * Smooth((patches - .45f) / .2f);
                }
                else if (IslandHeight(local) > WaterLevel - .3f)
                {
                    // Grassy island tops over sandy, gravelly rims sorted by the water.
                    float top = Smooth((IslandHeight(local) - WaterLevel - .12f) / .15f);
                    target[sand] = (1 - top) * (.8f + .4f * grain);
                    target[mud] = .45f * (1 - top) * (1 - grain);
                    target[gravel] = .5f * (1 - top) * Smooth((patches - .5f) / .2f);
                    target[grassy] = 1.5f * top;
                }
                else
                {
                    target[gravel] = .6f + .4f * patches;
                    target[mud] = .5f * grain;
                    target[sand] = 1.5f * Smooth((WaterLevel + .6f - h) / .5f);
                }
                if (camp > 0)
                {
                    // Around the plot and camp: dark trampled mud along its edge, then packed sediment
                    // with worked patches and gravel. The paler plot stands out inside it.
                    float trampled = 1 - Smooth((SiteLayout.BeyondOpening(local) - DressingClearance) / 3.5f);
                    for (int l = 0; l < layers; l++) target[l] *= 1 - camp;
                    target[sediment] += camp * (1 + .25f * grain) * (1 - .7f * trampled);
                    target[mud] += camp * (.35f * Smooth((patches - .45f) / .2f) + 1.2f * trampled * (.6f + .4f * grain));
                    target[rubble] += camp * .45f * Smooth((growth - .55f) / .15f);
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
                    float beyond = SiteLayout.BeyondOpening(local);
                    bool changed = s.After[z, x] < s.Before[z, x] - .3f || s.After[z, x] > s.Before[z, x] + .3f;
                    bool bed = s.Lake[z, x] && s.After[z, x] < OldShore - .2f;
                    if (!changed && !bed && beyond >= CampBlend - 6) continue;
                    map[v, u] = 0;
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
                if (SiteLayout.BeyondOpening(local) < CampBlend || Mathf.Abs(s.Sample(s.After, demo) - s.Sample(s.Before, demo)) > .2f) continue;
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
            for (int z = 0; z < WindowCells; z++)
            for (int x = 0; x < WindowCells; x++)
                solid[z, x] = SiteLayout.BeyondOpening(s.Local(x + .5f, z + .5f)) >= SiteLayout.RimBand - .5f;
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
                        // Follow the reshaped ground by its smallest change under the footprint, so no
                        // part floats; the rest settles a little deeper. Objects that would end up mostly
                        // buried (a channel cut beneath one side) are left out.
                        var (low, high) = Footprint(bounds, xz => s.Sample(s.After, new Vector3(xz.x, 0, xz.y)) - s.Sample(s.Before, new Vector3(xz.x, 0, xz.y)));
                        if (high - low > Mathf.Max(.3f, bounds.size.y * .7f)) { skipped++; continue; }
                        lift = Mathf.Abs(low) < .05f && Mathf.Abs(high) < .05f ? 0 : low;
                        if (lift != 0 && bounds.max.y + lift < WaterLevel + .15f) { skipped++; continue; }
                    }
                    var copy = Copy(item.gameObject, parent);
                    copy.transform.SetPositionAndRotation(pivot + Vector3.up * lift - site, item.rotation);
                    copy.transform.localScale = item.lossyScale;
                    // Boulders standing in the old lake or on the drained section are bare, water-worn rock
                    // rather than moss-topped.
                    if ((name == "Boulders" || name == "BigBoulders") && s.Contains(pivot)
                        && (s.InLake(pivot) || DrainedWeight(new Vector2(pivot.x - site.x, pivot.z - site.z)) > .5f)) Bare(copy);
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

        // Lowest and highest value of a field across a footprint, sampled inside its XZ bounds.
        private static (float low, float high) Footprint(Bounds bounds, Func<Vector2, float> field)
        {
            float low = float.MaxValue, high = float.MinValue;
            for (int gz = 0; gz < 3; gz++)
            for (int gx = 0; gx < 3; gx++)
            {
                float value = field(new Vector2(Mathf.Lerp(bounds.min.x, bounds.max.x, .15f + gx * .35f), Mathf.Lerp(bounds.min.z, bounds.max.z, .15f + gz * .35f)));
                low = Mathf.Min(low, value); high = Mathf.Max(high, value);
            }
            return (low, high);
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
                if (SiteLayout.BeyondOpening(s.Local((gx + .5f) * step, (gz + .5f) * step)) < CampFlat) continue;
                // The lake covers the uncarved bed and only the deep part of each carved channel mouth;
                // shallow channel water is the stream's own, which keeps real depth to its banks (flat
                // lake water over a nearly level bed draws an irregular polygonal waterline). Every
                // sample counts: shore dips between cell corners otherwise leave straight gaps.
                bool wet = false;
                for (int z = gz * step; z <= (gz + 1) * step && !wet; z++)
                for (int x = gx * step; x <= (gx + 1) * step && !wet; x++) wet = s.Uncarved[z, x] < below || s.After[z, x] < level - .3f;
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
            // CopySerialized updates the stored arrays but can leave the Editor's GPU buffers with
            // the previous shape until restart; rewrite the geometry through the Mesh API as well.
            using (var source = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var data = source[0];
                const MeshUpdateFlags keep = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
                saved.SetVertexBufferParams(mesh.vertexCount, mesh.GetVertexAttributes());
                for (int stream = 0; stream < data.vertexBufferCount; stream++)
                {
                    var bytes = data.GetVertexData<byte>(stream);
                    saved.SetVertexBufferData(bytes, 0, 0, bytes.Length, stream, keep);
                }
                if (data.indexFormat == IndexFormat.UInt16)
                {
                    var indices = data.GetIndexData<ushort>();
                    saved.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
                    saved.SetIndexBufferData(indices, 0, 0, indices.Length, keep);
                }
                else
                {
                    var indices = data.GetIndexData<uint>();
                    saved.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
                    saved.SetIndexBufferData(indices, 0, 0, indices.Length, keep);
                }
                saved.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++) saved.SetSubMesh(i, mesh.GetSubMesh(i), keep);
                saved.bounds = mesh.bounds;
            }
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            return saved;
        }

        // Closed collar: a lip following the plot outline over the terrain-hole edge and a roof
        // over the rest of the rectangular grid, in the dig surface's own ground material.
        private static void BuildRim(Transform surface, Material ground)
        {
            const int segments = 360;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                foreach (var v in new[] { a, b, c, d }) uv.Add(new Vector2(v.x, v.z) / TileSize);
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            float halfX = SiteLayout.Extent.x * .5f + CollarOverlap, halfZ = SiteLayout.Extent.z * .5f + CollarOverlap;
            Vector3 Point(float compass, float offset)
            {
                var p = SiteLayout.OpeningPoint(compass, offset);
                return new Vector3(p.x, 0, p.y);
            }
            Vector3 Outer(float compass)
            {
                float c = compass * Mathf.Deg2Rad, dx = Mathf.Abs(Mathf.Sin(c)), dz = Mathf.Abs(Mathf.Cos(c));
                float reach = Mathf.Min(dx > 1e-4f ? halfX / dx : float.MaxValue, dz > 1e-4f ? halfZ / dz : float.MaxValue);
                return new Vector3(Mathf.Sin(c), 0, Mathf.Cos(c)) * reach;
            }
            Vector3 top = Vector3.up * SiteLayout.RimTop, skirt = Vector3.up * (SiteLayout.GroundTop - .015f), bottom = Vector3.up * SiteLayout.RimBottom;
            for (int i = 0; i < segments; i++)
            {
                // Descending compass keeps the collar's faces pointing up.
                float a = 90 - i * 360f / segments, b = 90 - (i + 1) * 360f / segments;
                Vector3 ia = Point(a, 0), ib = Point(b, 0), ca = Point(a, SiteLayout.RimBand), cb = Point(b, SiteLayout.RimBand);
                Vector3 la = Point(a, SiteLayout.RimBand + .35f), lb = Point(b, SiteLayout.RimBand + .35f), oa = Outer(a), ob = Outer(b);
                Quad(ia + top, ib + top, cb + top, ca + top);
                Quad(ca + top, cb + top, lb + skirt, la + skirt);
                Quad(la + skirt, lb + skirt, ob, oa);
                Quad(ia + bottom, oa + bottom, ob + bottom, ib + bottom);
                Quad(ia + top, ia + bottom, ib + bottom, ib + top);
                Quad(oa, ob, ob + bottom, oa + bottom);
            }
            var mesh = new Mesh { name = "ExcavationRim" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            // Zero deposit weights: the ground shader draws plain soil under the sediment cap.
            mesh.SetUVs(2, new Vector2[vertices.Count]);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            mesh.SetPreBakeCollisionMesh(false, true);
            var saved = SaveMesh(mesh, RimMeshPath);
            var rim = new GameObject("Excavation rim");
            rim.transform.SetParent(surface, false);
            rim.AddComponent<MeshFilter>().sharedMesh = saved;
            rim.AddComponent<MeshRenderer>().sharedMaterial = ground;
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
