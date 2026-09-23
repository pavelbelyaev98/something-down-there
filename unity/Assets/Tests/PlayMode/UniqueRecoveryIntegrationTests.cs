#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class UniqueRecoveryIntegrationTests
    {
        private Scene scene;
        private InputTestFixture devices;
        private SimulationMode originalSimulation;

        [SetUp] public void RememberPhysics() => originalSimulation=Physics.simulationMode;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices?.TearDown();
            Physics.simulationMode=originalSimulation;
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator FullBagBentTunnelReloadAndDisplayKeepOneComputer() => Recover(false);

        [UnityTest]
        public IEnumerator SideAttachmentCanSwingThroughBentTunnelAndSettleOnPad() => Recover(true);

        [UnityTest]
        public IEnumerator PopulatedAngledPassageLoosensBlockingFindsAndRecoversComputer() => Recover(true, true);

        private IEnumerator Recover(bool sideAttachment, bool populated = false)
        {
            devices = new InputTestFixture(); devices.Setup();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.sceneLoaded += TestInputPreferences.Configure;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects()[0];
            var player = root.GetComponentInChildren<FpsPlayer>();
            var terrain = player.ExcavationTerrain;
            var field = player.Discoveries;
            var winch = player.Winch;
            Assert.That(winch.Configured, Is.True);
            winch.enabled = false;
            player.Tuning.Gravity = 0;
            yield return null;
            player.SetApplicationFocus(true); player.CloseMenu();
            yield return null;
            player.SetApplicationFocus(true);
            var find = field.Finds.Single(f => f.Kind == DiscoveryKind.Unique);
            var identities=field.Finds.Select(f=>f.Item.InstanceId).ToArray();
            string identity = find.Item.InstanceId;
            Assert.That(find.Exposure, Is.Zero);
            Assert.That(find.GetComponent<FindPhysics>().Released, Is.False);
            Assert.That(player.Inventory.TryAdd(find.Item), Is.False);
            Assert.That(find.TryCollect(player), Is.False);
            foreach (var common in field.Finds.Where(f => f.Kind == DiscoveryKind.Common).Take(player.Inventory.Capacity))
            {
                var state = common.Capture(); state.State = FindState.Collected; common.Restore(state);
                Assert.That(player.Inventory.TryAdd(common.Item), Is.True);
            }
            int bagCount = player.Inventory.Count;
            Vector3 local = terrain.transform.InverseTransformPoint(find.transform.position);
            var grid = new ExcavationGrid(terrain.Capture().Size, terrain.Capture().CellSize);
            grid.RemoveSphere(local, 1.08f, out _);
            Vector3 bend=local+(populated?new Vector3(2,2,4):Vector3.right*2);
            int steps=Mathf.CeilToInt(Vector3.Distance(local,bend)/.1f);
            for(int i=0;i<=steps;i++) grid.RemoveSphere(Vector3.Lerp(local,bend,i/(float)steps),.32f,out _);
            for (float y = bend.y; y <= grid.Extent.y + .3f; y += .1f)
                grid.RemoveSphere(new Vector3(bend.x, y, bend.z), .32f, out _);
            yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
            if(!populated) foreach (var common in field.Finds.Where(f => f.Kind == DiscoveryKind.Common)) common.gameObject.SetActive(false);
            find.RefreshExposure();
            float buriedY=find.GetComponent<Rigidbody>().position.y;
            yield return new WaitForSeconds(.25f);
            Assert.That(find.GetComponent<FindPhysics>().Released, Is.True, "A fully uncovered unique must fall before marking.");
            Assert.That(find.GetComponent<Rigidbody>().position.y, Is.LessThan(buriedY-.05f));
            Assert.That(find.CanMark, Is.True, $"exposure {find.Exposure}");
            player.ViewCamera.transform.position = find.transform.position
                + (sideAttachment ? new Vector3(0,.1f,-.9f) : Vector3.up*.9f);
            player.ViewCamera.transform.LookAt(find.transform.position);
            Physics.SyncTransforms();
            Assert.That(player.TryGetTarget(player.Tuning.InteractReach, out var aimed) && aimed.collider.GetComponentInParent<BuriedFind>() == find, Is.True);
            var holding = new FindExtractionInteraction(player);
            holding.Tick(default, .1f);
            holding.Tick(new FpsInputFrame { InteractHeld = true }, winch.Settings.MarkSeconds * .5f);
            Assert.That(winch.Busy, Is.False);
            holding.Reset();
            holding.Tick(new FpsInputFrame { InteractHeld = true }, winch.Settings.MarkSeconds * 2);
            Assert.That(winch.Busy, Is.False, "Menu/focus cancellation requires release before a fresh hold.");
            holding.Tick(default, .1f);
            holding.Tick(new FpsInputFrame { InteractHeld = true }, winch.Settings.MarkSeconds + .01f);
            Assert.That(winch.Busy, Is.True);
            float battery = player.Battery.Charge;
            var phases = new System.Collections.Generic.HashSet<ExtractionPhase>();
            bool reloaded = false;
            double worstPlanning = 0, worstHaul = 0;
            var timer = new System.Diagnostics.Stopwatch();
            float initialVolume = terrain.Capture().RemovedVolume;
            Quaternion initialRotation=find.GetComponent<Rigidbody>().rotation;
            float largestRotation=0;
            bool pauseChecked=false;
            GameObject wall=null;
            bool wallPlaced=populated, wallChecked=populated;
            Physics.simulationMode=SimulationMode.Script;
            for (int frame = 0; frame < 6000 && winch.Busy; frame++)
            {
                player.SetApplicationFocus(true);
                var job = winch.Capture(); phases.Add(job.Phase);
                if(!wallPlaced && job.Phase==ExtractionPhase.Hauling)
                {
                    wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    SceneManager.MoveGameObjectToScene(wall,scene);
                    wall.name="Permanent obstruction during a physical haul";
                    wall.transform.position=terrain.transform.TransformPoint(local+Vector3.right*1.3f);
                    wall.transform.localScale=new Vector3(.15f,4,4);
                    Physics.SyncTransforms(); wallPlaced=true;
                }
                if(job.Phase==ExtractionPhase.Obstructed && wall!=null)
                {
                    Assert.That(find.GetComponent<Rigidbody>().position.x, Is.LessThan(wall.transform.position.x),
                        "Physical contacts stop the computer at an undiggable wall.");
                    Assert.That(find.GetComponent<Rigidbody>().isKinematic, Is.True, "A stalled load is secured for retry.");
                    wall.SetActive(false); UnityEngine.Object.Destroy(wall); wall=null;
                    Assert.That(winch.Retry(find), Is.True);
                    wallChecked=true;
                    yield return null;
                    continue;
                }
                Assert.That(job.Phase, Is.Not.EqualTo(ExtractionPhase.Obstructed), winch.Prompt);
                if (job.Phase == ExtractionPhase.Hauling && job.Progress > .4f && !reloaded)
                {
                    Vector3 before = find.GetComponent<Rigidbody>().position;
                    Quaternion rotationBefore=find.GetComponent<Rigidbody>().rotation;
                    var checkpoint = new WorldSnapshot { Sequence = 1, UtcTicks = DateTime.UtcNow.Ticks,
                        Terrain = terrain.Capture(), TerrainPosition = terrain.transform.position, TerrainRotation = terrain.transform.rotation,
                        Finds = field.Capture(), Extraction = job, ExcavationSeed = terrain.ExcavationSeed, DiscoverySeed = field.Seed };
                    player.Capture(checkpoint);
                    using var bytes = new MemoryStream(); WorldSaveCodec.Write(bytes, checkpoint); bytes.Position = 0;
                    var loaded = WorldSaveCodec.Read(bytes);
                    field.ValidateRestore(loaded.Finds); winch.ValidateRestore(loaded);
                    field.Restore(loaded.Finds, loaded.DiscoverySeed);
                    winch.Restore(loaded.Extraction);
                    find = field.Find(identity);
                    Assert.That(Vector3.Distance(before, find.transform.position), Is.LessThan(.0001f));
                    Assert.That(Quaternion.Angle(rotationBefore,find.transform.rotation), Is.LessThan(.001f));
                    Assert.That(winch.Capture().LinearVelocity, Is.EqualTo(job.LinearVelocity));
                    Assert.That(winch.Capture().AngularVelocity, Is.EqualTo(job.AngularVelocity));
                    if(!populated) foreach (var common in field.Finds.Where(f => f.Kind == DiscoveryKind.Common)) common.gameObject.SetActive(false);
                    reloaded = true;
                    Physics.SyncTransforms();
                }
                timer.Restart();
                winch.Tick(.02f);
                if(winch.Busy && job.Phase==ExtractionPhase.Hauling && winch.Capture().Phase==ExtractionPhase.Hauling)
                {
                    Assert.That(find.GetComponent<Rigidbody>().isKinematic, Is.False);
                    Assert.That(find.GetComponent<Rigidbody>().constraints, Is.EqualTo(RigidbodyConstraints.None));
                    largestRotation=Mathf.Max(largestRotation,Quaternion.Angle(initialRotation,find.GetComponent<Rigidbody>().rotation));
                    if(!pauseChecked && job.Progress>.6f)
                    {
                        var before=find.GetComponent<Rigidbody>().position;
                        var beforeTerrain=terrain.Revision;
                        player.SetApplicationFocus(false);
                        winch.Tick(.02f);
                        for(int i=0;i<10;i++) Physics.Simulate(.02f);
                        Assert.That(find.GetComponent<Rigidbody>().position, Is.EqualTo(before));
                        Assert.That(terrain.Revision, Is.EqualTo(beforeTerrain));
                        player.SetApplicationFocus(true); player.CloseMenu();
                        pauseChecked=true;
                    }
                }
                Physics.Simulate(.02f);
                timer.Stop();
                if (job.Phase == ExtractionPhase.Planning) worstPlanning = Math.Max(worstPlanning, timer.Elapsed.TotalMilliseconds);
                else worstHaul = Math.Max(worstHaul, timer.Elapsed.TotalMilliseconds);
                if (frame % 20 == 0) yield return null;
            }
            Assert.That(winch.Busy, Is.False, "Recovery must finish in bounded time.");
            Assert.That(reloaded, Is.True, player.Feedback + " phases: " + string.Join(",", phases));
            Assert.That(pauseChecked, Is.True);
            Assert.That(wallChecked, Is.True, "A blocked physical haul must pause and remain recoverable.");
            Assert.That(largestRotation, Is.GreaterThan(10f), "The marked point pulls a rotating physical load.");
            CollectionAssert.IsSubsetOf(new[] { ExtractionPhase.Deploying, ExtractionPhase.Attaching,
                ExtractionPhase.Hauling, ExtractionPhase.Delivering }, phases);
            Assert.That(find.State, Is.EqualTo(FindState.Stored));
            Assert.That(terrain.Capture().RemovedVolume, Is.GreaterThan(initialVolume), "The narrow dug route is widened by the load.");
            Assert.That(player.Inventory.Count, Is.EqualTo(bagCount));
            Assert.That(player.Battery.Charge, Is.EqualTo(battery));
            var stand = root.GetComponentInChildren<UniqueDisplayStand>();
            Assert.That(stand.TryInteract(player), Is.True);
            Assert.That(find.State, Is.EqualTo(FindState.Displayed));
            Assert.That(stand.TryInteract(player), Is.True, "Inspecting again must not create a second object.");
            Assert.That(field.Finds.Count(f => f.Kind == DiscoveryKind.Unique), Is.EqualTo(1));
            CollectionAssert.AreEquivalent(identities,field.Finds.Select(f=>f.Item.InstanceId), "Recovery never deletes finds in its way.");
            Assert.That(find.Item.InstanceId, Is.EqualTo(identity));
            Assert.That(find.DepthRecorded && find.DiscoveryDepth > 0, Is.True);
            Assert.That(stand.GetPrompt(player), Does.Contain(find.DisplayName));
            Debug.Log($"Recovery fixture worst planning/haul substep: {worstPlanning:F1}/{worstHaul:F1} ms.");
        }
    }
}
#endif
