#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class DetectorIntegrationTests
    {
        private Scene scene;
        private InputTestFixture devices;
        private FpsPlayer player;
        private SimulationMode simulation;

        [UnitySetUp]
        public IEnumerator Open()
        {
            simulation = Physics.simulationMode;
            devices = new InputTestFixture(); devices.Setup();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.sceneLoaded += TestInputPreferences.Configure;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            player = scene.GetRootGameObjects()[0].GetComponentInChildren<FpsPlayer>();
            yield return null;
            player.Tuning.Gravity = 0;
            player.SetApplicationFocus(true); player.CloseMenu();
            player.Winch.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices?.TearDown();
            Physics.simulationMode = simulation;
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator AimSignalStopsAtPartialRevealAndRespectsOwnershipAndReload()
        {
            var field = player.Discoveries;
            var terrain = player.ExcavationTerrain;
            var detector = player.Detector;
            var camera = player.ViewCamera.transform;
            var first = field.Finds.Single(f => f.SaveContentId == "unique_reservoir_computer");
            detector.Reset(); detector.Tick();
            Assert.That(detector.SourceCount, Is.EqualTo(field.Catalog.Entries.Count(e => e.Prefab.DetectorEligible)));
            Assert.That(detector.SourceCount, Is.LessThan(field.Finds.Count / 100));
            Assert.That(detector.SignalLevel, Is.Zero, "The detector stays off at the starting rim.");
            for (int x = -8; x <= 8; x += 8)
                for (int z = -8; z <= 8; z += 8)
                {
                    camera.position = new Vector3(x, 1.7f, z);
                    camera.LookAt(first.WorldBounds.center);
                    detector.Tick();
                    Assert.That(detector.SignalLevel, Is.Zero, "Looking toward distant computers from the surface stays quiet.");
                }

            Vector3 approach = first.WorldBounds.center + Vector3.up * (EquipmentProgression.DetectorRange * .8f);
            camera.position = approach;
            Quaternion direct = Quaternion.FromToRotation(Vector3.forward, first.WorldBounds.center - camera.position);
            void Aim(float yaw, float pitch = 0)
            {
                camera.rotation = direct * Quaternion.Euler(pitch, yaw, 0);
                detector.Tick();
            }
            Aim(40); Assert.That(detector.SignalLevel, Is.EqualTo(1));
            Aim(20); Assert.That(detector.SignalLevel, Is.EqualTo(2));
            Aim(0); Assert.That(detector.SignalLevel, Is.EqualTo(3));
            Assert.That(detector.Target, Is.SameAs(first));
            Assert.That(first.Exposure, Is.Zero);
            Aim(90); Assert.That(detector.SignalLevel, Is.Zero);
            Aim(180); Assert.That(detector.Target, Is.Null);
            Aim(0, 20); Assert.That(detector.SignalLevel, Is.EqualTo(2));
            Aim(0, -40); Assert.That(detector.SignalLevel, Is.EqualTo(1));
            camera.position = first.WorldBounds.center + Vector3.up * (EquipmentProgression.DetectorRange * .3f);
            Aim(0); Assert.That(detector.SignalLevel, Is.EqualTo(3), "Alignment controls level, not distance.");
            camera.position = first.WorldBounds.center + Vector3.up * (EquipmentProgression.DetectorRetentionRange + 1);
            Aim(0); Assert.That(detector.SignalLevel, Is.Zero, "Range still limits a direct look.");
            camera.position = approach; Aim(0);

            foreach (var common in field.Finds.Where(f => f.Kind == DiscoveryKind.Common).Take(player.Inventory.Capacity))
            {
                Assert.That(FindDetector.Eligible(common), Is.False);
                var state = common.Capture(); state.State = FindState.Collected; common.Restore(state);
                Assert.That(player.Inventory.TryAdd(common.Item), Is.True);
            }
            player.Battery.TrySpend(player.Battery.Charge);
            detector.Tick();
            Assert.That(detector.SignalLevel, Is.EqualTo(3), "Full bag and empty fuel do not suppress the detector.");
            Assert.That(player.Battery.Charge, Is.Zero);

            var silent = field.Finds.First(f => f.Kind == DiscoveryKind.Common && f.State == FindState.World && f.Exposure == 0);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(BuriedFind).GetField("minor", flags).SetValue(silent, false);
            Assert.That(FindDetector.Eligible(silent), Is.False);
            typeof(BuriedFind).GetField("detectorEligible", flags).SetValue(silent, true);
            Assert.That(FindDetector.Eligible(silent), Is.True, "Eligibility is authored.");
            typeof(BuriedFind).GetField("minor", flags).SetValue(silent, true);
            Assert.That(FindDetector.Eligible(silent), Is.False, "Routine commons remain silent.");

            var firstState = first.Capture();
            firstState.State = FindState.Extracting; firstState.DepthRecorded = true;
            first.Restore(firstState);
            Assert.That(detector.SignalLevel, Is.Zero, "Extraction removes the signal before the next detector tick.");
            firstState.State = FindState.World;
            first.Restore(firstState);
            Assert.That(FindDetector.Eligible(first), Is.False, "An already observed visible sliver cannot signal between exposure samples.");
            firstState.DepthRecorded = false; first.Restore(firstState);

            var second = field.Finds.Single(f => f.SaveContentId == "unique_survey_computer");
            var originalSecond = second.Capture();
            var secondState = second.Capture();
            secondState.Position = terrain.transform.InverseTransformPoint(first.transform.position + Vector3.right * 2);
            second.Restore(secondState);
            camera.LookAt(second.WorldBounds.center); detector.Tick();
            Assert.That(detector.Target, Is.SameAs(second), "Aim can choose a farther find immediately.");
            Aim(0); Assert.That(detector.Target, Is.SameAs(first));
            second.Restore(originalSecond);
            player.SetApplicationFocus(false);
            camera.rotation = direct * Quaternion.Euler(0, 180, 0); detector.Tick();
            Assert.That(detector.Target, Is.SameAs(first), "Selection pauses with gameplay.");
            player.SetApplicationFocus(true); player.CloseMenu(); detector.Tick();
            Assert.That(detector.SignalLevel, Is.Zero, "Resuming reads the current view immediately.");
            Aim(0);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++) detector.Tick();
            watch.Stop();
            Debug.Log($"Detector sampling: {watch.Elapsed.TotalMilliseconds / 10000:F4} ms/update across {detector.SourceCount} sources in {field.Finds.Count} finds.");
            Assert.That(watch.Elapsed.TotalMilliseconds / 10000, Is.LessThan(.1));

            // A small actual terrain cut must silence the find before it can be marked.
            var samples = (Vector3[])typeof(BuriedFind).GetField("exposureSamples", flags).GetValue(first);
            Vector3 surface = samples.Select(first.transform.TransformPoint).OrderByDescending(p => p.y).First();
            Physics.SyncTransforms();
            terrain.ClearLoadSweep(surface, surface, Quaternion.identity, Vector3.one * terrain.CellSize * 1.5f);
            Assert.That(first.Exposure, Is.GreaterThan(0).And.LessThan(first.RequiredExposure));
            Assert.That(first.CanMark, Is.False);
            Assert.That(detector.SignalLevel, Is.Zero, "Partial reveal turns the signal off immediately, without a selection delay.");
            detector.Tick();
            Assert.That(detector.Target, Is.Null, "Revealed objects never become fallback targets.");

            long revision = field.PopulationRevision;
            var saved = field.Capture();
            field.Restore(saved, field.Seed);
            Assert.That(field.PopulationRevision, Is.GreaterThan(revision));
            detector.Tick();
            Assert.That(detector.SignalLevel, Is.Zero, "Reload must preserve silence for the partially exposed object.");
            var restoredSecond = field.Finds.Single(f => f.SaveContentId == "unique_survey_computer");
            camera.position = restoredSecond.WorldBounds.center + Vector3.up * 3;
            camera.LookAt(restoredSecond.WorldBounds.center); detector.Tick();
            Assert.That(detector.SignalLevel, Is.EqualTo(3));
            Assert.That(detector.Target, Is.SameAs(restoredSecond));
            Assert.That(detector.Target, Is.Not.SameAs(second), "Population replacement releases stale references.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ComputerCopiesRecoverToSeparatePadsAndKeepIndividualDisplaysOnReload()
        {
            Physics.simulationMode = SimulationMode.Script;
            var field = player.Discoveries;
            var terrain = player.ExcavationTerrain;
            var winch = player.Winch;
            var uniques = field.Finds.Where(f => f.Kind == DiscoveryKind.Unique).ToArray();
            Assert.That(uniques.Length, Is.GreaterThan(1));
            Assert.That(uniques.Select(f => f.SaveContentId).Distinct().Count(), Is.EqualTo(uniques.Length));
            var stands = scene.GetRootGameObjects()[0].GetComponentsInChildren<UniqueDisplayStand>();
            Assert.That(stands.Length, Is.EqualTo(uniques.Length));
            Assert.That(stands.Select(s => s.SocketId).Distinct().Count(), Is.EqualTo(stands.Length));
            foreach (var common in field.Finds.Where(f => f.Kind == DiscoveryKind.Common)) common.gameObject.SetActive(false);
            var arrivals = new System.Collections.Generic.List<Vector3>();
            foreach (var find in uniques)
            {
                // Existing bent-underground recovery tests own terrain/rope strain.
                // This fixture exercises the real mark -> plan -> haul -> arrival
                // pipeline repeatedly with the prior computers still on their pads.
                var state = find.Capture();
                state.Position = terrain.transform.InverseTransformPoint(new Vector3(1.5f, .9f, -8.3f));
                state.PhysicsReleased = false;
                find.Restore(state);
                player.ViewCamera.transform.position = find.WorldBounds.center + Vector3.back * 2;
                player.ViewCamera.transform.LookAt(find.WorldBounds.center);
                Physics.SyncTransforms();
                Assert.That(player.TryGetTarget(player.Tuning.InteractReach, out var hit), Is.True);
                Assert.That(hit.collider.GetComponentInParent<BuriedFind>(), Is.SameAs(find));
                Assert.That(winch.TryMark(find, hit.point, hit.normal), Is.True);
                Assert.That(FindDetector.Eligible(find), Is.False);
                for (int step = 0; step < 5000 && winch.Busy; step++)
                {
                    player.SetApplicationFocus(true); winch.Tick(.02f); Physics.Simulate(.02f);
                    if (step % 50 == 0) yield return null;
                }
                Assert.That(winch.Busy, Is.False, "Each computer must arrive without needing the previous one displayed.");
                Assert.That(find.State, Is.EqualTo(FindState.Stored), player.Feedback);
                foreach (var arrival in arrivals) Assert.That(Vector3.Distance(arrival, find.WorldBounds.center), Is.GreaterThan(2));
                arrivals.Add(find.WorldBounds.center);
            }
            foreach (var stand in stands) Assert.That(stand.TryInteract(player), Is.True);
            Assert.That(uniques.All(f => f.State == FindState.Displayed), Is.True);
            var checkpoint = new WorldSnapshot { Sequence = 1, UtcTicks = DateTime.UtcNow.Ticks,
                Terrain = terrain.Capture(), TerrainPosition = terrain.transform.position, TerrainRotation = terrain.transform.rotation,
                Finds = field.Capture(), ExcavationSeed = terrain.ExcavationSeed, DiscoverySeed = field.Seed };
            player.Capture(checkpoint);
            using var bytes = new MemoryStream(); WorldSaveCodec.Write(bytes, checkpoint); bytes.Position = 0;
            var loaded = WorldSaveCodec.Read(bytes);
            winch.ValidateRestore(loaded); field.Restore(loaded.Finds, loaded.DiscoverySeed);
            winch.Restore(loaded.Extraction);
            foreach (var stand in stands)
            {
                Assert.That(stand.Displayed, Is.Not.Null);
                Assert.That(FindDetector.Eligible(stand.Displayed), Is.False);
                Assert.That(stand.TryInteract(player), Is.True);
                Assert.That(stand.GetPrompt(player), Does.Contain(stand.Displayed.DisplayName));
            }
            Assert.That(field.Finds.Count(f => f.Kind == DiscoveryKind.Unique), Is.EqualTo(uniques.Length));
        }
    }
}
#endif
