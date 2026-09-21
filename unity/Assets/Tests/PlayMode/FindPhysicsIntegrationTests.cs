#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed partial class FindPhysicsIntegrationTests
    {
        private Scene scene;
        private TerrainVolume terrain;
        private DiscoveryField field;
        private FpsPlayer player;
        private InputTestFixture devices;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            devices = new InputTestFixture(); devices.Setup();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.sceneLoaded += TestInputPreferences.Configure;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects()[0];
            terrain = root.GetComponentInChildren<TerrainVolume>(); field = root.GetComponentInChildren<DiscoveryField>();
            player = root.GetComponentInChildren<FpsPlayer>(); player.enabled = false; player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null; // Let generation finish before constructing a legacy save fixture.
            TestInputPreferences.RestoreSmallFindFixture(field);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
        }

        [UnityTest]
        public IEnumerator SixtyPercentAllowsPickupButRetainedSoilStillAnchorsSmallFinds()
        {
            foreach (var find in Variants())
            {
                var physical = find.GetComponent<FindPhysics>();
                bool partial = false;
                for (float y = -.12f; y <= .12f; y += .002f)
                {
                    Place(find, y);
                    if (find.Exposure >= .4f && find.Exposure < .6f) Assert.That(find.Collectible, Is.False);
                    if (find.Exposure < .6f || find.Exposure > .85f) continue;
                    partial = true; break;
                }
                Assert.That(partial, Is.True, find.SaveContentId);
                Assert.That(find.Collectible, Is.True);
                Vector3 anchored = physical.Body.position;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(physical.Released, Is.False, "60% collection exposure must not release retained soil attachment.");
                Assert.That(physical.Body.position, Is.EqualTo(anchored));
                Assert.That(Physics.GetIgnoreCollision(find.GetComponent<MeshCollider>(), player.GetComponent<CharacterController>()), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator DetachedVariantsFallAndSettleWithoutLosingTheirIdentity()
        {
            int index = 0;
            foreach (var find in Variants())
            {
                Place(find, .75f, index++ * .5f);
                var physical = find.GetComponent<FindPhysics>();
                string id = find.Item.InstanceId; Vector3 initial = physical.Body.position;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(physical.Released, Is.True, find.SaveContentId);
                yield return WaitForSimulation(1.8f);
                Assert.That(physical.Body.position.y, Is.LessThan(initial.y - .4f));
                Assert.That(find.WorldBounds.min.y, Is.InRange(terrain.SurfaceHeight - .04f, terrain.SurfaceHeight + .06f));
                Assert.That(physical.Body.linearVelocity.magnitude, Is.LessThan(.1f));
                Assert.That(find.Item.InstanceId, Is.EqualTo(id)); Assert.That(find.Collected, Is.False);
                Assert.That(find.Collectible, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator SettledSmallFindWakesWhenGroundIsDugAndReanchorsWhenReset()
        {
            var find = field.Finds[0]; Place(find, .65f);
            yield return WaitForSimulation(1.5f);
            var physical = find.GetComponent<FindPhysics>(); Vector3 rest = physical.Body.position;
            var hits = Physics.RaycastAll(rest + Vector3.up * 1.5f, Vector3.down, 4);
            var soil = hits.Where(h => h.collider.GetComponentInParent<TerrainVolume>() == terrain).OrderBy(h => h.distance).First();
            Assert.That(terrain.TryDig(soil, .9f), Is.True);
            yield return WaitForSimulation(1.5f);
            Assert.That(physical.Body.position.y, Is.LessThan(rest.y - .35f));
            Assert.That(find.Exposure, Is.GreaterThanOrEqualTo(.6f));
            terrain.ResetExcavation();
            yield return new WaitForFixedUpdate();
            Assert.That(find.Exposure, Is.Zero); Assert.That(find.Collectible, Is.False);
            Assert.That(physical.Released, Is.False); Assert.That(physical.Body.isKinematic, Is.True);
        }

        [UnityTest]
        public IEnumerator PauseAndRestorePreserveAReleasedSmallFindAndInvalidPoseRecoversSameIdentity()
        {
            var find = field.Finds[0]; Place(find, 1.2f);
            var physical = find.GetComponent<FindPhysics>();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            player.OpenMenu(PlayerMenu.Pause);
            var snapshot = find.Capture(); Vector3 paused = physical.Body.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(physical.Body.position, Is.EqualTo(paused)); Assert.That(snapshot.PhysicsReleased, Is.True);
            find.Restore(snapshot);
            Assert.That(find.Capture().Position, Is.EqualTo(snapshot.Position)); Assert.That(physical.Released, Is.True);
            player.CloseMenu();
            yield return WaitForSimulation(.15f);
            Assert.That(physical.Body.position.y, Is.LessThan(paused.y));
            Vector3 valid = physical.Body.position;
            physical.Body.position = terrain.transform.TransformPoint(new Vector3(-10, -10, -10));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(physical.Body.position, valid), Is.LessThan(.3f));
            Assert.That(find.Item.InstanceId, Is.EqualTo(snapshot.Item.Id)); Assert.That(find.Collected, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HeldAimCollectsEligibleFindDuringShovelCooldown(bool rock)
        {
            var find = field.Finds.First(f => (f.Size == FindSize.Large) == rock);
            player.Tuning.Gravity = 0;
            Place(find, .65f);
            var motor = player.GetComponent<CharacterController>(); motor.enabled = false;
            player.transform.SetPositionAndRotation(find.transform.position + new Vector3(0, -.60f, -1.2f), Quaternion.identity);
            player.ViewCamera.transform.localPosition = Vector3.up * 2.1f;
            motor.enabled = true;
            AimRock(find);
            // Start a real stroke beside the find while Dig is held, then turn
            // onto the already-uncovered item on the immediately following frame.
            LookRock(terrain.transform.TransformPoint(new Vector3(10.8f, terrain.Dimensions.y * terrain.CellSize, 12)));
            int strokes = player.SuccessfulStrokes;
            player.Tick(new FpsInputFrame { DigHeld = true }, .01f);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 1));
            Assert.That(find.Collected, Is.False);
            Assert.That(player.EffectiveDigInterval, Is.GreaterThan(.02f));
            int revision = terrain.Revision; float charge = player.Battery.Charge;
            AimRock(find);
            Assert.That(player.TryGetTarget(player.Tuning.InteractReach, out var hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(find.GetComponent<Collider>()), "The next held frame must be directly aimed at the find.");
            player.Tick(new FpsInputFrame { DigHeld = true }, .01f);
            Assert.That(find.Collected, Is.True, "Eligible held pickup must not wait for recognition or the active shovel cooldown.");
            Assert.That(terrain.Revision, Is.EqualTo(revision));
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 1));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
            player.Tick(new FpsInputFrame { DigHeld = true }, .01f);
            Assert.That(terrain.Revision, Is.EqualTo(revision), "Pickup still blocks an accidental follow-through dig.");
        }

        [UnityTest]
        public IEnumerator EveryAppearanceCanBeLiftedFromPartialExposureDroppedThrownAndRestored()
        {
            player.Tuning.Gravity = 0;
            int index = 0;
            var appearances = field.Finds.GroupBy(f => f.SaveContentId).Select(g => g.First()).ToArray();
            foreach (var find in appearances)
            {
                bool partial = false;
                for (float y = -.25f; y < .3f; y += .002f)
                {
                    Place(find, y, Mathf.Lerp(-8, 8, index / (float)Mathf.Max(1, appearances.Length - 1)));
                    if (find.Exposure < .61f || find.Exposure > .8f) continue;
                    partial = true; break;
                }
                Assert.That(partial, Is.True, find.SaveContentId);
                AimRock(find);
                var physical = find.GetComponent<FindPhysics>();
                float charge = player.Battery.Charge;
                Assert.That(player.TryGrabOrDrop(), Is.True, find.SaveContentId);
                Assert.That(player.HeldFind, Is.SameAs(find)); Assert.That(physical.Held, Is.True);
                Assert.That(find.TryCollect(player), Is.False); Assert.That(player.TryDig(), Is.False);
                string id = find.Item.InstanceId;
                player.enabled = true; player.SetApplicationFocus(true);
                yield return WaitForSimulation(.8f);
                Assert.That(player.HeldFind, Is.SameAs(find), "Partial exposure must lift without losing the hold: " + find.SaveContentId);
                Assert.That(find.Exposure, Is.GreaterThanOrEqualTo(.95f), "Lift pulls the object out of remaining soil.");
                Assert.That(Vector3.Distance(physical.Body.position, player.ViewCamera.transform.position), Is.InRange(.5f, 1.7f));
                var saved = find.Capture();
                Assert.That(saved.Collected, Is.False); Assert.That(saved.PhysicsReleased, Is.True);
                player.OpenMenu(PlayerMenu.Pause); Vector3 paused = physical.Body.position;
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(physical.Body.position, Is.EqualTo(paused)); Assert.That(player.TryThrow(), Is.False);
                player.CloseMenu(); yield return null;
                Assert.That(player.TryGrabOrDrop(), Is.True, "Second grab drops without collecting.");
                Assert.That(player.HeldFind, Is.Null); Assert.That(physical.Body.useGravity, Is.True);
                player.enabled = false;
                AimRock(find);
                Assert.That(player.TryGrabOrDrop(), Is.True);
                Assert.That(player.TryThrow(), Is.True);
                Assert.That(physical.Body.linearVelocity.magnitude, Is.EqualTo(physical.ThrowSpeed).Within(.01f));
                Assert.That(player.Inventory.Count, Is.Zero); Assert.That(player.Battery.Charge, Is.EqualTo(charge));
                find.Restore(saved);
                Assert.That(physical.Held, Is.False); Assert.That(physical.Released, Is.True);
                Assert.That(find.Capture().Position, Is.EqualTo(saved.Position));
                Assert.That(find.Item.InstanceId, Is.EqualTo(id)); Assert.That(find.Collected, Is.False);
                index++;
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator ActualGrabAndThrowInputsRespectToggleMenusFullBagAndHeldSuppression()
        {
            var find = field.Finds.First(f => f.Size == FindSize.Small); Place(find, .6f);
            yield return WaitForSimulation(1);
            player.Tuning.Gravity = 0; AimRock(find);
            while (!player.Inventory.IsFull) player.Inventory.TryAdd(new InventoryItem("full-" + player.Inventory.Count, "Carried", 1));
            player.InputSettings.SetToggleDig(true);
            player.enabled = true; player.SetApplicationFocus(true); yield return null; yield return null;
            devices.Press(Mouse.current.rightButton, queueEventOnly: true); yield return null; yield return null;
            Assert.That(player.HeldFind, Is.SameAs(find), "Full inventory does not prevent optional physical lifting.");
            devices.Release(Mouse.current.rightButton, queueEventOnly: true);
            yield return WaitForSimulation(.5f);
            player.SetApplicationFocus(false);
            devices.Press(Mouse.current.leftButton, queueEventOnly: true); yield return null;
            Assert.That(player.HeldFind, Is.SameAs(find));
            player.SetApplicationFocus(true); player.CloseMenu(); yield return null; yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.None), "Focus resume closes the test pause menu.");
            Assert.That(Time.timeScale, Is.GreaterThan(0), "Focus resume restores simulation time.");
            Assert.That(player.HeldFind, Is.SameAs(find), "A held menu click cannot become a throw after resume.");
            devices.Release(Mouse.current.leftButton, queueEventOnly: true); yield return null;
            int revision = terrain.Revision; float charge = player.Battery.Charge;
            devices.Press(Mouse.current.leftButton, queueEventOnly: true); yield return null; yield return null;
            Assert.That(player.HeldFind, Is.Null); Assert.That(find.Collected, Is.False);
            yield return WaitForSimulation(.8f);
            Assert.That(terrain.Revision, Is.EqualTo(revision), "Throw consumes toggle intent instead of starting digging.");
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            Assert.That(player.Inventory.Items.Any(i => i.InstanceId == find.Item.InstanceId), Is.False);
        }

        [UnityTest]
        public IEnumerator HeldAndThrownRockCollideWithWallsAndRescueLeavesItInTheWorld()
        {
            var find = field.Finds.First(f => f.Size == FindSize.Large); Place(find, .6f);
            yield return WaitForSimulation(1);
            player.Tuning.Gravity = 0; AimRock(find);
            Assert.That(player.TryGrabOrDrop(), Is.True);
            player.enabled = true; player.SetApplicationFocus(true);
            yield return WaitForSimulation(.7f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var eye = player.ViewCamera.transform;
                Vector3 start = find.transform.position;
                Vector3 direction = eye.forward;
                wall.transform.SetPositionAndRotation(start + direction * .8f, Quaternion.LookRotation(direction));
                wall.transform.localScale = new Vector3(4, 4, .15f); Physics.SyncTransforms();
                Assert.That(player.TryThrow(), Is.True);
                yield return WaitForSimulation(.25f);
                Assert.That(Vector3.Dot(find.transform.position - start, direction), Is.LessThan(.9f), "Throw cannot tunnel through a thin wall.");
                player.enabled = false; AimRock(find);
                Assert.That(player.TryGrabOrDrop(), Is.True);
                int count = player.Inventory.Count; string id = find.Item.InstanceId;
                player.AdminReturnToSurface();
                Assert.That(player.HeldFind, Is.Null); Assert.That(find.Collected, Is.False);
                Assert.That(find.Item.InstanceId, Is.EqualTo(id)); Assert.That(player.Inventory.Count, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(wall); }
        }

        private IEnumerator WaitForSimulation(float seconds, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds * 3 + 2;
            while (Time.time < until && Time.realtimeSinceStartup < deadline)
            {
                // These waits explicitly require running simulation. Native Editor
                // window focus changes are external to the fixture; dedicated pause
                // assertions use real-time waits and are never resumed here.
                if (player.Menu == PlayerMenu.Pause) { player.SetApplicationFocus(true); player.CloseMenu(); }
                yield return null;
            }
            Assert.That(Time.time, Is.GreaterThanOrEqualTo(until), $"Simulation stopped at wait line {line}: menu={player.Menu}, enabled={player.enabled}, scale={Time.timeScale}");
        }

        private BuriedFind[] Variants() => field.Finds.Where(f => f.Size == FindSize.Small).Take(3).ToArray();

        [UnityTest]
        public IEnumerator LargeFindVariantsFallSettleAndRestoreTheirExactAppearanceAndPose()
        {
            var rocks = field.Finds.Where(f => f.Size == FindSize.Large).GroupBy(f => f.SaveContentId).Select(g => g.First()).Take(3).ToArray();
            Assert.That(rocks.Length, Is.EqualTo(3));
            for (int i = 0; i < rocks.Length; i++)
            {
                var find = rocks[i];
                Place(find, 1f, i * 1.5f);
                find.transform.rotation = Quaternion.Euler(25 + i * 33, i * 67, 16 + i * 51);
                var physical = find.GetComponent<FindPhysics>(); physical.Restore(false);
                Physics.SyncTransforms(); find.RefreshExposure();
                float start = physical.Body.position.y;
                yield return WaitForSimulation(2.5f);
                Assert.That(physical.Released, Is.True, find.SaveContentId);
                Assert.That(physical.Body.position.y, Is.LessThan(start - .5f));
                // A rotated renderer's world AABB includes empty box corners below the actual find.
                float lowestVertex = find.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v => find.transform.TransformPoint(v).y);
                Assert.That(lowestVertex, Is.InRange(terrain.SurfaceHeight - .065f, terrain.SurfaceHeight + .08f), find.SaveContentId);
                Assert.That(physical.Body.linearVelocity.magnitude, Is.LessThan(.12f));
                Assert.That(find.Collected, Is.False);
                Assert.That(find.Collectible, Is.True);
            }
            // Restore the whole population through production catalog resolution, not the original objects.
            Time.timeScale = 0;
            var snapshots = field.Capture();
            field.Restore(snapshots, field.Seed);
            var restoredIds = new System.Collections.Generic.HashSet<string>(rocks.Select(f => f.SaveContentId), System.StringComparer.Ordinal);
            foreach (var expected in snapshots.Where(s => restoredIds.Contains(s.ContentId)))
            {
                var restored = field.Finds.Single(f => f.Item.InstanceId == expected.Item.Id);
                var actual = restored.Capture();
                Assert.That(actual.ContentId, Is.EqualTo(expected.ContentId));
                Assert.That(actual.Position, Is.EqualTo(expected.Position));
                Assert.That(Quaternion.Angle(actual.Rotation, expected.Rotation), Is.LessThan(.01f));
                Assert.That(actual.PhysicsReleased, Is.EqualTo(expected.PhysicsReleased));
                Assert.That(actual.Item.Value, Is.EqualTo(expected.Item.Value));
            }
        }

        [UnityTest]
        public IEnumerator LargeFindsRequireSixtyPercentThenHeldAimAndLeaveFullBagOrOffAimFindsInPlace()
        {
            var rocks = field.Finds.Where(f => f.Size == FindSize.Large).GroupBy(f => f.SaveContentId).Select(g => g.First()).Take(3).ToArray();
            player.Tuning.Gravity = 0;
            int index = 0;
            foreach (var find in rocks)
            {
                bool partial = false;
                for (float y = -.25f; y < .3f; y += .004f)
                {
                    Place(find, y, index * 1.5f);
                    if (find.Exposure < .4f || find.Exposure >= .6f) continue;
                    partial = true; break;
                }
                Assert.That(partial, Is.True, find.SaveContentId);
                AimRock(find);
                Assert.That(find.Collectible, Is.False);
                int revision = terrain.Revision;
                Assert.That(player.TryDig(), Is.True, "Physical-test setup excavates covering soil without issuing a collection action.");
                Assert.That(terrain.Revision, Is.GreaterThan(revision));
                yield return new WaitForFixedUpdate();
                Assert.That(find.Collected, Is.False);
                Place(find, .9f, index++ * 1.5f);
                yield return WaitForSimulation(2);
                player.ViewCamera.transform.position = find.transform.position + Vector3.up * 2;
                LookRock(player.ViewCamera.transform.position + Vector3.up);
                player.Tick(new FpsInputFrame { DigHeld = true }, .1f);
                Assert.That(find.Collected, Is.False, "Holding away from a dropped rock cannot collect it.");
                while (!player.Inventory.IsFull)
                    player.Inventory.TryAdd(new InventoryItem("fill-" + player.Inventory.Count, "Carried", 1));
                AimRock(find);
                player.Tick(new FpsInputFrame { DigHeld = true }, .1f);
                Assert.That(find.Collected, Is.False);
                Assert.That(player.Inventory.TryRemove(player.Inventory.Items.First().InstanceId, out _), Is.True);
                float charge = player.Battery.Charge;
                player.Tick(new FpsInputFrame { DigHeld = true }, .01f);
                Assert.That(find.Collected, Is.True, "Already-held Dig must collect the directly aimed eligible rock.");
                Assert.That(player.Inventory.Items.Count(item => item.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
                Assert.That(player.Battery.Charge, Is.EqualTo(charge));
                foreach (var item in player.Inventory.Items.ToArray()) player.Inventory.TryRemove(item.InstanceId, out _);
                player.Tick(new FpsInputFrame(), .5f);
            }
        }

        private void AimRock(BuriedFind find)
        {
            player.ViewCamera.transform.position = find.transform.position + new Vector3(0, 1.5f, -1.2f);
            LookRock(find.transform.position);
            Physics.SyncTransforms();
        }

        private void LookRock(Vector3 point)
        {
            Vector3 direction = point - player.ViewCamera.transform.position;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            player.Tick(new FpsInputFrame { Look = new Vector2(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), player.Pitch - pitch)
                / player.Tuning.LookSensitivity }, .001f);
        }

        private void Place(BuriedFind find, float aboveSurface, float offset = 0, float zOffset = 0)
        {
            var local = new Vector3(12 + offset, terrain.Dimensions.y * terrain.CellSize + aboveSurface, 12 + zOffset);
            find.transform.SetPositionAndRotation(terrain.transform.TransformPoint(local), Quaternion.Euler(0, 0, 90));
            find.GetComponent<FindPhysics>().Restore(false);
            Physics.SyncTransforms(); find.RefreshExposure();
        }
    }
}
#endif
