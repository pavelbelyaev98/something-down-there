using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class FpsPlayerTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private FpsPlayer player;
        private float originalTimeScale;
        private CursorLockMode originalCursor;
        private bool originalCursorVisible;

        [SetUp]
        public void SetUp()
        {
            originalTimeScale = Time.timeScale;
            originalCursor = Cursor.lockState;
            originalCursorVisible = Cursor.visible;
            Time.timeScale = 1f;
            var root = Track(new GameObject("Test Player"));
            root.SetActive(false);
            root.layer = 2;
            var motor = root.AddComponent<CharacterController>();
            motor.height = 1.8f;
            motor.center = new Vector3(0, 0.9f, 0);
            motor.radius = 0.3f;
            motor.minMoveDistance = 0;
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.6f, 0);
            player = root.AddComponent<FpsPlayer>();
            root.SetActive(true);
            Box("Floor", new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100));
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in objects) if (item != null) Object.DestroyImmediate(item);
            objects.Clear();
            Time.timeScale = originalTimeScale;
            Cursor.lockState = originalCursor;
            Cursor.visible = originalCursorVisible;
        }

        [Test]
        public void LookingDownKeepsWalkingHorizontalAndClampsPitch()
        {
            float energy = player.Battery.Charge;
            player.Tick(new FpsInputFrame { Look = new Vector2(0, -10000), Move = new Vector2(1, 1) }, 0.1f);
            var position = player.transform.position;
            Assert.That(new Vector2(position.x, position.z).magnitude, Is.EqualTo(0.4f).Within(0.02f));
            // CharacterController depenetration may lift the feet by its skin width.
            Assert.That(position.y, Is.LessThanOrEqualTo(player.GetComponent<CharacterController>().skinWidth + 0.01f));
            Assert.That(player.Pitch, Is.EqualTo(85f));
            Assert.That(player.Battery.Charge, Is.EqualTo(energy));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WalkingAndSprintingCannotPassThroughSolidWall(bool sprint)
        {
            Box("Wall", new Vector3(0, 1.5f, 1.5f), new Vector3(5, 3, 0.5f));
            Physics.SyncTransforms();
            for (int i = 0; i < 60; i++) player.Tick(new FpsInputFrame { Move = Vector2.up, SprintHeld = sprint }, 1f / 60f);
            Assert.That(player.transform.position.z, Is.LessThan(1.1f));
            Assert.That(player.transform.position.z, Is.GreaterThan(0.5f));
        }

        [Test]
        public void OccluderAndReachPreventDiggingAndCollection()
        {
            var target = Box("Target", new Vector3(0, 1.6f, 2), Vector3.one * 0.4f);
            var dig = target.AddComponent<ValidationDigTarget>();
            target.AddComponent<ValidationFind>();
            var wall = Box("Blocker", new Vector3(0, 1.6f, 1), Vector3.one * 0.5f);
            Physics.SyncTransforms();
            Assert.That(player.TryDig(), Is.False);
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            wall.SetActive(false);
            target.transform.position = new Vector3(0, 1.6f, 3.9f);
            Physics.SyncTransforms();
            Assert.That(player.TryDig(), Is.False);
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(dig.HitsRemaining, Is.EqualTo(3));
            for (int level = 2; level <= 6; level++) player.Shovel.TryUpgradeTo(level);
            Assert.That(player.AdminAvailable, Is.False, "Owned reach must not depend on admin scene wiring.");
            Assert.That(player.TryDig(), Is.True);
            Assert.That(player.TryInteract(), Is.False, "Shovel upgrades do not extend collection reach.");
            wall.SetActive(true);
            Physics.SyncTransforms();
            Assert.That(player.TryDig(), Is.False, "Long reach cannot tunnel a ray through an occluder.");
            wall.SetActive(false);
            player.Tuning.DigReach = 20;
            target.transform.position = new Vector3(0, 1.6f, FpsPlayer.MaximumDigReach + 1f);
            Physics.SyncTransforms();
            Assert.That(player.EffectiveDigReach, Is.EqualTo(FpsPlayer.MaximumDigReach), "The reach cap also covers custom tuning.");
            Assert.That(player.TryDig(), Is.False, "Nothing beyond the reach cap can be excavated.");
        }

        [Test]
        public void DigCadenceChargesOnlyAcceptedHitsAndDoesNotCollect()
        {
            var target = Box("Dig", new Vector3(0, 1.6f, 2), Vector3.one);
            var dig = target.AddComponent<ValidationDigTarget>();
            var find = Box("Find", new Vector3(0, 1.6f, 2.8f), Vector3.one * 0.2f);
            find.AddComponent<ValidationFind>();
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) player.Tick(new FpsInputFrame { DigHeld = true }, 0.01f);
            Assert.That(dig.HitsRemaining, Is.EqualTo(2));
            Assert.That(player.Battery.Charge, Is.EqualTo(99));
            player.Tick(new FpsInputFrame { DigHeld = true }, player.EffectiveDigInterval);
            Assert.That(dig.HitsRemaining, Is.EqualTo(1));
            Assert.That(player.Inventory.Count, Is.Zero);
        }

        [Test]
        public void EmptyBatteryBlocksDigAndJetpackButAllowsWalking()
        {
            var target = Box("Dig", new Vector3(0, 1.6f, 2), Vector3.one).AddComponent<ValidationDigTarget>();
            Physics.SyncTransforms();
            player.Battery.TrySpend(100);
            Assert.That(player.TryDig(), Is.False);
            player.Tick(new FpsInputFrame { JetpackHeld = true, Move = Vector2.right }, 0.1f);
            Assert.That(player.transform.position.x, Is.GreaterThan(0.3f));
            Assert.That(player.VerticalSpeed, Is.LessThanOrEqualTo(0));
            Assert.That(target.HitsRemaining, Is.EqualTo(3));
        }

        [Test]
        public void JetpackUsesSharedBatteryAndCannotPassThroughCeiling()
        {
            Box("Ceiling", new Vector3(0, 3, 0), new Vector3(10, 0.5f, 10));
            Physics.SyncTransforms();
            for (int i = 0; i < 120; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
            Assert.That(player.transform.position.y, Is.InRange(0.5f, 1.1f));
            Assert.That(player.Battery.Charge, Is.EqualTo(85.76f).Within(0.01f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TapJumpsWithoutBatteryCostOrMidairJumpAndLands(bool emptyBattery)
        {
            if (emptyBattery) player.Battery.TrySpend(100);
            player.Tick(default, 0.02f); // Establish ground contact.
            float charge = player.Battery.Charge;
            player.Tick(new FpsInputFrame { JumpPressed = true, JetpackHeld = true }, 0.02f);
            Assert.That(player.VerticalSpeed, Is.GreaterThan(6f));
            Assert.That(player.IsJetpackActive, Is.False);
            float upwardSpeed = player.VerticalSpeed;
            // A second press in the air cannot reset the jump impulse.
            player.Tick(new FpsInputFrame { JumpPressed = true }, 0.02f);
            Assert.That(player.VerticalSpeed, Is.LessThan(upwardSpeed));
            float apex = player.transform.position.y;
            for (int i = 0; i < 100; i++)
            {
                player.Tick(default, 0.01f);
                apex = Mathf.Max(apex, player.transform.position.y);
            }
            Assert.That(apex, Is.InRange(1f, 1.25f));
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
        }

        [Test]
        public void HoldDelaysThrustAndReleaseOrDepletionStopsConsumption()
        {
            player.Tick(default, 0.02f);
            player.Tick(new FpsInputFrame { JumpPressed = true, JetpackHeld = true }, 0.1f);
            player.Tick(new FpsInputFrame { JetpackHeld = true }, 0.1f);
            Assert.That(player.Battery.Charge, Is.EqualTo(100f));
            Assert.That(player.IsJetpackActive, Is.False);
            player.Tick(new FpsInputFrame { JetpackHeld = true }, 0.1f);
            Assert.That(player.IsJetpackActive, Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(99.36f).Within(0.001f));
            player.Tick(default, 0.02f);
            Assert.That(player.IsJetpackActive, Is.False);
            player.Tick(new FpsInputFrame { JetpackHeld = true }, 0.1f);
            Assert.That(player.IsJetpackActive, Is.True, "Re-pressing during the same flight resumes immediately.");
            Assert.That(player.Battery.Charge, Is.EqualTo(98.56f).Within(0.001f));
            player.Battery.TrySpend(player.Battery.Charge);
            for (int i = 0; i < 150; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 0.02f);
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(player.Battery.Charge, Is.Zero);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True,
                "Keeping Space down cannot cause repeated jumps when landing.");
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void JetpackUsesLastFuelThenStaysOffAcrossFrameRateChanges(int framesPerSecond)
        {
            float step = 1f / framesPerSecond;
            for (int i = 0; i < framesPerSecond; i++)
                player.Tick(new FpsInputFrame { JetpackHeld = true }, step);
            Assert.That(player.IsJetpackActive, Is.True);
            player.Battery.TrySpend(player.Battery.Charge - 0.05f);

            // Less than one frame's fuel still belongs to the player. Leaving it
            // stranded makes the pack restart as soon as a shorter frame arrives.
            player.Tick(new FpsInputFrame { JetpackHeld = true }, step);
            Assert.That(player.Battery.Charge, Is.Zero);
            float speed = player.VerticalSpeed;
            foreach (float nextStep in new[] { 1f / 30f, 1f / 240f, 1f / 60f })
            {
                player.Tick(new FpsInputFrame { JetpackHeld = true }, nextStep);
                Assert.That(player.IsJetpackActive, Is.False);
                Assert.That(player.Battery.Charge, Is.Zero);
                Assert.That(player.VerticalSpeed, Is.LessThan(speed));
                speed = player.VerticalSpeed;
            }
        }

        [Test]
        public void JetpackCanArrestRepeatedFallsAndLandingRestoresInitialHoldDelay()
        {
            for (int i = 0; i < 75; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 0.02f);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = 0; i < 25; i++) player.Tick(default, 0.02f);
                Assert.That(player.VerticalSpeed, Is.LessThan(0));
                float energy = player.Battery.Charge;
                float y = player.transform.position.y;
                player.Tick(new FpsInputFrame { JetpackHeld = true, JumpPressed = true }, 0.02f);
                Assert.That(player.IsJetpackActive, Is.True);
                Assert.That(player.VerticalSpeed, Is.GreaterThan(0));
                Assert.That(player.transform.position.y, Is.GreaterThan(y));
                Assert.That(player.Battery.Charge, Is.EqualTo(energy - 0.16f).Within(0.001f));
            }
            for (int i = 0; i < 200; i++) player.Tick(default, 0.02f);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
            float landedEnergy = player.Battery.Charge;
            player.Tick(new FpsInputFrame { JumpPressed = true, JetpackHeld = true }, 0.02f);
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(landedEnergy));
        }

        [TestCase(1)] [TestCase(4)] [TestCase(10)]
        public void EachJetpackLevelClimbsAtItsOwnSpeedAndRate(int level)
        {
            for (int next = 2; next <= level; next++) Assert.That(player.Jetpack.TryUpgradeTo(next), Is.True);
            var jet = EquipmentProgression.Jetpack(level);
            player.Tick(default, 0.02f);
            float charge = player.Battery.Charge;
            for (int i = 0; i < 120; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
            Assert.That(player.VerticalSpeed, Is.EqualTo(jet.MaxAscentSpeed).Within(.01f));
            Assert.That(charge - player.Battery.Charge, Is.EqualTo(jet.EnergyPerSecond * (2f - player.Tuning.JetpackHoldDelay)).Within(.15f));
        }

        [Test]
        public void HoverHoldsHeightWhileDiggingFromTheFirstJetpackPurchase()
        {
            player.Tick(default, 0.02f);
            for (int i = 0; i < 72; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
            Assert.That(player.transform.position.y, Is.GreaterThan(2f));
            for (int i = 0; i < 200; i++)
            {
                player.Tick(new FpsInputFrame { DigHeld = true }, 1f / 60f);
                Assert.That(player.IsHovering, Is.False, "The starter jetpack has no hover hold.");
            }
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);

            Assert.That(player.Jetpack.TryUpgradeTo(2), Is.True);
            for (int i = 0; i < 72; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
            for (int i = 0; i < 30; i++) player.Tick(new FpsInputFrame { DigHeld = true }, 1f / 60f);
            Assert.That(player.IsHovering, Is.True);
            float held = player.transform.position.y, charge = player.Battery.Charge;
            Assert.That(held, Is.GreaterThan(2f));
            for (int i = 0; i < 120; i++)
            {
                player.Tick(new FpsInputFrame { DigHeld = true }, 1f / 60f);
                Assert.That(player.transform.position.y, Is.EqualTo(held).Within(.03f));
            }
            Assert.That(charge - player.Battery.Charge, Is.EqualTo(2f * 8.1f * EquipmentProgression.HoverEnergyScale).Within(.05f));
            for (int i = 0; i < 20; i++) player.Tick(default, 1f / 60f);
            Assert.That(player.IsHovering, Is.False, "Letting go of dig drops as before.");
            Assert.That(player.VerticalSpeed, Is.LessThan(0));
        }

        [Test]
        public void HoverNeverHoldsJustAboveTheFloorOrOnAnEmptyBattery()
        {
            Assert.That(player.Jetpack.TryUpgradeTo(2), Is.True);
            player.Tick(default, 0.02f);
            for (int i = 0; i < 17; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
            for (int i = 0; i < 90; i++)
            {
                player.Tick(new FpsInputFrame { DigHeld = true }, 1f / 60f);
                Assert.That(player.IsHovering, Is.False);
            }
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True, "Arriving near a floor lands.");
            for (int i = 0; i < 48; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
            player.Battery.TrySpend(player.Battery.Charge);
            player.Tick(new FpsInputFrame { DigHeld = true }, 1f / 60f);
            Assert.That(player.IsHovering, Is.False);
            for (int i = 0; i < 200; i++) player.Tick(new FpsInputFrame { DigHeld = true }, 1f / 60f);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
        }

        [Test]
        public void FullOrUnexposedFindStaysInWorldAndCannotBeCollectedTwice()
        {
            var find = Box("Find", new Vector3(0, 1.6f, 2), Vector3.one * 0.4f).AddComponent<ValidationFind>();
            Physics.SyncTransforms();
            find.Exposed = false;
            Assert.That(player.TryInteract(), Is.False);
            find.Exposed = true;
            var record = find.Item;
            Assert.That(player.Inventory.Capacity, Is.EqualTo(10));
            for (int i = 0; i < player.Inventory.Capacity; i++)
                player.Inventory.TryAdd(new InventoryItem("tin-" + i, "Tin", 5));
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(find.gameObject.activeSelf, Is.True);
            player.Inventory.TryRemove("tin-0", out _);
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(player.Inventory.Count, Is.EqualTo(player.Inventory.Capacity));
            Assert.That(player.Inventory.Items[player.Inventory.Count - 1], Is.SameAs(record));
            Assert.That(find.gameObject.activeSelf, Is.False);
            find.gameObject.SetActive(true);
            Assert.That(find.Item, Is.SameAs(record));
            Assert.That(find.TryInteract(player), Is.False);
        }

        [Test]
        public void FixtureRecordKeepsItsIdentityAfterDuplicateRejectionAndRetry()
        {
            var find = Box("Find", new Vector3(0, 1.6f, 2), Vector3.one * 0.4f).AddComponent<ValidationFind>();
            var second = Box("Second find", new Vector3(2, 1.6f, 2), Vector3.one * 0.4f).AddComponent<ValidationFind>();
            Assert.That(second.Item.DisplayName, Is.EqualTo(find.Item.DisplayName));
            Assert.That(second.Item.InstanceId, Is.Not.EqualTo(find.Item.InstanceId));
            var record = find.Item;
            player.Inventory.TryAdd(record);
            Assert.That(find.TryInteract(player), Is.False);
            Assert.That(find.gameObject.activeSelf, Is.True);
            player.Inventory.TryRemove(record.InstanceId, out _);
            Assert.That(find.TryInteract(player), Is.True);
            Assert.That(second.TryInteract(player), Is.True);
            Assert.That(player.Inventory.Items, Is.EqualTo(new[] { record, second.Item }));
        }

        [UnityTest]
        public IEnumerator MenuFreezesGameplayAndRestoresPreviousTimeScaleOnCloseAndDisable()
        {
            Time.timeScale = 0.5f;
            player.OpenMenu(PlayerMenu.Inventory);
            yield return null;
            var position = player.transform.position;
            float energy = player.Battery.Charge;
            player.Tick(new FpsInputFrame { Move = Vector2.up, Look = Vector2.one, DigHeld = true, JetpackHeld = true }, 0.1f);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(player.Battery.Charge, Is.EqualTo(energy));
            Assert.That(Cursor.visible, Is.True);
            player.CloseMenu();
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            yield return null;
            player.OpenMenu(PlayerMenu.Pause);
            player.enabled = false;
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
        }

        [UnityTest]
        public IEnumerator FocusReturnDoesNotResumeUntilExplicitClose()
        {
            player.SetApplicationFocus(false);
            yield return null;
            player.CloseMenu();
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            player.SetApplicationFocus(true);
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Pause));
            Assert.That(Time.timeScale, Is.Zero);
            player.CloseMenu();
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None));
        }

        [UnityTest]
        public IEnumerator StationOpeningDoesNotSellAndInventoryCannotRunStationCommands()
        {
            var station = Box("Sell", new Vector3(0, 1.6f, 2), Vector3.one).AddComponent<ValidationStation>();
            Physics.SyncTransforms();
            player.Inventory.TryAdd(new InventoryItem("coin-01", "Coin", 5));
            player.OpenMenu(PlayerMenu.Inventory);
            Assert.That(player.ExecuteStationCommand(0), Is.False);
            player.CloseMenu();
            yield return null;
            Assert.That(player.TryInteract(), Is.True);
            Assert.That(player.Inventory.Count, Is.EqualTo(1));
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.Station));
            Assert.That(player.ExecuteStationCommand(0), Is.True);
            Assert.That(player.Inventory.Count, Is.Zero);
            Assert.That(player.ExecuteStationCommand(0), Is.False);
            Assert.That(station, Is.EqualTo(player.Station));
        }

        [Test]
        public void DisabledStationCannotExecuteAStaleCommand()
        {
            var station = Box("Sell", new Vector3(0, 1.6f, 2), Vector3.one).AddComponent<ValidationStation>();
            Physics.SyncTransforms();
            player.Inventory.TryAdd(new InventoryItem("coin-01", "Coin", 5));
            player.TryInteract();
            station.enabled = false;
            Assert.That(player.ExecuteStationCommand(0), Is.False);
            Assert.That(player.Inventory.Count, Is.EqualTo(1));
        }

        private GameObject Box(string name, Vector3 position, Vector3 scale)
        {
            var box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            return box;
        }

        private GameObject Track(GameObject item) { objects.Add(item); return item; }
    }
}
