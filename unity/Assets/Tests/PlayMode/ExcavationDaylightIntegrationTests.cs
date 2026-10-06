using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;

namespace SomethingDownThere.Tests
{
    public sealed class ExcavationDaylightIntegrationTests
    {
        [UnityTest]
        public IEnumerator CacheFollowsExcavationRestoreResetAndReceiverLifecycle()
        {
            var root = new GameObject("Daylight validation fixture");
            root.SetActive(false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var terrain = root.AddComponent<TerrainVolume>();
            terrain.Configure(new Vector3Int(16, 24, 16), .125f, 8, .4f, material);
            var daylight = root.AddComponent<ExcavationDaylight>();
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiver.transform.SetParent(root.transform);
            receiver.transform.localPosition = new Vector3(1, 1, 1);
            receiver.transform.localScale = Vector3.one * .1f;
            var renderer = receiver.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            try
            {
                root.SetActive(true);
                // Player builds validate Unity's reserved message signatures.
                // Our custom Bounds event must not shadow the integer message.
                root.SendMessage("OnTerrainChanged", 1, SendMessageOptions.DontRequireReceiver);
                yield return Ready(daylight);
                Assert.That(daylight.PublishedRevision, Is.GreaterThan(0));
                Assert.That(daylight.SampleAmbient(new Vector3(1, 1, 1)), Is.Zero);
                Assert.That(daylight.SampleAmbient(new Vector3(1, 3.1f, 1)), Is.EqualTo(1));
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Something Down There/Excavation Lit"));
                var grid = new ExcavationGrid(terrain.Dimensions, terrain.CellSize);
                for (float y = 3; y >= .75f; y -= .4f) grid.RemoveSphere(new Vector3(1, y, 1), .65f, out _);
                yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
                yield return Ready(daylight);
                Assert.That(daylight.SampleAmbient(new Vector3(1, 1, 1)), Is.GreaterThan(.55f));
                terrain.ResetExcavation();
                yield return Ready(daylight);
                Assert.That(daylight.SampleAmbient(new Vector3(1, 1, 1)), Is.Zero);
                daylight.enabled = false;
                Assert.That(Shader.GetGlobalFloat("_ExcavationDaylightEnabled"), Is.Zero);
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                daylight.enabled = true;
                yield return Ready(daylight);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Something Down There/Excavation Lit"));
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(material);
            }
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator UnlitGroundAndFindRejectSunlightButReceiveLocalLight()
        {
            // Exercise actual GPU output, including the case where a distant
            // terrain blocker is absent from the sun's shadow map.
            var root = new GameObject("Daylight rendering fixture");
            root.SetActive(false);
            var ground = new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Content/Nature/ReservoirSediment.mat"));
            ground.SetFloat("_SurfaceHeight", 3);
            var find = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            find.SetColor("_BaseColor", new Color(.6f, .4f, .2f));
            find.SetFloat("_Metallic", .5f);
            find.SetFloat("_Smoothness", .65f);
            var terrain = root.AddComponent<TerrainVolume>();
            terrain.Configure(new Vector3Int(16, 24, 16), .125f, 8, .4f, ground);
            var daylight = root.AddComponent<ExcavationDaylight>();
            for (int i = 0; i < 2; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(root.transform);
                cube.transform.localPosition = new Vector3(.55f + i * .9f, 1, 1.4f);
                cube.transform.localScale = Vector3.one * .65f;
                cube.GetComponent<Renderer>().sharedMaterial = i == 0 ? ground : find;
            }
            var camera = new GameObject("Lighting test camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.localPosition = new Vector3(1, 1, .1f);
            camera.orthographic = true;
            camera.orthographicSize = .8f;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            var sun = new GameObject("Unshadowed test sun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(root.transform);
            sun.type = LightType.Directional;
            sun.intensity = 1.4f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(35, 20, 0);
            var lamp = new GameObject("Test work light", typeof(Light)).GetComponent<Light>();
            lamp.transform.SetParent(root.transform);
            lamp.transform.localPosition = new Vector3(1, 1.2f, .4f);
            lamp.type = LightType.Point;
            lamp.range = 5;
            lamp.intensity = 4;
            lamp.enabled = false;
            var previousMode = RenderSettings.ambientMode;
            var previousAmbient = RenderSettings.ambientLight;
            var previousSun = RenderSettings.sun;
            bool previousFog = RenderSettings.fog;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white;
                RenderSettings.sun = sun;
                RenderSettings.fog = false;
                root.SetActive(true);
                yield return Ready(daylight);
                yield return null;
                yield return null;
                float[] dark = CaptureLighting(camera, pixels, "unlit");
                for (int i = 0; i < dark.Length; i++)
                    Assert.That(dark[i], Is.LessThan(.005f), "Neither soil nor a metallic find may glow without daylight.");

                lamp.enabled = true;
                yield return null;
                yield return null;
                float[] lit = CaptureLighting(camera, pixels, "local-lamp");
                for (int i = 0; i < lit.Length; i++)
                    Assert.That(lit[i], Is.GreaterThan(.04f), "Local lamps must illuminate both shaders in a black daylight field.");

                lamp.enabled = false;
                daylight.enabled = false;
                yield return null;
                yield return null;
                float[] surface = CaptureLighting(camera, pixels, "daylight-control");
                for (int i = 0; i < surface.Length; i++)
                    Assert.That(surface[i], Is.GreaterThan(.04f), "The control must actually render illuminated geometry.");
            }
            finally
            {
                RenderSettings.ambientMode = previousMode;
                RenderSettings.ambientLight = previousAmbient;
                RenderSettings.sun = previousSun;
                RenderSettings.fog = previousFog;
                camera.targetTexture = null;
                Object.Destroy(root);
                Object.Destroy(target);
                Object.Destroy(pixels);
                Object.Destroy(ground);
                Object.Destroy(find);
            }
        }

        private static float[] CaptureLighting(Camera camera, Texture2D pixels, string name)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = camera.targetTexture;
                pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                pixels.Apply();
                string directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../Logs/Task049"));
                System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
                var means = new float[2];
                for (int i = 0; i < means.Length; i++)
                {
                    int centre = i == 0 ? 56 : 200;
                    for (int y = 104; y < 152; y++)
                    for (int x = centre - 16; x < centre + 16; x++)
                        means[i] += pixels.GetPixel(x, y).grayscale / (48 * 32);
                }
                return means;
            }
            finally { RenderTexture.active = previous; }
        }
#endif

        private static IEnumerator Ready(ExcavationDaylight daylight)
        {
            // Always allow one LateUpdate after a terrain restore/reset event.
            yield return null;
            for (int i = 0; i < 180 && daylight.IsUpdating; i++) yield return null;
            Assert.That(daylight.IsUpdating, Is.False, "Daylight must publish rather than remain in a failed rebuild.");
        }
    }
}
