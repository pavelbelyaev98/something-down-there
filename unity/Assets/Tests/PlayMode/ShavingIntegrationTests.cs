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
            player.SelectAdminLevel(EquipmentProgression.DrillLevel);
            Assert.That(player.ShavingEnabled, Is.True);
            float charge = player.Battery.Charge;
            var timings = new List<double>();
            float previousY = 0;
            int notifications = 0;
            terrain.Changed += _ => notifications++;
            for (int i = 0; i < 12; i++)
            {
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var before), Is.True, $"Target before cut {i} (last at {previousY:F3})");
                Assert.That(player.TryDig(), Is.True, "Shave " + i);
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var after), Is.True, $"Target after cut {i} (before at {before.point.y:F3})");
                Assert.That(after.point.y, Is.LessThan(before.point.y - .001f));
                // The bit goes a few layers further at most (while it bores in, more in the softest ground), never a whole
                // scoop (about 0.75 R).
                Assert.That(before.point.y - after.point.y, Is.LessThan(player.EffectiveShovel.Radius * 1.4f
                    * EquipmentProgression.DrillAdvanceRatio * EquipmentProgression.DrillPushes),
                    "Drilling removes shallow layers, not whole scoops.");
                timings.Add(terrain.LastDigMilliseconds);
                Assert.That(terrain.TryToolCut(before, player.EffectiveShovel.Radius, true, Vector3.down), Is.False, "Reject stale contact.");
                previousY = after.point.y;
            }
            Assert.That(previousY, Is.LessThan(-.2f), "Steady held contact must keep advancing.");
            Assert.That(notifications, Is.EqualTo(12));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - 12 * player.EffectiveDigEnergy).Within(.001f));
            foreach (var collider in terrain.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled))
                Assert.That(collider.sharedMesh, Is.SameAs(collider.GetComponent<MeshFilter>().sharedMesh));
            var saved = terrain.Capture();
            terrain.ResetExcavation();
            yield return terrain.Restore(saved, terrain.ExcavationSeed);
            Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var restored), Is.True, "Target after restore");
            Assert.That(restored.point.y, Is.EqualTo(previousY).Within(.001f));
            TestContext.WriteLine($"Shaving: mean {timings.Average():F2} ms, max {timings.Max():F2} ms per cut.");
        }

        [TestCase(6, false)] [TestCase(7, true)] [TestCase(10, true)]
        public void OwnedMotionAndSaveRestoreIgnoreDeveloperOverrides(int level, bool drill)
        {
            Assert.That(player.ShavingEnabled, Is.False, "A new player starts with a shovel.");
            for (int next = 2; next <= level; next++) Assert.That(player.Shovel.TryUpgradeTo(next), Is.True);
            Assert.That(player.ShavingEnabled, Is.EqualTo(drill));
            float charge = player.Battery.Charge;
            Assert.That(player.TryDig(), Is.True);
            Assert.That(player.Battery.Charge, Is.LessThan(charge));
            var saved = new WorldSnapshot(); player.Capture(saved);
            player.SelectAdminLevel(drill ? 1 : 10);
            player.Restore(saved);
            Assert.That(player.Shovel.Level, Is.EqualTo(level));
            Assert.That(player.ShavingEnabled, Is.EqualTo(drill));
            Assert.That(player.HasAdminOverrides, Is.False);
            Assert.That(player.DigIntervalAtLevel(7), Is.LessThan(player.DigIntervalAtLevel(6)));
        }

        // Flight is Space alone (user, 2026-10-10): letting go falls whatever else is held; there is no hover.
        [Test]
        public void LettingGoOfSpaceFallsEvenWhileDiggingOrCrouching()
        {
            player.Tuning.Gravity = -20;
            Assert.That(player.Jetpack.TryUpgradeTo(2), Is.True);
            foreach (var held in new[] { new FpsInputFrame { DigHeld = true }, new FpsInputFrame { CrouchHeld = true }, default })
            {
                for (int i = 0; i < 48; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
                for (int i = 0; i < 60; i++) player.Tick(held, 1f / 60f);
                Assert.That(player.VerticalSpeed, Is.LessThan(0));
                for (int i = 0; i < 240; i++) player.Tick(default, 1f / 60f);
            }
        }

        // Ground thinner than the contact search (an overhang about a cell thick) still takes a drill cut (user,
        // 2026-10-05: a thin bridge could not be dug).
        [Test]
        public void DrillCutsThinOverhangs()
        {
            player.SelectAdminLevel(EquipmentProgression.DrillLevel);
            // A cavity under the surface leaves a roof about one cell thick.
            Assert.That(terrain.ClearLoadSweep(new Vector3(0, -.6f, 0), new Vector3(0, -.6f, .01f), Quaternion.identity, new Vector3(.8f, .45f, .8f)), Is.True);
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(new Vector3(0, -.8f, 0), Vector3.up, out var hit, 1f), Is.True);
            Assert.That(hit.point.y, Is.GreaterThan(-.25f), "The roof is thin.");
            Assert.That(terrain.TryToolCut(hit, player.EffectiveShovel.Radius, true, Vector3.up), Is.True);
        }

        [TestCase(1)] [TestCase(7)]
        public void HeldCadenceIsStableAcrossFrameRatesAndDoesNotBankIdleTime(int level)
        {
            // Cadence alone: a shovel's cut otherwise lands at its scoop, after the press (TerrainIntegrationTests).
            player.Tuning.CutAtScoop = false;
            int previousCount = -1;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                terrain.ResetExcavation(); player.RestoreAdminOverrides(); player.RefillAdminBattery();
                player.SelectAdminLevel(level);
                player.Tick(default, 20f);
                int start = player.SuccessfulStrokes;
                for (int frame = 0; frame < fps; frame++)
                    player.Tick(new FpsInputFrame { DigHeld = true, DigPressed = frame == 0 }, 1f / fps);
                int count = player.SuccessfulStrokes - start;
                Assert.That(count, Is.EqualTo(Mathf.CeilToInt(1f / player.EffectiveDigInterval)).Within(1));
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
