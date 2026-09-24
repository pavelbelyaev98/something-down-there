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
                for (int corner = 0; corner < 4; corner++)
                {
                    var local = new Vector3((corner & 1) == 0 ? -.5f : .5f, 0, (corner & 2) == 0 ? -.5f : .5f);
                    var point = recharge.transform.TransformPoint(Vector3.Scale(local, new Vector3(recharge.Footprint.x, 0, recharge.Footprint.y)));
                    Assert.That(new Vector2(point.x, point.z).magnitude, Is.GreaterThan(SiteLayout.OpeningRadius),
                        "Recharge must stay on the permanent rim, outside excavatable soil.");
                }
                Assert.That(root.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1));
                var camera = root.GetComponentInChildren<Camera>();
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
                // that the demo's haze is on, since the canyon and peaks are read through it.
                Assert.That(RenderSettings.fog, Is.True);
                Assert.That(Vector3.Angle(root.Find("Sun").forward, Vector3.down), Is.LessThan(3f));
                Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Exponential));
                Assert.That(RenderSettings.fogDensity, Is.InRange(.001f, .006f));
                Assert.That(root.GetComponentsInChildren<MonoBehaviour>().Any(c => c.GetType().Name.StartsWith("Validation")), Is.False);
                Assert.That(root.GetComponentsInChildren<StationTarget>().Single(), Is.TypeOf<ComputerStation>());
                Assert.That(root.GetComponentInChildren<ComputerStation>().transform, Is.SameAs(root.Find("Surface/ComputerStation")));
                foreach (var station in root.GetComponentsInChildren<StationTarget>())
                {
                    Assert.That(station.GetComponent<Collider>(), Is.Not.Null);
                    foreach (var mesh in station.GetComponentsInChildren<MeshFilter>())
                        StringAssert.StartsWith("Assets/Cosmic_Retro_Computer_1_FREE/Models/Cosmic_Retro_Computer_3.fbx", AssetDatabase.GetAssetPath(mesh.sharedMesh), "Use the computer selected by the user.");
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
                Assert.That(grassMaterial.shader.name, Is.EqualTo("Something Down There/Excavation Grass"));
                Assert.That(grassMaterial.GetTexture("_MainTex"), Is.Not.Null);
                var meadowLayers = grassSettings.FindProperty("detailLayers");
                Assert.That(meadowLayers.arraySize, Is.GreaterThanOrEqualTo(8), "The whole dig site keeps mixed grass, flowers and ferns.");
                for (int i = 0; i < meadowLayers.arraySize; i++)
                {
                    var layer = meadowLayers.GetArrayElementAtIndex(i);
                    var mesh = layer.FindPropertyRelative("mesh").objectReferenceValue as Mesh;
                    var material = layer.FindPropertyRelative("material").objectReferenceValue as Material;
                    Assert.That(mesh, Is.Not.Null);
                    StringAssert.StartsWith("Assets/BK/", AssetDatabase.GetAssetPath(mesh));
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.enableInstancing, Is.True);
                    Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                }
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
                StringAssert.StartsWith(GroundTextureSetup.PackTextureFolder, AssetDatabase.GetAssetPath(ground.GetTexture("_SoilAlbedo")),
                    "Pack ground uses project copies with close-range texture imports.");
                Assert.That(ground.GetFloat("_SoilComparison"), Is.Zero, "Pack ground covers the entire dig site.");
                foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
                    Assert.That(ground.GetTexture("_Comparison" + channel), Is.Null, "Inactive custom soil must not remain bound to active terrain.");
                Assert.That(ground.GetFloat("_MaskLayout"), Is.EqualTo(1));
                Assert.That(ground.GetFloat("_ComparisonMaskLayout"), Is.Zero);
                Assert.That(ground.GetFloat("_TurfMaskLayout"), Is.EqualTo(1));
                Assert.That(ground.GetFloat("_MaxSmoothness"), Is.LessThanOrEqualTo(.15f));
                Assert.That(preview.GetComponent<Renderer>().sharedMaterial, Is.SameAs(ground));
                var camp = (Material)AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/GroundTextures/GardenGround.mat");
                foreach (string channel in new[] { "Albedo", "Normal", "Roughness" })
                {
                    Assert.That(camp.GetTexture("_Turf" + channel), Is.SameAs(ground.GetTexture("_Turf" + channel)),
                        "Camp and excavation must use the same pack meadow cap.");
                    var mask = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(ground.GetTexture("_TurfRoughness")));
                    Assert.That(mask.sRGBTexture, Is.False, "Packed masks are linear data.");
                    Assert.That(mask.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), "Preserve smoothness alpha.");
                }
                foreach (string name in new[] { "ComputerStation", "RechargeZone", "ReturnAnchor" })
                {
                    Transform anchor = root.Find("Surface/" + name);
                    Assert.That(anchor, Is.Not.Null, name);
                    Assert.That(anchor.position.y, Is.InRange(0, 0.2f));
                    Assert.That(new Vector2(anchor.position.x, anchor.position.z).magnitude,
                        Is.InRange(SiteLayout.OpeningRadius + .5f, SiteLayout.OpeningRadius + 6), name + " stands beside the opening.");
                }
                Assert.That(root.Find("Bedrock").GetComponentsInChildren<PermanentTerrainBoundary>().Length, Is.EqualTo(5));
                Assert.That(root.Find("Perimeter"), Is.Null,
                    "The valley wall replaced the invisible arena box.");
                Assert.That(root.Find("Player").gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void DrainedLakebedFramesTheOpeningWithPermanentGroundAndStations()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single().transform;
                Physics.SyncTransforms();
                var environment = root.Find("Environment");
                Assert.That(environment, Is.Not.Null);
                Assert.That(environment.GetComponent<PermanentTerrainBoundary>().CanDig, Is.False);
                var terrain = root.GetComponentsInChildren<Terrain>(true).Single();
                Assert.That(terrain.transform.IsChildOf(environment), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(terrain.terrainData), Is.EqualTo(LakebedSiteSetup.TerrainDataPath));
                Assert.That(terrain.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),
                    "Terrain shadow casters ignore holes and would shade the whole shaft.");
                var ground = terrain.GetComponent<TerrainCollider>();
                Assert.That(ground.terrainData, Is.SameAs(terrain.terrainData));
                var rim = root.Find("Surface/Excavation rim");
                Assert.That(rim.GetComponent<PermanentTerrainBoundary>().CanDig, Is.False);
                var collar = rim.GetComponent<MeshCollider>();
                Assert.That(collar.sharedMesh, Is.SameAs(rim.GetComponent<MeshFilter>().sharedMesh));
                for (int i = 0; i < 72; i++)
                {
                    Vector3 direction = Quaternion.Euler(0, i * 5, 0) * Vector3.forward;
                    Ray Down(float radius) => new Ray(direction * radius + Vector3.up * 2, Vector3.down);
                    Assert.That(collar.Raycast(Down(SiteLayout.OpeningRadius - .1f), out _, 3), Is.False, "The rim never caps the opening.");
                    Assert.That(collar.Raycast(Down(SiteLayout.OpeningRadius + .1f), out var lip, 3), Is.True);
                    Assert.That(lip.point.y, Is.EqualTo(SiteLayout.RimTop).Within(.002f));
                    Assert.That(ground.Raycast(Down(SiteLayout.OpeningRadius - .1f), out _, 3), Is.False, "The terrain is open over the dig ground.");
                    Assert.That(ground.Raycast(Down(SiteLayout.RimOuterRadius + .5f), out var beyond, 3), Is.True);
                    Assert.That(beyond.point.y, Is.EqualTo(SiteLayout.GroundTop).Within(.02f), "Flat permanent ground surrounds the rim.");
                }
                // Scenery, water and colliders stay out of the dig column.
                var column = Physics.OverlapCapsule(Vector3.down * 99, Vector3.up * 3, SiteLayout.OpeningRadius - .05f);
                Assert.That(column.Where(c => c.transform.IsChildOf(environment)), Is.Empty);
                foreach (var renderer in environment.GetComponentsInChildren<Renderer>())
                {
                    // Edit-mode particle bounds collapse to the origin; the waterfall splashes are far away.
                    // The lake surface spans the canyon; its triangles are checked below.
                    if (renderer is ParticleSystemRenderer || renderer.name == "Lake surface" || renderer.name == "Border stones") continue;
                    var bounds = renderer.bounds;
                    float dx = Mathf.Max(0, Mathf.Abs(bounds.center.x) - bounds.extents.x), dz = Mathf.Max(0, Mathf.Abs(bounds.center.z) - bounds.extents.z);
                    Assert.That(Mathf.Sqrt(dx * dx + dz * dz), Is.GreaterThan(SiteLayout.OpeningRadius - .3f), renderer.name);
                }
                Assert.That(environment.GetComponentsInChildren<Transform>(true)
                    .Any(t => GameObjectUtility.AreStaticEditorFlagsSet(t.gameObject, StaticEditorFlags.BatchingStatic)), Is.False,
                    "Runtime static batching of the vendor scenery exhausts memory on every scene load.");
                var lake = environment.Find("Water/Lake surface");
                Assert.That(lake.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("BK/Water"));
                Assert.That(lake.GetComponent<Collider>(), Is.Null, "The water surface is not walkable.");
                Assert.That(lake.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v => new Vector2(v.x, v.z).magnitude),
                    Is.GreaterThan(SiteLayout.RimRadius), "Water never reaches the dig column or rim.");
                var border = environment.Find("Dig boundary/Border stones");
                Assert.That(border.GetComponent<Collider>(), Is.Null, "Border pebbles never snag the player.");
                Assert.That(border.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v => new Vector2(v.x, v.z).magnitude),
                    Is.GreaterThan(SiteLayout.OpeningRadius), "The pebble border stays outside the diggable circle.");
                var probe = environment.GetComponentInChildren<ReflectionProbe>();
                Assert.That(probe.mode, Is.EqualTo(UnityEngine.Rendering.ReflectionProbeMode.Custom));
                StringAssert.StartsWith("Assets/BK/PureNature_Highlands/", AssetDatabase.GetAssetPath(probe.customBakedTexture),
                    "Water reflects the demo's baked canyon cubemap.");
                Assert.That(new Bounds(probe.transform.position + probe.center, probe.size).Contains(Vector3.up), Is.True);
                // The play area keeps the player on the drained section, below a flight ceiling.
                var area = LakebedSiteSetup.PlayArea();
                var playBounds = environment.Find("Play area bounds");
                foreach (var wall in playBounds.GetComponentsInChildren<Collider>())
                    Assert.That(wall.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")), "Aim, digging and lamps ignore the bounds.");
                Assert.That(playBounds.Find("Flight ceiling").GetComponent<Collider>().bounds.min.y,
                    Is.EqualTo(LakebedSiteSetup.FlightCeiling + 1.9f).Within(.05f));
                Assert.That(Physics.Raycast(new Vector3(0, 30, -16), Vector3.down, out var skyHit, 40), Is.True);
                Assert.That(skyHit.point.y, Is.LessThan(1f), "Default rays pass through the flight ceiling.");
                // Only elevated demo ponds keep their walkable surface; nothing collides at lake level.
                float lakeTop = lake.GetComponent<Renderer>().bounds.max.y;
                Assert.That(environment.GetComponentsInChildren<Collider>().Where(c => c.name.StartsWith("Water") && c.enabled && c.bounds.max.y < lakeTop + 5), Is.Empty);
                // Stations stand on permanent ground around the opening.
                foreach (var station in root.GetComponentsInChildren<StationTarget>().Select(s => s.transform)
                    .Concat(new[] { root.Find("Surface/SalvageWinch/WinchFixture"), root.Find("Surface/RechargeZone"), root.Find("Player") }))
                {
                    var position = station.position;
                    Assert.That(new Vector2(position.x, position.z).magnitude, Is.InRange(SiteLayout.OpeningRadius, SiteLayout.RimRadius + 6), station.name);
                    var support = Physics.RaycastAll(position + Vector3.up * 2.5f, Vector3.down, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        .Where(h => h.collider.GetComponentInParent<PermanentTerrainBoundary>() != null).ToArray();
                    Assert.That(support, Is.Not.Empty, station.name + " stands on permanent ground.");
                    Assert.That(LakebedSiteSetup.InPlayArea(area, new Vector2(position.x, position.z)), Is.True, station.name + " is inside the play area.");
                    Assert.That(support.Max(h => h.point.y), Is.InRange(SiteLayout.GroundTop - .02f, SiteLayout.RimTop + .01f), station.name);
                }
                var grass = new SerializedObject(root.GetComponentInChildren<SurfaceGrassRenderer>());
                Assert.That(grass.FindProperty("surfaceRadius").floatValue, Is.EqualTo(SiteLayout.OpeningRadius));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
