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
            SceneManager.sceneLoaded += TestInputPreferences.Configure;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            player = scene.GetRootGameObjects()[0].GetComponentInChildren<FpsPlayer>();
            tools = player.WorksiteTools; terrain = player.ExcavationTerrain;
            Assert.That(tools, Is.Not.Null); Assert.That(player.Persistence, Is.Null);
            player.Tuning.Gravity = 0; player.SetApplicationFocus(true); player.CloseMenu();
            foreach (var find in player.Discoveries.Finds) find.gameObject.SetActive(false);
            yield return null; player.SetApplicationFocus(true);
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices?.TearDown(); Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator PlacementIsBoundedLampsFallPauseReloadAndReturnToKit()
        {
            Assert.That(Physics.Raycast(new Vector3(-7, 2, -7), Vector3.down, out var ground, 4), Is.True);
            var lamp = tools.PlaceLamp(ground.point, ground.normal, Quaternion.identity);
            Assert.That(lamp, Is.Not.Null); Assert.That(lamp.Anchored, Is.True);
            Vector3 placed = lamp.transform.position;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(lamp.Body.position, placed), Is.LessThan(.001f), "Interpolation must not restore the prefab origin.");
            Assert.That(Vector3.Distance(lamp.transform.position, placed), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(lamp.WorkLight.transform.position, placed), Is.LessThan(.6f));
            Assert.That(tools.AvailableLamps, Is.EqualTo(WorksiteTools.LampCapacity - 1));
            Physics.SyncTransforms();
            Assert.That(tools.PlaceLamp(ground.point, ground.normal, Quaternion.identity), Is.Null, "Cannot overlap another lamp.");
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
            Assert.That(tools.AvailableLamps, Is.EqualTo(WorksiteTools.LampCapacity));
            for (int i = 0; i < WorksiteTools.LampCapacity; i++)
            {
                Assert.That(Physics.Raycast(new Vector3(-6 + i * .65f, 2, -7), Vector3.down, out var floor, 4), Is.True);
                Assert.That(tools.PlaceLamp(floor.point, floor.normal, Quaternion.identity), Is.Not.Null);
                Physics.SyncTransforms();
            }
            Assert.That(tools.AvailableLamps, Is.Zero);
            Assert.That(tools.PlaceLamp(new Vector3(0, terrain.SurfaceHeight, -5), Vector3.up, Quaternion.identity), Is.Null);
            Assert.That(tools.Capture().Lamps.Length, Is.EqualTo(WorksiteTools.LampCapacity));
        }

        [UnityTest]
        public IEnumerator LampsAttachToWallsCeilingsAndPropsOrFallWhenPlacedInAir()
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.transform.SetParent(tools.transform); prop.transform.position = new Vector3(-6, 2, -6);
            Physics.SyncTransforms();
            foreach (var normal in new[] { Vector3.up, Vector3.back, Vector3.down })
            {
                var lamp = tools.PlaceLamp(prop.transform.position + normal * .5f, normal, Quaternion.FromToRotation(Vector3.up, normal));
                Assert.That(lamp, Is.Not.Null, "Solid scenery accepts placement in every orientation.");
                Assert.That(lamp.Anchored, Is.True);
                Assert.That(lamp.WorkLight.type, Is.EqualTo(LightType.Point));
                Physics.SyncTransforms();
            }
            var dropped = tools.PlaceLamp(new Vector3(-3, 3, -6), Vector3.up, Quaternion.identity);
            Assert.That(dropped, Is.Not.Null); Assert.That(dropped.Anchored, Is.False);
            float height = dropped.Body.position.y;
            yield return new WaitForSeconds(.18f);
            Assert.That(dropped.Body.position.y, Is.LessThan(height - .04f));
            var saved = tools.Capture(); tools.Restore(saved);
            Assert.That(tools.Lamps.Count, Is.EqualTo(4));
            Assert.That(tools.Lamps[1].Anchored && tools.Lamps[2].Anchored, Is.True);
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
