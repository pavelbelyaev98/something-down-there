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
        private SalvageWinchSettings tuningOverride;

        [SetUp] public void RememberPhysics() => originalSimulation=Physics.simulationMode;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices?.TearDown();
            if(tuningOverride!=null) UnityEngine.Object.Destroy(tuningOverride);
            Physics.simulationMode=originalSimulation;
            Time.timeScale = 1;
        }

        [UnityTest]
        public IEnumerator FullBagBentTunnelReloadAndDisplayKeepOneComputer() => Recover(false);

        [UnityTest]
        public IEnumerator SideAttachmentCanSwingThroughBentTunnelAndSettleOnPad() => Recover(true);

        [UnityTest]
        public IEnumerator PopulatedAngledPassageLoosensBlockingFindsAndRecoversComputer() => Recover(true, true);

        [UnityTest]
        public IEnumerator SwingNearSoilCutsOnlyAfterSustainedPhysicalContact() => ContactRecovery(false);

        [UnityTest]
        public IEnumerator BriefContactThatPullsFreeLeavesSoilIntact() => ContactRecovery(true);

        [UnityTest]
        public IEnumerator PersistentJamRestoresAndPullsThroughWithoutPlayerInput() => ContactRecovery(false, true);

        [UnityTest]
        public IEnumerator MountedLampIsKnockedLooseByContactWithoutLosingItsSlot() => ContactRecovery(false, false, true);

        private IEnumerator ContactRecovery(bool pullFree, bool persistentJam = false, bool lampObstacle = false)
        {
            devices = new InputTestFixture(); devices.Setup();
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            SceneManager.sceneLoaded += TestInputPreferences.Configure;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.Configure;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var player = scene.GetRootGameObjects()[0].GetComponentInChildren<FpsPlayer>();
            yield return null; // Let startup generate the population.
            player.Tuning.Gravity = 0;
            player.SetApplicationFocus(true); player.CloseMenu();
            var terrain = player.ExcavationTerrain;
            var winch = player.Winch; winch.enabled = false;
            if(persistentJam)
            {
                tuningOverride=UnityEngine.Object.Instantiate(winch.Settings);
                tuningOverride.ContactStallSeconds=2f;
                typeof(SalvageWinch).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                    .SetValue(winch,tuningOverride);
            }
            var find = player.Discoveries.Finds.Single(f => f.SaveContentId == "unique_reservoir_computer");
            foreach (var common in player.Discoveries.Finds.Where(f => f.Kind == DiscoveryKind.Common)) common.gameObject.SetActive(false);
            var state = find.Capture(); state.State = FindState.Extracting; find.Restore(state);
            var physical = find.GetComponent<FindPhysics>();
            var body = physical.Body;
            Physics.SyncTransforms();
            var bounds = find.GetComponent<Collider>().bounds;
            var grid = new ExcavationGrid(terrain.Dimensions, terrain.CellSize);
            var local = terrain.transform.InverseTransformPoint(bounds.center);
            // The real collider fits. The old predictive clearance box would
            // already erase these nearby walls on the very first winch tick.
            var half = bounds.extents + new Vector3(.18f, .9f, .18f);
            grid.RemoveBoxSweep(local, local, Quaternion.identity, half, out _);
            // Air above a thin retaining ceiling lets us measure the released
            // load's real speed, instead of keeping it inside endless solid soil.
            var above = local + Vector3.up * (half.y + .25f + 2f);
            grid.RemoveBoxSweep(above, above, Quaternion.identity, new Vector3(half.x + .5f, 2f, half.z + .5f), out _);
            yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
            player.SetApplicationFocus(true); player.CloseMenu();
            Vector3 attach = find.LocalHull.center + Vector3.up * find.LocalHull.extents.y;
            Vector3 start = terrain.transform.InverseTransformPoint(find.transform.TransformPoint(attach));
            var job = new ExtractionSnapshot { FindId = find.Item.InstanceId, Phase = ExtractionPhase.Hauling,
                AttachLocal = attach, Outward = Vector3.up, Attached = true, AngularVelocity = Vector3.up * 1.5f,
                Route = new[] { start, start + Vector3.up * 4, start + Vector3.up * 5,
                    start + Vector3.up * 5 + Vector3.right, start + Vector3.up * 4 + Vector3.right } };
            winch.Restore(job);
            Physics.simulationMode = SimulationMode.Script;
            if(lampObstacle)
            {
                Assert.That(Physics.Raycast(bounds.center+Vector3.up*(bounds.extents.y+.05f),Vector3.up,out var ceiling,2f), Is.True);
                var lamp=player.WorksiteTools.PlaceLamp(ceiling.point,ceiling.normal,Quaternion.FromToRotation(Vector3.up,ceiling.normal));
                Assert.That(lamp, Is.Not.Null); Assert.That(lamp.Anchored, Is.True);
                Physics.SyncTransforms();
                yield return PullPastLamp(player,find,lamp);
                yield break;
            }
            long revision = terrain.StateRevision;
            float volume = terrain.RemovedVolume, elapsed = 0;
            var contactProbe = find.gameObject.AddComponent<RecoveryContactProbe>();
            contactProbe.Terrain = terrain;
            Quaternion initialRotation = body.rotation;
            float rotation = 0, strongestBlockedPull = 0, fastestLoadedGuide = 0;
            bool redirected = false, retensionRestored = false;
            winch.Tick(.02f);
            Assert.That(terrain.StateRevision, Is.EqualTo(revision), "No physical simulation/contact has happened: nearby soil must stay intact.");
            float ordinaryPull = body.mass * winch.Settings.SpringAcceleration;
            Assert.That(find.GetComponent<SpringJoint>().spring, Is.EqualTo(ordinaryPull), "Free travel does not wind up extra pulling force.");
            Assert.That(winch.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount), Is.Zero, "No break means no soil particles.");
            for (int step = 0; step < 700; step++)
            {
                elapsed += .02f; contactProbe.SimulationSeconds = elapsed; Physics.Simulate(.02f);
                rotation = Mathf.Max(rotation, Quaternion.Angle(initialRotation, body.rotation));
                float firstContact = contactProbe.FirstContactSeconds;
                if (pullFree && firstContact >= 0 && !redirected)
                {
                    var retreat = winch.Capture();
                    var hook = terrain.transform.InverseTransformPoint(body.position + body.rotation * Vector3.Scale(find.transform.lossyScale, attach));
                    retreat.Progress = 0; retreat.LinearVelocity = retreat.AngularVelocity = Vector3.zero;
                    retreat.Route = new[] { hook, start, start, start, start };
                    winch.Restore(retreat); redirected = true;
                }
                player.SetApplicationFocus(true);
                Vector3 beforePosition = body.position, beforeVelocity = body.linearVelocity;
                Quaternion beforeRotation = body.rotation;
                float beforeGuide = winch.Capture().Progress;
                winch.Tick(.02f);
                fastestLoadedGuide = Mathf.Max(fastestLoadedGuide, (winch.Capture().Progress - beforeGuide) / .02f);
                Assert.That(body.isKinematic, Is.False, "A jam must never secure the load and wait for the player.");
                Assert.That(winch.Prompt, Does.Not.Contain("retry").And.Not.Contain("Clear"));
                if(persistentJam && !retensionRestored && winch.Capture().Phase==ExtractionPhase.Retensioning)
                {
                    var stalled=winch.Capture();
                    winch.Restore(stalled);
                    yield return null; // Finish destroying the previous joint.
                    player.SetApplicationFocus(true); player.CloseMenu();
                    winch.Tick(.02f);
                    Assert.That(body.isKinematic, Is.False, "Saved automatic recovery resumes without Interact.");
                    retensionRestored=true;
                }
                if (terrain.StateRevision != revision)
                {
                    Assert.That(pullFree, Is.False, "A brief impact followed by a successful pull must not excavate.");
                    Assert.That(firstContact, Is.GreaterThanOrEqualTo(0));
                    Assert.That(elapsed - firstContact, Is.GreaterThanOrEqualTo(winch.Settings.ContactStallSeconds - .02f),
                        "The load must physically try to escape before breaking the contacted ground.");
                    Assert.That(terrain.RemovedVolume - volume, Is.InRange(.0001f, .25f), "Break a local patch, not a load-sized cavity.");
                    Assert.That(terrain.IsSolid(contactProbe.LastPoint + Vector3.right * 1.4f), Is.True, "Distant soil stays intact.");
                    Assert.That(strongestBlockedPull, Is.GreaterThan(ordinaryPull * 1.5f), "The rope must pull harder before the soil breaks.");
                    Assert.That(fastestLoadedGuide, Is.GreaterThan(winch.Settings.HaulSpeed * 1.5f), "A jam must speed up actual reeling, not only increase spring stiffness.");
                    Assert.That(winch.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount), Is.GreaterThan(0), "A committed local break emits visible soil debris.");
                    Assert.That(body.position, Is.EqualTo(beforePosition), "Soil release must not teleport the body.");
                    Assert.That(body.rotation, Is.EqualTo(beforeRotation));
                    Assert.That(body.linearVelocity, Is.EqualTo(beforeVelocity), "The loaded spring, not a scripted velocity kick, produces recoil.");
                    float releasedPull = find.GetComponent<SpringJoint>().spring;
                    Assert.That(releasedPull, Is.GreaterThanOrEqualTo(strongestBlockedPull), "Do not discard tension at the instant soil gives way.");
                    float blockedSpeed = beforeVelocity.magnitude, releaseAge = 0, fastestRelease = 0;
                    bool recoiled = false, burst = false;
                    // One shallow chip may leave other corners pinned. Check
                    // the response when enough retaining contacts actually let
                    // go, still within the loaded spring's release interval.
                    for (int releaseStep = 0; releaseStep < 300; releaseStep++)
                    {
                        Physics.Simulate(.02f);
                        releaseAge += .02f;
                        fastestRelease = Mathf.Max(fastestRelease, body.linearVelocity.magnitude);
                        if (releaseAge <= winch.Settings.TensionReleaseSeconds
                            && body.linearVelocity.magnitude > blockedSpeed + .1f)
                            recoiled = true;
                        if (body.linearVelocity.magnitude > winch.Settings.HaulSpeed * 1.5f) burst = true;
                        if (recoiled && burst) break;
                        long beforeRelease = terrain.StateRevision;
                        float speed = body.linearVelocity.magnitude;
                        winch.Tick(.02f);
                        if (terrain.StateRevision != beforeRelease) { releaseAge = 0; blockedSpeed = speed; }
                        if (releaseStep % 20 == 0) yield return null;
                    }
                    Assert.That(recoiled, Is.True, "When the retaining soil gives way, the loaded spring must accelerate the body.");
                    Assert.That(burst, Is.True, $"Release must visibly exceed cruising speed; peak was {fastestRelease:F2}.");
                    break;
                }
                strongestBlockedPull = Mathf.Max(strongestBlockedPull, find.GetComponent<SpringJoint>().spring);
                if (pullFree && redirected && elapsed > firstContact + 2f) break;
                if (step % 20 == 0) yield return null;
            }
            Assert.That(contactProbe.FirstContactSeconds, Is.GreaterThanOrEqualTo(0), "The fixture must exercise a real terrain collision.");
            Assert.That(rotation, Is.GreaterThan(5), "The payload is free to turn near the soil.");
            if (pullFree)
            {
                Assert.That(terrain.StateRevision, Is.EqualTo(revision));
                Assert.That(winch.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount), Is.Zero, "Brief contact must not fake soil destruction.");
            }
            else Assert.That(terrain.StateRevision, Is.GreaterThan(revision), "A persistent loaded contact eventually chips the obstruction.");
            if(persistentJam)
            {
                Assert.That(retensionRestored, Is.True, "Exercise a genuine sustained jam and its restore path.");
                Assert.That(strongestBlockedPull, Is.GreaterThan(ordinaryPull*winch.Settings.BlockedPullMultiplier),
                    "A persistent jam automatically builds more pull than an ordinary chip.");
            }
        }

        private static IEnumerator PullPastLamp(FpsPlayer player, BuriedFind find, WorkLamp lamp)
        {
            var winch=player.Winch;var terrain=player.ExcavationTerrain;
            var probe=find.gameObject.AddComponent<RecoveryContactProbe>();
            probe.WatchedCollider=lamp.GetComponent<Collider>();
            long revision=terrain.StateRevision;
            int slot=lamp.Slot;
            float elapsed=0;
            for(int step=0;step<500 && lamp.Anchored;step++)
            {
                player.SetApplicationFocus(true);player.CloseMenu();
                elapsed+=.02f;probe.SimulationSeconds=elapsed;
                winch.Tick(.02f);Physics.Simulate(.02f);
                if(step%20==0) yield return null;
            }
            Assert.That(probe.FirstContactSeconds, Is.GreaterThanOrEqualTo(0), "The load must actually hit the mounted lamp.");
            Assert.That(elapsed-probe.FirstContactSeconds, Is.GreaterThanOrEqualTo(winch.Settings.ContactStallSeconds-.02f));
            Assert.That(lamp.Anchored, Is.False, "An anchored work lamp cannot be an immovable recovery barrier.");
            Assert.That(lamp.Body.isKinematic, Is.False);
            Assert.That(terrain.StateRevision, Is.EqualTo(revision), "Knocking a mount loose does not authorize a fake dirt cut.");
            Assert.That(winch.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount), Is.Zero);
            var saved=player.WorksiteTools.Capture();
            Assert.That(saved.Lamps.Single().Slot, Is.EqualTo(slot));
            Assert.That(saved.Lamps.Single().Anchored, Is.False);
            Assert.That(player.WorksiteTools.AvailableLamps, Is.EqualTo(WorksiteTools.LampCapacity-1));
        }

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
            var find = field.Finds.Single(f => f.SaveContentId == "unique_reservoir_computer");
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
            var rope = root.GetComponentInChildren<WinchRopeView>();
            bool ropeChecked = false;
            double ropeCost = 0; int ropeSteps = 0;
            var timer = new System.Diagnostics.Stopwatch();
            float initialVolume = terrain.Capture().RemovedVolume;
            Quaternion initialRotation=find.GetComponent<Rigidbody>().rotation;
            float largestRotation=0;
            bool pauseChecked=false;
            GameObject wall=null;
            bool wallPlaced=populated, wallChecked=populated;
            float wallSeconds=0;
            Physics.simulationMode=SimulationMode.Script;
            // This deliberately undersized tunnel now needs physical jam/retry
            // intervals at each bottleneck instead of being pre-cleared at speed.
            for (int frame = 0; frame < 15000 && winch.Busy; frame++)
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
                if(wall!=null && job.Phase==ExtractionPhase.Retensioning)
                {
                    wallSeconds+=.02f;
                    Assert.That(find.GetComponent<Rigidbody>().position.x, Is.LessThan(wall.transform.position.x),
                        "Physical contacts stop the computer at an undiggable wall.");
                    Assert.That(find.GetComponent<Rigidbody>().isKinematic, Is.False, "Even a permanent obstruction cannot put the winch into a manual pause.");
                    // Keep this temporary fixture barrier beyond the former hard
                    // timeout. Its removal changes the world, never the job/input.
                    if(wallSeconds>4f)
                    {
                        wall.SetActive(false); UnityEngine.Object.Destroy(wall); wall=null;
                        wallChecked=true;
                    }
                }
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
                if(winch.Busy && (job.Phase==ExtractionPhase.Hauling || job.Phase==ExtractionPhase.Retensioning)
                    && (winch.Capture().Phase==ExtractionPhase.Hauling || winch.Capture().Phase==ExtractionPhase.Retensioning))
                {
                    Assert.That(rope.ParticleCount, Is.InRange(2, RopeDynamics.Capacity));
                    Assert.That(rope.LastSimulationMilliseconds, Is.GreaterThan(0));
                    ropeCost += rope.LastSimulationMilliseconds; ropeSteps++;
                    // Exclude the attachment's immediate neighbours: marking can
                    // start on a partly embedded surface of the rigid load.
                    if (job.Progress > .6f && frame % 20 == 0 && rope.ParticleCount > 4)
                    {
                        for (int i = 1; i < rope.ParticleCount - 3; i++)
                        {
                            var p = rope.ParticlePosition(i);
                            Assert.That(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z), Is.True);
                            Assert.That(terrain.SignedDensity(p), Is.LessThan(terrain.CellSize * .15f), "The cable must stay in the dug air around route bends.");
                        }
                        ropeChecked = true;
                    }
                    Assert.That(find.GetComponent<Rigidbody>().isKinematic, Is.False);
                    Assert.That(find.GetComponent<Rigidbody>().constraints, Is.EqualTo(RigidbodyConstraints.None));
                    largestRotation=Mathf.Max(largestRotation,Quaternion.Angle(initialRotation,find.GetComponent<Rigidbody>().rotation));
                    if(!pauseChecked && job.Progress>.6f)
                    {
                        var before=find.GetComponent<Rigidbody>().position;
                        var beforeTerrain=terrain.Revision;
                        Vector3 ropeBefore = rope.ParticlePosition(rope.ParticleCount / 2);
                        player.SetApplicationFocus(false);
                        winch.Tick(.02f);
                        for(int i=0;i<10;i++) Physics.Simulate(.02f);
                        Assert.That(find.GetComponent<Rigidbody>().position, Is.EqualTo(before));
                        Assert.That(terrain.Revision, Is.EqualTo(beforeTerrain));
                        Assert.That(rope.ParticlePosition(rope.ParticleCount / 2), Is.EqualTo(ropeBefore), "Paused cable dynamics do not continue under gravity.");
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
            Assert.That(winch.Busy, Is.False, winch.Busy ? DescribeHaul(winch, find, terrain) : "Recovery finished.");
            Assert.That(reloaded, Is.True, player.Feedback + " phases: " + string.Join(",", phases));
            Assert.That(pauseChecked, Is.True);
            Assert.That(wallChecked, Is.True, "A blocked haul keeps pulling and resumes automatically when free.");
            Assert.That(ropeChecked, Is.True);
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
            Assert.That(field.Finds.Count(f => f.Item.InstanceId == identity), Is.EqualTo(1));
            CollectionAssert.AreEquivalent(identities,field.Finds.Select(f=>f.Item.InstanceId), "Recovery never deletes finds in its way.");
            Assert.That(find.Item.InstanceId, Is.EqualTo(identity));
            Assert.That(find.DepthRecorded && find.DiscoveryDepth > 0, Is.True);
            Assert.That(stand.GetPrompt(player), Does.Contain(find.DisplayName));
            Debug.Log($"Recovery fixture worst planning/haul substep: {worstPlanning:F1}/{worstHaul:F1} ms.");
            Debug.Log($"Recovery rope mean simulation: {ropeCost / System.Math.Max(1, ropeSteps):F3} ms.");
        }

        private static string DescribeHaul(SalvageWinch winch, BuriedFind find, TerrainVolume terrain)
        {
            var job = winch.Capture(); var body = find.GetComponent<Rigidbody>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            string fields = string.Join(", ", new[] { "pressureSeconds", "pressureObstacle", "pressurePosition", "contactCount", "stalledSeconds" }
                .Select(name => name + "=" + typeof(SalvageWinch).GetField(name, flags)?.GetValue(winch)));
            var contacts = (ContactPoint[])typeof(SalvageWinch).GetField("loadContacts", flags).GetValue(winch);
            string lastContacts = string.Join("; ", contacts.Take(4).Select(c => {
                var other = c.otherCollider?.GetComponentInParent<BuriedFind>();
                var otherBody = other?.GetComponent<Rigidbody>();
                return $"{c.otherCollider?.name}/{c.thisCollider?.name}: sep {c.separation:F4} normal {c.normal}, kin {otherBody?.isKinematic}, exposure {other?.Exposure}, speed {otherBody?.linearVelocity}, spin {otherBody?.angularVelocity}";
            }));
            return $"Recovery {job.Phase} progress {job.Progress:F2}/{ExtractionSnapshot.Length(job.Route):F2}; position {body.position}, velocity {body.linearVelocity}, spin {body.angularVelocity}; removed {terrain.RemovedVolume:F2}; {fields}; {lastContacts}";
        }
    }

    public sealed class RecoveryContactProbe : MonoBehaviour
    {
        public TerrainVolume Terrain;
        public Collider WatchedCollider;
        public float SimulationSeconds, FirstContactSeconds = -1;
        public Vector3 LastPoint;
        private void OnCollisionEnter(Collision collision) => Record(collision);
        private void OnCollisionStay(Collision collision) => Record(collision);
        private void Record(Collision collision)
        {
            if (WatchedCollider != null ? collision.collider != WatchedCollider
                : collision.collider.GetComponentInParent<TerrainVolume>() != Terrain) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);
                if (contact.separation > .011f) continue;
                if (FirstContactSeconds < 0) FirstContactSeconds = SimulationSeconds;
                LastPoint = contact.point;
            }
        }
    }
}
#endif
