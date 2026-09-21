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
                Assert.That(RenderSettings.fog, Is.False);
                Assert.That(Vector3.Angle(root.Find("Sun").forward, Vector3.down), Is.LessThan(3f));
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
                Assert.That(ground.GetFloat("_SoilComparison"), Is.EqualTo(1));
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
                Assert.That(root.Find("Player").gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ignore Raycast")));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void RoundOpeningHasPermanentGravelAndOnlyStoresAroundIt()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = scene.GetRootGameObjects().Single().transform;
                Assert.That(root.Find("Environment"), Is.Null);
                Assert.That(root.GetComponentsInChildren<Terrain>(true), Is.Empty);
                var apron = root.Find("Surface/Walking apron");
                Assert.That(apron, Is.Not.Null);
                Assert.That(apron.GetComponent<PermanentTerrainBoundary>().CanDig, Is.False);
                var collider = apron.GetComponent<MeshCollider>();
                Assert.That(collider.sharedMesh, Is.SameAs(apron.GetComponent<MeshFilter>().sharedMesh));
                var material = apron.GetComponent<Renderer>().sharedMaterial;
                Assert.That(material.GetFloat("_Smoothness"), Is.Zero);
                StringAssert.Contains("Gravel", AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")));
                for (int i = 0; i < 72; i++)
                {
                    Vector3 direction = Quaternion.Euler(0, i * 5, 0) * Vector3.forward;
                    Assert.That(collider.Raycast(new Ray(direction * (SiteLayout.OpeningRadius - .1f) + Vector3.up * 2, Vector3.down), out _, 3), Is.False);
                    Assert.That(collider.Raycast(new Ray(direction * (SiteLayout.OpeningRadius + .1f) + Vector3.up * 2, Vector3.down), out _, 3), Is.True);
                }
                foreach (var station in root.GetComponentsInChildren<StationTarget>())
                    Assert.That(collider.Raycast(new Ray(station.transform.position + Vector3.up * 2, Vector3.down), out _, 3), Is.True);
                var grass = new SerializedObject(root.GetComponentInChildren<SurfaceGrassRenderer>());
                Assert.That(grass.FindProperty("surfaceRadius").floatValue, Is.EqualTo(SiteLayout.OpeningRadius));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
