using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed class TerrainIntegrationTests
    {
        private Scene scene;
        private TerrainVolume terrain;
        private FpsPlayer player;
        private float previousTimeScale;
        private CursorLockMode previousCursor;
        private bool previousCursorVisible;
        private InputTestFixture devices;
        private Keyboard keyboard;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousCursor = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            devices = new InputTestFixture();
            devices.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            Time.timeScale = 1;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            GameObject root = scene.GetRootGameObjects()[0];
            terrain = root.GetComponentInChildren<TerrainVolume>();
            // These checks own terrain geometry. Discovery interaction has its own
            // MainGame integration fixture, with the generated finds enabled.
            root.GetComponentInChildren<DiscoveryField>()?.gameObject.SetActive(false);
            player = root.GetComponentInChildren<FpsPlayer>();
            player.enabled = false; // Tick explicitly; real device state must not influence checks.
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
            player.SetApplicationFocus(true);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursor;
            Cursor.visible = previousCursorVisible;
        }

        [Test]
        public void ScoopsChargeOnceMatchCollisionAndRejectStaleHits()
        {
            if (player.ShavingEnabled) player.ToggleAdminShaving();
            player.enabled = true;
            for (int stroke = 0; stroke < 4; stroke++)
            {
                player.ViewCamera.transform.position = new Vector3(-7 + stroke * 4, 1.5f, -7);
                player.ViewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                Physics.SyncTransforms();
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var before), Is.True);
                float charge = player.Battery.Charge;
                int revision = terrain.Revision;
                Assert.That(player.TryDig(), Is.True, "Stroke " + stroke);
                Assert.That(player.Battery.Charge, Is.EqualTo(charge - player.EffectiveDigEnergy).Within(.001f));
                Assert.That(terrain.Revision, Is.EqualTo(revision + 1));
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var after), Is.True);
                Assert.That(after.point.y, Is.LessThan(before.point.y - .05f));
                Assert.That(terrain.TryDig(before, player.EffectiveShovel.Radius), Is.False);
                Assert.That(terrain.Revision, Is.EqualTo(revision + 1));
            }
            player.OpenMenu(PlayerMenu.Pause);
            float pausedCharge = player.Battery.Charge;
            Assert.That(player.TryDig(), Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(pausedCharge));
            player.enabled = false;
        }

        [UnityTest]
        public IEnumerator RepeatedPrimaryActionsCannotBypassACutCooldownOrSpendFuel()
        {
            player.enabled = true;
            player.ViewCamera.transform.position = new Vector3(0, 1.5f, -7);
            player.ViewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            Physics.SyncTransforms();
            Assert.That(player.TryPrimaryAction(), Is.True);
            float charge = player.Battery.Charge; int strokes = player.SuccessfulStrokes;
            Assert.That(player.TryPrimaryAction(), Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes));
            player.enabled = false;
            yield return null;
        }

        [Test]
        public void DetachedColumnDisappearsAcrossChunksInTheSamePaidStroke()
        {
            if (player.ShavingEnabled) player.ToggleAdminShaving();
            // A moat leaves a tall, narrow pillar supported from below. Its crown
            // crosses four chunk seams and lies well outside the final shovel brush.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                    var origin = new Vector3(Mathf.Cos(angle) * 1.2f, 2, Mathf.Sin(angle) * 1.2f);
                for (int cut = 0; cut < 14; cut++)
                {
                    var floor = Hit(origin, Vector3.down);
                    if (floor.point.y < -5.3f) break;
                    Assert.That(terrain.TryDig(floor, 0.8f), Is.True);
                }
            }
            var crown = new Vector3(0, -0.2f, 0);
            Assert.That(terrain.IsSolid(crown), Is.True, "The pillar must survive while its base is attached.");
            var oldCrownHit = Hit(new Vector3(0, 2, 0), Vector3.down);
            player.SelectAdminLevel(6);
            player.ViewCamera.transform.position = new Vector3(1.2f, -4.1f, 0);
            player.ViewCamera.transform.LookAt(new Vector3(0, -4.1f, 0));
            Physics.SyncTransforms();
            int revision = terrain.Revision;
            float energy = player.Battery.Charge, beforeStroke = terrain.RemovedVolume;
            // The weaker top tier may need a few paid strokes to cut the pillar through;
            // whichever stroke severs it must clear the column inside that same stroke.
            int severingStrokes = 0;
            float paidEnergy = 0;
            // Mixed deposits can shed a small chip before the crown itself detaches.
            while (terrain.IsSolid(crown) && severingStrokes < 10)
            {
                beforeStroke = terrain.RemovedVolume;
                Assert.That(player.TryDig(), Is.True);
                paidEnergy += player.EffectiveDigEnergy * EquipmentProgression.MaterialResponse(player.LastDigMaterial).Interval;
                severingStrokes++;
            }
            Assert.That(severingStrokes, Is.LessThanOrEqualTo(10), "Even a mixed hard pillar must yield to the top tier.");
            Assert.That(terrain.LastDetachedSamples, Is.GreaterThan(0));
            Assert.That(terrain.LastDetachedVolume, Is.GreaterThan(0));
            Assert.That(terrain.IsSolid(crown), Is.False);
            Assert.That(Hit(new Vector3(0, 2, 0), Vector3.down).point.y, Is.LessThan(-3),
                "The crown's collider must disappear before the accepted dig returns.");
            Assert.That(player.Battery.Charge, Is.EqualTo(energy - paidEnergy).Within(.001f));
            Assert.That(terrain.Revision, Is.EqualTo(revision + severingStrokes));
            Assert.That(terrain.RemovedVolume - beforeStroke, Is.EqualTo(player.LastScoopVolume).Within(0.001f));
            Assert.That(terrain.GetComponentsInChildren<Rigidbody>(), Is.Empty);
            foreach (var collider in terrain.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled))
                Assert.That(collider.sharedMesh, Is.SameAs(collider.GetComponent<MeshFilter>().sharedMesh));
            float removed = terrain.RemovedVolume;
            Assert.That(terrain.TryDig(oldCrownHit), Is.False, "A cached ray cannot dig the disappeared crown again.");
            Assert.That(terrain.RemovedVolume, Is.EqualTo(removed));
            player.OpenMenu(PlayerMenu.Pause);
            player.ShowAdminMenu();
            player.RequestTerrainReset();
            Assert.That(player.ConfirmTerrainReset(), Is.True);
            Assert.That(terrain.IsSolid(crown), Is.True);
            Assert.That(Hit(new Vector3(0, 2, 0), Vector3.down).point.y, Is.EqualTo(0).Within(0.001f));
        }

        [Test]
        public void FreshSceneHasUntouchedSoilAndNoSurfaceSlabBlockingExcavation()
        {
            Assert.That(terrain.RemovedVolume, Is.Zero);
            Assert.That(terrain.Dimensions, Is.EqualTo(SiteLayout.Size));
            // A 100 m volume only materializes the top layer that owns the ground plane.
            var chunks = SiteLayout.Size / SiteLayout.ChunkSize;
            Assert.That(terrain.ChunkKeyCount, Is.EqualTo(chunks.x * chunks.y * chunks.z));
            Assert.That(terrain.ChunkCount, Is.EqualTo(chunks.x * chunks.z));
            string surfaceLayer = ((terrain.Dimensions.y - 1) / 16).ToString();
            foreach (var chunk in terrain.GetComponentsInChildren<MeshFilter>())
                Assert.That(chunk.name.Split(',')[1], Is.EqualTo(surfaceLayer), "Only the surface layer is materialized.");
            Assert.That(terrain.Revision, Is.Zero);
            foreach (Vector3 origin in new[] { new Vector3(-7, 2, -7), new Vector3(0, 2, 0), new Vector3(7, 2, 7) })
            {
                RaycastHit hit = Hit(origin, Vector3.down);
                Assert.That(hit.collider.GetComponentInParent<TerrainVolume>(), Is.EqualTo(terrain));
                Assert.That(hit.point.y, Is.EqualTo(0).Within(0.001f));
            }
            var anchor = scene.GetRootGameObjects()[0].transform.Find("Surface/ReturnAnchor");
            Assert.That(Physics.CheckCapsule(anchor.position + Vector3.up * 0.35f,
                anchor.position + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.False);
        }

        [Test]
        public void RealDigChargesOnceRejectsStaleHitsAndRespectsPauseDepletionAndBedrock()
        {
            PlacePlayer(new Vector3(0, 0.1f, 0));
            player.ViewCamera.transform.LookAt(new Vector3(0, -1, 0));
            RaycastHit stale = Hit(player.ViewCamera.transform.position, Vector3.down);
            Assert.That(player.TryDig(), Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(100 - player.EffectiveDigEnergy).Within(.001f));
            int remaining = terrain.RemainingCells;
            Assert.That(terrain.TryDig(stale), Is.False);
            Assert.That(terrain.RemainingCells, Is.EqualTo(remaining));
            player.OpenMenu(PlayerMenu.Pause);
            Assert.That(player.TryDig(), Is.False);
            player.CloseMenu();
            player.Battery.TrySpend(player.Battery.Charge);
            Assert.That(player.TryDig(), Is.False);
            Assert.That(terrain.RemainingCells, Is.EqualTo(remaining));
            player.Battery.Recharge();
            PlacePlayer(new Vector3(0, 0.1f, -15));
            player.ViewCamera.transform.LookAt(new Vector3(0, 0.8f, -17));
            Assert.That(player.TryDig(), Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
        }

        [Test]
        public void SeamCutsUpdateRenderAndCollisionLocallyAndSurviveLeavingAndReenabling()
        {
            var filters = terrain.GetComponentsInChildren<MeshFilter>();
            // A materialized chunk on the far side of the ground plane: untouched, still meshed.
            MeshFilter distant = filters.First(f => f.name == $"Chunk 0,{(terrain.Dimensions.y - 1) / 16},11");
            Vector3[] previousVertices = distant.sharedMesh.vertices;
            var above = Seam + Vector3.up * 2;
            RaycastHit top = Hit(above, Vector3.down);
            Assert.That(terrain.TryDig(top), Is.True);
            Assert.That(terrain.LastRebuiltChunkCount, Is.InRange(4, 8));
            Assert.That(terrain.LastRebuiltChunkCount, Is.LessThan(terrain.ChunkCount));
            CollectionAssert.AreEqual(previousVertices, distant.sharedMesh.vertices);
            foreach (var collider in terrain.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled))
                Assert.That(collider.sharedMesh, Is.SameAs(collider.GetComponent<MeshFilter>().sharedMesh));
            RaycastHit floor = Hit(above, Vector3.down);
            // One bite opens usable space: the depth scales with the tool's own radius.
            Assert.That(floor.point.y, Is.InRange(-terrain.DigRadius * 1.2f, -terrain.DigRadius * .4f),
                "A shallow shovel bite still opens usable space.");
            Assert.That(terrain.IsSolid(Seam + new Vector3(0.1f, -0.15f, 0.1f)), Is.False);
            int count = terrain.RemainingCells;
            // A physical walk along the surface must not initialize a new excavation.
            // Keep this terrain fixture's walk clear of the permanent yard winch.
            PlacePlayer(new Vector3(0, .1f, -8));
            for (int i = 0; i < 60; i++) player.Tick(new FpsInputFrame { Move = Vector2.right }, 1f / 60f);
            Assert.That(player.transform.position.x, Is.GreaterThan(3));
            terrain.gameObject.SetActive(false);
            terrain.gameObject.SetActive(true);
            terrain.InitializeSession();
            Physics.SyncTransforms();
            Assert.That(terrain.RemainingCells, Is.EqualTo(count));
            Assert.That(Hit(above, Vector3.down).point.y, Is.EqualTo(floor.point.y).Within(0.001f));
        }

        // A chunk corner beside the site centre, where four chunks meet. The odd chunk count
        // north-south leaves the centre itself mid-chunk.
        private static Vector3 Seam
        {
            get
            {
                float chunk = SiteLayout.ChunkSize * SiteLayout.CellSize;
                return new Vector3(SiteLayout.Origin.x + Mathf.Floor(-SiteLayout.Origin.x / chunk) * chunk, 0,
                    SiteLayout.Origin.z + Mathf.Floor(-SiteLayout.Origin.z / chunk) * chunk);
            }
        }

        [Test]
        public void PlayerCanDescendWalkIntoLateralCutAndFlyBackThroughOwnShaft()
        {
            terrain.DigRadius = 1.1f; // A wider tool makes a body-sized lateral passage in one pass.
            for (int i = 0; i < 4; i++) Assert.That(terrain.TryDig(Hit(new Vector3(0, 2, 0), Vector3.down)), Is.True);
            float floorY = Hit(new Vector3(0, 2, 0), Vector3.down).point.y;
            Vector3 tunnelOrigin = new Vector3(0, floorY + 1.25f, 0);
            for (int i = 0; i < 4; i++) Assert.That(terrain.TryDig(Hit(tunnelOrigin, Vector3.forward)), Is.True);
            PlacePlayer(new Vector3(0, 0.1f, 0));
            for (int i = 0; i < 180; i++) player.Tick(default, 1f / 60f);
            Assert.That(player.transform.position.y, Is.InRange(floorY - 0.5f, floorY + 0.2f));
            for (int i = 0; i < 30; i++) player.Tick(new FpsInputFrame { Move = Vector2.up }, 1f / 60f);
            Assert.That(player.transform.position.z, Is.GreaterThan(1.2f), "Lateral cut must fit the CharacterController.");
            for (int i = 0; i < 30; i++) player.Tick(new FpsInputFrame { Move = Vector2.down }, 1f / 60f);
            int count = terrain.RemainingCells;
            for (int i = 0; i < 100; i++) player.Tick(new FpsInputFrame { JetpackHeld = true }, 1f / 60f);
            Assert.That(player.transform.position.y, Is.GreaterThan(0.5f));
            Assert.That(terrain.RemainingCells, Is.EqualTo(count));
            Assert.That(terrain.IsSolid(new Vector3(0.25f, -0.25f, 0.25f)), Is.False);
        }

        [Test]
        public void PlayerStaysOnTheDrainedSectionAndBelowTheFlightCeiling()
        {
            var bounds = scene.GetRootGameObjects()[0].transform.Find("Environment/Play area bounds");
            var walls = bounds.Cast<Transform>().Where(w => w.name.StartsWith("Wall")).Select(w => new Vector2(w.position.x, w.position.z)).ToArray();
            float feetCeiling = bounds.Find("Flight ceiling").GetComponent<Collider>().bounds.min.y - 1.9f;
            PlacePlayer(new Vector3(0, .2f, -17));
            for (int i = 0; i < 900; i++)
            {
                player.Battery.Recharge();
                player.Tick(new FpsInputFrame { Move = Vector2.left, JetpackHeld = true }, 1f / 60f);
            }
            var position = new Vector2(player.transform.position.x, player.transform.position.z);
            bool inside = false;
            for (int i = 0, j = walls.Length - 1; i < walls.Length; j = i++)
                if ((walls[i].y > position.y) != (walls[j].y > position.y)
                    && position.x < (walls[j].x - walls[i].x) * (position.y - walls[i].y) / (walls[j].y - walls[i].y) + walls[i].x)
                    inside = !inside;
            Assert.That(inside, Is.True, "Invisible walls keep the player on the drained lakebed.");
            Assert.That(walls.Min(w => Vector2.Distance(w, position)), Is.LessThan(3f), "The walk reached the western wall.");
            Assert.That(player.transform.position.y, Is.InRange(feetCeiling - 1, feetCeiling + .1f), "The jetpack stops at the flight ceiling.");
        }

        [Test]
        public void LargeRepeatedCutsExposeButNeverRemoveFloorOrSideBoundaries()
        {
            terrain.DigRadius = 4;
            DigUntilBoundary(new Vector3(0, 2, 0), Vector3.down, -SiteLayout.Extent.y);
            // The retaining walls hold at every depth: shallow soil, mid-shaft and just
            // above the bedrock shelf.
            foreach (float depth in new[] { 30f, 60f, 90f })
            foreach (Vector3 direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                DigUntilBoundary(new Vector3(0, -depth, 0), direction, Mathf.Abs(Vector3.Dot(direction, SiteLayout.Extent)) * .5f);
        }

        [Test]
        public void RepresentativeAcceptedCutsReportMeshAndColliderUpdateCosts()
        {
            var timings = new List<double>();
            var dirtyCounts = new List<int>();
            foreach (float coordinate in new[] { -6f, -2f, 2f, 6f })
            for (int i = 0; i < 5; i++)
            {
                Assert.That(terrain.TryDig(Hit(new Vector3(coordinate, 2, coordinate), Vector3.down)), Is.True);
                timings.Add(terrain.LastDigMilliseconds);
                dirtyCounts.Add(terrain.LastRebuiltChunkCount);
            }
            TestContext.WriteLine($"Smooth terrain: {timings.Count} accepted cuts, mean {timings.Average():F3} ms, max {timings.Max():F3} ms; "
                + $"rebuilt {dirtyCounts.Min()}-{dirtyCounts.Max()} of {terrain.ChunkCount} chunks per cut (includes collision cooking).");
            Assert.That(dirtyCounts.Max(), Is.LessThan(terrain.ChunkCount));
        }

        [Test]
        public void AdminShortcutsRemainGatedByPauseAndFocus()
        {
            Assert.That(player.AdminAvailable, Is.True);
            Assert.That(player.HasAdminOverrides, Is.False);
            player.Battery.TrySpend(20);
            player.Tick(new FpsInputFrame { RefillPressed = true }, 0.01f);
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            player.OpenMenu(PlayerMenu.Pause);
            player.Tick(new FpsInputFrame { AdminLevel = 6, DigHeld = true }, 0.01f);
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(1));
            Assert.That(terrain.Revision, Is.Zero);
            player.SetApplicationFocus(false);
            Assert.That(player.SelectAdminLevel(6), Is.False);
            player.ToggleAdminUnlimitedBattery();
            Assert.That(player.UnlimitedBattery, Is.False);
            Assert.That(player.ConfirmTerrainReset(), Is.False);
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RealAdminChordsWorkOnNormalLaunchInFlightAndInsideAdminMenu()
        {
            Assert.That(player.AdminAvailable, Is.True);
            PlacePlayer(new Vector3(0, 5, 0));
            player.enabled = true;
            player.SetApplicationFocus(true);
            player.Battery.TrySpend(50);
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.LeftShift, Key.R));
            yield return null;
            yield return null;
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightCtrl, Key.RightShift, Key.Numpad6));
            yield return null;
            yield return null;
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(6));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(0.35f);
            Assert.That(player.IsJetpackActive, Is.True);
            float altitude = player.transform.position.y;
            player.Battery.TrySpend(25);
            // Submit each chord as one keyboard state. Several queued single-key
            // writes would copy stale state and inadvertently resurrect released keys.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.LeftShift, Key.Space, Key.R, Key.Digit2));
            yield return null;
            yield return null;
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(2));
            Assert.That(player.Battery.Charge, Is.GreaterThan(98));
            Assert.That(player.IsJetpackActive, Is.True, "Refill and strength must not disarm held Space.");
            Assert.That(player.transform.position.y, Is.GreaterThan(altitude));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.45f);
            Assert.That(player.VerticalSpeed, Is.LessThan(0));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            yield return null;
            Assert.That(player.IsJetpackActive, Is.True);
            Assert.That(player.VerticalSpeed, Is.GreaterThan(0));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.LeftShift, Key.F10));
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeveloperAdmin));
            player.Battery.TrySpend(10);
            Vector3 pausedPosition = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.LeftShift, Key.Numpad4, Key.R));
            yield return null;
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeveloperAdmin));
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(4));
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(player.transform.position, Is.EqualTo(pausedPosition));
            Assert.That(terrain.Revision, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AdminMenuButtonsChooseStrengthAndKeepResetConfirmationSeparate()
        {
            player.OpenMenu(PlayerMenu.Pause);
            yield return null;
            MenuTestUI.Click(MenuTestUI.Button(player, "Developer admin"));
            yield return null;
            MenuTestUI.Click(MenuTestUI.View(player).CurrentScreen.Query<UnityEngine.UIElements.Button>().ToList().Single(b => b.name.StartsWith("Shovel 6")));
            yield return null;
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(6));
            // The dev sliders must open on the selected shovel, and each shovel keeps the
            // values calibrated for it while the session runs.
            var root = MenuTestUI.View(player).Root;
            Assert.That(MenuTestUI.Text(player, "adminBiteValue"), Is.EqualTo(player.EffectiveShovel.Radius.ToString("0.000") + " m"),
                "Sliders open on the selected shovel's values, not zero.");
            float shovelSixBite = player.AdminTuningValue(6, FpsPlayer.TuningDial.Bite);
            root.Q<UnityEngine.UIElements.SliderInt>("adminBite").value = 800;
            Assert.That(player.AdminTuningValue(6, FpsPlayer.TuningDial.Bite), Is.EqualTo(.8f).Within(.001f));
            Assert.That(MenuTestUI.Text(player, "adminBiteValue"), Is.EqualTo("0.800 m"),
                "The value label follows the drag, not only a rebuild.");
            MenuTestUI.Click(MenuTestUI.View(player).CurrentScreen.Query<UnityEngine.UIElements.Button>().ToList().Single(b => b.name.StartsWith("Shovel 1")));
            yield return null;
            Assert.That(player.AdminTuningValue(1, FpsPlayer.TuningDial.Bite), Is.EqualTo(EquipmentProgression.ToolProfiles()[0].Radius).Within(.001f),
                "Another shovel keeps the authored numbers.");
            Assert.That(MenuTestUI.Text(player, "adminBiteValue"), Is.EqualTo(player.EffectiveShovel.Radius.ToString("0.000") + " m"));
            MenuTestUI.Click(MenuTestUI.View(player).CurrentScreen.Query<UnityEngine.UIElements.Button>().ToList().Single(b => b.name.StartsWith("Shovel 6")));
            yield return null;
            Assert.That(player.AdminTuningValue(6, FpsPlayer.TuningDial.Bite), Is.EqualTo(.8f).Within(.001f),
                "Returning to a shovel shows the values calibrated for it.");
            Assert.That(MenuTestUI.Text(player, "adminBiteValue"), Is.EqualTo(.8f.ToString("0.000") + " m"));
            player.SetAdminTuning(FpsPlayer.TuningDial.Bite, shovelSixBite);
            MenuTestUI.Click(MenuTestUI.Button(player, "Reset ground..."));
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.ConfirmTerrainReset));
            MenuTestUI.Click(MenuTestUI.Button(player, "Keep excavation"));
            yield return null;
            Assert.That(player.Menu, Is.EqualTo(PlayerMenu.DeveloperAdmin));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
        }

        [Test]
        public void AdminLevelsUseAutomaticMotionAndNeverChangeOwnedProgression()
        {
            if (player.ShavingEnabled) player.ToggleAdminShaving();
            Assert.That(player.AdminAvailable, Is.True);
            float previous = 0;
            for (int level = 1; level <= EquipmentProgression.LevelCount; level++)
            {
                Assert.That(player.SelectAdminLevel(level), Is.True);
                Assert.That(player.EffectiveShovel.Radius, Is.EqualTo(EquipmentProgression.ToolProfiles()[level - 1].Radius),
                    "MainGame must use the current shovel tuning, including serialized scene profiles.");
                terrain.ResetExcavation();
                float x = 0;
                PlacePlayer(new Vector3(x, 0.1f, 0));
                player.ViewCamera.transform.LookAt(new Vector3(x, -1, 0));
                float energy = player.Battery.Charge;
                Assert.That(player.TryDig(), Is.True);
                Assert.That(player.Battery.Charge, Is.EqualTo(energy - player.EffectiveDigEnergy).Within(.001f));
                float rate = player.LastScoopVolume / player.LastDigInterval;
                if (previous > 0) Assert.That(rate, Is.GreaterThan(previous));
                Assert.That(player.ShavingEnabled, Is.EqualTo(EquipmentProgression.UsesDrill(level)));
                // The starter is deliberately weak (048 follow-up); the ceiling still
                // guards against an explosive late-tier bite.
                Assert.That(player.LastScoopVolume, Is.GreaterThan(.005f));
                previous = rate;
                Assert.That(player.Shovel.Level, Is.EqualTo(1));
            }
            Assert.That(player.SelectAdminLevel(0), Is.False);
            Assert.That(player.SelectAdminLevel(EquipmentProgression.LevelCount + 1), Is.False);
            player.AdminReturnToSurface();
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(EquipmentProgression.LevelCount));
            Assert.That(player.ExcavatedVolume, Is.GreaterThan(0));
            player.RestoreAdminOverrides();
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(1));
        }

        [Test]
        public void OwnedUpgradesExtendRealDigReachAtEveryLevelWithoutAdminOverrides()
        {
            float previous = 0;
            for (int level = 1; level <= EquipmentProgression.LevelCount; level++)
            {
                if (level > 1) Assert.That(player.Shovel.TryUpgradeTo(level), Is.True);
                Assert.That(player.HasAdminOverrides, Is.False);
                float reach = player.EffectiveDigReach;
                Assert.That(reach, Is.GreaterThan(previous));
                terrain.ResetExcavation();
                float x = 0;
                PlacePlayer(new Vector3(x, reach + 0.15f - 1.6f, 0));
                player.ViewCamera.transform.LookAt(new Vector3(x, -1, 0));
                float charge = player.Battery.Charge;
                Assert.That(player.TryDig(), Is.False, $"Level {level} must respect its maximum reach.");
                Assert.That(player.Battery.Charge, Is.EqualTo(charge));
                player.RefreshTargetPrompt();
                Assert.That(player.TargetPrompt, Is.Empty, "Out-of-range digging remains silent.");
                PlacePlayer(new Vector3(x, reach - 0.1f - 1.6f, 0));
                Assert.That(player.TryDig(), Is.True, $"Level {level} must dig at its advertised range.");
                Assert.That(player.Battery.Charge, Is.EqualTo(charge - player.EffectiveDigEnergy).Within(.001f));
                previous = reach;
            }
            Assert.That(player.EffectiveDigReach, Is.EqualTo(player.Tuning.DigReach + EquipmentProgression.ToolProfiles().Last().ReachBonus));
        }

        [UnityTest]
        public IEnumerator AttachedRemnantClearsPlayerTraversalAndCollisionInOnePaidStroke()
        {
            if (player.ShavingEnabled) player.ToggleAdminShaving();
            InstallExcavatedSpikeFixture();
            var tip = Seam + new Vector3(0, -1.75f, 0);
            var feet = Seam + new Vector3(0, -1.94f, -1);
            var motor = player.GetComponent<CharacterController>();
            bool Sweep() => Physics.CapsuleCast(feet + Vector3.up * motor.radius,
                feet + Vector3.up * (motor.height - motor.radius), motor.radius, Vector3.forward,
                out _, 2, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Assert.That(terrain.IsSolid(tip), Is.True);
            Assert.That(Sweep(), Is.True, "The actual player-sized sweep initially snags the attached spike.");
            var oldTipHit = Hit(Seam + new Vector3(0, -0.5f, 0), Vector3.down);
            Bounds notification = default;
            int notifications = 0;
            terrain.Changed += bounds => { notification = bounds; notifications++; };
            // Stay just outside the spike and cut its attachment with the tuned
            // smaller shovel; the old 0.5 m offset no longer reaches the neck.
            float cutOffset = player.EffectiveShovel.Radius + .09f;
            player.ViewCamera.transform.position = Seam + new Vector3(cutOffset, -0.5f, 0);
            player.ViewCamera.transform.LookAt(Seam + new Vector3(cutOffset, -2, 0));
            float charge = player.Battery.Charge;
            Assert.That(player.TryDig(), Is.True);
            Assert.That(terrain.LastRemnantSamples, Is.GreaterThan(0));
            Assert.That(terrain.LastRemnantVolume, Is.GreaterThan(0));
            Assert.That(terrain.IsSolid(tip), Is.False);
            Assert.That(Sweep(), Is.False, "Collision clears before TryDig returns, across the four horizontal chunk seams.");
            Assert.That(terrain.LastRebuiltChunkCount, Is.InRange(4, 12));
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(notification.Contains(tip), Is.True, "Discovery exposure receives the cleared remnant bounds.");
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - player.EffectiveDigEnergy
                * EquipmentProgression.MaterialResponse(player.LastDigMaterial).Interval).Within(.001f));
            Assert.That(terrain.Revision, Is.EqualTo(1));
            Assert.That(player.LastScoopVolume, Is.EqualTo(terrain.RemovedVolume).Within(0.00001f));
            Assert.That(terrain.TryDig(oldTipHit), Is.False);
            Assert.That(terrain.Revision, Is.EqualTo(1));
            foreach (var collider in terrain.GetComponentsInChildren<MeshCollider>().Where(c => c.enabled))
                Assert.That(collider.sharedMesh, Is.SameAs(collider.GetComponent<MeshFilter>().sharedMesh));

            float chargeAfterCut = player.Battery.Charge;
            player.ViewCamera.transform.localPosition = Vector3.up * 1.6f;
            player.ViewCamera.transform.localRotation = Quaternion.identity;
            player.transform.rotation = Quaternion.identity;
            PlacePlayer(feet);
            yield return null;
            for (int i = 0; i < 60; i++) player.Tick(new FpsInputFrame { Move = Vector2.up }, 1f / 60);
            Assert.That(player.transform.position.z, Is.GreaterThan(Seam.z + 0.8f), "Walk across the former spike without a jump or jetpack.");
            Assert.That(player.FeetPosition.y, Is.InRange(-2.3f, -1.8f));
            Assert.That(player.Battery.Charge, Is.EqualTo(chargeAfterCut).Within(.001f));
        }

        private void InstallExcavatedSpikeFixture()
        {
            // Controlled previously-excavated space in the real MainGame terrain,
            // with a 32 cm wide, 37.5 cm tall attached spike on a flat floor. No
            // runtime authoring API, substitute mesh, or separate collider is used.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var grid = (ExcavationGrid)typeof(TerrainVolume).GetField("grid", flags).GetValue(terrain);
            var snapshot = grid.Capture();
            var samples = snapshot.Density.ToArray();
            snapshot.LowestCarvedY = terrain.Dimensions.y - 16;
            // Centred on a chunk corner, so the cut crosses the four horizontal chunk seams.
            var centre = Vector3Int.RoundToInt((Seam - terrain.transform.position) / terrain.CellSize);
            int stride = terrain.Dimensions.x + 1, plane = stride * (terrain.Dimensions.y + 1);
            for (int z = centre.z - 18; z <= centre.z + 18; z++) for (int y = terrain.Dimensions.y - 20; y <= terrain.Dimensions.y; y++) for (int x = centre.x - 18; x <= centre.x + 18; x++)
            {
                Vector3 p = terrain.transform.TransformPoint(new Vector3(x, y, z) * terrain.CellSize) - Seam;
                float cavity = Mathf.Max(Mathf.Abs(p.x) - 1.8f, Mathf.Abs(p.z) - 1.8f, -2 - p.y);
                float h = p.y + 2;
                float spike = Mathf.Min(0.16f - Mathf.Abs(p.x), 0.16f - Mathf.Abs(p.z), 0.375f - h, h + 0.05f);
                int index = x + y * stride + z * plane;
                samples[index] = Mathf.Clamp(Mathf.Min(samples[index], Mathf.Max(cavity, spike)), -0.25f, 0.25f);
            }
            snapshot.Density = DensitySnapshot.CopyFrom(samples);
            grid.Restore(snapshot);
            // Chunks are materialized on demand now, so refresh every key the authored
            // cavity can reach instead of trusting what happened to exist already.
            var refresh = typeof(TerrainVolume).GetMethod("Refresh", flags);
            for (int keyY = (terrain.Dimensions.y - 32) / 16; keyY <= (terrain.Dimensions.y - 1) / 16; keyY++)
            for (int keyZ = (centre.z - 18) / 16; keyZ <= (centre.z + 18) / 16; keyZ++)
            for (int keyX = (centre.x - 18) / 16; keyX <= (centre.x + 18) / 16; keyX++)
                refresh.Invoke(terrain, new object[] { new Vector3Int(keyX, keyY, keyZ) });
            Physics.SyncTransforms();
        }

        [Test]
        public void UnlimitedBatteryCoversDigAndFlightAndRestoresNormalRules()
        {
            PlacePlayer(new Vector3(0, 0.1f, 0));
            player.ViewCamera.transform.LookAt(Vector3.down);
            player.Battery.TrySpend(100);
            player.SelectAdminLevel(3);
            player.ToggleAdminUnlimitedBattery();
            Assert.That(player.TryDig(), Is.True);
            PlacePlayer(new Vector3(0, 3, 0));
            player.Tick(new FpsInputFrame { JetpackHeld = true }, 0.3f);
            Assert.That(player.IsJetpackActive, Is.True);
            Assert.That(player.Battery.Charge, Is.Zero);
            player.RestoreAdminOverrides();
            Assert.That(player.UnlimitedBattery || player.HasAdminOverrides, Is.False);
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(player.Shovel.Level));
            player.Tick(new FpsInputFrame { JetpackHeld = true }, 0.1f);
            Assert.That(player.IsJetpackActive, Is.False);
            Assert.That(player.TryDig(), Is.False);
            player.RefillAdminBattery();
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
        }

        [Test]
        public void FreshScoopDepthVariesAndResetReplaysTheSameShapes()
        {
            var depths = new List<float>();
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < 6; i++)
                {
                    var origin = new Vector3(-8 + i * 3, 2, 0);
                    Assert.That(terrain.TryDig(Hit(origin, Vector3.down), 0.85f), Is.True);
                    float depth = -Hit(origin, Vector3.down).point.y;
                    Assert.That(depth, Is.InRange(0.5f, 1.0f));
                    if (pass == 0) depths.Add(depth);
                    else Assert.That(depth, Is.EqualTo(depths[i]).Within(0.0001f));
                }
                if (pass == 0) terrain.ResetExcavation();
            }
            Assert.That(depths.Max() - depths.Min(), Is.GreaterThan(0.02f));
        }

        [Test]
        public void AdminResetRequiresConfirmationAndReturnsPlayerBeforeFillingTerrain()
        {
            Assert.That(player.ConfirmTerrainReset(), Is.False);
            Assert.That(player.AdminAvailable, Is.True);
            player.SelectAdminLevel(3);
            Assert.That(terrain.TryDig(Hit(new Vector3(0, 2, 0), Vector3.down)), Is.True);
            player.Battery.TrySpend(20);
            PlacePlayer(new Vector3(0, -0.2f, 0));
            player.OpenMenu(PlayerMenu.Pause);
            player.ShowAdminMenu();
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(3), "Reopening admin preserves selection.");
            Assert.That(player.ConfirmTerrainReset(), Is.False);
            player.RequestTerrainReset();
            player.CancelTerrainReset();
            Assert.That(terrain.Revision, Is.EqualTo(1));
            player.RequestTerrainReset();
            Assert.That(player.ConfirmTerrainReset(), Is.True);
            Assert.That(terrain.Revision, Is.Zero);
            Assert.That(terrain.RemovedVolume, Is.Zero);
            Assert.That(Hit(new Vector3(0, 2, 0), Vector3.down).point.y, Is.EqualTo(0).Within(0.001f));
            var anchor = scene.GetRootGameObjects()[0].transform.Find("Surface/ReturnAnchor").position;
            Assert.That(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(anchor.x, anchor.z)), Is.LessThan(1f));
            Assert.That(player.Battery.Charge, Is.EqualTo(100));
            Assert.That(player.EffectiveShovelLevel, Is.EqualTo(3));
            Assert.That(player.Shovel.Level, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CrouchTraversesSupportedTerrainAndLowTunnelsAcrossFrameRates()
        {
            yield return CrouchTerrainFixture.Prepare(terrain);
            var motor = player.GetComponent<CharacterController>();
            foreach (int fps in new[] { 30, 60, 144 })
            foreach (bool crouched in new[] { false, true })
            {
                float speed = crouched ? 1.4f : 4f;
                var walk = new FpsInputFrame { Move = Vector2.up, CrouchHeld = crouched };
                CrouchTerrainFixture.Place(player, new Vector3(-6, -1.9f, -6), crouched ? 1 : 0);
                CrouchTerrainFixture.Advance(player, 6f / speed, walk, fps);
                Assert.That(player.transform.position.z, Is.EqualTo(0).Within(0.03f));
                Assert.That(motor.isGrounded, Is.True, "Supported ledge across multiple chunk seams.");
                CrouchTerrainFixture.Place(player, new Vector3(-2, -3.9f, -7), crouched ? 1 : 0);
                CrouchTerrainFixture.Advance(player, 7f / speed, walk, fps);
                Assert.That(player.transform.position.z, Is.GreaterThan(-0.1f), "Ramp must remain traversable in either stance.");
                Assert.That(player.transform.position.y, Is.InRange(-1.1f, -0.85f));
                CrouchTerrainFixture.Place(player, new Vector3(2, -2.9f, -6.5f), crouched ? 1 : 0);
                CrouchTerrainFixture.Advance(player, 6f / speed, walk, fps);
                Assert.That(player.transform.position.z, Is.EqualTo(-0.5f).Within(0.03f));
                Assert.That(motor.isGrounded, Is.True, "0.2 m supported steps do not become stance gates.");
                CrouchTerrainFixture.Place(player, new Vector3(6, -2.9f, -7), crouched ? 1 : 0);
                CrouchTerrainFixture.Advance(player, 7.5f / speed, walk, fps);
                Assert.That(player.transform.position.z, crouched ? Is.GreaterThan(0.1f) : Is.LessThan(-2.2f),
                    "The rounded low-tunnel mouth must be traversable while crouched.");
                if (crouched)
                {
                    float entered = player.transform.position.z;
                    CrouchTerrainFixture.Advance(player, 0.5f, walk, fps);
                    Assert.That(player.transform.position.z - entered, Is.EqualTo(0.7f).Within(0.03f));
                }
                CrouchTerrainFixture.Place(player, new Vector3(6, -2.9f, -3), crouched ? 1 : 0);
                CrouchTerrainFixture.Advance(player, 2f / speed, new FpsInputFrame { Move = Vector2.right, CrouchHeld = crouched }, fps);
                Assert.That(player.transform.position.x, Is.EqualTo(8).Within(0.03f));
            }
            CrouchTerrainFixture.Place(player, new Vector3(-6, -1.9f, 0), 1);
            CrouchTerrainFixture.Advance(player, 0.2f, new FpsInputFrame { CrouchHeld = true, Move = Vector2.right }, 60);
            Assert.That(player.transform.position.x, Is.EqualTo(-5.72f).Within(0.01f));
            Assert.That(motor.isGrounded, Is.True, "Small correction remains on the ledge.");
            CrouchTerrainFixture.Advance(player, 1f, new FpsInputFrame { CrouchHeld = true, Move = Vector2.right }, 60);
            Assert.That(player.transform.position.y, Is.LessThan(-2.2f), "Crouch supplies no automatic cliff guard.");
        }

        [UnityTest]
        public IEnumerator CrouchedHeldDiggingClearsTheActualRoofAndRescueRestoresStanding()
        {
            yield return CrouchTerrainFixture.Prepare(terrain);
            CrouchTerrainFixture.Place(player, new Vector3(6, -2.9f, 0), 1);
            player.Tick(default, 1f / 60);
            Assert.That(player.StandBlocked, Is.True);
            float before = terrain.RemovedVolume;
            player.Tick(new FpsInputFrame { Look = new Vector2(0, 700) }, 1f / 60);
            // The crouch/rescue claim is independent of tool power: the top tier clears a
            // bite wider than the standing capsule, while the starter would need an aimed
            // sweep to open the same roof (048 follow-up).
            Assert.That(player.SelectAdminLevel(6), Is.True);
            CrouchTerrainFixture.Advance(player, 8f, new FpsInputFrame { DigHeld = true }, 60);
            Assert.That(terrain.RemovedVolume, Is.GreaterThan(before));
            Assert.That(player.SuccessfulStrokes, Is.GreaterThan(0));
            Assert.That(player.CrouchAmount, Is.Zero, "Standing becomes safe after real excavation removes the roof. "
                + $"strokes={player.SuccessfulStrokes}, removed={terrain.RemovedVolume - before:F2}, blocked={player.StandBlocked}");
            Assert.That(player.StandBlocked, Is.False);
            player.Tick(new FpsInputFrame { CrouchHeld = true }, 0.2f);
            player.AdminReturnToSurface();
            Assert.That(player.CrouchAmount, Is.Zero);
            player.Tick(new FpsInputFrame { CrouchHeld = true, Move = Vector2.right }, 0.02f);
            Assert.That(player.CrouchAmount, Is.GreaterThan(0));
        }

        private void DigUntilBoundary(Vector3 origin, Vector3 direction, float expectedCoordinate)
        {
            RaycastHit hit = default;
            // A 4 m cut can leave a sliver whose surface-net face lies about a cell outside the
            // density surface, where TryDig rejects the hit as stale air. Like a player, the bore
            // re-aims a few centimetres aside instead of treating that as a failed boundary.
            var side = Vector3.Cross(direction, Mathf.Abs(direction.y) > .5f ? Vector3.right : Vector3.up).normalized;
            var aims = new[] { Vector3.zero, side * .3f, -side * .3f, Vector3.Cross(direction, side) * .3f };
            for (int i = 0; i < 64; i++)
            {
                bool cut = false;
                foreach (var aim in aims)
                {
                    hit = Hit(origin + aim, direction);
                    if (hit.collider.GetComponent<PermanentTerrainBoundary>() != null || (cut = terrain.TryDig(hit))) break;
                }
                if (hit.collider.GetComponent<PermanentTerrainBoundary>() != null) break;
                Assert.That(cut, Is.True, $"Cut {i} toward {direction} at {hit.point} on {hit.collider.name}");
                // Follow the bore so a 100 m descent stays inside the 40 m probe range.
                origin = hit.point - direction * 1.5f;
            }
            var boundary = hit.collider.GetComponent<PermanentTerrainBoundary>();
            Assert.That(boundary, Is.Not.Null);
            float coordinate = direction == Vector3.down ? hit.point.y : Mathf.Abs(Vector3.Dot(hit.point, direction));
            Assert.That(coordinate, Is.EqualTo(expectedCoordinate).Within(0.01f));
            int count = terrain.RemainingCells;
            for (int i = 0; i < 5; i++)
            {
                Assert.That(boundary.TryDig(hit), Is.False);
                Assert.That(terrain.TryDig(hit), Is.False);
            }
            Assert.That(terrain.RemainingCells, Is.EqualTo(count));
            Assert.That(boundary.GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator MainGameExcavatesThroughFormerFloorAndStopsAtOneHundredMetres()
        {
            Assert.That(terrain.SurfaceHeight, Is.Zero);
            Assert.That(terrain.Dimensions, Is.EqualTo(SiteLayout.Size));
            int strokes = 0, busiest = 0;
            double maximumMilliseconds = 0;
            var ray = new Vector3(0, 2, 0);
            var hit = Hit(ray, Vector3.down);
            while (hit.collider.GetComponentInParent<TerrainVolume>() == terrain && strokes < 600)
            {
                // This guard is about the bedrock floor, not tool strength: a fixed bore
                // keeps it independent of shovel tuning.
                Assert.That(terrain.TryDig(hit, 0.8f), Is.True);
                maximumMilliseconds = System.Math.Max(maximumMilliseconds, terrain.LastDigMilliseconds);
                busiest = System.Math.Max(busiest, terrain.LastRebuiltChunkCount);
                strokes++;
                // Follow the shaft down: the probe range is 40 m and the site is 100 m deep.
                ray = hit.point + Vector3.up * 1.5f;
                hit = Hit(ray, Vector3.down);
                if (strokes % 12 == 0) yield return null;
            }
            Assert.That(strokes, Is.InRange(30, 599));
            Assert.That(hit.collider.GetComponent<PermanentTerrainBoundary>(), Is.Not.Null);
            Assert.That(hit.point.y, Is.EqualTo(-SiteLayout.Extent.y).Within(.02f));
            Assert.That(terrain.IsSolid(new Vector3(0, -40, 0)), Is.False);
            Assert.That(terrain.IsSolid(new Vector3(0, -99, 0)), Is.False);
            Assert.That(terrain.TryDig(hit), Is.False);
            var root = terrain.transform.parent;
            foreach (string side in new[] { "West", "East", "North", "South" })
            {
                var bounds = root.Find("Bedrock/" + side).GetComponent<Collider>().bounds;
                Assert.That(bounds.min.y, Is.EqualTo(-SiteLayout.Extent.y).Within(.001f));
                Assert.That(bounds.max.y, Is.EqualTo(SiteLayout.RimBottom).Within(.001f));
            }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var snapshot = terrain.Capture(); timer.Stop();
            Assert.That(snapshot.Density.Length, Is.EqualTo((SiteLayout.Size.x + 1) * (SiteLayout.Size.y + 1) * (SiteLayout.Size.z + 1)));
            Assert.That(busiest, Is.InRange(1, 16), "A narrow deep cut rebuilds only the chunks around it.");
            // The hole must survive a checkpoint restore at the new depth.
            yield return terrain.Restore(snapshot, terrain.ExcavationSeed);
            RaycastHit restored = Hit(ray, Vector3.down);
            Assert.That(restored.point.y, Is.EqualTo(hit.point.y).Within(.02f));
            TestContext.WriteLine($"100 m MainGame: {strokes} largest-shovel cuts; slowest cut {maximumMilliseconds:F2} ms; "
                + $"busiest {busiest} chunks; capture {timer.Elapsed.TotalMilliseconds:F2} ms.");
        }

        [UnityTest]
        public IEnumerator ToolAdaptsToActualContactWithMatchingFuelFeedbackAndCollision()
        {
            var snapshot = terrain.Capture();
            byte[] ids = snapshot.Materials.ToArray();
            int stride = snapshot.Size.x + 1, plane = stride * (snapshot.Size.y + 1);
            // Shallow strips exercise the real MainGame player without tunnelling past
            // the find population or depending on one generated deposit's location.
            for (int z = 0; z <= snapshot.Size.z; z++)
            for (int y = snapshot.Size.y - 4; y <= snapshot.Size.y; y++)
            for (int x = 0; x <= snapshot.Size.x; x++)
                ids[x + y * stride + z * plane] = (byte)Mathf.Min(2, x * 3 / snapshot.Size.x);
            snapshot.Materials = TerrainMaterialSnapshot.CopyFrom(ids);
            yield return terrain.Restore(snapshot, terrain.ExcavationSeed);
            int notifications = 0;
            TerrainCutFeedback feedback = default;
            terrain.ToolCut += value => { notifications++; feedback = value; };
            for (int i = 0; i < 3; i++)
            {
                // Well inside each third of the grid and inside the plot.
                player.ViewCamera.transform.position = new Vector3(-10 + i * 10, 1.5f, -4);
                player.ViewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                Physics.SyncTransforms();
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var hit), Is.True);
                var material = (TerrainMaterialId)i;
                Assert.That(terrain.ToolMaterialAt(hit), Is.EqualTo(material));
                float charge = player.Battery.Charge;
                Assert.That(player.TryDig(), Is.True);
                float scale = EquipmentProgression.MaterialResponse(material).Interval;
                Assert.That(player.Battery.Charge, Is.EqualTo(charge - player.EffectiveDigEnergy * scale).Within(.001f));
                Assert.That(player.LastDigInterval, Is.EqualTo(player.EffectiveDigInterval * scale).Within(.00001f));
                Assert.That(player.LastDigMaterial, Is.EqualTo(material));
                Assert.That(notifications, Is.EqualTo(i + 1));
                Assert.That(feedback.Material, Is.EqualTo(material));
                Assert.That(feedback.RemovedVolume, Is.EqualTo(terrain.LastRemovedVolume).And.GreaterThan(0));
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var after), Is.True);
                Assert.That(after.point.y, Is.LessThan(hit.point.y));
                Assert.That(terrain.TryToolCut(hit, player.EffectiveShovel.Radius, true), Is.False);
                Assert.That(notifications, Is.EqualTo(i + 1), "Rejected stale cuts publish nothing.");
            }
            Vector3 cameraPosition = player.ViewCamera.transform.position;
            Quaternion cameraRotation = player.ViewCamera.transform.rotation;
            Assert.That(player.TryPrimaryAction(), Is.True);
            int strokes = player.SuccessfulStrokes;
            player.Tick(default, player.EffectiveDigInterval * 1.1f);
            player.ViewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            Assert.That(player.TryPrimaryAction(), Is.False, "Rock cadence must outlast the soil interval.");
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes));
            player.Tick(default, player.EffectiveDigInterval * .4f);
            player.ViewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            Assert.That(player.TryPrimaryAction(), Is.True, "The next rock cut must resume automatically after its interval.");
            float paid = player.Battery.Charge;
            player.OpenMenu(PlayerMenu.Pause);
            Assert.That(player.TryDig(), Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(paid));
            Assert.That(notifications, Is.EqualTo(5));
        }

        private void PlacePlayer(Vector3 position)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            // Fixture moves are relative to +z, independent of the authored spawn heading.
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            motor.enabled = true;
            Physics.SyncTransforms();
        }

        private static RaycastHit Hit(Vector3 origin, Vector3 direction)
        {
            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(origin, direction, out RaycastHit hit, 40, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore), Is.True, $"Missing collision at {origin} toward {direction}.");
            return hit;
        }
    }
}
#endif
