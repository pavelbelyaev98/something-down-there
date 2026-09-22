using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class ShavingIntegrationTests
    {
        private GameObject root;
        private TerrainVolume terrain;
        private FpsPlayer player;
        private float timeScale;
        private CursorLockMode cursor;
        private bool cursorVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            timeScale = Time.timeScale; cursor = Cursor.lockState; cursorVisible = Cursor.visible;
            Time.timeScale = 1;
            root = new GameObject("Shaving fixture");
            var ground = new GameObject("Terrain");
            ground.transform.SetParent(root.transform);
            ground.SetActive(false);
            ground.transform.position = new Vector3(-3, -6, -3);
            terrain = ground.AddComponent<TerrainVolume>();
            terrain.Configure(new Vector3Int(48, 48, 48), .125f, 16, .23f, null);
            ground.SetActive(true);
            var actor = new GameObject("Player");
            actor.transform.SetParent(root.transform);
            actor.SetActive(false); actor.layer = 2;
            actor.transform.position = new Vector3(0, .2f, 0);
            var motor = actor.AddComponent<CharacterController>();
            motor.height = 1.8f; motor.center = Vector3.up * .9f; motor.radius = .3f;
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(actor.transform, false);
            camera.transform.localPosition = Vector3.up * 1.6f;
            camera.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            player = actor.AddComponent<FpsPlayer>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(FpsPlayer).GetField("excavationTerrain", flags).SetValue(player, terrain);
            typeof(FpsPlayer).GetField("surfaceReturn", flags).SetValue(player, root.transform);
            player.Tuning.Gravity = 0;
            actor.SetActive(true); player.enabled = false;
            player.SetApplicationFocus(true);
            yield return null;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Time.timeScale = timeScale; Cursor.lockState = cursor; Cursor.visible = cursorVisible;
        }

        [UnityTest]
        public IEnumerator HeldShavingCutsContinuouslyWithSynchronizedCollisionAndRestoresExactly()
        {
            Assert.That(player.ShavingEnabled, Is.True);
            float charge = player.Battery.Charge;
            var timings = new List<double>();
            float previousY = 0;
            int notifications = 0;
            terrain.Changed += _ => notifications++;
            for (int i = 0; i < 32; i++)
            {
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var before), Is.True);
                Assert.That(player.TryDig(), Is.True, "Shave " + i);
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var after), Is.True);
                Assert.That(after.point.y, Is.LessThan(before.point.y - .001f));
                Assert.That(before.point.y - after.point.y, Is.LessThan(.04f), "The slower shave must stay shallow.");
                timings.Add(terrain.LastDigMilliseconds);
                Assert.That(terrain.TryShave(before, player.EffectiveShovel.Radius, .03f), Is.False, "Reject stale contact.");
                previousY = after.point.y;
            }
            Assert.That(previousY, Is.InRange(-.5f, -.15f), "Steady held contact advances without the former rapid plunge.");
            Assert.That(notifications, Is.EqualTo(32));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - 32 * player.EffectiveDigEnergy).Within(.001f));
            foreach (var collider in terrain.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled))
                Assert.That(collider.sharedMesh, Is.SameAs(collider.GetComponent<MeshFilter>().sharedMesh));
            var saved = terrain.Capture();
            terrain.ResetExcavation();
            yield return terrain.Restore(saved, terrain.ExcavationSeed);
            Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var restored), Is.True);
            Assert.That(restored.point.y, Is.EqualTo(previousY).Within(.001f));
            TestContext.WriteLine($"Shaving: mean {timings.Average():F2} ms, max {timings.Max():F2} ms per cut.");
        }

        [Test]
        public void ComparisonSwitchPreservesWorldAndMatchesEnergyRate()
        {
            float shavingInterval = player.EffectiveDigInterval;
            float shavingRate = player.EffectiveDigEnergy / shavingInterval;
            Assert.That(player.TryDig(), Is.True);
            float shaved = terrain.RemovedVolume;
            int revision = terrain.Revision;
            player.ToggleAdminShaving();
            Assert.That(player.ShavingEnabled, Is.False);
            Assert.That(player.HasAdminOverrides, Is.True);
            Assert.That(terrain.Revision, Is.EqualTo(revision));
            Assert.That(terrain.RemovedVolume, Is.EqualTo(shaved));
            Assert.That(player.EffectiveDigInterval, Is.GreaterThan(shavingInterval * 5));
            Assert.That(player.EffectiveDigEnergy / player.EffectiveDigInterval, Is.EqualTo(shavingRate).Within(.0001f));
            Assert.That(player.TryDig(), Is.True);
            Assert.That(player.LastScoopVolume, Is.GreaterThan(shaved * 2));
            player.ToggleAdminShaving();
            Assert.That(player.ShavingEnabled, Is.True);
            Assert.That(player.TryDig(), Is.True);
            player.ToggleAdminShaving(); player.RestoreAdminOverrides();
            Assert.That(player.ShavingEnabled, Is.True);
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
        }

        [Test]
        public void HeldCadenceIsStableAcrossFrameRatesAndDoesNotBankIdleTime()
        {
            int previousCount = -1;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                terrain.ResetExcavation(); player.RestoreAdminOverrides(); player.RefillAdminBattery();
                player.Tick(default, 20f);
                int start = player.SuccessfulStrokes;
                for (int frame = 0; frame < fps; frame++)
                    player.Tick(new FpsInputFrame { DigHeld = true, DigPressed = frame == 0 }, 1f / fps);
                int count = player.SuccessfulStrokes - start;
                Assert.That(count, Is.InRange(17, 19));
                if (previousCount >= 0) Assert.That(count, Is.EqualTo(previousCount).Within(1));
                previousCount = count;
                player.Tick(default, 30);
                start = player.SuccessfulStrokes;
                player.Tick(new FpsInputFrame { DigHeld = true, DigPressed = true }, 30);
                Assert.That(player.SuccessfulStrokes, Is.EqualTo(start + 1));
            }
            int paused = player.SuccessfulStrokes;
            player.OpenMenu(PlayerMenu.Pause);
            player.Tick(new FpsInputFrame { DigHeld = true }, 30);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(paused));
        }
    }
}
