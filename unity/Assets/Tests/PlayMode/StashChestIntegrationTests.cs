#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    // Concept 05 §3 finds inside finds (106, 109): the stash pits' old chest lies with its contents loose inside, refuses to
    // open until its lid has room, opens where it lies when Interact is held on its rusted lock, stays open across a save,
    // and once emptied goes when out of sight and untouched.
    public sealed class StashChestIntegrationTests
    {
        private Scene scene;
        private TerrainVolume terrain;
        private DiscoveryField field;
        private FpsPlayer player;

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
            player = root.GetComponentInChildren<FpsPlayer>();
            player.enabled = false;
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

        private BuriedFind[] Contents(BuriedChest chest)
            => field.Finds.Where(f => !f.Collected && Vector3.Distance(f.transform.position, chest.transform.position) < chest.Radius).ToArray();

        // Dig out the space the lid sweeps; the soil above the chest goes, the chest stays.
        private void ClearLid(BuriedChest chest)
        {
            var t = chest.transform;
            Assert.That(terrain.ClearLoadSweep(t.TransformPoint(new Vector3(-.1f, .75f, 0)), t.TransformPoint(new Vector3(-.1f, .75f, 0)),
                t.rotation, new Vector3(.62f, .55f, .8f)), Is.True);
        }

        [UnityTest]
        public IEnumerator AStashChestOpensWhereItLiesWhenItsLockIsForced()
        {
            Assert.That(field.Chests.Count, Is.EqualTo(3), "One chest per stash pit.");
            var chest = FirstChest();
            Assert.That(terrain.SurfaceHeight - chest.transform.position.y, Is.InRange(3.5f, 6.5f), "The first stash lies a few metres down.");
            Assert.That(chest.Opened || chest.Released, Is.False);
            Assert.That(chest.CanHold(player), Is.False, "Buried, its lid has no room.");
            Assert.That(chest.GetPrompt(player), Does.Contain("Clear the soil above its lid"));
            var contents = Contents(chest);
            Assert.That(contents.Length, Is.EqualTo(field.Catalog.ChestItems));
            // Loose in the seeded hollow, they settle on the chest's floor.
            yield return new WaitForSeconds(1.5f);
            Assert.That(contents.All(f => f.IsReleased), Is.True, "The contents lie loose inside.");
            Assert.That(chest.Released, Is.False, "Soil holds the chest.");

            ClearLid(chest);
            yield return new WaitForFixedUpdate();
            Assert.That(chest.LidHasRoom(), Is.True);
            Assert.That(chest.Released, Is.False, "Digging above does not free it.");
            var t = chest.transform;
            var eye = t.position + Vector3.up * 1.15f;
            var item = contents[0].transform.position;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(eye, (item - eye).normalized, out var blocked, 3) && blocked.collider.GetComponentInParent<BuriedChest>() == chest,
                Is.True, "The closed lid hides what it holds.");
            Assert.That(chest.GetPrompt(player), Does.Contain("Hold").And.Contain("rusted lock"));
            Assert.That(chest.CanHold(player), Is.True);
            Assert.That(chest.HoldSeconds(player), Is.EqualTo(BuriedChest.LockSeconds));
            Assert.That(typeof(IDigTarget).IsAssignableFrom(chest.GetType()), Is.False, "The tool never opens it.");

            Assert.That(chest.CompleteHold(player, default), Is.True, "The forced lock gives.");
            Assert.That(chest.Opened, Is.True);
            Assert.That(chest.CanHold(player), Is.False, "It opens once.");
            yield return new WaitForSeconds(3.5f);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(eye, (item - eye).normalized, out var seen, 3) && seen.collider.GetComponentInParent<BuriedFind>() == contents[0],
                Is.True, "The open chest shows what it holds.");
            Assert.That(contents.All(f => f.Collectible), Is.True, "Its contents are ordinary finds now.");
            Assert.That(field.Chests.Contains(chest), Is.True, "With its contents inside it stays.");

            var chests = field.CaptureChests();
            field.Restore(field.Capture(), field.Seed, chests);
            yield return null;
            var restored = FirstChest();
            Assert.That(restored, Is.Not.SameAs(chest));
            Assert.That(restored.Opened, Is.True, "Loading keeps it open.");
            var lid = restored.GetComponentsInChildren<Transform>().Single(x => x.name == "MainAxis");
            Assert.That(Quaternion.Angle(lid.localRotation, Quaternion.identity), Is.GreaterThan(60), "Its lid stays up.");
        }

        // An opened chest stays while anything lies in it, while the player can see it and while they touch it; emptied,
        // out of sight and untouched, it goes, and a load keeps it gone.
        [UnityTest]
        public IEnumerator AnEmptiedChestGoesOnlyOutOfSightAndUntouched()
        {
            var chest = FirstChest();
            var t = chest.transform;
            ClearLid(chest);
            yield return new WaitForSeconds(1.5f);
            var contents = Contents(chest);
            Assert.That(contents.Length, Is.EqualTo(field.Catalog.ChestItems));
            Assert.That(chest.CompleteHold(player, default), Is.True);
            yield return new WaitForSeconds(3.5f);
            var camera = player.ViewCamera.transform;
            var home = player.transform.position;
            void LookAway() { camera.rotation = Quaternion.LookRotation(Vector3.up); Physics.SyncTransforms(); }

            LookAway();
            yield return new WaitForSeconds(.6f);
            Assert.That(field.Chests.Contains(chest), Is.True, "Something still lies in it.");

            var commit = typeof(BuriedFind).GetMethod("CommitCollection", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var find in contents) Assert.That((bool)commit.Invoke(find, new object[] { player }), Is.True, find.Item.DisplayName);
            Assert.That(chest.HoldsAnything(), Is.False, "Emptied.");

            // Looking into the cleared lid space at it.
            camera.position = t.TransformPoint(new Vector3(-.1f, 1.05f, .6f));
            camera.LookAt(t.position);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            Assert.That(field.Chests.Contains(chest), Is.True, "In sight it stays.");

            // Standing on it, looking away.
            player.transform.position = t.position + Vector3.up * .2f;
            LookAway();
            yield return new WaitForSeconds(.6f);
            Assert.That(field.Chests.Contains(chest), Is.True, "Touched, it stays.");

            player.transform.position = home;
            LookAway();
            yield return new WaitForSeconds(.6f);
            Assert.That(field.Chests.Contains(chest), Is.False, "Emptied, out of sight and untouched, it goes.");
            Assert.That(chest == null, Is.True);

            var saved = field.CaptureChests();
            Assert.That(saved.Length, Is.EqualTo(2));
            field.Restore(field.Capture(), field.Seed, saved);
            yield return null;
            Assert.That(field.Chests.Count, Is.EqualTo(2), "A load keeps it gone.");
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
