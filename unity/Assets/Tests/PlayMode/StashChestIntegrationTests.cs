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
    // Concept 05 §3 finds inside finds (106, 109): the stash pits' old chest stands in a pocket of air with its contents
    // loose inside, opens where it lies when Interact is held on its rusted lock, stays open across a save, and once
    // emptied goes when out of sight and untouched.
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

        // The player's eye a metre in front of the chest's lock (or behind it), looking at it.
        private void Stand(BuriedChest chest, float side)
        {
            var camera = player.ViewCamera.transform;
            camera.position = chest.transform.TransformPoint(new Vector3(side, .6f, 0));
            camera.LookAt(chest.transform.position);
            Physics.SyncTransforms();
        }

        private BuriedFind[] Contents(BuriedChest chest)
            => field.Finds.Where(f => !f.Collected && chest.Hollow.Contains(chest.transform.InverseTransformPoint(f.transform.position))).ToArray();

        [UnityTest]
        public IEnumerator AStashChestOpensWhereItLiesWhenItsLockIsForced()
        {
            Assert.That(field.Chests.Count, Is.EqualTo(3), "One chest per stash pit.");
            var chest = FirstChest();
            Assert.That(terrain.SurfaceHeight - chest.transform.position.y, Is.InRange(3.5f, 6.5f), "The first stash lies a few metres down.");
            Assert.That(chest.Opened || chest.Released, Is.False);
            var t = chest.transform;
            var pocket = chest.Pocket;
            // The fill settled away from it: open air on every side and above the lid, a floor of ground under it.
            foreach (var side in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                Assert.That(terrain.IsSolid(t.TransformPoint(pocket.center + Vector3.Scale(side, pocket.extents) * .6f)), Is.False, "Air beside it.");
            Assert.That(terrain.IsSolid(t.TransformPoint(new Vector3(pocket.center.x, pocket.max.y - .15f, pocket.center.z))), Is.False, "Air above it.");
            Assert.That(chest.LidHasRoom(), Is.True, "Its lid can swing the moment it is reached.");
            var contents = Contents(chest);
            Assert.That(contents.Length, Is.EqualTo(field.Catalog.ChestItems));
            // Loose in the seeded pocket, they settle on the chest's floor.
            yield return new WaitForSeconds(1.5f);
            Assert.That(contents.All(f => f.IsReleased), Is.True, "The contents lie loose inside.");
            Assert.That(chest.Released, Is.False, "Its footing holds the chest.");
            var eye = t.position + Vector3.up * 1.15f;
            // The top of the heap (113), which nothing else covers.
            var top = contents.OrderByDescending(f => f.WorldBounds.max.y).First();
            var item = top.transform.position;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(eye, (item - eye).normalized, out var blocked, 3) && blocked.collider.GetComponentInParent<BuriedChest>() == chest,
                Is.True, "The closed lid hides what it holds.");
            Stand(chest, -1);
            Assert.That(chest.CanHold(player), Is.False, "Not from behind it.");
            Assert.That(chest.GetPrompt(player), Does.Contain("Go round to its lock"));
            Stand(chest, 1);
            Assert.That(chest.GetPrompt(player), Does.Contain("Hold").And.Contain("rusted lock"));
            Assert.That(chest.CanHold(player), Is.True, "From in front of its lock.");
            Assert.That(chest.HoldSeconds(player), Is.EqualTo(BuriedChest.LockSeconds));
            Assert.That(typeof(IDigTarget).IsAssignableFrom(chest.GetType()), Is.False, "The tool never opens it.");

            Assert.That(chest.CompleteHold(player, default), Is.True, "The forced lock gives.");
            Assert.That(chest.Opened, Is.True);
            Assert.That(chest.CanHold(player), Is.False, "It opens once.");
            yield return new WaitForSeconds(3.5f);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(eye, (item - eye).normalized, out var seen, 3) && seen.collider.GetComponentInParent<BuriedFind>() == top,
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
            yield return new WaitForSeconds(1.5f);
            var contents = Contents(chest);
            Assert.That(contents.Length, Is.EqualTo(field.Catalog.ChestItems));
            Stand(chest, 1);
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

            // Looking at it from inside its pocket.
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

        // What it holds (113: ingots and crystals) is taken by hand, one piece per Interact; neither the dig action nor
        // walking past takes it.
        [UnityTest]
        public IEnumerator ItsTreasureIsTakenByHandOnePieceAtATime()
        {
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            var chest = FirstChest();
            var t = chest.transform;
            yield return new WaitForSeconds(1.5f);
            Stand(chest, 1);
            Assert.That(chest.CompleteHold(player, default), Is.True);
            yield return new WaitForSeconds(3.5f);
            var contents = Contents(chest);
            Assert.That(contents.Length, Is.EqualTo(field.Catalog.ChestItems));
            Assert.That(contents.All(f => f.HandPicked && f.Collectible), Is.True, "Ingots and crystals, free to take.");
            // The top of the heap (113), which nothing else covers.
            var find = contents.OrderByDescending(f => f.WorldBounds.max.y).First();
            var camera = player.ViewCamera.transform;
            camera.position = t.TransformPoint(new Vector3(.1f, .9f, 0));
            camera.LookAt(find.WorldBounds.center);
            Physics.SyncTransforms();
            Assert.That(find.GetPrompt(player), Does.Contain("to take"));
            Assert.That(find.TryCollect(player), Is.False, "The dig action never takes it.");
            var nearby = typeof(BuriedFind).GetMethod("TryCollectNearby", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)nearby.Invoke(find, new object[] { player }), Is.False, "Walking past never takes it.");
            player.enabled = true;
            Assert.That(find.TryInteract(player), Is.True, "Interact takes it.");
            Assert.That(find.Collected, Is.True);
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
            Assert.That(contents.Where(f => f != find).Any(f => f.Collected), Is.False, "One piece at a time.");
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
