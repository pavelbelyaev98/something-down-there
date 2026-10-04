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
            SceneManager.sceneLoaded += TestInputPreferences.ConfigureCompactFinds;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.ConfigureCompactFinds;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects()[0];
            terrain = root.GetComponentInChildren<TerrainVolume>(); field = root.GetComponentInChildren<DiscoveryField>();
            player = root.GetComponentInChildren<FpsPlayer>(); player.enabled = false; player.SetApplicationFocus(true);
            // Aim helpers turn the player toward +z targets; start from that heading, not the authored spawn.
            player.transform.rotation = Quaternion.identity;
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null; // Let generation finish before restoring the compact fixture.
            TestInputPreferences.RestoreSmallFindFixture(field);
            yield return null;
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
            player.SetApplicationFocus(true);
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
                Assert.That(Physics.GetIgnoreCollision(find.GetComponent<MeshCollider>(), player.GetComponent<CharacterController>()), Is.EqualTo(find.Kind == DiscoveryKind.Common),
                    "The player walks through commons and stands on uniques.");
            }
        }

        [UnityTest]
        public IEnumerator NearlyUncoveredRockReleasesWhileShallowSurfaceContactRemains()
        {
            var find=field.Finds.First(f=>!TestInputPreferences.IsCoalFixture(f));
            bool ready=false;
            for(float height=.05f;height<1f;height+=.001f)
            {
                Place(find,height);
                if(find.Exposure>=.9f && find.Exposure<1f
                    && find.WorldBounds.min.y>-.03f && find.WorldBounds.min.y<-.006f)
                { ready=true;break; }
            }
            Assert.That(ready,Is.True,"Prepare a nearly exposed rock with a small remaining contact.");
            var physical=find.GetComponent<FindPhysics>();
            string identity=find.Item.InstanceId;
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
            Assert.That(physical.Released,Is.True,"A shallow final contact must no longer keep the rock anchored.");
            Assert.That(physical.Body.isKinematic,Is.False);
            Assert.That(find.Item.InstanceId,Is.EqualTo(identity));
            Assert.That(find.Collected,Is.False);
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
            var find = field.Finds.First(f => !TestInputPreferences.IsCoalFixture(f) == rock);
            player.Tuning.Gravity = 0;
            // Cadence on its own: the cut lands on the press here (TerrainIntegrationTests covers the scoop).
            player.Tuning.CutAtScoop = false;
            Place(find, .65f);
            var motor = player.GetComponent<CharacterController>(); motor.enabled = false;
            player.transform.SetPositionAndRotation(find.transform.position + new Vector3(0, -.60f, -1.2f), Quaternion.identity);
            player.ViewCamera.transform.localPosition = Vector3.up * 2.1f;
            motor.enabled = true;
            AimRock(find);
            // Start a real stroke before the find arrives, then expose it to
            // automatic collection on the immediately following held frame.
            find.gameObject.SetActive(false);
            LookRock(terrain.transform.TransformPoint(new Vector3(10.8f, terrain.Dimensions.y * terrain.CellSize, 12)));
            int strokes = player.SuccessfulStrokes;
            player.Tick(new FpsInputFrame { DigHeld = true }, .01f);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 1));
            Assert.That(find.Collected, Is.False);
            Assert.That(player.EffectiveDigInterval, Is.GreaterThan(.02f));
            int revision = terrain.Revision; float charge = player.Battery.Charge;
            find.gameObject.SetActive(true);
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

        private BuriedFind[] Variants() => field.Finds.Where(f => TestInputPreferences.IsCoalFixture(f)).Take(3).ToArray();

        [UnityTest]
        public IEnumerator LargeFindVariantsFallSettleAndRestoreTheirExactAppearanceAndPose()
        {
            var rocks = field.Finds.Where(f => f.Kind == DiscoveryKind.Common && !TestInputPreferences.IsCoalFixture(f)).GroupBy(f => f.SaveContentId).Select(g => g.First()).Take(3).ToArray();
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
            var rocks = field.Finds.Where(f => f.Kind == DiscoveryKind.Common && !TestInputPreferences.IsCoalFixture(f)).GroupBy(f => f.SaveContentId).Select(g => g.First()).Take(3).ToArray();
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
                float charge = player.Battery.Charge; int beforePickupStrokes = player.SuccessfulStrokes;
                player.Tick(new FpsInputFrame { DigHeld = true }, .01f);
                Assert.That(find.Collected, Is.True, "Already-held Dig must collect the directly aimed eligible rock.");
                Assert.That(player.Inventory.Items.Count(item => item.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
                // Held input may perform its already-due cut after removing the aimed item.
                int newStrokes = player.SuccessfulStrokes - beforePickupStrokes;
                Assert.That(newStrokes, Is.InRange(0, 1));
                Assert.That(player.Battery.Charge, Is.EqualTo(charge - newStrokes * player.EffectiveDigEnergy).Within(.0001f));
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

        // Concept 03 §4: undercut gravel pours; its finds drop and are never deleted, and the
        // ground around the section stays.
        [UnityTest]
        public IEnumerator PouredGravelDropsItsFindsWithoutLosingThem()
        {
            var grid = (ExcavationGrid)typeof(TerrainVolume).GetField("grid",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(terrain);
            var saved = grid.Capture();
            var ids = saved.Materials.ToArray();
            float cell = terrain.CellSize, top = terrain.Dimensions.y * cell;
            int stride = terrain.Dimensions.x + 1, plane = stride * (terrain.Dimensions.y + 1);
            // A gravel pocket 2.4-4 m under local (12, 12), above a sealed room.
            for (int z = Mathf.RoundToInt(10.6f / cell); z <= Mathf.RoundToInt(13.4f / cell); z++)
            for (int y = Mathf.RoundToInt((top - 4f) / cell); y <= Mathf.RoundToInt((top - 2.4f) / cell); y++)
            for (int x = Mathf.RoundToInt(10.6f / cell); x <= Mathf.RoundToInt(13.4f / cell); x++)
                ids[x + y * stride + z * plane] = (byte)TerrainMaterialId.Gravel;
            saved.Materials = TerrainMaterialSnapshot.CopyFrom(ids);
            grid.Restore(saved);
            grid.RemoveSphere(new Vector3(12, top - 5.4f, 12), 1.5f, out _);
            yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
            var find = field.Finds.First(f => !TestInputPreferences.IsCoalFixture(f));
            Place(find, -3.1f);
            var physical = find.GetComponent<FindPhysics>();
            string identity = find.Item.InstanceId;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(physical.Released, Is.False, "Buried in the gravel, the find is anchored.");
            float buried = physical.Body.position.y;
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(terrain.transform.TransformPoint(new Vector3(12, top - 5.4f, 12)), Vector3.up, out var ceiling, 3), Is.True);
            Assert.That(terrain.TryToolCut(ceiling, .35f, false), Is.True);
            Assert.That(terrain.ReleasedVolume(GroundRelease.GravelPour), Is.GreaterThan(1), "Cutting into the gravel ceiling pours the pocket.");
            yield return WaitForSimulation(2f);
            Assert.That(find != null && find.gameObject.activeInHierarchy, Is.True, "A pour never deletes a find.");
            Assert.That(find.Item.InstanceId, Is.EqualTo(identity));
            Assert.That(physical.Released, Is.True);
            Assert.That(physical.Body.position.y, Is.LessThan(buried - .5f), "The find tumbled down into the room.");
            Assert.That(find.Collected, Is.False);
            Assert.That(terrain.IsSolid(terrain.transform.TransformPoint(new Vector3(14.2f, top - 3.2f, 12))), Is.True, "Soil beside the pocket stays.");
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
