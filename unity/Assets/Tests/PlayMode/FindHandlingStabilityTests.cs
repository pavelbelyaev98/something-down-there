#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed partial class FindPhysicsIntegrationTests
    {
        [UnityTest]
        public IEnumerator DroppedRocksSettleAfterRepeatedExtremePitchChanges()
        {
            // Stable ids and a fixed band keep every case's drop spot independent of
            // the layout order, on the flat yard the sibling settle test uses.
            var rocks = field.Finds.Where(f => f.Kind == DiscoveryKind.Common && f.Size == FindSize.Large).GroupBy(f => f.SaveContentId)
                .Select(g => g.First()).OrderBy(f => f.SaveContentId, System.StringComparer.Ordinal).ToArray();
            for (int i = 0; i < rocks.Length; i++)
            {
                var find = rocks[i];
                // Same flat yard grid the seeded settle test proves, three metres
                // apart so no case rests against an earlier drop.
                PrepareNaturalHold(find, 3 * (i % 4) - 6, find.transform.rotation, 4 * (i / 4) - 6);
                player.enabled = true; player.SetApplicationFocus(true);
                yield return WaitForSimulation(.8f);
                for (int turn = 0; turn < 6; turn++)
                {
                    SetHoldPitch(turn % 2 == 0 ? -80 : 80);
                    yield return WaitForSimulation(.35f);
                    Assert.That(player.HeldFind, Is.SameAs(find), "Looking up/down must retain " + find.SaveContentId);
                    Assert.That(terrain.SignedDensity(find.transform.position), Is.LessThan(terrain.CellSize * .6f));
                }
                Assert.That(player.TryGrabOrDrop(), Is.True);
                Assert.That(player.HeldFind, Is.Null);
                // A hard drop on voxel ground can keep creeping for a while before
                // PhysX sleeps it; the drift window measures a settled body.
                yield return WaitForSimulation(10);
                var body = find.GetComponent<Rigidbody>();
                Vector3 rest = body.position; Quaternion orientation = body.rotation;
                float drift = 0, wobble = 0;
                for (int sample = 0; sample < 20; sample++)
                {
                    yield return WaitForSimulation(.1f);
                    drift = Mathf.Max(drift, Vector3.Distance(rest, body.position));
                    wobble = Mathf.Max(wobble, Quaternion.Angle(orientation, body.rotation));
                }
                Assert.That(drift, Is.LessThan(.003f), $"{find.SaveContentId}: resting drift={drift}, wobble={wobble}, speed={body.linearVelocity.magnitude}, spin={body.angularVelocity.magnitude}");
                Assert.That(wobble, Is.LessThan(.25f), find.SaveContentId + " must stop visibly jiggling.");
                Assert.That(body.IsSleeping(), Is.True, find.SaveContentId + " should settle into physical sleep.");
                Assert.That(body.isKinematic, Is.False, "Resting keeps real physics, not an anchored workaround.");
                Assert.That(find.Collected, Is.False); Assert.That(player.Inventory.Count, Is.Zero);
                player.enabled = false;
            }
        }

        [UnityTest]
        public IEnumerator SeededRockDropsRestWithoutDriftAndWakeWhenSupportIsDug()
        {
            var rocks = field.Finds.Where(f => f.Kind == DiscoveryKind.Common && f.Size == FindSize.Large).Take(12).ToArray();
            foreach (var find in rocks)
            {
                int i = System.Array.IndexOf(rocks, find);
                Quaternion original = find.transform.rotation;
                var point = terrain.transform.TransformPoint(new Vector3(6 + i % 4 * 3, terrain.Dimensions.y * terrain.CellSize + 1f, 6 + i / 4 * 4));
                find.transform.SetPositionAndRotation(point, original);
                find.GetComponent<FindPhysics>().Restore(false); Physics.SyncTransforms(); find.RefreshExposure();
                AimRock(find);
                Assert.That(player.TryGrabOrDrop(), Is.True);
                Assert.That(player.TryGrabOrDrop(), Is.True);
            }
            yield return WaitForSimulation(6);
            var positions = rocks.Select(f => f.GetComponent<Rigidbody>().position).ToArray();
            var rotations = rocks.Select(f => f.GetComponent<Rigidbody>().rotation).ToArray();
            var drift = new float[rocks.Length]; var wobble = new float[rocks.Length];
            for (int sample = 0; sample < 20; sample++)
            {
                yield return WaitForSimulation(.1f);
                for (int i = 0; i < rocks.Length; i++)
                {
                    var body = rocks[i].GetComponent<Rigidbody>();
                    drift[i] = Mathf.Max(drift[i], Vector3.Distance(positions[i], body.position));
                    wobble[i] = Mathf.Max(wobble[i], Quaternion.Angle(rotations[i], body.rotation));
                }
            }
            var metrics = rocks.Select((f, i) => $"{f.Item.InstanceId} {f.SaveContentId}: drift={drift[i]} wobble={wobble[i]} sleeping={f.GetComponent<Rigidbody>().IsSleeping()} speed={f.GetComponent<Rigidbody>().linearVelocity.magnitude} spin={f.GetComponent<Rigidbody>().angularVelocity.magnitude} position={f.transform.position}").ToArray();
            System.IO.Directory.CreateDirectory("Logs/Task116");
            System.IO.File.WriteAllLines("Logs/Task116/seeded-rest.txt", metrics);
            for (int i = 0; i < rocks.Length; i++)
            {
                Assert.That(drift[i], Is.LessThan(.003f), metrics[i]);
                Assert.That(wobble[i], Is.LessThan(.25f), rocks[i].Item.InstanceId);
                Assert.That(rocks[i].GetComponent<Rigidbody>().IsSleeping(), Is.True, rocks[i].Item.InstanceId);
            }
            var last = rocks[rocks.Length - 1]; var restingBody = last.GetComponent<Rigidbody>(); Vector3 rest = restingBody.position;
            var soil = Physics.RaycastAll(rest + Vector3.up * 2, Vector3.down, 4)
                .Where(h => h.collider.GetComponentInParent<TerrainVolume>() == terrain).OrderBy(h => h.distance).First();
            Assert.That(terrain.TryDig(soil, 1.1f), Is.True);
            yield return WaitForSimulation(1.2f);
            Assert.That(restingBody.position.y, Is.LessThan(rest.y - .35f), "A sleeping rock must fall again when its support disappears.");
            Assert.That(last.Collected, Is.False); Assert.That(player.Inventory.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MaximumJetpackAscentKeepsRockInHandThroughLookChanges()
        {
            var find = field.Finds.First(f => f.Size == FindSize.Large);
            // This measures hand stability over a long climb, not the play area's flight ceiling.
            player.transform.root.Find("Environment/Play area bounds").gameObject.SetActive(false);
            PrepareNaturalHold(find, 0);
            player.enabled = true; player.SetApplicationFocus(true);
            yield return WaitForSimulation(.8f);
            float start = player.transform.position.y;
            for (int sample = 0; sample < 40; sample++)
            {
                if (sample % 10 == 0)
                {
                    // One complete device state: separately queued button helpers
                    // would overwrite earlier unprocessed keys in this same frame.
                    InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState(
                        Key.Space, Key.LeftShift, sample % 20 == 0 ? Key.D : Key.A));
                }
                if (sample % 5 == 0) SetHoldPitch(sample % 10 == 0 ? -70 : 70);
                yield return WaitForSimulation(.1f);
                Assert.That(player.HeldFind, Is.SameAs(find), "Jetpack ascent must not outrun the hand.");
                Assert.That(Vector3.Distance(find.transform.position, player.ViewCamera.transform.position), Is.LessThan(2f), "The held find stays visible within arm's reach.");
            }
            Assert.That(player.IsJetpackActive, Is.True);
            Assert.That(player.transform.position.y - start, Is.GreaterThan(20), "Exercise sustained production jetpack speed.");
            InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            Assert.That(player.TryGrabOrDrop(), Is.True);
            var body = find.GetComponent<Rigidbody>(); Vector3 released = body.position;
            yield return WaitForSimulation(.2f);
            Assert.That(body.position.y, Is.LessThan(released.y - .1f), "An explicitly dropped airborne find must fall.");
            Assert.That(find.Collected, Is.False); Assert.That(player.Inventory.Count, Is.Zero);
        }

        private void PrepareNaturalHold(BuriedFind find, float offset, Quaternion? orientation = null, float zOffset = 0)
        {
            Place(find, .65f, offset, zOffset);
            if (orientation.HasValue)
            {
                find.transform.rotation = orientation.Value; find.GetComponent<FindPhysics>().Restore(false);
                Physics.SyncTransforms(); find.RefreshExposure();
            }
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = terrain.transform.TransformPoint(new Vector3(12 + offset, terrain.Dimensions.y * terrain.CellSize + .02f, 10.6f + zOffset));
            player.ViewCamera.transform.localPosition = Vector3.up * 1.6f;
            controller.enabled = true;
            LookRock(find.transform.position); Physics.SyncTransforms();
            Assert.That(player.TryGrabOrDrop(), Is.True, find.SaveContentId);
        }

        private void SetHoldPitch(float desired)
        {
            player.Tick(new FpsInputFrame { Look = new Vector2(0, (player.Pitch - desired) / player.Tuning.LookSensitivity) }, .0001f);
        }
    }

}
#endif
