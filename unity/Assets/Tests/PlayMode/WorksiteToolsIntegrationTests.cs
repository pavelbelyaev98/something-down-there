#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class WorksiteToolsIntegrationTests
    {
        private Scene scene;
        private InputTestFixture devices;
        private FpsPlayer player;
        private WorksiteTools tools;
        private TerrainVolume terrain;

        [UnitySetUp]
        public IEnumerator Open()
        {
            devices = new InputTestFixture(); devices.Setup(); InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.sceneLoaded += TestInputPreferences.ConfigureWithoutFinds;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.ConfigureWithoutFinds;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            player = scene.GetRootGameObjects()[0].GetComponentInChildren<FpsPlayer>();
            tools = player.WorksiteTools; terrain = player.ExcavationTerrain;
            Assert.That(tools, Is.Not.Null); Assert.That(player.Persistence, Is.Null);
            player.Tuning.Gravity = 0; player.SetApplicationFocus(true); player.CloseMenu();
            yield return null; player.SetApplicationFocus(true);
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices?.TearDown(); Time.timeScale = 1;
        }

        // C4 (026): an empty kit places nothing; a charge sticks exactly where aimed; detonating takes about its ball of
        // ground through the dig pipeline, spends one charge and no battery, and a lamp beside it survives (it falls).
        [UnityTest]
        public IEnumerator ChargesStickAndBlastTheirBallWithoutTakingLampsOrBattery()
        {
            Assert.That(Physics.Raycast(new Vector3(-3, 2, -3), Vector3.down, out var ground, 4), Is.True);
            var pose = new ChargeSnapshot { Position = ground.point, Rotation = Quaternion.LookRotation(Vector3.forward, ground.normal), Stuck = true };
            Assert.That(player.Charges.Owned, Is.Zero, "New Game brings no charges.");
            Assert.That(tools.PlaceCharge(pose), Is.Null, "An empty kit places nothing and spends nothing.");
            player.FillAdminCharges();
            int owned = player.Charges.Owned;
            Assert.That(owned, Is.EqualTo(EquipmentProgression.C4(1).PackSize));
            var lamp = tools.PlaceLamp(tools.SolveLamp(ground.point + new Vector3(.7f, 1, 0), Vector3.down, 3, 0));
            var charge = tools.PlaceCharge(pose);
            Assert.That(charge, Is.Not.Null); Assert.That(charge.Stuck, Is.True);
            Assert.That(Vector3.Distance(charge.transform.position, ground.point), Is.LessThan(.001f), "It sticks where aimed.");
            Assert.That(tools.ArmedCharges, Is.EqualTo(1)); Assert.That(tools.AvailableCharges, Is.EqualTo(owned - 1));
            yield return null;
            float battery = player.Battery.Charge, radius = player.Charges.Current.BlastRadius;
            var centre = WorksiteTools.BlastCentre(ground.point, ground.normal, radius);
            Assert.That(tools.Detonate(), Is.True);
            yield return null;
            float below = radius * (1 - EquipmentProgression.BlastSink);
            float expected = 4f / 3 * Mathf.PI * radius * radius * radius - Mathf.PI * (radius - radius * EquipmentProgression.BlastSink)
                * (radius - radius * EquipmentProgression.BlastSink) * (2 * radius + radius * EquipmentProgression.BlastSink) / 3;
            Assert.That(terrain.LastRemovedVolume, Is.EqualTo(expected).Within(expected * .25f), "About the previewed ball below the ground.");
            Assert.That(terrain.IsSolid(centre), Is.False); Assert.That(terrain.IsSolid(centre - ground.normal * below * .8f), Is.False);
            Assert.That(player.Charges.Owned, Is.EqualTo(owned - 1)); Assert.That(tools.ArmedCharges, Is.Zero);
            Assert.That(player.Battery.Charge, Is.EqualTo(battery), "C4 costs no battery.");
            Assert.That(lamp != null && System.Linq.Enumerable.Contains(tools.Lamps, lamp), Is.True, "The lamp survives the blast.");
            Assert.That(lamp.Anchored, Is.False, "Its ground went, so it fell.");
            Assert.That(tools.Detonate(), Is.False, "Nothing armed, nothing to set off.");
        }

        [UnityTest]
        public IEnumerator PlacementIsBoundedLampsFallPauseReloadAndReturnToKit()
        {
            int owned = player.LampKit.Owned;
            Assert.That(owned, Is.EqualTo(EquipmentProgression.StarterLamps));
            Assert.That(Physics.Raycast(new Vector3(-7, 2, -7), Vector3.down, out var ground, 4), Is.True);
            var lamp = tools.PlaceLamp(tools.SolveLamp(new Vector3(-7, 2, -7), Vector3.down, 4, 0));
            Assert.That(lamp, Is.Not.Null); Assert.That(lamp.Anchored, Is.True);
            Vector3 placed = lamp.transform.position;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(lamp.Body.position, placed), Is.LessThan(.001f), "Interpolation must not restore the prefab origin.");
            Assert.That(Vector3.Distance(lamp.transform.position, placed), Is.LessThan(.001f));
            Assert.That(lamp.WorkLight.transform.position.y - placed.y, Is.InRange(.05f, .4f), "It shines from just above the small lamp.");
            Assert.That(tools.AvailableLamps, Is.EqualTo(owned - 1));
            Physics.SyncTransforms();
            var stacked = tools.PlaceLamp(tools.SolveLamp(new Vector3(-7, 2, -7), Vector3.down, 4, 0));
            Assert.That(stacked, Is.Not.Null, "Aiming at a lamp places a loose lamp on it instead of refusing.");
            Assert.That(stacked.Anchored, Is.False);
            Assert.That(stacked.transform.position.y, Is.GreaterThan(placed.y + WorksiteTools.LampHalfSize.y * 2 - .01f));
            Assert.That(tools.Retrieve(stacked), Is.True);
            Assert.That(terrain.TryDig(ground, .7f), Is.True);
            Assert.That(lamp.Anchored, Is.False, "Digging releases support, not lamp ownership.");
            float start = lamp.transform.position.y;
            yield return new WaitForSeconds(.18f); player.SetApplicationFocus(true);
            Assert.That(lamp.transform.position.y, Is.LessThan(start - .04f));
            var saved = tools.Capture();
            player.OpenMenu(PlayerMenu.Pause); yield return null;
            Vector3 paused = lamp.transform.position;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(lamp.transform.position, Is.EqualTo(paused));
            tools.Restore(saved); yield return null;
            Assert.That(tools.Lamps.Count, Is.EqualTo(1));
            Assert.That(tools.Lamps[0].Slot, Is.EqualTo(saved.Lamps[0].Slot));
            Assert.That(tools.Lamps[0].transform.position, Is.EqualTo(saved.Lamps[0].Position));
            Assert.That(tools.Retrieve(tools.Lamps[0]), Is.True);
            Assert.That(tools.AvailableLamps, Is.EqualTo(owned));
            for (int i = 0; i < owned; i++)
            {
                Assert.That(tools.PlaceLamp(tools.SolveLamp(new Vector3(-6 + i * .65f, 2, -7), Vector3.down, 4, 0)), Is.Not.Null);
                Physics.SyncTransforms();
            }
            Assert.That(tools.AvailableLamps, Is.Zero);
            Assert.That(tools.PlaceLamp(tools.SolveLamp(new Vector3(0, 2, -5), Vector3.down, 4, 0)), Is.Null, "Only an empty kit refuses.");
            Assert.That(tools.Capture().Lamps.Length, Is.EqualTo(owned));
        }

        [UnityTest]
        public IEnumerator LampsAttachToWallsCeilingsAndPropsOrFallWhenPlacedInAir()
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.transform.SetParent(tools.transform); prop.transform.position = new Vector3(-6, 2, -6);
            Physics.SyncTransforms();
            foreach (var normal in new[] { Vector3.up, Vector3.back, Vector3.down })
            {
                var lamp = tools.PlaceLamp(tools.SolveLamp(prop.transform.position + normal * 1.5f, -normal, 2, 0));
                Assert.That(lamp, Is.Not.Null, "Solid scenery accepts placement in every orientation.");
                Assert.That(lamp.Anchored, Is.True);
                Assert.That(Vector3.Dot(lamp.transform.up, normal), Is.GreaterThan(.99f), "Mounted spike-first along the surface.");
                Assert.That(lamp.WorkLight.type, Is.EqualTo(LightType.Point));
                Physics.SyncTransforms();
            }
            var dropped = tools.PlaceLamp(tools.SolveLamp(new Vector3(-3, 3, -6), Vector3.down, .5f, 0));
            Assert.That(dropped, Is.Not.Null); Assert.That(dropped.Anchored, Is.False);
            float height = dropped.Body.position.y;
            yield return new WaitForSeconds(.18f);
            Assert.That(dropped.Body.position.y, Is.LessThan(height - .04f));
            var saved = tools.Capture(); tools.Restore(saved);
            Assert.That(tools.Lamps.Count, Is.EqualTo(4));
            Assert.That(tools.Lamps[1].Anchored && tools.Lamps[2].Anchored, Is.True);
        }

        [UnityTest]
        public IEnumerator TooNarrowGapOwnFeetAndEmptyKitStillGiveAnswers()
        {
            // A slot narrower than the lamp: it backs up the aim and rests across the slot's lips.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(tools.transform); floor.transform.position = new Vector3(-6, 1, -6); floor.transform.localScale = new Vector3(2, 1, 2);
            foreach (float side in new[] { -1f, 1f })
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(tools.transform); wall.transform.localScale = new Vector3(.5f, .4f, 1);
                wall.transform.position = new Vector3(-6 + side * (.04f + .25f), 1.7f, -6);
            }
            Physics.SyncTransforms();
            var lamp = tools.PlaceLamp(tools.SolveLamp(new Vector3(-6, 3, -6), Vector3.down, 3, 0));
            Assert.That(lamp, Is.Not.Null);
            Assert.That(lamp.Anchored, Is.False, "Nothing fixed under the base: it settles physically.");
            Assert.That(lamp.transform.position.y, Is.GreaterThan(1.85f), "Never pushed into the slot or behind the aimed surface.");
            var shape = lamp.GetComponent<BoxCollider>();
            foreach (var other in Physics.OverlapBox(shape.bounds.center, WorksiteTools.LampHalfSize * .98f, lamp.transform.rotation))
                Assert.That(other, Is.SameAs(shape), "Placed clear of the slot walls.");
            Assert.That(tools.Retrieve(lamp), Is.True);

            // Under a low overhang the light is thrown off the floor but never from inside the rock.
            Assert.That(Physics.Raycast(new Vector3(-3, 2, -8), Vector3.down, out var open, 4), Is.True);
            float underside = open.point.y + .3f;
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.SetParent(tools.transform); roof.transform.localScale = new Vector3(1, .2f, 1);
            roof.transform.position = new Vector3(-3, underside + .1f, -8);
            Physics.SyncTransforms();
            var low = tools.PlaceLamp(tools.SolveLamp(new Vector3(-3, underside - .1f, -6.5f), new Vector3(0, -.15f, -1), 3, 0));
            Assert.That(low, Is.Not.Null);
            Assert.That(low.WorkLight.transform.position.y, Is.LessThan(underside - .05f), "The light stays below the overhang.");
            Assert.That(low.WorkLight.transform.position.y, Is.GreaterThan(low.transform.position.y + .07f), "It still lifts off the floor.");
            Assert.That(tools.Retrieve(low), Is.True);

            // Straight down at the player's own feet.
            var motor = player.GetComponent<CharacterController>(); motor.enabled = false;
            player.transform.position = new Vector3(-7, .1f, -9); motor.enabled = true;
            player.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            player.Tick(new FpsInputFrame { LampPressed = true, Look = new Vector2(0, -(85 - player.Pitch) / player.Tuning.LookSensitivity) }, .016f);
            Assert.That(tools.PlacementValid, Is.True, player.TargetPrompt);
            player.Tick(new FpsInputFrame { DigPressed = true, DigHeld = true }, .016f);
            Assert.That(tools.Lamps.Count, Is.EqualTo(1));
            yield return null; yield return new WaitForFixedUpdate();
            Assert.That(Physics.GetIgnoreCollision(tools.Lamps[0].GetComponent<Collider>(), motor), Is.True, "Lamps never block the player.");

            // An empty kit is the only refusal, and it says where more lamps come from.
            while (tools.AvailableLamps > 0) Assert.That(tools.PlaceLamp(tools.SolveLamp(new Vector3(-3, 3, -6), Vector3.down, .5f, 0)), Is.Not.Null);
            player.Tick(new FpsInputFrame { LampPressed = true }, .016f);
            Assert.That(tools.PlacementValid, Is.False);
            StringAssert.Contains("computer", player.TargetPrompt);
        }

        [UnityTest]
        public IEnumerator AllSymbolsPersistAndRemovedSupportErasesPaintWithoutBlockingDigging()
        {
            var rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            for (int i = 0; i < 3; i++)
            {
                Vector3 point = new Vector3(-7 + i, terrain.SurfaceHeight, -5);
                Assert.That(tools.PlaceMark(new MarkSnapshot { Kind = (WorldMarkKind)i, Position = point, Rotation = rotation }), Is.True);
            }
            var saved = tools.Capture();
            var coveringProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coveringProp.transform.SetParent(tools.transform);
            coveringProp.transform.position = new Vector3(-7, terrain.SurfaceHeight + .055f, -5);
            coveringProp.transform.localScale = new Vector3(.55f, .09f, .55f);
            Physics.SyncTransforms();
            tools.Restore(saved); yield return null;
            coveringProp.SetActive(false); Object.Destroy(coveringProp);
            Assert.That(tools.MarkCount, Is.EqualTo(3));
            Assert.That(tools.PlaceMark(saved.Marks[0]), Is.False, "No overlapping paint stacks.");
            Assert.That(Physics.Raycast(new Vector3(-7, 2, -5), Vector3.down, out var ground, 4), Is.True);
            Assert.That(ground.collider.GetComponentInParent<TerrainVolume>(), Is.SameAs(terrain));
            Assert.That(terrain.TryDig(ground, .6f), Is.True);
            Assert.That(tools.MarkCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PlacementConsumesPrimaryCancelsOnFocusLossAndNeverIlluminatesFromGhost()
        {
            var motor = player.GetComponent<CharacterController>(); motor.enabled = false;
            player.transform.position = new Vector3(-7, .1f, -9); motor.enabled = true;
            player.transform.rotation = Quaternion.identity;
            float targetPitch = Mathf.Atan2(player.ViewCamera.transform.position.y - terrain.SurfaceHeight, 2) * Mathf.Rad2Deg;
            Physics.SyncTransforms();
            player.Tick(new FpsInputFrame { LampPressed = true, Look = new Vector2(0, -(targetPitch - player.Pitch) / player.Tuning.LookSensitivity) }, .016f);
            Assert.That(tools.IsPlacing, Is.True);
            Assert.That(tools.PlacementValid, Is.True, player.TargetPrompt);
            foreach (var light in tools.GetComponentsInChildren<Light>()) Assert.That(light.enabled, Is.False);
            float removed = terrain.RemovedVolume;
            player.Tick(new FpsInputFrame { DigPressed = true, DigHeld = true }, .016f);
            Assert.That(tools.Lamps.Count, Is.EqualTo(1)); Assert.That(tools.IsPlacing, Is.False);
            Assert.That(terrain.RemovedVolume, Is.EqualTo(removed));
            player.Tick(new FpsInputFrame { MarkPressed = true }, .016f);
            Assert.That(tools.IsPlacing, Is.True);
            player.SetApplicationFocus(false); yield return null;
            Assert.That(tools.IsPlacing, Is.False);
        }
    }
}
#endif
