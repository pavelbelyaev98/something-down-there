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
    // Authors the worksite surroundings as a close match of the approved Pure Nature 2:
    // Mountains demo valley: green meadow, dense conifer forest, grass-ledged cliffs,
    // stream -> lake -> waterfall and hazy blue mountains. The diggable sediment pad is
    // untouched; the ground around it is non-diggable meadow and rock; steep terrain and
    // cliff colliders form the natural boundary (the old invisible Perimeter is removed).
    // Deterministic and re-runnable: the tool replaces MainGameRoot/Environment and
    // regenerates the terrain assets, never touching excavation, stations or save state.
    public static class ReservoirEnvironmentSetup
    {
        public const string EnvironmentName = "Environment";
        public const string TerrainFolder = "Assets/Content/Environment";
        public const string SedimentPath = "Assets/Content/Nature/ReservoirSediment.mat";
        private const string PrefabRoot = "Assets/BK/PureNature_Mountains/Prefabs/";
        private const string LayerRoot = "Assets/BK/PureNature_Mountains/Textures/Surfaces/Layers/";
        private const string TextureRoot = "Assets/BK/PureNature_Mountains/Textures/Surfaces/";
        private const string WaterMaterialRoot = "Assets/BK/PureNature_Mountains/Textures/Water/Materials/";
        private const int Seed = 640064;
        private const float PadHalf = 16f;
        private const float SiteClear = 15f;
        private const float TerrainBase = -12f;
        private const float TerrainHeight = 180f;

        private static readonly string[] Cliffs = Names("Rocks/Cliff", 1, 8);
        private static readonly string[] BigRocks = { "Rocks/Boulder1", "Rocks/Boulder2", "Rocks/Boulder3", "Rocks/Stone4", "Rocks/Stone5" };
        private static readonly string[] SmallRocks = { "Rocks/Stone1", "Rocks/Stone2", "Rocks/Stone3" };
        private static readonly string[] Pebbles = Names("Rocks/Pebble", 1, 3);
        private static readonly string[] Trees = new[] { "Trees/Fir1", "Trees/Fir2", "Trees/Fir3", "Trees/Fir4", "Trees/Fir5", "Trees/Fir6" }
            .Concat(Names("Trees/Pine", 1, 6)).Concat(Names("Trees/Spruce", 1, 6)).ToArray();
        private static readonly string[] Bushes = Names("Trees/Bush", 1, 3);
        private static readonly string[] GrassPlants = Names("Plants/Grass", 1, 4).Concat(Names("Plants/GrassMountain", 1, 4)).ToArray();
        private static readonly string[] Flowers = { "Plants/Daisy", "Plants/DaisyBlue", "Plants/Lupin1", "Plants/Lupin2",
            "Plants/Gorse1", "Plants/Gorse2", "Plants/Sorrel", "Plants/Fern1", "Plants/Fern2", "Plants/Reeds", "Plants/Carot1", "Plants/Berries" };
        private static readonly string[] Branches = { "Plants/Branchs" };
        private static readonly string[] Mountains = { "Mountains/Mountain1", "Mountains/Mountain2" };
        // Same detail set (and order) the vendor demo terrain uses, so the meadow
        // reads with the demo's grass/flowers density.
        private static readonly string[] Details = { "Plants/Grass1", "Plants/Grass2", "Plants/Grass3", "Plants/Grass4",
            "Plants/Carot1", "Plants/Carot2", "Plants/Sorrel", "Plants/Lupin1", "Plants/Lupin2", "Plants/Gorse1", "Plants/Gorse2",
            "Plants/Daisy", "Plants/DaisyBlue", "Plants/Branchs", "Plants/Fern1", "Plants/Fern2",
            "Rocks/Pebble1", "Rocks/Pebble2", "Rocks/Pebble3",
            "Plants/GrassMountain1", "Plants/GrassMountain2", "Plants/GrassMountain3", "Plants/GrassMountain4" };
        private static readonly string[] WaterPrefabs = { "Water/WaterStream", "Water/WaterFall" };

        private static readonly Vector2[] StreamPath =
        {
            new Vector2(7f, 22f), new Vector2(3f, 34f), new Vector2(-3f, 48f), new Vector2(-7f, 62f),
            new Vector2(-5f, 78f), new Vector2(0f, 94f), new Vector2(2f, 110f),
        };

        private static readonly Vector2[] TrailPath =
        {
            new Vector2(6f, -15f), new Vector2(17f, -19f), new Vector2(20f, -4f), new Vector2(19f, 12f),
            new Vector2(13f, 24f), new Vector2(7f, 42f), new Vector2(3f, 66f), new Vector2(0f, 96f),
        };

        private sealed class Tile
        {
            public string Name;
            public float MinX, MaxX, MinZ, MaxZ;
            public int Resolution;
        }

        private static readonly Tile[] Tiles =
        {
            new Tile { Name = "Reservoir North", MinX = -200, MaxX = 200, MinZ = 16, MaxZ = 300, Resolution = 513 },
            new Tile { Name = "Reservoir South", MinX = -200, MaxX = 200, MinZ = -120, MaxZ = -16, Resolution = 513 },
            new Tile { Name = "Reservoir East", MinX = 16, MaxX = 200, MinZ = -16, MaxZ = 16, Resolution = 513 },
            new Tile { Name = "Reservoir West", MinX = -200, MaxX = -16, MinZ = -16, MaxZ = 16, Resolution = 513 },
        };

        [MenuItem("Tools/Something Down There/Build Drained Reservoir Environment")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath && scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode to build the reservoir environment.");
            Transform root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            Transform existing = root.Find(EnvironmentName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            EnsureFolders();
            ValidateVendorPrefabs();

            var environment = new GameObject(EnvironmentName).transform;
            environment.SetParent(root, false);
            var terrainGroup = Group("Terrain", environment);
            var bankGroup = Group("Banks", environment);
            var forestGroup = Group("Forest", environment);
            var coverGroup = Group("Ground Cover", environment);
            var waterGroup = Group("Water", environment);
            var mountainGroup = Group("Mountains", environment);

            try
            {
                EditorUtility.DisplayProgressBar("Reservoir valley", "Building terrain tiles", .08f);
                var terrains = new List<Terrain>();
                foreach (Tile tile in Tiles)
                {
                    var terrain = CreateTerrain(tile);
                    terrain.transform.SetParent(terrainGroup, false);
                    terrains.Add(terrain);
                }
                for (int i = 0; i < terrains.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Reservoir valley", "Painting " + Tiles[i].Name, .18f + .1f * i);
                    PaintTerrain(terrains[i], Tiles[i]);
                }

                EditorUtility.DisplayProgressBar("Reservoir valley", "Configuring excavation grounds", .6f);
                ConfigureSediment(root, terrains);
                ConfigureGrass(root);
                RemoveInvisiblePerimeter(root);

                EditorUtility.DisplayProgressBar("Reservoir valley", "Scattering rocks and banks", .64f);
                var scatter = new Scatter(terrains);
                ScatterCliffs(scatter, bankGroup);
                ScatterBoulders(scatter, bankGroup);

                EditorUtility.DisplayProgressBar("Reservoir valley", "Planting the valley", .76f);
                ScatterForest(scatter, forestGroup);
                ScatterGroundCover(scatter, coverGroup);

                EditorUtility.DisplayProgressBar("Reservoir valley", "Pouring the stream and lake", .86f);
                ScatterWater(scatter, waterGroup);
                AddLakeReflection(environment);

                EditorUtility.DisplayProgressBar("Reservoir valley", "Raising the peaks", .9f);
                ScatterMountains(scatter, mountainGroup);

                EditorUtility.DisplayProgressBar("Reservoir valley", "Finishing the presentation", .94f);
                SunPresentationSetup.Configure();
                root.GetComponentInChildren<Camera>().farClipPlane = 3200f;

                EditorUtility.DisplayProgressBar("Reservoir valley", "Saving scene", .97f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("Reservoir valley built: " + scatter.Report + ".");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // --- Terrain -------------------------------------------------------

        private static Terrain CreateTerrain(Tile tile)
        {
            int resolution = tile.Resolution;
            var data = new TerrainData
            {
                name = tile.Name + " Data",
                heightmapResolution = resolution,
                alphamapResolution = 512,
                size = new Vector3(tile.MaxX - tile.MinX, TerrainHeight, tile.MaxZ - tile.MinZ)
            };
            data.SetDetailResolution(512, 16);
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                float wz = tile.MinZ + (float)z / (resolution - 1) * data.size.z;
                for (int x = 0; x < resolution; x++)
                {
                    float wx = tile.MinX + (float)x / (resolution - 1) * data.size.x;
                    heights[z, x] = Mathf.Clamp01((GroundHeight(wx, wz) - TerrainBase) / TerrainHeight);
                }
            }
            data.SetHeights(0, 0, heights);
            string path = TerrainFolder + "/" + tile.Name + ".asset";
            AssetDatabase.CreateAsset(data, path);
            Terrain terrain = Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();
            terrain.transform.position = new Vector3(tile.MinX, TerrainBase, tile.MinZ);
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 1200f;
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = true;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            // The demo runs high detail density over a limited distance; ours keeps
            // the meadow lush without paying for the whole 400 m tile.
            terrain.detailObjectDensity = .55f;
            terrain.detailObjectDistance = 220f;
            terrain.terrainData.terrainLayers = new[]
            {
                LoadAsset<TerrainLayer>(LayerRoot + "Gravel1.terrainlayer"),
                LoadAsset<TerrainLayer>(LayerRoot + "Gravel2.terrainlayer"),
                LoadAsset<TerrainLayer>(LayerRoot + "Grass01.terrainlayer"),
                LoadAsset<TerrainLayer>(LayerRoot + "Mud02.terrainlayer"),
                LoadAsset<TerrainLayer>(LayerRoot + "Mud01.terrainlayer"),
            };
            terrain.materialTemplate = LoadAsset<Material>(LayerRoot + "Terrain.mat");
            terrain.terrainData.detailPrototypes = Details.Select(CreateDetail).ToArray();
            return terrain;
        }

        private static DetailPrototype CreateDetail(string path)
        {
            var prefab = LoadAsset<GameObject>(PrefabRoot + path + ".prefab");
            return new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = .7f, maxWidth = 1.3f,
                minHeight = .7f, maxHeight = 1.4f,
                noiseSpread = .35f,
                healthyColor = new Color(.263f, .976f, .165f),
                dryColor = new Color(.804f, .737f, .102f),
            };
        }

        private static void PaintTerrain(Terrain terrain, Tile tile)
        {
            TerrainData data = terrain.terrainData;
            int resolution = data.alphamapResolution;
            var map = new float[resolution, resolution, 5];
            Vector3 origin = terrain.transform.position;
            for (int z = 0; z < resolution; z++)
            {
                float wz = origin.z + ((float)z + .5f) / resolution * data.size.z;
                for (int x = 0; x < resolution; x++)
                {
                    float wx = origin.x + ((float)x + .5f) / resolution * data.size.x;
                    float h = GroundHeight(wx, wz);
                    const float step = 1.5f;
                    float gx = (GroundHeight(wx + step, wz) - GroundHeight(wx - step, wz)) / (2f * step);
                    float gz = (GroundHeight(wx, wz + step) - GroundHeight(wx, wz - step)) / (2f * step);
                    float slope = Mathf.Sqrt(gx * gx + gz * gz);
                    float steep = Ramp(.45f, 1f, slope);
                    float stream = 1f - Ramp(2.5f, 9f, DistanceToPath(wx, wz, StreamPath));
                    float trail = 1f - Ramp(1.6f, 4.5f, DistanceToPath(wx, wz, TrailPath));
                    float lake = LakeMask(wx, wz);
                    float wet = Mathf.Max(Ramp(.3f, .8f, lake), stream * .85f);
                    float variation = .5f + .5f * Mathf.Sin(wx * .11f + wz * .17f) * Mathf.Sin(wx * .05f - wz * .07f + 2.2f);
                    // The demo meadow is Grass01 nearly everywhere; gravel only breaks
                    // through on steep faces, the path and the water line.
                    float grass = (1f - steep) * (.72f + .28f * variation) * (1f - .8f * trail) * (1f - .72f * wet);
                    float gravel = steep * .8f + trail * .9f + wet * .65f;
                    float gravelFine = gravel * (.35f + .3f * variation);
                    float mudSoft = wet * .32f * (1f - variation) * (1f - steep);
                    float mud = wet * .2f * variation * (1f - steep);
                    float total = grass + gravel + gravelFine + mudSoft + mud + 1e-4f;
                    map[z, x, 0] = gravel / total;
                    map[z, x, 1] = gravelFine / total;
                    map[z, x, 2] = grass / total;
                    map[z, x, 3] = mudSoft / total;
                    map[z, x, 4] = mud / total;
                }
            }
            data.SetAlphamaps(0, 0, map);
            PaintDetails(terrain, tile);
            EditorUtility.SetDirty(data);
        }

        private static void PaintDetails(Terrain terrain, Tile tile)
        {
            TerrainData data = terrain.terrainData;
            int details = data.detailResolution;
            var layers = new int[Details.Length][,];
            for (int i = 0; i < layers.Length; i++) layers[i] = new int[details, details];
            Vector3 origin = terrain.transform.position;
            for (int z = 0; z < details; z++)
            {
                float wz = origin.z + ((float)z + .5f) / details * data.size.z;
                for (int x = 0; x < details; x++)
                {
                    float wx = origin.x + ((float)x + .5f) / details * data.size.x;
                    float distance = Mathf.Sqrt(wx * wx + wz * wz);
                    float falloff = 1f - Ramp(175f, 265f, distance);
                    if (falloff <= 0f) continue;
                    float h = GroundHeight(wx, wz);
                    const float step = 1.5f;
                    float gx = (GroundHeight(wx + step, wz) - GroundHeight(wx - step, wz)) / (2f * step);
                    float gz = (GroundHeight(wx, wz + step) - GroundHeight(wx, wz - step)) / (2f * step);
                    float slope = Mathf.Sqrt(gx * gx + gz * gz);
                    float steep = Ramp(.45f, 1.05f, slope);
                    float stream = 1f - Ramp(2.2f, 8f, DistanceToPath(wx, wz, StreamPath));
                    float trail = 1f - Ramp(1.8f, 4f, DistanceToPath(wx, wz, TrailPath));
                    float lake = LakeMask(wx, wz);
                    float wet = Mathf.Max(Ramp(.3f, .8f, lake), stream * .8f);
                    float variation = .5f + .5f * Mathf.Sin(wx * .13f + wz * .19f + 2.7f) * Mathf.Sin(wx * .044f - wz * .06f);
                    float meadow = falloff * (1f - steep) * (1f - trail * .8f) * (1f - wet);
                    float alpine = falloff * Ramp(2f, 26f, h) * (1f - Ramp(84f, 118f, h)) * (.4f + .6f * variation);
                    float wetness = Mathf.Clamp01(stream + lake);
                    // Detail densities are bytes in the vendor demo (255 packed meadow);
                    // the previous 1..14 values rendered almost no grass at all.
                    int canopy = Byte(meadow * (.62f + .38f * variation) * 255f);
                    int mid = Byte(meadow * (.5f + .5f * (1f - variation)) * 205f);
                    int low = Byte(meadow * variation * 140f);
                    int sparse = Byte(meadow * (1f - variation) * 64f);
                    int carrots = Byte(meadow * Ramp(.55f, .85f, variation) * 30f);
                    int sorrel = Byte(meadow * Ramp(.35f, .75f, variation) * 120f);
                    int lupins = Byte(meadow * Ramp(.6f, .9f, variation) * 14f);
                    int gorse = Byte(falloff * Ramp(.45f, .8f, variation) * 26f);
                    int daisies = Byte(meadow * (variation > .55f ? 1f : 0f) * 58f);
                    int daisyBlue = Byte(meadow * (variation > .7f ? 1f : 0f) * 32f);
                    int branches = Byte(falloff * (variation > .82f ? 1f : 0f) * 14f);
                    int ferns = Byte(meadow * Ramp(.5f, .8f, variation) * 40f);
                    int pebbles = Byte(falloff * Mathf.Clamp01(wetness + steep) * 26f);
                    int alpineLow = Byte(alpine * 52f);
                    int alpineHigh = Byte(alpine * 96f);
                    layers[0][z, x] = canopy;
                    layers[1][z, x] = Mathf.RoundToInt(canopy * .8f);
                    layers[2][z, x] = Mathf.Clamp(low + Mathf.RoundToInt(mid * .5f), 0, 255);
                    layers[3][z, x] = sparse;
                    layers[4][z, x] = carrots;
                    layers[5][z, x] = Mathf.RoundToInt(carrots * .6f);
                    layers[6][z, x] = sorrel;
                    layers[7][z, x] = lupins;
                    layers[8][z, x] = Mathf.RoundToInt(lupins * .7f);
                    layers[9][z, x] = gorse;
                    layers[10][z, x] = Mathf.RoundToInt(gorse * .8f);
                    layers[11][z, x] = daisies;
                    layers[12][z, x] = daisyBlue;
                    layers[13][z, x] = branches;
                    layers[14][z, x] = ferns;
                    layers[15][z, x] = Mathf.RoundToInt(ferns * .5f);
                    layers[16][z, x] = pebbles;
                    layers[17][z, x] = Mathf.RoundToInt(pebbles * .7f);
                    layers[18][z, x] = Mathf.RoundToInt(pebbles * .5f);
                    layers[19][z, x] = alpineLow;
                    layers[20][z, x] = Mathf.RoundToInt(alpineLow * .7f);
                    layers[21][z, x] = alpineHigh;
                    layers[22][z, x] = Mathf.RoundToInt(alpineHigh * .6f);
                }
            }
            for (int i = 0; i < layers.Length; i++) data.SetDetailLayer(0, 0, i, layers[i]);
        }

        // Authoring height in metres. The valley is a shallow radial bowl, not a box
        // canyon: flat worksite pad -> walkable meadow -> forested slope -> one short
        // steep bank that does the containing -> a broken ridge line that settles low
        // enough for the vendor peaks to tower over it. A north corridor carves
        // through the bowl, descends past the stream into the lake and ends under the
        // terminal falls.
        public static float GroundHeight(float x, float z)
        {
            float ax = Mathf.Abs(x);
            float az = Mathf.Abs(z);
            float d = Mathf.Max(Mathf.Max(0f, ax - PadHalf), Mathf.Max(0f, az - PadHalf));
            float radius = Mathf.Sqrt(x * x + z * z);
            float lobe = LobeAt(x, z);
            float noise = RidgeNoise(x, z);

            float roll = .55f * Ramp(2f, 14f, d) * Mathf.Sin(x * .095f + 1.3f) * Mathf.Sin(z * .081f - .6f)
                       + .22f * Ramp(1f, 7f, d) * Mathf.Sin(x * .27f + z * .23f + 1.1f)
                       + 2.2f * Ramp(12f, 70f, radius) * noise;
            // Meadow the player can actually walk, then the treed slope.
            float meadow = 6f * Ramp(17f, MeadowEdge * lobe, radius);
            float slope = 20f * Ramp(MeadowEdge * lobe, SlopeEdge * lobe, radius);
            // The boundary is two over-steep steps with a grassy bench between them.
            // Each step beats the 45 deg character-controller slope limit, while the
            // bench catches grass and flowers - a single smooth ramp of the same height
            // renders as one stretched sheet of terrain texture, which is what the
            // previous pass looked like.
            float bankStart = SlopeEdge * lobe;
            float bank = 26f * Ramp(bankStart, bankStart + 13f, radius)
                       + 20f * Ramp(bankStart + 27f, bankStart + BankRun, radius);
            float ridge = (12f + 22f * noise) * Ramp(bankStart + BankRun, 260f, radius);
            float h = roll + meadow + slope + bank + ridge;

            float corridorT = Ramp(10f, 34f, z);
            if (corridorT > 0f)
            {
                float halfWidth = CorridorHalfWidth(z);
                // The channel floor stays above the waterline so the lake ends on a
                // readable shore instead of a sheet of water lying over the grass.
                float floor = -2.6f * Ramp(26f, 92f, z)
                            + .3f * Ramp(18f, 40f, z) * Mathf.Sin(x * .17f + z * .13f);
                float bench = .9f + .1f * Mathf.Sin(z * .033f + 2.4f);
                float corridorWall = bench * (26f * Ramp(halfWidth, halfWidth + 14f, ax)
                                            + 22f * Ramp(halfWidth + 28f, halfWidth + 44f, ax));
                float notch = (1f - Ramp(9f, 26f, ax)) * (1f - Ramp(160f, 170f, z));
                float terminal = 9f * Ramp(148f, 170f, z) * (1f - .92f * notch)
                               + (19f * Ramp(170f, 189f, z) + 21f * Ramp(193f, 214f, z)) * (1f - .55f * notch);
                float beyond = (20f + 22f * noise) * Ramp(halfWidth + 36f, halfWidth + 130f, ax);
                // The corridor only has authority over its own channel. Blending it in
                // on z alone let a shallow bearing cross the channel wall while the
                // blend was still weak, which smeared a 40 m wall into a walkable
                // diagonal ramp and opened the valley on a few headings.
                float authority = corridorT * (1f - Ramp(halfWidth + 24f, halfWidth + 80f, ax));
                float carved = Mathf.Lerp(h, floor + corridorWall + terminal + beyond, authority);
                h = Mathf.Max(carved, h * Ramp(halfWidth + 10f, halfWidth + 44f, ax));
            }

            float stream = 1f - Ramp(1.7f, 7f, DistanceToPath(x, z, StreamPath));
            h -= stream * 1.15f * Ramp(18f, 26f, z);
            h *= Ramp(0f, 4f, d);
            float lake = LakeMask(x, z);
            if (lake > 0f) h = Mathf.Lerp(h, -5.05f + .12f * Mathf.Sin(x * .18f) * Mathf.Sin(z * .22f), lake);
            return Mathf.Clamp(h, TerrainBase + .5f, TerrainBase + TerrainHeight - 4f);
        }

        // Radial control points, in metres from the worksite centre. Every scatter pass
        // reads the same numbers, so props always land on the band they were authored for.
        public const float MeadowEdge = 68f;
        public const float SlopeEdge = 106f;
        public const float BankRun = 43f;

        // The bowl edge is pushed in and out by a few integer harmonics of the bearing
        // (integer only, so the field stays continuous across +-pi) - otherwise the
        // valley reads as a drawn circle.
        public static float Lobe(float angle) =>
            1f + .2f * Mathf.Sin(3f * angle + 1.2f) + .1f * Mathf.Sin(5f * angle - .7f);

        public static float LobeAt(float x, float z) => Lobe(Mathf.Atan2(z, x));

        // Large-scale ridge field: keeps the skyline rolling instead of flat-topped.
        public static float RidgeNoise(float x, float z) =>
            .62f * Mathf.Sin(x * .0118f + 1.9f) * Mathf.Sin(z * .0102f - 1.1f)
          + .26f * Mathf.Sin(x * .0263f - 2.2f) * Mathf.Sin(z * .0231f + .7f)
          + .12f * Mathf.Sin(x * .0571f + .4f) * Mathf.Sin(z * .0498f - 2.6f);

        public static float CorridorHalfWidth(float z) =>
            42f - 14f * Ramp(30f, 84f, z) + 20f * Ramp(88f, 124f, z);

        // Water surface height; the basin is cut below it and the channel floor sits
        // above it, so the shoreline is where the terrain crosses this plane.
        public const float LakeLevel = -3.35f;

        public static float LakeMask(float x, float z)
        {
            float distance = Mathf.Max(Mathf.Abs(x) / 44f, Mathf.Abs(z - 122f) / 31f);
            return 1f - Ramp(.76f, 1.06f, distance);
        }

        public static float DistanceToPath(float x, float z, Vector2[] points)
        {
            float best = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(new Vector2(x, z), a + ab * t));
            }
            return best;
        }

        // --- Excavation grounds -------------------------------------------

        private static void ConfigureSediment(Transform root, List<Terrain> terrains)
        {
            Shader shader = Shader.Find(GroundTextureSetup.ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The ground shader must compile before integration.");
            Material sediment = AssetDatabase.LoadAssetAtPath<Material>(SedimentPath);
            if (sediment == null)
            {
                sediment = new Material(shader) { name = "ReservoirSediment" };
                AssetDatabase.CreateAsset(sediment, SedimentPath);
            }
            sediment.shader = shader;
            sediment.SetTexture("_SoilAlbedo", LoadAsset<Texture2D>(TextureRoot + "Mud01_a.png"));
            sediment.SetTexture("_SoilNormal", LoadAsset<Texture2D>(TextureRoot + "Mud01_n.png"));
            sediment.SetTexture("_SoilRoughness", LoadAsset<Texture2D>(TextureRoot + "Mud01_m.png"));
            sediment.SetTexture("_TurfAlbedo", LoadAsset<Texture2D>(TextureRoot + "Gravel_a.png"));
            sediment.SetTexture("_TurfNormal", LoadAsset<Texture2D>(TextureRoot + "Gravel_n.png"));
            sediment.SetTexture("_TurfRoughness", LoadAsset<Texture2D>(TextureRoot + "Gravel_m.png"));
            sediment.SetFloat("_TileMetres", 3.4f);
            sediment.SetFloat("_SoilTileMetres", 4.2f);
            sediment.SetFloat("_NormalStrength", .45f);
            sediment.SetFloat("_StoneNormalStrength", .55f);
            sediment.SetFloat("_TurfNormalStrength", .3f);
            sediment.SetFloat("_SurfaceHeight", 0f);
            sediment.SetFloat("_TurfDepth", .03f);
            sediment.SetFloat("_MacroVariation", .06f);
            EditorUtility.SetDirty(sediment);

            var terrain = root.GetComponentInChildren<TerrainVolume>();
            var settings = new SerializedObject(terrain);
            settings.FindProperty("soilMaterial").objectReferenceValue = sediment;
            var preview = settings.FindProperty("untouchedPreview").objectReferenceValue as GameObject;
            if (preview == null) throw new InvalidOperationException("Keep the existing edit-mode terrain preview.");
            preview.GetComponent<Renderer>().sharedMaterial = sediment;
            settings.ApplyModifiedPropertiesWithoutUndo();
            // The soft sediment spreads over the neutral rims; the camp side keeps
            // its firm turf so the two ground types stay readable on one level.
            foreach (string side in new[] { "North", "East", "West" })
                root.Find("Surface/" + side + " rim").GetComponent<Renderer>().sharedMaterial = sediment;
            // Terrain tiles must never reach over the dig opening.
            foreach (Terrain tile in terrains)
            {
                Bounds bounds = tile.terrainData.bounds;
                bounds.center += tile.transform.position;
                if (bounds.max.x > -PadHalf && bounds.min.x < PadHalf && bounds.max.z > -PadHalf && bounds.min.z < PadHalf)
                    throw new InvalidOperationException(tile.name + " overlaps the excavatable opening.");
            }
        }

        private static void ConfigureGrass(Transform root)
        {
            var grass = root.GetComponentInChildren<SurfaceGrassRenderer>();
            if (grass == null) throw new InvalidOperationException("MainGame must retain the approved moving grass.");
            var settings = new SerializedObject(grass);
            settings.FindProperty("coverage").floatValue = .025f;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        // The user-facing boundary is the natural valley wall now; the invisible
        // airspace/perimeter walls from the old arena must not exist anymore.
        private static void RemoveInvisiblePerimeter(Transform root)
        {
            Transform perimeter = root.Find("Perimeter");
            if (perimeter != null) UnityEngine.Object.DestroyImmediate(perimeter.gameObject);
        }

        private static void AddLakeReflection(Transform environment)
        {
            Transform existing = environment.Find("Lake reflection");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var probe = new GameObject("Lake reflection", typeof(ReflectionProbe)).GetComponent<ReflectionProbe>();
            probe.transform.SetParent(environment, false);
            probe.transform.position = new Vector3(0f, 8f, 122f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = 128;
            probe.size = new Vector3(280f, 140f, 240f);
            probe.boxProjection = true;
            probe.intensity = 1f;
            probe.nearClipPlane = .3f;
            probe.farClipPlane = 1000f;
            probe.cullingMask = ~0;
        }

        // --- Scatter ------------------------------------------------------

        private sealed class Scatter
        {
            private readonly List<Terrain> tiles;
            private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            private readonly Dictionary<string, Bounds> bounds = new Dictionary<string, Bounds>();
            public readonly System.Random Rng = new System.Random(Seed);
            public int Count { get; private set; }
            public int TreeCount { get; private set; }
            public int WaterCount { get; private set; }
            public string Report => $"{Count} vendor props ({TreeCount} trees, {WaterCount} water), {tiles.Count} terrain tiles";

            public Scatter(List<Terrain> tiles) { this.tiles = tiles; }

            public float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
            public int Range(int min, int max) => Rng.Next(min, max + 1);
            public T Pick<T>(T[] items) => items[Rng.Next(items.Length)];

            public float Slope(float x, float z)
            {
                const float step = 2f;
                float gx = (GroundHeight(x + step, z) - GroundHeight(x - step, z)) / (2f * step);
                float gz = (GroundHeight(x, z + step) - GroundHeight(x, z - step)) / (2f * step);
                return Mathf.Sqrt(gx * gx + gz * gz);
            }

            public bool Ground(float x, float z, out float height)
            {
                foreach (Terrain tile in tiles)
                {
                    Vector3 position = tile.transform.position;
                    Vector3 size = tile.terrainData.size;
                    if (x < position.x || x > position.x + size.x || z < position.z || z > position.z + size.z) continue;
                    height = tile.SampleHeight(new Vector3(x, 0f, z)) + position.y;
                    return true;
                }
                height = 0f;
                return false;
            }

            public GameObject Spawn(Transform parent, string path, float x, float z, float yaw, float scale, float embedFraction)
            {
                if (!Ground(x, z, out float ground)) return null;
                GameObject prefab = Prefab(path);
                Bounds local = BoundsOf(path, prefab);
                float y = ground - embedFraction * local.size.y - local.min.y * scale;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                item.transform.position = new Vector3(x, y, z);
                item.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                item.transform.localScale = Vector3.one * scale;
                item.name = prefab.name + " " + Count;
                Count++;
                return item;
            }

            public GameObject SpawnAt(Transform parent, string path, Vector3 position, float yaw, float scale)
            {
                GameObject prefab = Prefab(path);
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                item.transform.position = position;
                item.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                item.transform.localScale = Vector3.one * scale;
                item.name = prefab.name + " " + Count;
                Count++;
                return item;
            }

            public GameObject SpawnTree(Transform parent, float x, float z, float scale)
            {
                TreeCount++;
                return Spawn(parent, Pick(Trees), x, z, Range(0f, 360f), scale, Range(.03f, .1f));
            }

            public void CountWater() => WaterCount++;

            public float SizeOf(string path) => BoundsOf(path, Prefab(path)).size.y;

            public Vector3 ExtentsOf(string path) => BoundsOf(path, Prefab(path)).size;

            private GameObject Prefab(string path)
            {
                if (prefabs.TryGetValue(path, out GameObject cached)) return cached;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + path + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing approved vendor prefab: " + path);
                prefabs.Add(path, prefab);
                return prefab;
            }

            private Bounds BoundsOf(string path, GameObject prefab)
            {
                if (bounds.TryGetValue(path, out Bounds cached)) return cached;
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Prefab has no renderer: " + path);
                Bounds local = renderers[0].bounds;
                foreach (Renderer renderer in renderers) local.Encapsulate(renderer.bounds);
                local.center -= prefab.transform.position;
                bounds.Add(path, local);
                return local;
            }
        }

        private static bool ClearOfSite(float x, float z) =>
            Mathf.Abs(x) >= SiteClear || Mathf.Abs(z) >= SiteClear;

        private static bool ClearOfWater(float x, float z) =>
            LakeMask(x, z) < .05f && DistanceToPath(x, z, StreamPath) > 5f;

        // Vendor cliffs are 64 x 30 x 58 m at scale 1, so a 1.1 scale already spans
        // 70 m. Keep their faces clear of the worksite pad and the corridor floor.
        private static bool CliffClearsPad(float x, float z, float scale) =>
            Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - 32f * scale >= 17.5f;

        // Anything inside the corridor mouth belongs to the channel, not the bowl rim.
        private static bool InCorridor(float x, float z) =>
            z > 12f && Mathf.Abs(x) < CorridorHalfWidth(z) + 12f;

        private static bool CliffClearsCorridor(float x, float z, float scale) =>
            z < 14f || z > 146f || Mathf.Abs(x) - 18f * scale >= CorridorHalfWidth(z) - 12f;

        private static void ScatterCliffs(Scatter scatter, Transform parent)
        {
            Transform group = Group("Rock walls", parent);
            // Unity's terrain material projects planar UVs, so any face steep enough to
            // shed grass renders as a vertical smear. Rather than hand-placed rings
            // that go stale the moment the height field is retuned, the generator walks
            // the whole valley and drops overlapping vendor cliffs onto every steep
            // patch it finds - bank, corridor shoulders, lake basin and terminal wall
            // are all clothed by the same rule.
            var placed = new List<Vector2>();
            for (float z = -116f; z <= 294f; z += 7f)
            for (float x = -194f; x <= 194f; x += 7f)
            {
                float px = x + scatter.Range(-3.5f, 3.5f);
                float pz = z + scatter.Range(-3.5f, 3.5f);
                if (scatter.Slope(px, pz) < .8f) continue;
                if (!ClearOfSite(px, pz) || GroundHeight(px, pz) < LakeLevel + 1f) continue;
                // The waterfall lane must stay an open notch.
                if (Mathf.Abs(px) < 13f && pz > 138f && pz < 170f) continue;
                var point = new Vector2(px, pz);
                bool crowded = false;
                foreach (Vector2 other in placed)
                    if ((other - point).sqrMagnitude < 144f) { crowded = true; break; }
                if (crowded) continue;
                float scale = scatter.Range(.46f, .8f);
                if (!CliffClearsPad(px, pz, scale) || !CliffClearsCorridor(px, pz, scale)) continue;
                placed.Add(point);
                PlaceCliff(scatter, group, px, pz, scale,
                    Mathf.Clamp(.16f + .24f * scatter.Slope(px, pz), .2f, .62f) + scatter.Range(-.05f, .05f));
            }
            // Full-size cliffs capping the bank read as the valley's headwall the way
            // the demo's do; the slope pass alone is all mid-size rock.
            for (int index = 0; index < 34; index++)
            {
                float angle = (index + scatter.Range(-.4f, .4f)) / 34f * Mathf.PI * 2f;
                float radius = (SlopeEdge + BankRun - 4f) * Lobe(angle) + scatter.Range(-6f, 6f);
                float px = Mathf.Cos(angle) * radius;
                float pz = Mathf.Sin(angle) * radius;
                if (InCorridor(px, pz)) continue;
                float scale = scatter.Range(.85f, 1.25f);
                if (!CliffClearsPad(px, pz, scale)) continue;
                PlaceCliff(scatter, group, px, pz, scale, scatter.Range(.42f, .62f));
            }
            // A few low outcrops standing in the meadow, like the vendor demo.
            for (int index = 0; index < 12; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(38f, 64f);
                float px = Mathf.Cos(angle) * radius;
                float pz = Mathf.Sin(angle) * radius;
                float scale = scatter.Range(.34f, .5f);
                if (!ClearOfSite(px, pz) || !ClearOfWater(px, pz) || !CliffClearsPad(px, pz, scale)) continue;
                PlaceCliff(scatter, group, px, pz, scale, scatter.Range(.5f, .7f));
            }
        }

        private static void PlaceCliff(Scatter scatter, Transform parent, float x, float z, float scale, float embed)
        {
            GameObject cliff = scatter.Spawn(parent, scatter.Pick(Cliffs), x, z, scatter.Range(0f, 360f), scale, embed);
            if (cliff == null) return;
            // Spawn seats props on the ground under their pivot; a 40 m wide cliff on a
            // steep face needs seating against the lowest ground it covers, or its
            // downhill half floats with daylight beneath it.
            float reach = 26f * scale;
            float here = GroundHeight(x, z);
            float low = Mathf.Min(Mathf.Min(GroundHeight(x - reach, z), GroundHeight(x + reach, z)),
                                  Mathf.Min(GroundHeight(x, z - reach), GroundHeight(x, z + reach)));
            cliff.transform.position -= new Vector3(0f, Mathf.Clamp(here - low, 0f, 20f) * .45f, 0f);
            // The grass-faced variants read as mossy alpine rock and keep shaded
            // outcrops from going coal-black against the green banks.
            foreach (string name in new[] { "Cliff01", "Cliff02", "Cliff03", "Cliff04", "Cliff05", "Cliff06", "Cliff07", "Cliff08" })
            {
                var grass = AssetDatabase.LoadAssetAtPath<Material>(PrefabRoot.Replace("Prefabs/", "Models/Rocks/Textures/Materials/") + name + "_Grass.mat");
                if (grass == null) continue;
                foreach (Renderer renderer in cliff.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.name != name) continue;
                    renderer.sharedMaterial = grass;
                }
            }
        }

        private static void ScatterBoulders(Scatter scatter, Transform parent)
        {
            Transform group = Group("Boulders", parent);
            // Meadow ring: readable boulders outside the worksite pad.
            for (int index = 0; index < 230; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(18f, MeadowEdge * Lobe(angle));
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (!ClearOfSite(x, z) || !ClearOfWater(x, z) || scatter.Slope(x, z) > 1.3f) continue;
                bool big = scatter.Rng.NextDouble() < .3;
                string path = big ? scatter.Pick(BigRocks) : scatter.Pick(SmallRocks);
                scatter.Spawn(group, path, x, z, scatter.Range(0f, 360f), big ? scatter.Range(1.6f, 3.6f) : scatter.Range(.8f, 2f), scatter.Range(.25f, .5f));
            }
            // Stream and lake shores.
            for (int index = 0; index < 110; index++)
            {
                float t = scatter.Range(0f, 1f);
                int segment = Mathf.Min(StreamPath.Length - 2, (int)(t * (StreamPath.Length - 1)));
                Vector2 a = StreamPath[segment], b = StreamPath[segment + 1];
                Vector2 point = Vector2.Lerp(a, b, scatter.Range(0f, 1f));
                float side = scatter.Rng.NextDouble() < .5 ? -1f : 1f;
                float x = point.x + side * scatter.Range(2.6f, 6.5f) + scatter.Range(-1f, 1f);
                float z = point.y + scatter.Range(-1f, 1f);
                if (!ClearOfSite(x, z) || scatter.Slope(x, z) > 1.3f) continue;
                scatter.Spawn(group, scatter.Pick(SmallRocks), x, z, scatter.Range(0f, 360f), scatter.Range(.7f, 1.8f), scatter.Range(.3f, .5f));
            }
            for (int index = 0; index < 90; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float x = Mathf.Cos(angle) * scatter.Range(44f, 60f);
                float z = 122f + Mathf.Sin(angle) * scatter.Range(28f, 42f);
                if (!ClearOfSite(x, z) || scatter.Slope(x, z) > 1.3f) continue;
                bool big = scatter.Rng.NextDouble() < .3;
                string path = big ? scatter.Pick(BigRocks) : scatter.Pick(SmallRocks);
                scatter.Spawn(group, path, x, z, scatter.Range(0f, 360f), big ? scatter.Range(1.4f, 3f) : scatter.Range(.8f, 2f), scatter.Range(.3f, .5f));
            }
            // Pebble fields in the meadow.
            Transform pebbles = Group("Pebbles", parent);
            for (int index = 0; index < 320; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(17f, (MeadowEdge + 24f) * Lobe(angle));
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (!ClearOfSite(x, z) || !ClearOfWater(x, z) || scatter.Slope(x, z) > 1.3f) continue;
                scatter.Spawn(pebbles, scatter.Pick(Pebbles), x, z, scatter.Range(0f, 360f), scatter.Range(.5f, 1.5f), scatter.Range(.25f, .5f));
            }
        }

        private static void ScatterForest(Scatter scatter, Transform parent)
        {
            Transform group = Group("Conifer forest", parent);
            // Clustered stands break the tree line up, exactly as the demo does: the
            // slope band and the ridge beyond the bank carry almost all of them, so the
            // rim reads as forest against the peaks rather than as bare terrain.
            for (int cluster = 0; cluster < 210; cluster++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float lobe = Lobe(angle);
                int zone = scatter.Range(0, 3);
                float radius = zone == 0 ? scatter.Range(MeadowEdge * lobe * .82f, SlopeEdge * lobe)
                             : zone == 1 ? scatter.Range(SlopeEdge * lobe + BankRun, (SlopeEdge + 80f) * lobe)
                             : scatter.Range((SlopeEdge + 60f) * lobe, 285f);
                float cx = Mathf.Cos(angle) * radius;
                float cz = Mathf.Sin(angle) * radius;
                int count = scatter.Range(9, 20);
                for (int tree = 0; tree < count; tree++)
                {
                    float x = cx + scatter.Range(-11f, 11f);
                    float z = cz + scatter.Range(-11f, 11f);
                    if (!ClearOfSite(x, z) || !ClearOfWater(x, z)) continue;
                    if (!scatter.Ground(x, z, out float h) || h < .6f || h > 130f || scatter.Slope(x, z) > 1.45f) continue;
                    scatter.SpawnTree(group, x, z, scatter.Range(1f, 1.9f));
                }
            }
            // An even fill so no stretch of slope is left naked between clusters.
            for (int index = 0; index < 700; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(MeadowEdge * .8f, 285f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (!ClearOfSite(x, z) || !ClearOfWater(x, z)) continue;
                if (!scatter.Ground(x, z, out float h) || h < .6f || h > 130f || scatter.Slope(x, z) > 1.45f) continue;
                scatter.SpawnTree(group, x, z, scatter.Range(.95f, 1.8f));
            }
            // Corridor shoulders and lake shore, so the walk north stays enclosed.
            for (int index = 0; index < 260; index++)
            {
                float z = scatter.Range(24f, 285f);
                float side = scatter.Rng.NextDouble() < .5 ? -1f : 1f;
                float x = side * (CorridorHalfWidth(z) + scatter.Range(6f, 70f));
                if (!ClearOfWater(x, z)) continue;
                if (!scatter.Ground(x, z, out float h) || h < .4f || h > 130f || scatter.Slope(x, z) > 1.45f) continue;
                scatter.SpawnTree(group, x, z, scatter.Range(1f, 1.9f));
            }
            // Lone meadow trees framing the worksite, like the demo meadow's singles.
            for (int index = 0; index < 34; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(28f, 58f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (!ClearOfSite(x, z) || !ClearOfWater(x, z)) continue;
                if (!scatter.Ground(x, z, out float h) || h < .2f || h > 10f) continue;
                scatter.SpawnTree(group, x, z, scatter.Range(.85f, 1.5f));
            }
            Transform bushes = Group("Bushes", parent);
            for (int index = 0; index < 240; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(20f, 220f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (!ClearOfSite(x, z) || !ClearOfWater(x, z)) continue;
                if (!scatter.Ground(x, z, out float h) || h < .5f || h > 110f || scatter.Slope(x, z) > 1.7f) continue;
                scatter.Spawn(bushes, scatter.Pick(Bushes), x, z, scatter.Range(0f, 360f), scatter.Range(.8f, 1.6f), scatter.Range(.08f, .18f));
            }
        }

        private static void ScatterGroundCover(Scatter scatter, Transform parent)
        {
            Transform group = Group("Grass and flowers", parent);
            // Lush meadow ring around the worksite.
            for (int index = 0; index < 780; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(17f, MeadowEdge * Lobe(angle));
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (!ClearOfSite(x, z) || !ClearOfWater(x, z)) continue;
                if (scatter.Rng.NextDouble() < .62) scatter.Spawn(group, scatter.Pick(GrassPlants), x, z, scatter.Range(0f, 360f), scatter.Range(.7f, 1.4f), scatter.Range(.02f, .08f));
                else scatter.Spawn(group, scatter.Pick(Flowers), x, z, scatter.Range(0f, 360f), scatter.Range(.8f, 1.5f), scatter.Range(.02f, .08f));
            }
            // Firm camp side keeps denser turf and flowers.
            for (int index = 0; index < 170; index++)
            {
                float x = scatter.Range(-30f, 30f);
                float z = scatter.Range(-28f, -16.8f);
                if (!ClearOfSite(x, z)) continue;
                if (scatter.Rng.NextDouble() < .6) scatter.Spawn(group, scatter.Pick(GrassPlants), x, z, scatter.Range(0f, 360f), scatter.Range(.7f, 1.3f), scatter.Range(.02f, .08f));
                else scatter.Spawn(group, scatter.Pick(Flowers), x, z, scatter.Range(0f, 360f), scatter.Range(.8f, 1.5f), scatter.Range(.02f, .08f));
            }
            // Stream and lake reeds.
            for (int index = 0; index < 160; index++)
            {
                float t = scatter.Range(0f, 1f);
                int segment = Mathf.Min(StreamPath.Length - 2, (int)(t * (StreamPath.Length - 1)));
                Vector2 point = Vector2.Lerp(StreamPath[segment], StreamPath[segment + 1], scatter.Range(0f, 1f));
                float side = scatter.Rng.NextDouble() < .5 ? -1f : 1f;
                float x = point.x + side * scatter.Range(1.8f, 4.4f);
                float z = point.y + scatter.Range(-2f, 2f);
                if (!ClearOfSite(x, z)) continue;
                scatter.Spawn(group, scatter.Pick(Flowers), x, z, scatter.Range(0f, 360f), scatter.Range(.7f, 1.4f), scatter.Range(.02f, .1f));
            }
            for (int index = 0; index < 150; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float x = Mathf.Cos(angle) * scatter.Range(34f, 46f);
                float z = 122f + Mathf.Sin(angle) * scatter.Range(24f, 33f);
                if (!scatter.Ground(x, z, out float h) || h < LakeLevel - .4f || h > LakeLevel + 2.4f) continue;
                scatter.Spawn(group, scatter.Rng.NextDouble() < .55 ? "Plants/Reeds" : scatter.Pick(GrassPlants),
                    x, z, scatter.Range(0f, 360f), scatter.Range(.8f, 1.6f), scatter.Range(.05f, .18f));
            }
            Transform litter = Group("Branch litter", parent);
            for (int index = 0; index < 34; index++)
            {
                float angle = scatter.Range(-Mathf.PI, Mathf.PI);
                float radius = scatter.Range(20f, 90f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                if (!ClearOfSite(x, z) || !ClearOfWater(x, z)) continue;
                scatter.Spawn(litter, scatter.Pick(Branches), x, z, scatter.Range(0f, 360f), scatter.Range(.8f, 1.6f), scatter.Range(.1f, .3f));
            }
        }

        private static void ScatterWater(Scatter scatter, Transform parent)
        {
            var lakeMaterial = LoadAsset<Material>(WaterMaterialRoot + "Ocean2.mat");
            var streamMaterials = new[] { "Waterfall1", "Waterfall2" }.Select(n => LoadAsset<Material>(WaterMaterialRoot + n + ".mat")).ToArray();
            var lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lake.name = "Lake";
            UnityEngine.Object.DestroyImmediate(lake.GetComponent<Collider>());
            lake.transform.SetParent(parent, false);
            lake.transform.position = new Vector3(0f, LakeLevel, 123f);
            lake.transform.localScale = new Vector3(8.4f, 1f, 6.1f);
            lake.GetComponent<MeshRenderer>().sharedMaterial = lakeMaterial;
            scatter.CountWater();

            Transform streamGroup = Group("Stream", parent);
            for (float z = 24f; z <= 104f; z += 6.5f)
            {
                float ahead = z + 1f;
                float x = PathX(z, StreamPath), xAhead = PathX(ahead, StreamPath);
                float yaw = Mathf.Atan2(xAhead - x, 1f) * Mathf.Rad2Deg;
                var item = scatter.SpawnAt(streamGroup, "Water/WaterStream", new Vector3(x, GroundHeight(x, z) + .1f, z), yaw, 1.4f);
                item.transform.localScale = new Vector3(1.4f, 1f, 2.8f);
                item.GetComponent<MeshRenderer>().sharedMaterial = scatter.Pick(streamMaterials);
                scatter.CountWater();
            }

            Transform falls = Group("Waterfall", parent);
            var fallPrefab = LoadAsset<GameObject>(PrefabRoot + "Water/WaterFall.prefab");
            Bounds local = new Bounds(fallPrefab.transform.position, Vector3.zero);
            foreach (Renderer renderer in fallPrefab.GetComponentsInChildren<Renderer>()) local.Encapsulate(renderer.bounds);
            local.center -= fallPrefab.transform.position;
            // Walk the notch north to find the waterline and the lip above it, then
            // lay the fall along that face. A vertical card placed by eye ends up
            // hanging in the channel with daylight between it and the rock.
            float foot = 138f;
            while (foot < 176f && GroundHeight(0f, foot) < LakeLevel) foot += .5f;
            float rise = Mathf.Max(8f, GroundHeight(0f, foot + 14f) - LakeLevel);
            var fall = scatter.SpawnAt(falls, "Water/WaterFall", new Vector3(0f, LakeLevel, foot + 4f), 180f, 1f);
            fall.transform.localScale = new Vector3(13f, rise / Mathf.Max(1f, local.size.y), 1f);
            fall.GetComponentInChildren<MeshRenderer>().sharedMaterial = LoadAsset<Material>(WaterMaterialRoot + "Waterfall3.mat");
            scatter.CountWater();
        }

        private static float PathX(float z, Vector2[] path)
        {
            for (int i = 0; i < path.Length - 1; i++)
                if (z >= path[i].y && z <= path[i + 1].y)
                    return Mathf.Lerp(path[i].x, path[i + 1].x, (z - path[i].y) / (path[i + 1].y - path[i].y));
            return path[path.Length - 1].x;
        }

        // The vendor mountain meshes are only ~2.8 cm across at scale 1, which is why
        // the demo drives them with five-figure scales. Everything here is authored in
        // metres of finished peak instead, then converted, so the ring is sized against
        // the valley rim (~100 m tall, ~230 m out => 24 deg) rather than guessed: the
        // inner range clears it by better than ten degrees and closes the horizon.
        private static void ScatterMountains(Scatter scatter, Transform parent)
        {
            Transform group = Group("Distant peaks", parent);
            RaiseRange(scatter, Group("Inner range", group), 18, 720f, 900f, 470f, 690f, 1.3f, 1.8f, -130f);
            RaiseRange(scatter, Group("Outer range", group), 13, 1250f, 1650f, 700f, 1000f, 1.5f, 2.1f, -170f);
        }

        private static void RaiseRange(Scatter scatter, Transform group, int count,
            float nearRadius, float farRadius, float minPeak, float maxPeak,
            float minAspect, float maxAspect, float baseY)
        {
            for (int index = 0; index < count; index++)
            {
                float angle = (index + scatter.Range(-.36f, .36f)) / count * Mathf.PI * 2f;
                float radius = scatter.Range(nearRadius, farRadius);
                string path = scatter.Pick(Mountains);
                Vector3 extents = scatter.ExtentsOf(path);
                float peak = scatter.Range(minPeak, maxPeak);
                // Squashing the footprint against the height turns the vendor's broad
                // ranges into the steeper peaks a small valley needs, and lets them sit
                // close enough to fill the sky without overlapping the playable terrain.
                float spread = peak * scatter.Range(minAspect, maxAspect) / Mathf.Max(extents.x, extents.z);
                GameObject mountain = scatter.SpawnAt(group, path,
                    new Vector3(Mathf.Cos(angle) * radius, baseY, Mathf.Sin(angle) * radius),
                    scatter.Range(0f, 360f), 1f);
                mountain.transform.localScale = new Vector3(spread, peak / extents.y, spread);
                // Kilometre-wide backdrops must never spend shadow-map budget.
                foreach (Renderer renderer in mountain.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // --- Helpers ------------------------------------------------------

        private static void ValidateVendorPrefabs()
        {
            foreach (string path in Cliffs.Concat(BigRocks).Concat(SmallRocks).Concat(Pebbles).Concat(Trees)
                .Concat(Bushes).Concat(GrassPlants).Concat(Flowers).Concat(Branches).Concat(Mountains)
                .Concat(Details).Concat(WaterPrefabs))
                LoadAsset<GameObject>(PrefabRoot + path + ".prefab");
            foreach (string layer in new[] { "Gravel1", "Gravel2", "Grass01", "Mud01", "Mud02" })
                LoadAsset<TerrainLayer>(LayerRoot + layer + ".terrainlayer");
            LoadAsset<Material>(LayerRoot + "Terrain.mat");
            foreach (string texture in new[] { "Mud01_a", "Mud01_n", "Mud01_m", "Gravel_a", "Gravel_n", "Gravel_m" })
                LoadAsset<Texture2D>(TextureRoot + texture + ".png");
            foreach (string water in new[] { "Ocean", "Ocean2", "Waterfall1", "Waterfall2", "Waterfall3" })
                LoadAsset<Material>(WaterMaterialRoot + water + ".mat");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Content/Environment"))
                AssetDatabase.CreateFolder("Assets/Content", "Environment");
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing required vendor asset: " + path);
            return asset;
        }

        private static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        // GLSL-style smoothstep(edge0, edge1, x): 0 outside, 1 inside, eased.
        private static float Ramp(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        // Terrain detail layers store density as a byte; the vendor demo packs the
        // meadow near 255 while sparse plants sit low.
        private static int Byte(float value) => Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);

        private static string[] Names(string prefix, int first, int last)
        {
            var names = new string[last - first + 1];
            for (int index = 0; index < names.Length; index++) names[index] = prefix + (first + index);
            return names;
        }
    }
}
