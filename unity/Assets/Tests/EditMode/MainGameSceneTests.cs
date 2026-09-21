using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SomethingDownThere.Editor;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace SomethingDownThere.Tests
{
    public sealed class MainGameSceneTests
    {
        private const string ScenePath = "Assets/Scenes/MainGame.unity";

        [Test]
        public void AuthoredShellHasValidOwnersUrpMaterialsAndReachableSurfaceAnchors()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                // The test runner executes EditMode tests in an empty scene, so the scene
                // under test must go active before scene-owned globals (fog, ambient) are
                // read back; otherwise they keep the empty runner scene's defaults.
                SceneManager.SetActiveScene(scene);
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots.Length, Is.EqualTo(1));
                Transform root = roots[0].transform;
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject), Is.Zero, item.name);
                Assert.That(root.GetComponentsInChildren<TerrainVolume>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<FpsPlayer>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<FpsHud>().Length, Is.EqualTo(1));
                var recharge = root.GetComponentInChildren<SurfaceRecharge>();
                Assert.That(recharge, Is.Not.Null);
                Assert.That(recharge.Player, Is.SameAs(root.GetComponentInChildren<FpsPlayer>()));
                Assert.That(recharge.Player.SurfaceRecharge, Is.SameAs(recharge));
                Assert.That(recharge.Terrain, Is.SameAs(root.GetComponentInChildren<TerrainVolume>()));
                Assert.That(recharge.transform, Is.SameAs(root.Find("Surface/RechargeZone")));
                Assert.That(recharge.transform.position.z + recharge.Footprint.y * 0.5f, Is.LessThan(-12f),
                    "Recharge must stay on the permanent rim, outside excavatable soil.");
                Assert.That(root.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1));
                var camera = root.GetComponentInChildren<Camera>();
                Assert.That(root.GetComponentInChildren<ExcavatorView>(true), Is.Null, "Trial art must not be part of the normal scene.");
                Assert.That(camera.GetUniversalAdditionalCameraData().cameraStack, Is.Empty);
                Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                var sky = camera.GetComponent<Skybox>();
                Assert.That(sky, Is.Not.Null);
                Assert.That(sky.material, Is.Not.Null);
                // Task 064 moved presentation onto the vendor demo's sky/fog stack; the sky is a
                // project-owned copy of the vendor material so tuning never edits Assets/BK.
                Assert.That(sky.material.shader.name, Is.EqualTo("BK/Sky"));
                Assert.That(ShaderUtil.ShaderHasError(sky.material.shader), Is.False);
                StringAssert.StartsWith("Assets/Content/", AssetDatabase.GetAssetPath(sky.material));
                Assert.That(root.Find("Clouds"), Is.Null);
                // Clear flags are Skybox, so backgroundColor never renders; what matters is
                // that the demo's haze is on, since the peaks are read through it.
                Assert.That(RenderSettings.fog, Is.True);
                Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Exponential));
                Assert.That(RenderSettings.fogDensity, Is.InRange(.001f, .006f));
                Assert.That(root.GetComponentsInChildren<MonoBehaviour>().Any(c => c.GetType().Name.StartsWith("Validation")), Is.False);
                Assert.That(root.GetComponentsInChildren<SellStation>().Single().transform, Is.SameAs(root.Find("Surface/SellStation")));
                Assert.That(root.GetComponentsInChildren<UpgradeStation>().Single().transform, Is.SameAs(root.Find("Surface/UpgradeStation")));
                foreach (var station in root.GetComponentsInChildren<StationTarget>())
                {
                    Assert.That(station.GetComponent<Collider>(), Is.Not.Null);
                    Assert.That(station.GetComponent<StationMotion>(), Is.Not.Null);
                    foreach (var mesh in station.GetComponentsInChildren<MeshFilter>())
                        StringAssert.StartsWith("Assets/Content/Stations/", AssetDatabase.GetAssetPath(mesh.sharedMesh), "Stations must use their approved Blender models.");
                    foreach (var renderer in station.GetComponentsInChildren<Renderer>())
                        Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null, "Keep authored station textures.");
                }
                var terrainSettings = new SerializedObject(root.GetComponentInChildren<TerrainVolume>());
                var excavation = root.GetComponentInChildren<TerrainVolume>();
                Assert.That(excavation.Dimensions, Is.EqualTo(SiteLayout.Size));
                Assert.That(excavation.CellSize, Is.EqualTo(SiteLayout.CellSize));
                Assert.That(excavation.SurfaceHeight, Is.Zero.Within(.0001f));
                Assert.That(excavation.transform.position, Is.EqualTo(SiteLayout.Origin));
                // The reservoir is 24 x 100 x 24 m: the floor is bedrock at -100 and the
                // preview block spans exactly the diggable volume.
                Assert.That(root.Find("Bedrock/Floor").GetComponent<Collider>().bounds.max.y,
                    Is.EqualTo(-SiteLayout.Extent.y).Within(.0001f));
                var preview = root.Find("Excavation/Untouched preview (edit mode only)");
                Assert.That(preview, Is.Not.Null);
                var previewBounds = preview.GetComponent<Renderer>().bounds;
                Assert.That(previewBounds.min.y, Is.EqualTo(-SiteLayout.Extent.y).Within(.0001f));
                Assert.That(previewBounds.max.y, Is.EqualTo(0).Within(.0001f));
                Assert.That(previewBounds.size.x, Is.EqualTo(SiteLayout.Extent.x).Within(.0001f));
                Assert.That(previewBounds.size.z, Is.EqualTo(SiteLayout.Extent.z).Within(.0001f));
                var grass = root.GetComponentInChildren<SurfaceGrassRenderer>();
                Assert.That(grass, Is.Not.Null, "The main game must retain the approved moving grass.");
                var grassSettings = new SerializedObject(grass);
                StringAssert.StartsWith("Assets/BK/", AssetDatabase.GetAssetPath(
                    grassSettings.FindProperty("nearMesh").objectReferenceValue));
                Assert.That(grassSettings.FindProperty("farMesh").objectReferenceValue, Is.Null);
                StringAssert.StartsWith("Assets/Content/Nature/", AssetDatabase.GetAssetPath(
                    grassSettings.FindProperty("material").objectReferenceValue));
                var grassMesh = (Mesh)grassSettings.FindProperty("nearMesh").objectReferenceValue;
                Assert.That(grassMesh.GetIndexCount(0) / 3, Is.LessThanOrEqualTo(64), "Vendor grass must keep inexpensive instanced geometry.");
                Assert.That(grassMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color), Is.True,
                    "The vendor shader uses vertex color to weight wind.");
                var grassMaterial = (Material)grassSettings.FindProperty("material").objectReferenceValue;
                Assert.That(grassMaterial.enableInstancing, Is.True);
                Assert.That(ShaderUtil.ShaderHasError(grassMaterial.shader), Is.False);
                Assert.That(grassMaterial.shader.name, Is.EqualTo("BK/Grass"));
                Assert.That(grassMaterial.GetTexture("_MainTex"), Is.Not.Null);
                Assert.That(grassSettings.FindProperty("coverage").floatValue, Is.InRange(.005f, .12f),
                    "The sediment bed keeps only sparse tufts.");
                var daylight = root.GetComponentInChildren<ExcavationDaylight>();
                Assert.That(daylight, Is.Not.Null, "Excavation must attenuate ambient sky light in enclosed soil.");
                var daylightShader = new SerializedObject(daylight).FindProperty("litShader").objectReferenceValue as Shader;
                Assert.That(daylightShader, Is.Not.Null, "Keep the runtime material shader referenced in Windows builds.");
                Assert.That(ShaderUtil.ShaderHasError(daylightShader), Is.False);
                var ground = (Material)terrainSettings.FindProperty("soilMaterial").objectReferenceValue;
                Assert.That(ground.shader.name, Is.EqualTo("Something Down There/Ground Triplanar"));
                Assert.That(ShaderUtil.ShaderHasError(ground.shader), Is.False);
                Assert.That(ground.name, Is.EqualTo("ReservoirSediment"), "The diggable center must read as loose sediment.");
                foreach (string kind in new[] { "Soil", "Turf" })
                foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
                    Assert.That(ground.GetTexture("_" + kind + channel), Is.Not.Null, kind + channel);
                StringAssert.StartsWith("Assets/BK/", AssetDatabase.GetAssetPath(ground.GetTexture("_SoilAlbedo")),
                    "The sediment bed uses the approved vendor mud and gravel.");
                Assert.That(preview.GetComponent<Renderer>().sharedMaterial, Is.SameAs(ground));
                var camp = (Material)AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/GroundTextures/GardenGround.mat");
                foreach (string side in new[] { "North", "East", "West" })
                    Assert.That(root.Find("Surface/" + side + " rim").GetComponent<Renderer>().sharedMaterial, Is.SameAs(ground),
                        side + " rim continues the soft sediment.");
                Assert.That(root.Find("Surface/South rim").GetComponent<Renderer>().sharedMaterial, Is.SameAs(camp),
                    "The camp side keeps the firm turf ground.");
                foreach (string name in new[] { "SellStation", "UpgradeStation", "RechargeZone", "ReturnAnchor" })
                {
                    Transform anchor = root.Find("Surface/" + name);
                    Assert.That(anchor, Is.Not.Null, name);
                    Assert.That(anchor.position.y, Is.InRange(0, 0.2f));
                    Assert.That(Vector3.Distance(anchor.position, new Vector3(0, 0, -12)), Is.LessThan(5), name);
                }
                Assert.That(root.Find("Bedrock").GetComponentsInChildren<PermanentTerrainBoundary>().Length, Is.EqualTo(5));
                Assert.That(root.Find("Perimeter"), Is.Null,
                    "The valley wall replaced the invisible arena box.");
                foreach (string side in new[] { "West", "East", "North", "South" })
                {
                    var wall = root.Find("Bedrock/" + side).GetComponent<Renderer>().bounds;
                    var rim = root.Find("Surface/" + side + " rim").GetComponent<Renderer>().bounds;
                    Assert.That(wall.max.y, Is.EqualTo(rim.min.y).Within(0.0001f),
                        side + ": coincident vertical wall/rim faces must meet without overlapping or leaving a gap.");
                }
                Assert.That(root.Find("Player").gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void DrainedReservoirSurroundsTheDigWithVendorTerrainAndProps()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform root = scene.GetRootGameObjects().Single().transform;
                Transform environment = root.Find("Environment");
                Assert.That(environment, Is.Not.Null, "The drained reservoir must surround the worksite.");
                Terrain[] tiles = environment.GetComponentsInChildren<Terrain>();
                Assert.That(tiles.Length, Is.EqualTo(4), "Four terrain tiles frame the excavation without a cap.");
                foreach (Terrain tile in tiles)
                {
                    Assert.That(tile.GetComponent<TerrainCollider>(), Is.Not.Null, tile.name);
                    Bounds bounds = tile.terrainData.bounds;
                    bounds.center += tile.transform.position;
                    bool overOpening = bounds.min.x < 15.999f && bounds.max.x > -15.999f
                        && bounds.min.z < 15.999f && bounds.max.z > -15.999f;
                    Assert.That(overOpening, Is.False,
                        tile.name + " must never reach over the excavatable opening.");
                    Assert.That(tile.terrainData.terrainLayers.Length, Is.EqualTo(5), tile.name);
                    Assert.That(tile.terrainData.detailPrototypes.Length, Is.GreaterThan(15),
                        tile.name + " carries the demo's grass and flower detail set.");
                    foreach (TerrainLayer layer in tile.terrainData.terrainLayers)
                    {
                        Assert.That(layer, Is.Not.Null, tile.name);
                        StringAssert.StartsWith("Assets/BK/", AssetDatabase.GetAssetPath(layer),
                            "The basin surface uses the approved vendor layers.");
                    }
                }
                Camera camera = root.GetComponentInChildren<Camera>();
                Assert.That(camera.farClipPlane, Is.GreaterThanOrEqualTo(2000f),
                    "The camera must reach the outer mountain range.");
                // The valley is closed by terrain now, so every bearing out of the bowl
                // has to cross ground steeper than the character controller can walk.
                float weakest = float.MaxValue;
                for (int step = 0; step < 360; step++)
                {
                    float bearing = step / 360f * Mathf.PI * 2f;
                    float steepest = 0f;
                    int sampled = 0;
                    for (float distance = 40f; distance < 210f; distance += 1f)
                    {
                        float x = Mathf.Cos(bearing) * distance;
                        float z = Mathf.Sin(bearing) * distance;
                        if (z > 8f && Mathf.Abs(x) < ReservoirEnvironmentSetup.CorridorHalfWidth(z) + 4f) continue;
                        sampled++;
                        float here = ReservoirEnvironmentSetup.GroundHeight(x, z);
                        float ahead = ReservoirEnvironmentSetup.GroundHeight(
                            Mathf.Cos(bearing) * (distance + 2f), Mathf.Sin(bearing) * (distance + 2f));
                        steepest = Mathf.Max(steepest, (ahead - here) / 2f);
                    }
                    if (sampled > 60) weakest = Mathf.Min(weakest, steepest);
                }
                Assert.That(weakest, Is.GreaterThan(1.05f),
                    "Every bearing out of the valley must cross ground steeper than the 45 deg slope limit.");
                int props = 0;
                foreach (Transform item in environment.GetComponentsInChildren<Transform>(true))
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
                    if (source == null) continue;
                    StringAssert.StartsWith("Assets/BK/", AssetDatabase.GetAssetPath(source),
                        item.name + " must stay a vendor prefab instance.");
                    props++;
                }
                Assert.That(props, Is.GreaterThan(2000), "Expect a populated valley of vendor props.");
                Transform peaks = environment.Find("Mountains/Distant peaks");
                Assert.That(peaks, Is.Not.Null, "The small valley is enclosed by vendor peaks.");
                var heights = new List<float>();
                foreach (Renderer renderer in peaks.GetComponentsInChildren<Renderer>())
                    heights.Add(renderer.bounds.size.y);
                Assert.That(heights.Count, Is.GreaterThan(20), "Two ranges ring the valley.");
                Assert.That(heights.Min(), Is.GreaterThan(300f),
                    "Peaks must clear the ~100 m valley rim by a wide margin or they read as pebbles.");
                // The drained reservoir bed stays bare: the forest line starts on the
                // over-steep bank, so no tree stands on the basin floor or its slope.
                Transform forest = environment.Find("Forest/Conifer forest");
                Assert.That(forest, Is.Not.Null, "The valley keeps its conifer forest.");
                Assert.That(forest.childCount, Is.GreaterThan(1200), "The bank and ridge keep a dense tree line.");
                foreach (Transform tree in forest)
                {
                    float radius = Mathf.Sqrt(tree.position.x * tree.position.x + tree.position.z * tree.position.z);
                    float bedEdge = ReservoirEnvironmentSetup.SlopeEdge
                        * ReservoirEnvironmentSetup.LobeAt(tree.position.x, tree.position.z);
                    Assert.That(radius, Is.GreaterThanOrEqualTo(bedEdge),
                        tree.name + " must not stand inside the reservoir bed.");
                }
                foreach (Renderer renderer in environment.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
                    Assert.That(ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader), Is.False, renderer.name);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
