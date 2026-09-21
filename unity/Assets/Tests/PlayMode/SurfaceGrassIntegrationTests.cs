#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class SurfaceGrassIntegrationTests
    {
        [UnityTest]
        public IEnumerator GrassTracksLocalCutsRestorationResetAndReenableWithoutSaveState()
            => TrackLifecycle(false);

        [UnityTest]
        public IEnumerator MeadowSpeciesTrackLocalCutsRestorationResetAndReenableWithoutSaveState()
            => TrackLifecycle(true);

        [UnityTest]
        public IEnumerator NarrowOpeningBetweenOldSupportProbesClearsTheWholeCanopy()
        {
            var root = new GameObject("Canopy footprint fixture");
            root.SetActive(false);
            root.transform.position = new Vector3(100, 0, 100);
            var soil = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var terrain = root.AddComponent<TerrainVolume>();
            terrain.Configure(new Vector3Int(64, 24, 64), .125f, 16, .4f, soil);
            var grass = root.AddComponent<SurfaceGrassRenderer>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var supported = typeof(SurfaceGrassRenderer).GetMethod("RootSupported", flags);
            Vector3 canopyRoot = new Vector3(104, 2.994f, 104);
            bool HasSupport() => (bool)supported.Invoke(grass, new object[] { canopyRoot, 1.2f });
            try
            {
                root.SetActive(true); yield return null;
                Assert.That(HasSupport(), Is.True);
                var origin = canopyRoot + new Vector3(.45f, 2, .2f);
                Assert.That(Physics.Raycast(origin, Vector3.down, out var hit, 3), Is.True);
                Assert.That(terrain.TryDig(hit, .125f), Is.True);
                // The root and all eight former perimeter probes still have soil.
                Assert.That(terrain.IsSolid(canopyRoot - Vector3.up * .018f), Is.True);
                for (int i = 0; i < 8; i++)
                    Assert.That(terrain.IsSolid(canopyRoot - Vector3.up * .018f +
                        Quaternion.Euler(0, i * 45, 0) * Vector3.forward * 1.2f), Is.True);
                Assert.That(HasSupport(), Is.False, "Wide leaves must not remain over an opening between support probes.");
                terrain.ResetExcavation();
                Assert.That(HasSupport(), Is.True);
                typeof(SurfaceGrassRenderer).GetField("surfaceRadius", flags).SetValue(grass, 1f);
                Assert.That(HasSupport(), Is.False, "The whole canopy must fit inside the round opening.");
            }
            finally { Object.Destroy(root); Object.Destroy(soil); }
        }

        private IEnumerator TrackLifecycle(bool meadow)
        {
            var root = new GameObject("Grass validation fixture");
            root.SetActive(false);
            root.transform.position = new Vector3(100, 0, 100);
            var soil = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var terrain = root.AddComponent<TerrainVolume>();
            terrain.Configure(new Vector3Int(64, 24, 64), .125f, 16, .4f, soil);
            var grass = root.AddComponent<SurfaceGrassRenderer>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BK/PureNature_Mountains/Prefabs/Plants/Grass1.prefab");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(SurfaceGrassRenderer).GetField("nearMesh", flags).SetValue(grass, prefab.GetComponentInChildren<MeshFilter>().sharedMesh);
            typeof(SurfaceGrassRenderer).GetField("cellsPerPatch", flags).SetValue(grass, 6);
            typeof(SurfaceGrassRenderer).GetField("material", flags).SetValue(grass,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/Nature/MountainGrass.mat"));
            if (meadow)
            {
                var flower = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BK/PureNature_Mountains/Prefabs/Plants/Carot1.prefab");
                typeof(SurfaceGrassRenderer).GetField("detailLayers", flags).SetValue(grass, new[] {
                    new SurfaceGrassRenderer.DetailLayer {
                        mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh,
                        material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial,
                        cellsPerPatch = 6, coverage = .65f, patchiness = 0,
                        meshScale = new Vector3(.35f, 1.8f, .35f), scaleRange = new Vector2(.95f, 1.35f)
                    },
                    new SurfaceGrassRenderer.DetailLayer {
                        mesh = flower.GetComponentInChildren<MeshFilter>().sharedMesh,
                        material = flower.GetComponentInChildren<MeshRenderer>().sharedMaterial,
                        cellsPerPatch = 4, coverage = .3f, patchiness = 0, meshScale = Vector3.one * .65f
                    }
                });
            }
            var cameraObject = new GameObject("Grass fixture camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            try
            {
                root.SetActive(true);
                yield return null;
                yield return null;
                Assert.That(grass.PatchCount, Is.EqualTo(meadow ? 32 : 16));
                int initial = grass.SupportedClumps;
                ulong originalHash = grass.PlacementHash;
                Assert.That(initial, Is.InRange(300, 650), "Wide vendor cards cover the surface at a lower root density.");
                var patches = (System.Collections.IList)typeof(SurfaceGrassRenderer).GetField("patches", flags).GetValue(grass);
                var supportedPatch = patches.Cast<object>().First(p => (int)p.GetType().GetField("NearCount").GetValue(p) > 0);
                var matrices = (Matrix4x4[])supportedPatch.GetType().GetField("Near").GetValue(supportedPatch);
                Vector3 supportedRoot = matrices[0].GetColumn(3);
                foreach (object patch in patches.Cast<object>().Take(16))
                {
                    int count = (int)patch.GetType().GetField("NearCount").GetValue(patch);
                    Assert.That(count, Is.GreaterThan(meadow ? 5 : 10), "Every supported spatial patch must retain grass, including boundary patches.");
                }
                if (meadow)
                    Assert.That(patches.Cast<object>().Skip(16).Sum(p => (int)p.GetType().GetField("NearCount").GetValue(p)),
                        Is.GreaterThan(20), "The flower layer must have its own supported population.");
                int colliderCount = root.GetComponentsInChildren<Collider>().Length;
                Assert.That(Physics.Raycast(supportedRoot + Vector3.up * 2, Vector3.down, out var hit, 4), Is.True);
                Assert.That(terrain.TryDig(hit, .5f), Is.True);
                typeof(SurfaceGrassRenderer).GetMethod("LateUpdate", flags).Invoke(grass, null);
                Assert.That(grass.LastRebuiltPatches, Is.InRange(1, meadow ? 8 : 4), "Only touched patches of each species need soil checks.");
                yield return null;
                yield return null;
                Assert.That(grass.SupportedClumps, Is.LessThan(initial));
                Assert.That(grass.SupportedClumps, Is.GreaterThan(initial - 120));
                Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.EqualTo(colliderCount));
                ulong cutHash = grass.PlacementHash;
                var checkpoint = terrain.Capture();
                terrain.ResetExcavation();
                yield return null;
                yield return null;
                Assert.That(grass.PlacementHash, Is.EqualTo(originalHash));
                yield return terrain.Restore(checkpoint, terrain.ExcavationSeed);
                yield return null;
                yield return null;
                Assert.That(grass.PlacementHash, Is.EqualTo(cutHash));
                grass.enabled = false;
                Assert.That(grass.PatchCount, Is.Zero);
                grass.enabled = true;
                yield return null;
                yield return null;
                Assert.That(grass.PlacementHash, Is.EqualTo(cutHash));
                // Real camera submissions validate both the material and culling path.
                var render = typeof(SurfaceGrassRenderer).GetMethod("RenderCamera", flags);
                camera.orthographic = true;
                camera.orthographicSize = 12;
                camera.transform.position = new Vector3(104, 5, 96);
                camera.transform.LookAt(new Vector3(104, 3, 104));
                render.Invoke(grass, new object[] { default(ScriptableRenderContext), camera });
                int near = grass.LastVisibleClumps;
                Assert.That(near, Is.EqualTo(grass.SupportedClumps));
                int nearTriangles = grass.LastTriangles;
                camera.transform.position = new Vector3(104, 8, 85);
                camera.transform.LookAt(new Vector3(104, 3, 104));
                render.Invoke(grass, new object[] { default(ScriptableRenderContext), camera });
                Assert.That(grass.LastVisibleClumps, Is.EqualTo(near), "Full-site coverage must not change across the old distance threshold.");
                Assert.That(grass.LastTriangles, Is.EqualTo(nearTriangles), "Lightweight vendor cards retain their silhouette at distance.");
                Assert.That(grass.LastDrawCalls, Is.LessThan(4), "Visible spatial patches should share submission batches.");
                // Look across only the tall tips, above the former short-grass
                // bounds. Root-level bounds alone would wrongly cull this view.
                camera.orthographicSize = .02f;
                camera.transform.position = new Vector3(104, 3.415f, 96);
                camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
                render.Invoke(grass, new object[] { default(ScriptableRenderContext), camera });
                Assert.That(grass.LastDrawCalls, Is.GreaterThan(0), "Tall tips must remain drawable when their roots leave the view.");
                camera.transform.rotation = Quaternion.LookRotation(Vector3.back);
                render.Invoke(grass, new object[] { default(ScriptableRenderContext), camera });
                Assert.That(grass.LastDrawCalls, Is.Zero, "Off-camera patches must not submit grass.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(soil);
                Object.Destroy(cameraObject);
            }
        }
    }
}
#endif
