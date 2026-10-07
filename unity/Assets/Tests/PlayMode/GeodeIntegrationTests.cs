#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    // Concept 03 §5 geodes (110): sealed hollows in a shell of hard stone, lined with crystals that hold until the shell
    // around them is dug; the first way in opens a geode once, and a load knows it is open from the ground alone.
    public sealed class GeodeIntegrationTests
    {
        private Scene scene;
        private TerrainVolume terrain;
        private DiscoveryField field;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.sceneLoaded += TestInputPreferences.ConfigureLayerFinds;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.ConfigureLayerFinds;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects()[0];
            terrain = root.GetComponentInChildren<TerrainVolume>();
            field = root.GetComponentInChildren<DiscoveryField>();
            root.GetComponentInChildren<FpsPlayer>().enabled = false;
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
        }

        // A shaft 0.6 m square, swept less than a metre at a time (ClearLoadSweep's step).
        private void Dig(Vector3 from, Vector3 to)
        {
            int steps = Mathf.CeilToInt(Vector3.Distance(from, to) / .8f);
            for (int i = 0; i < steps; i++)
                terrain.ClearLoadSweep(Vector3.Lerp(from, to, i / (float)steps), Vector3.Lerp(from, to, (i + 1) / (float)steps), Quaternion.identity, Vector3.one * .3f);
        }

        [UnityTest]
        public IEnumerator AGeodeIsSealedUntilTheWayInReachesItsHollowAndStaysOpenAfterALoad()
        {
            // The whole population: an earlier suite's layer fixture leaves this scene's field without finds.
            TestInputPreferences.RestoreLayerFixture(field);
            TestInputPreferences.RestoreGeneratedPopulation(field);
            yield return null;
            Physics.SyncTransforms();
            var geodes = terrain.GroundLayout.Geodes;
            Assert.That(geodes.Length, Is.EqualTo(TerrainGround.GeodesPerZone.Sum()));
            Assert.That(Enumerable.Range(0, geodes.Length).Any(field.GeodeOpened), Is.False, "Sealed from New Game.");
            var geode = geodes[0];
            var centre = terrain.transform.TransformPoint((Vector3)geode.Centre);
            Assert.That(terrain.IsSolid(centre), Is.False, "Its hollow is air.");
            Assert.That(terrain.IsSolid(centre + Vector3.up * (geode.Radii.y + geode.Shell * .5f)), Is.True, "Its shell is stone.");
            // Its crystals line the hollow and hold until the shell around them is dug.
            var crystals = field.Finds.Where(f => field.Catalog.Entries.Any(e => e.Geode && e.Prefab.DisplayName == f.DisplayName)
                && Vector3.Distance(f.transform.position, centre) < geode.Reach).ToArray();
            Assert.That(crystals.Length, Is.EqualTo(DiscoveryCatalog.GeodeCrystals));
            yield return new WaitForSeconds(1f);
            Assert.That(crystals.Any(f => f.IsReleased), Is.False, "Anchored in the shell.");
            // A shaft from above stopping short of the hollow leaves it sealed; the last cut opens it, once.
            var top = centre + Vector3.up * (geode.Reach + 1);
            var shellTop = centre + Vector3.up * (geode.Radii.y + .2f);
            Dig(top, shellTop + Vector3.up * .35f);
            Assert.That(field.GeodeOpened(0), Is.False, "Digging toward it is not breaking in.");
            Dig(shellTop + Vector3.up * .35f, centre + Vector3.up * geode.Radii.y * .5f);
            Assert.That(field.GeodeOpened(0), Is.True, "Breaking through opens it.");
            Assert.That(Enumerable.Range(1, geodes.Length - 1).Any(field.GeodeOpened), Is.False, "The others stay sealed.");
            field.Restore(field.Capture(), field.Seed, field.CaptureChests());
            Assert.That(field.GeodeOpened(0), Is.True, "A load knows it is open from the ground.");
            Assert.That(Enumerable.Range(1, geodes.Length - 1).Any(field.GeodeOpened), Is.False);
        }
    }
}
#endif
