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
    // Concept 05 §3 finds inside finds (106): the stash pits' old chest lies with its contents loose inside, refuses to
    // open until its lid has room, opens where it lies with one strike, and stays open across a save.
    public sealed class StashChestIntegrationTests
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
            TestInputPreferences.RestoreLayerFixture(field);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
        }

        private BuriedChest FirstChest() => field.Chests.OrderBy(c => terrain.SurfaceHeight - c.transform.position.y).First();

        [UnityTest]
        public IEnumerator AStashChestOpensWhereItLiesOnceItsLidHasRoom()
        {
            Assert.That(field.Chests.Count, Is.EqualTo(3), "One chest per stash pit.");
            var chest = FirstChest();
            Assert.That(terrain.SurfaceHeight - chest.transform.position.y, Is.InRange(3.5f, 6.5f), "The first stash lies a few metres down.");
            Assert.That(chest.Opened || chest.Released, Is.False);
            Assert.That(chest.CanDig, Is.False, "Buried, its lid has no room.");
            var contents = field.Finds.Where(f => Vector3.Distance(f.transform.position, chest.transform.position) < chest.Radius).ToArray();
            Assert.That(contents.Length, Is.EqualTo(field.Catalog.ChestItems));
            // Loose in the seeded hollow, they settle on the chest's floor.
            yield return new WaitForSeconds(1.5f);
            Assert.That(contents.All(f => f.IsReleased), Is.True, "The contents lie loose inside.");
            Assert.That(chest.Released, Is.False, "Soil holds the chest.");

            // Dig out the space the lid sweeps; the soil above the chest goes, the chest stays.
            var t = chest.transform;
            Assert.That(terrain.ClearLoadSweep(t.TransformPoint(new Vector3(-.1f, .75f, 0)), t.TransformPoint(new Vector3(-.1f, .75f, 0)),
                t.rotation, new Vector3(.62f, .55f, .8f)), Is.True);
            yield return new WaitForFixedUpdate();
            Assert.That(chest.LidHasRoom(), Is.True);
            Assert.That(chest.Released, Is.False, "Digging above does not free it.");
            var eye = t.position + Vector3.up * 1.15f;
            var item = contents[0].transform.position;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(eye, (item - eye).normalized, out var blocked, 3) && blocked.collider.GetComponentInParent<BuriedChest>() == chest,
                Is.True, "The closed lid hides what it holds.");
            Assert.That(chest.GetPrompt(Object.FindAnyObjectByType<FpsPlayer>()), Does.Contain("rusted lock"));

            Assert.That(chest.TryDig(default), Is.True, "One strike breaks the lock.");
            Assert.That(chest.Opened, Is.True);
            Assert.That(chest.CanDig, Is.False, "It opens once.");
            yield return new WaitForSeconds(3.5f);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(eye, (item - eye).normalized, out var seen, 3) && seen.collider.GetComponentInParent<BuriedFind>() == contents[0],
                Is.True, "The open chest shows what it holds.");
            Assert.That(contents.All(f => f.Collectible), Is.True, "Its contents are ordinary finds now.");

            var chests = field.CaptureChests();
            field.Restore(field.Capture(), field.Seed, chests);
            yield return null;
            var restored = FirstChest();
            Assert.That(restored, Is.Not.SameAs(chest));
            Assert.That(restored.Opened, Is.True, "Loading keeps it open.");
            var lid = restored.GetComponentsInChildren<Transform>().Single(x => x.name == "MainAxis");
            Assert.That(Quaternion.Angle(lid.localRotation, Quaternion.identity), Is.GreaterThan(60), "Its lid stays up.");
        }

        // Dug out under and around its lower half, the chest falls and settles; a save keeps where it fell.
        [UnityTest]
        public IEnumerator AnUndercutChestFallsAndKeepsItsPlace()
        {
            var chest = FirstChest();
            var t = chest.transform;
            var start = t.position;
            Assert.That(terrain.ClearLoadSweep(t.TransformPoint(new Vector3(0, -.65f, 0)), t.TransformPoint(new Vector3(0, -.65f, 0)),
                t.rotation, new Vector3(.65f, .55f, .95f)), Is.True);
            yield return new WaitForSeconds(2.5f);
            Assert.That(chest.Released, Is.True, "Nothing holds it up.");
            Assert.That(chest.transform.position.y, Is.LessThan(start.y - .3f), "It falls into the hole.");
            var fell = chest.transform.position;
            var saved = field.CaptureChests();
            field.Restore(field.Capture(), field.Seed, saved);
            yield return null;
            var restored = field.Chests.OrderBy(c => Vector3.Distance(c.transform.position, fell)).First();
            Assert.That(Vector3.Distance(restored.transform.position, fell), Is.LessThan(.05f));
            Assert.That(restored.Released, Is.True);
        }
    }
}
#endif
