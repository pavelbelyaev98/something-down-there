#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace SomethingDownThere.Tests
{
    public sealed class DiscoveryIntegrationTests
    {
        private Scene scene;
        private TerrainVolume terrain;
        private FpsPlayer player;
        private DiscoveryField field;
        private InputTestFixture devices;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldTimeScale;
        private CursorLockMode oldCursor;
        private bool oldCursorVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldTimeScale = Time.timeScale;
            oldCursor = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            Time.timeScale = 1;
            devices = new InputTestFixture();
            devices.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            SceneManager.sceneLoaded += TestInputPreferences.ConfigureLayerFinds;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MainGame.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            SceneManager.sceneLoaded -= TestInputPreferences.ConfigureLayerFinds;
            scene = SceneManager.GetSceneByPath("Assets/Scenes/MainGame.unity");
            var root = scene.GetRootGameObjects()[0];
            terrain = root.GetComponentInChildren<TerrainVolume>();
            field = root.GetComponentInChildren<DiscoveryField>();
            player = root.GetComponentInChildren<FpsPlayer>();
            player.enabled = false;
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
            TestInputPreferences.RestoreLayerFixture(field);
            // Entering Play Mode can deliver its native focus notification on this frame.
            player.SetApplicationFocus(true);
            if (player.IsMenuOpen) player.CloseMenu();
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            devices.TearDown();
            Time.timeScale = oldTimeScale;
            Cursor.lockState = oldCursor;
            Cursor.visible = oldCursorVisible;
        }

        [Explicit("Slow end-to-end check; runs with tools/test-changed.ps1 -Full.")]
        [TestCase(11.45f, 2f)] [TestCase(3f, 10f)] [TestCase(18f, 17f)] [TestCase(11.45f, 16f)]
        public void DefaultShovelRevealsMultipleShallowFindsInAnUninformedSmallPatch(float x, float z)
        {
            TestInputPreferences.RestoreGeneratedPopulation(field); // measures the real layout, deep finds included
            Assert.That(player.EffectiveShovel.Radius, Is.EqualTo(EquipmentProgression.ToolProfiles()[0].Radius).Within(.00001f));
            int strokes = 0, firstEncounter = 0;
            // A fixed approximately 2 x 2 m excavation, independent of hidden find
            // positions. Shallow shaves get the same powered-time budget as scoops.
            for (int pass = 0; pass < 50; pass++)
                for (int iz = 0; iz < 3; iz++)
                    for (int ix = 0; ix < 3; ix++)
                    {
                        var origin = terrain.transform.TransformPoint(new Vector3(x + ix * .65f, terrain.Dimensions.y * terrain.CellSize + 1.25f, z + iz * .65f));
                        var ground = Physics.RaycastAll(origin, Vector3.down, 4)
                            .Where(h => h.collider.GetComponentInParent<TerrainVolume>() == terrain).OrderBy(h => h.distance).ToArray();
                        if (ground.Length == 0 || terrain.SurfaceHeight - ground[0].point.y >= 1.1f) continue;
                        Aim(origin, origin + Vector3.down);
                        if (player.TryDig()) strokes++;
                        if (firstEncounter == 0 && field.Finds.Any(f => f.Exposure > 0)) firstEncounter = strokes;
                    }
            Assert.That(strokes, Is.InRange(1, 450));
            Assert.That(firstEncounter, Is.GreaterThan(0));
            Assert.That(firstEncounter * player.EffectiveDigInterval, Is.LessThan(23f),
                "The entry layer must remain within the original powered-time budget.");
            // Authored entry density is ~0.54 finds/m², so a 2 x 2 m blind patch
            // averages two: the bar is "several reachable views", not a lucky spot.
            Assert.That(field.Finds.Count(f => f.Exposure > 0), Is.GreaterThanOrEqualTo(2));
            // The weak starter only partly frees finds in a blind patch; aimed finishing
            // is what turns one into a pickup (deliberate exposure).
            var revealed = field.Finds.Where(f => f.Exposure > 0).OrderByDescending(f => f.Exposure).First();
            float ring = Mathf.Max(revealed.WorldBounds.extents.x, revealed.WorldBounds.extents.z) * .75f;
            // A weak bite has to be walked all the way around the find, diagonals included,
            // before its surface is exposed enough to pick up.
            var around = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
                (Vector3.right + Vector3.forward).normalized, (Vector3.right + Vector3.back).normalized,
                (Vector3.left + Vector3.forward).normalized, (Vector3.left + Vector3.back).normalized };
            for (int pass = 0; pass < 10 && !revealed.Collectible; pass++)
                foreach (var offset in around)
                {
                    if (revealed.Collectible) break;
                    DigAbove(revealed, offset * ring, .23f);
                }
            Assert.That(revealed.Collectible, Is.True, "Aimed finishing turns a revealed find into a pickup.");
            Assert.That(player.Battery.Charge, Is.EqualTo(player.Battery.Capacity - strokes * player.EffectiveDigEnergy).Within(.001f));
            Assert.That(player.Battery.Charge, Is.GreaterThan(0));
            Assert.That(player.Inventory.Count, Is.Zero, "Revealing off-aim finds does not collect them automatically.");
            Debug.Log($"Shallow patch ({x}, {z}): {strokes} strokes, first encounter {firstEncounter}, " +
                $"{field.Finds.Count(f => f.Exposure > 0)} revealed, {field.Finds.Count(f => f.Collectible)} collectible.");
        }

        [Test]
        public void FindsRequireAuthoredExposureVisibilityAndOneIdentityWithFeedback()
        {
            // The scene's own generation, recorded before tests switch to the shallow layer.
            var generated = TestInputPreferences.GeneratedPopulation;
            Assert.That(generated.Length, Is.EqualTo(field.Catalog.TotalCount));
            Assert.That(generated.Select(f => f.Item.Id).Distinct().Count(), Is.EqualTo(field.Catalog.TotalCount));
            Assert.That(generated.Count(f => f.ContentId.StartsWith("mineral_")),
                Is.EqualTo(field.Catalog.Entries.Where(e => e.ItemId.StartsWith("mineral_")).Sum(e => e.Count)));
            // Every find starts buried, except those lying loose in a closed stash chest (106), those lining a sealed
            // geode (110) and those in a cavern's walls or crystals (115), exposed to its dark air but out of reach until
            // the player breaks in.
            Assert.That(field.Finds.Where(f => !InChest(f) && !InGeode(f) && !InCavern(f)).All(f => f.Exposure == 0), Is.True);
            Assert.That(field.Finds.Count(InChest), Is.EqualTo(field.Chests.Count * field.Catalog.ChestItems));
            var find = PrepareUprightFind();
            Aim(find.transform.position + Vector3.up * 2, find.transform.position);
            player.ToggleAdminXray();
            Assert.That(player.AdminXray, Is.True);
            Assert.That(player.TryInteract(), Is.False, "X-ray cannot collect through soil.");
            Assert.That(find.TryCollect(player), Is.False);
            for (int i = 0; i < 24 && find.Exposure == 0; i++) DigAbove(find, Vector3.zero, 0.22f);
            // An entry-layer find is smaller than the starter bite, so one stroke may take
            // it straight past its threshold; the contract is that collection still waits
            // for exposure and that the prompt matches the find's actual state.
            Assert.That(find.Exposure, Is.GreaterThan(0f));
            if (find.Exposure < find.RequiredExposure)
            {
                StringAssert.Contains("Uncover more", find.GetPrompt(player));
                StringAssert.Contains(Mathf.RoundToInt(find.RequiredExposure * 100) + "% exposed", find.GetPrompt(player));
                Assert.That(find.TryCollect(player), Is.False);
            }
            else Assert.That(find.Collectible, Is.True, "A small find may resolve inside one bite.");
            Expose(find);
            Aim(find.transform.position + Vector3.up * 4, find.transform.position);
            Assert.That(find.TryCollect(player), Is.False, "Collection keeps its separate 3 m reach.");
            Aim(find.transform.position + Vector3.up * 1.5f, find.transform.position);
            player.RefreshTargetPrompt();
            StringAssert.Contains(find.Item.DisplayName, player.TargetPrompt);
            StringAssert.Contains("Hold " + player.InputSettings.Display(PlayerBinding.Dig) + " to collect", player.TargetPrompt);
            Assert.That(player.TryInteract(), Is.False, "E is for stations, not discovery collection.");
            player.Battery.TrySpend(player.Battery.Charge);
            float energy = player.Battery.Charge;
            Assert.That(player.TryPrimaryAction(), Is.True);
            Assert.That(player.Inventory.Items.Single(), Is.SameAs(find.Item));
            StringAssert.Contains("Collected " + find.Item.DisplayName, player.Feedback);
            Assert.That(find.gameObject.activeSelf, Is.False);
            Assert.That(find.GetComponent<Collider>().enabled, Is.False);
            Assert.That(find.TryCollect(player), Is.False);
            Assert.That(player.Battery.Charge, Is.EqualTo(energy));
            Assert.That(player.TryPrimaryAction(), Is.False, "Pickup recovery prevents a second action in the same frame.");
            player.AdminReturnToSurface();
            terrain.ResetExcavation();
            Assert.That(find.Collected, Is.True);
            Assert.That(find.gameObject.activeSelf, Is.False);
            // Reset reburies everything except what lies in the re-carved stash chests, geodes and caverns.
            Assert.That(field.Finds.Skip(1).Where(f => !InChest(f) && !InGeode(f) && !InCavern(f)).All(f => f.Exposure == 0), Is.True);
            Assert.That(player.Inventory.Count, Is.EqualTo(1));
        }

        [Test]
        public void CapacityAndCoverKeepTheFindInTheWorldAndResetReburiesIt()
        {
            var find = field.Finds[1];
            Expose(find);
            // From below, the ray still meets untouched soil before the eligible object.
            Aim(find.transform.position + Vector3.down * 2, find.transform.position);
            Assert.That(find.TryCollect(player), Is.False);
            Aim(find.transform.position + Vector3.up * 1.5f, find.transform.position);
            for (int i = 0; i < player.Inventory.Capacity; i++)
                player.Inventory.TryAdd(new InventoryItem("full-" + i, "Carried find", 1));
            int revision = terrain.Revision;
            Assert.That(player.TryPrimaryAction(), Is.True, "A full bag still allows a cut through the common to reachable soil.");
            Assert.That(terrain.Revision, Is.GreaterThan(revision));
            StringAssert.Contains("Inventory full", player.Feedback);
            player.ShowFeedback("Other feedback");
            Assert.That(player.TryPrimaryAction(), Is.False);
            Assert.That(player.Feedback, Is.EqualTo("Other feedback"), "Holding on a full bag must not restart its error every frame.");
            Assert.That(find.Collected, Is.False);
            Assert.That(find.gameObject.activeSelf, Is.True);
            terrain.ResetExcavation();
            Assert.That(find.Exposure, Is.Zero);
            Assert.That(find.TryCollect(player), Is.False, "Reburied items cannot be collected through soil.");
            Assert.That(player.Inventory.Count, Is.EqualTo(player.Inventory.Capacity));
        }

        [Test]
        public void VisibleSliverCannotBypassExposureEvenBetweenSamples()
        {
            var find = field.Finds[0];
            // Deliberately sparse authored samples exercise visibility independently
            // from the percentage estimate; no exposure flag is set.
            var settings = new SerializedObject(find);
            var samples = settings.FindProperty("exposureSamples");
            samples.arraySize = 1;
            samples.GetArrayElementAtIndex(0).vector3Value = Vector3.down * 0.5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            find.RefreshExposure();
            Aim(find.transform.position + Vector3.up * 2, find.transform.position);
            bool visible = false;
            for (int i = 0; i < 24; i++)
            {
                Assert.That(player.TryGetTarget(3, out var hit), Is.True);
                if (hit.collider == find.GetComponent<Collider>()) { visible = true; break; }
                Assert.That(terrain.TryDig(hit, 0.22f), Is.True);
            }
            Assert.That(visible, Is.True);
            Assert.That(find.Exposure, Is.Zero, "The sampled underside should still be buried.");
            player.TryPrimaryAction(); // A visible sliver may dig, but cannot collect before exposure.
            Assert.That(find.Collected, Is.EqualTo(find.Exposure >= find.RequiredExposure),
                "The stroke may expose and finish the find, but never collect it below the threshold.");
            Assert.That(player.Inventory.Count, Is.EqualTo(find.Collected ? 1 : 0));
            Assert.That(find.gameObject.activeSelf, Is.EqualTo(!find.Collected), "An uncollected find stays in the world.");
        }

        [UnityTest]
        public IEnumerator HeldMouseUncoversAndCollectsWithoutAnotherPress() => ExerciseDigAndCollection(false, 1);

        [Test]
        public void ReleasedFindUsesLongerPickupReachWithoutExtendingBuriedFindOrStationReach()
        {
            var find = field.Finds[0];
            Expose(find);
            var position = find.transform.position;
            Aim(position + Vector3.up * 4.5f, position);
            Assert.That(find.TryCollect(player), Is.False, "An anchored find keeps the existing close interaction reach.");
            find.GetComponent<FindPhysics>().Restore(true);
            player.RefreshTargetPrompt();
            StringAssert.Contains(find.DisplayName, player.TargetPrompt);
            Assert.That(player.TryPrimaryAction(), Is.True, "An exposed released find should be collectable from the rim.");
            Assert.That(find.Collected, Is.True);
        }

        [Test]
        public void HeldPickupCollectsAFallingFindDuringRecoveryWithoutAnotherDigTick()
        {
            var first = field.Finds[0];
            var falling = field.Finds[1];
            Expose(first); Expose(falling);
            Aim(first.transform.position + Vector3.up * 1.5f, first.transform.position);
            Assert.That(player.TryPrimaryAction(), Is.True);
            // The previous pickup has just armed the soil-cut cooldown.
            var body = falling.GetComponent<Rigidbody>();
            falling.GetComponent<FindPhysics>().Restore(true);
            body.isKinematic = false; body.useGravity = true; body.linearVelocity = Vector3.down;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.SetPositionAndRotation(falling.transform.position + new Vector3(0, 2.9f, -.6f), Quaternion.identity);
            player.ViewCamera.transform.localPosition = Vector3.up * 1.6f;
            motor.enabled = true;
            player.Tuning.Gravity = 0;
            LookAt(falling.transform.position);
            Physics.SyncTransforms();
            Assert.That(player.TryGetTarget(player.MaximumPickupReach, out var aimed), Is.True);
            Assert.That(aimed.collider, Is.EqualTo(falling.GetComponent<Collider>()));
            int strokes = player.SuccessfulStrokes, revision = terrain.Revision;
            float charge = player.Battery.Charge;
            player.Tick(new FpsInputFrame { DigHeld = true }, .001f);
            Assert.That(falling.Collected, Is.True, "Held aim must collect the falling find immediately, not after a pickup/shovel timer.");
            Assert.That(player.Inventory.Count, Is.EqualTo(2));
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes));
            Assert.That(terrain.Revision, Is.EqualTo(revision));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            Assert.That(falling.TryCollect(player), Is.False, "Immediate collection still commits once.");
        }

        [Test]
        public void ExtendedLoosePickupStillRequiresClearAimAndFiniteReach()
        {
            var find = field.Finds[0];
            Expose(find);
            find.GetComponent<FindPhysics>().Restore(true);
            Aim(find.transform.position + Vector3.up * (player.MaximumPickupReach + 2), find.transform.position);
            Assert.That(find.TryCollect(player), Is.False);
            Aim(find.transform.position + Vector3.up * 4.5f, find.transform.position);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try {
                blocker.transform.position = find.transform.position + Vector3.up * 2;
                blocker.transform.localScale = Vector3.one * .5f;
                Physics.SyncTransforms();
                Assert.That(find.TryCollect(player), Is.False, "Longer reach must not pass through props.");
                Assert.That(find.Collected, Is.False);
            }
            finally { Object.DestroyImmediate(blocker); }
        }



        [UnityTest]
        public IEnumerator StrongRemappedToggleCollectsOnlyTheAimedFind() => ExerciseDigAndCollection(true, 6);

        private IEnumerator ExerciseDigAndCollection(bool toggle, int level)
        {
            var find = field.Finds[0];
            Assert.That(player.SelectAdminLevel(level), Is.True);
            player.InputSettings.SetToggleDig(toggle);
            if (toggle) player.InputSettings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true);
            PrepareDeviceView(find); player.enabled = true; player.SetApplicationFocus(true);
            yield return null; yield return null;
            var primary = toggle ? mouse.rightButton : mouse.leftButton;
            devices.Press(primary, queueEventOnly: true);
            bool releasedToggleButton = false;
            int beforePickupRevision = terrain.Revision;
            float beforePickupEnergy = player.Battery.Charge;
            int beforePickupStrokes = player.SuccessfulStrokes;
            // A weaker top tier needs longer to free the same soil volume.
            float deadline = Time.time + 30;
            while (!find.Collected && Time.time < deadline)
            {
                if (find.Collectible && Vector3.Distance(player.ViewCamera.transform.position, find.transform.position) > 2.8f)
                    PrepareDeviceView(find, closeToFind: true);
                LookAt(find.transform.position);
                if (toggle && player.SuccessfulStrokes > 0 && !releasedToggleButton)
                {
                    devices.Release(primary, queueEventOnly: true); releasedToggleButton = true;
                }
                beforePickupRevision = terrain.Revision; beforePickupEnergy = player.Battery.Charge;
                beforePickupStrokes = player.SuccessfulStrokes;
                yield return null;
            }
            Assert.That(player.SuccessfulStrokes, Is.GreaterThan(0), "This input starts by excavating real covering terrain.");
            Assert.That(find.Collected, Is.True, "Hovering with the original hold/toggle must collect without a fresh press. " + PickupState(find));
            Assert.That(player.Inventory.Count, Is.EqualTo(1));
            int finishingStrokes = player.SuccessfulStrokes - beforePickupStrokes;
            Assert.That(finishingStrokes, Is.InRange(0, 1), "An aimed uncovering stroke can finish pickup immediately.");
            Assert.That(terrain.Revision, Is.EqualTo(beforePickupRevision + finishingStrokes));
            Assert.That(player.Battery.Charge, Is.EqualTo(beforePickupEnergy - finishingStrokes * player.EffectiveDigEnergy).Within(.001f),
                "Only the uncovering stroke costs fuel; pickup adds no charge.");
            int revision = terrain.Revision; float energy = player.Battery.Charge;
            PrepareDeviceView(find);
            LookAt(new Vector3(find.transform.position.x, 0, find.transform.position.z - 1.5f));
            // The next stroke starts after one interval and cuts at its scoop.
            yield return new WaitForSeconds(player.ScoopDigInterval + ToolRigPresenter.ScoopDelay(player.ScoopDigInterval, TerrainMaterialId.Soil) + .1f);
            Assert.That(terrain.Revision, Is.GreaterThan(revision), "The same hold/toggle continues after pickup recovery.");
            Assert.That(player.Battery.Charge, Is.LessThan(energy));
            Assert.That(player.Inventory.Count, Is.EqualTo(1), "Continuing cannot duplicate the find.");
        }

        [UnityTest]
        public IEnumerator StrongHeldWideScoopsCollectFreedSmallFindsWithoutHover() => ExerciseWideScoop(false);


        private IEnumerator ExerciseWideScoop(bool toggle)
        {
            // Use current small coal finds at the known shallow fixture positions.
            TestInputPreferences.RestoreSmallFindFixture(field);
            yield return null;
            var variants = field.Finds.Where(f => TestInputPreferences.IsCoalFixture(f)).Take(3).ToArray();
            Assert.That(variants.Length, Is.EqualTo(3), "The fixture supplies three separate current small finds.");
            player.SelectAdminLevel(6);
            player.InputSettings.SetToggleDig(toggle);
            if (toggle) player.InputSettings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true);
            var primary = toggle ? mouse.rightButton : mouse.leftButton;
            // Native Editor focus changes are outside this input fixture's scope.
            // Resume with a fresh press, as the runtime correctly suppresses held input.
            IEnumerator ResumeAfterEditorFocusChange()
            {
                player.SetApplicationFocus(true);
                player.CloseMenu();
                devices.Release(primary, queueEventOnly: true);
                yield return null;
                yield return null;
                devices.Press(primary, queueEventOnly: true);
            }
            int collected = 0;
            foreach (var find in variants)
            {
                // Isolate this identity: with immediate pickup, another exposed
                // background find on the same ray can legitimately collect next.
                foreach (var other in field.Finds)
                    other.gameObject.SetActive(other == find && !other.Collected);
                player.enabled = false; devices.Release(primary, queueEventOnly: true);
                player.RefillAdminBattery(); PrepareDeviceView(find);
                var motor = player.GetComponent<CharacterController>(); motor.enabled = false;
                var position = player.transform.position; position.x += .55f;
                player.transform.position = position; motor.enabled = true;
                var scoopAim = find.transform.position + Vector3.right * .55f;
                LookAt(scoopAim); Physics.SyncTransforms();
                player.enabled = true; player.SetApplicationFocus(true);
                yield return null; yield return null;
                int initialStrokes = player.SuccessfulStrokes;
                float initialCharge = player.Battery.Charge;
                bool releasedToggleButton = false;
                yield return ResumeAfterEditorFocusChange();
                // The weaker top tier cannot engulf a find from one fixed aim, so the
                // held strokes walk around it; the centre ray still never lands on it.
                var ring = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
                int strokesLanded = 0;
                float deadline = Time.realtimeSinceStartup + 15;
                while (!find.Collected && Time.realtimeSinceStartup < deadline)
                {
                    if (player.IsMenuOpen) yield return ResumeAfterEditorFocusChange();
                    Assert.That(player.TryGetTarget(3, out var hit) && hit.collider == find.GetComponent<MeshCollider>(), Is.False,
                        "Keep the centre ray beside the find during the wide-scoop regression.");
                    if (player.SuccessfulStrokes > initialStrokes + strokesLanded)
                    {
                        strokesLanded++;
                        LookAt(find.transform.position + ring[strokesLanded % ring.Length] * .55f);
                        Physics.SyncTransforms();
                    }
                    if (toggle && player.SuccessfulStrokes > initialStrokes && !releasedToggleButton)
                    {
                        devices.Release(primary, queueEventOnly: true); releasedToggleButton = true;
                    }
                    yield return null;
                }
                LookAt(player.ViewCamera.transform.position + Vector3.up);
                Assert.That(find.Collected, Is.True, "Held/toggled digging should collect nearby freed loot without hovering: " + find.SaveContentId
                    + $". Strokes={player.SuccessfulStrokes - initialStrokes}, exposure={find.Exposure:F2}, "
                    + $"collectible={find.Collectible}, camera={player.ViewCamera.transform.position}, menu={player.Menu}, active={player.GameplayActive}, binding={player.InputSettings.Path(PlayerBinding.Dig)}, toggle={player.InputSettings.ToggleDig}");
                Assert.That(player.Inventory.Count, Is.EqualTo(++collected));
                Assert.That(player.Battery.Charge, Is.EqualTo(initialCharge
                    - (player.SuccessfulStrokes - initialStrokes) * player.EffectiveDigEnergy).Within(.001f),
                    "Automatic pickup adds no fuel cost to the paid strokes.");
                Assert.That(find.TryCollect(player), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator UniqueUncoveringDigsRoundTheAimedPointEvenWithAFullBag()
        {
            // A buried computer (the crystal trophies stand in caves).
            var find = field.Finds.First(f => f.Kind == DiscoveryKind.Unique && !f.StandsUpright);
            foreach (var other in field.Finds.Where(f => f != find)) other.gameObject.SetActive(false);
            var state = find.Capture(); state.Rotation = Quaternion.identity; find.Restore(state);
            Physics.SyncTransforms();
            var bounds = find.WorldBounds;
            var local = terrain.transform.InverseTransformPoint(bounds.center);
            var grid = new ExcavationGrid(terrain.Dimensions, terrain.CellSize);
            grid.RemoveSphere(local + Vector3.up * (bounds.extents.y + .6f), .75f, out _);
            yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
            find.RefreshExposure();
            var top = bounds.center + Vector3.up * bounds.extents.y;
            Aim(top + Vector3.up * .55f, bounds.center);
            Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<BuriedFind>(), Is.SameAs(find));
            Assert.That(find.Exposure, Is.GreaterThan(0).And.LessThan(find.RequiredExposure));
            float around = player.EffectiveShovel.Radius + BuriedFind.AimedSoil;
            var cut = new Bounds();
            void Cut(Bounds changed) => cut = changed;
            terrain.Changed += Cut;
            try
            {
                float exposure = find.Exposure;
                for (int bag = 0; bag < 2; bag++)
                {
                    int revision = terrain.Revision;
                    Assert.That(player.TryDig(), Is.True, "A stroke aimed at a unique digs the soil round the aimed point.");
                    Assert.That(terrain.Revision, Is.GreaterThan(revision));
                    Assert.That(cut.SqrDistance(hit.point), Is.LessThan(around * around), "The stroke dug next to the aimed point, not round the whole find.");
                    for (int i = 0; i < player.Inventory.Capacity; i++)
                        player.Inventory.TryAdd(new InventoryItem("manual-unique-" + i, "Rock", 1));
                }
                find.RefreshExposure();
                Assert.That(find.Exposure, Is.GreaterThan(exposure));
                Assert.That(find.State, Is.EqualTo(FindState.World));

                // Bared all round the aimed point, aiming at it digs nothing: the player moves round to the rest.
                grid.RemoveSphere(terrain.transform.InverseTransformPoint(hit.point), around + .3f, out _);
                yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
                find.RefreshExposure();
                Aim(top + Vector3.up * .55f, bounds.center);
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out hit), Is.True);
                Assert.That(hit.collider.GetComponentInParent<BuriedFind>(), Is.SameAs(find));
                int bared = terrain.Revision;
                float charge = player.Battery.Charge;
                Assert.That(player.TryDig(), Is.False, "No soil round the aimed point: the assist stops.");
                Assert.That(player.TryPrimaryAction(), Is.False);
                Assert.That(terrain.Revision, Is.EqualTo(bared));
                Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            }
            finally { terrain.Changed -= Cut; }
        }

        [Test]
        public void AssistedStrokeCannotReachDistantSoilOrIgnoreAnUnrelatedBlocker()
        {
            var find = PrepareUprightFind();
            Aim(find.transform.position + Vector3.up * 2, find.transform.position);
            for (int i = 0; i < 24; i++)
            {
                Assert.That(player.TryGetTarget(3, out var hit), Is.True);
                if (hit.collider == find.GetComponent<Collider>()) break;
                Assert.That(terrain.TryDig(hit, 0.22f), Is.True);
            }
            int revision = terrain.Revision;
            float charge = player.Battery.Charge;
            // The visible top of the find is reachable, its remaining soil is not.
            player.Tuning.DigReach = Vector3.Distance(player.ViewCamera.transform.position, find.WorldBounds.max) - 0.1f;
            Assert.That(player.TryDig(), Is.False);
            player.Tuning.DigReach = 3;
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                blocker.transform.position = find.transform.position + Vector3.up * 1;
                blocker.transform.localScale = new Vector3(3, 0.1f, 3);
                Physics.SyncTransforms();
                Assert.That(player.TryDig(), Is.False);
                Assert.That(terrain.Revision, Is.EqualTo(revision));
                Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            }
            finally { Object.DestroyImmediate(blocker); }
        }

        [UnityTest]
        public IEnumerator HeldCollectionNeedsReleaseAfterInventoryAndFocusResume()
        {
            var find = field.Finds[0];
            Expose(find);
            // This tests input barriers on a visible target, after real release/settling.
            yield return new WaitForSeconds(1.5f);
            PrepareDeviceView(find, closeToFind: true);
            player.enabled = true;
            player.SetApplicationFocus(true);
            yield return null;
            Assert.That(player.TryGetTarget(3, out var target) && target.collider.GetComponent<BuriedFind>() == find,
                Is.True, "The input-barrier fixture must aim at its settled target within pickup reach. " + PickupState(find));
            player.OpenMenu(PlayerMenu.Inventory);
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            player.CloseMenu();
            yield return new WaitForSecondsRealtime(player.EffectiveDigInterval + 0.05f);
            Assert.That(player.GameplayActive, Is.True, "Inventory resume must actually restore gameplay before checking held input.");
            Assert.That(find.Collected, Is.False, "An inspection click cannot become a held pickup after resume.");
            player.SetApplicationFocus(false);
            yield return null;
            player.SetApplicationFocus(true);
            player.CloseMenu();
            yield return new WaitForSecondsRealtime(player.EffectiveDigInterval + 0.05f);
            Assert.That(player.GameplayActive, Is.True, "Focus resume must actually restore gameplay before checking held input.");
            Assert.That(find.Collected, Is.False, "Focus recovery also requires release.");
            devices.Release(mouse.leftButton, queueEventOnly: true);
            yield return null;
            devices.Press(mouse.leftButton, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(find.Collected, Is.True, PickupState(find));
        }

        [UnityTest]
        public IEnumerator XrayUsesRealChordAndMenuButtonAndRestoresOpaqueGround()
        {
            Assert.That(player.AdminXray, Is.False);
            player.enabled = true;
            player.SetApplicationFocus(true);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.X));
            yield return null;
            yield return null;
            Assert.That(player.AdminXray, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.LeftShift, Key.X));
            yield return null;
            yield return null;
            Assert.That(player.AdminXray, Is.True);
            Assert.That(player.HasAdminOverrides, Is.True);
            Assert.That(terrain.XrayEnabled, Is.True);
            yield return null;
            Assert.That(player.AdminXray, Is.True, "A held chord cannot toggle repeatedly.");
            player.enabled = false;
            player.OpenMenu(PlayerMenu.Pause);
            player.ToggleAdminXray();
            Assert.That(player.AdminXray, Is.True, "Pause is a barrier to the direct shortcut action too.");
            player.ShowAdminMenu();
            yield return null;
            var button = MenuTestUI.Button(player, "X-ray: ON");
            MenuTestUI.Click(button);
            Assert.That(player.AdminXray, Is.False);
            player.SetApplicationFocus(false);
            player.ToggleAdminXray();
            Assert.That(player.AdminXray, Is.False);
            player.SetApplicationFocus(true);
            player.ToggleAdminXray();
            Assert.That(player.AdminXray, Is.True);
            player.RestoreAdminOverrides();
            Assert.That(player.AdminXray || player.HasAdminOverrides, Is.False);
            player.CloseMenu();
            yield return null;
            Assert.That(terrain.XrayEnabled, Is.False);
            Assert.That(Shader.GetGlobalFloat("_ExcavationDaylightEnabled"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator XrayRevealsRealFindsWithoutChangingSoilOrExposureAndRestoresAfterDigging()
        {
            // A buried computer (the crystal trophies stand in caves).
            var find = field.Finds.First(f => f.RopeTarget && !f.StandsUpright);
            Aim(new Vector3(find.transform.position.x, terrain.SurfaceHeight + 1.5f, find.transform.position.z), find.transform.position);
            var visual = find.GetComponent<MeshRenderer>();
            var chunks = terrain.transform.Find("Chunks");
            var original = chunks.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            var revision = terrain.Revision;
            Assert.That(visual.enabled, Is.False);
            player.ToggleAdminXray();
            yield return null; yield return null;
            Assert.That(visual.enabled, Is.True);
            Assert.That(find.Exposure, Is.Zero);
            Assert.That(find.CanMark, Is.False);
            Assert.That(player.TryInteract(), Is.False);
            Assert.That(terrain.Revision, Is.EqualTo(revision));
            Assert.That(terrain.IsSolid(find.transform.position), Is.True);
            Assert.That(Shader.GetGlobalFloat("_ExcavationDaylightEnabled"), Is.Zero);
            var transparent = chunks.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.That(transparent, Is.Not.SameAs(original));
            Assert.That(transparent.GetFloat("_GroundOpacity"), Is.InRange(.01f, .2f));
            Assert.That(transparent.GetFloat("_ZWrite"), Is.Zero);
            Assert.That(transparent.renderQueue, Is.EqualTo(3000));
            Assert.That(transparent.GetShaderPassEnabled("ShadowCaster"), Is.False);
            Assert.That(player.TryGetTarget(10, out var target), Is.True);
            Assert.That(target.collider.GetComponentInParent<TerrainVolume>(), Is.SameAs(terrain));
            Assert.That(player.TryDig(), Is.True);
            yield return null; yield return null;
            Assert.That(terrain.Revision, Is.GreaterThan(revision));
            Assert.That(chunks.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterial == transparent), Is.True);
            player.ToggleAdminXray();
            Assert.That(terrain.XrayEnabled, Is.False);
            Assert.That(chunks.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterial == original), Is.True);
            Assert.That(visual.enabled, Is.False, "A still-buried computer returns to normal renderer culling.");
            player.ToggleAdminXray();
            Assert.That(visual.enabled, Is.True);
            field.enabled = false;
            Assert.That(terrain.XrayEnabled || visual.enabled, Is.False);
        }

        [Test]
        public void BackpackPurchaseMakesAnAlreadyExposedFullBagFindCollectable()
        {
            var find = field.Finds[0];
            Expose(find);
            Aim(find.transform.position + Vector3.up * 1.5f, find.transform.position);
            for (int i = 0; i < 10; i++) player.Inventory.TryAdd(new InventoryItem("capacity-" + i, "Rock", 2));
            Assert.That(find.TryCollect(player), Is.False);
            Assert.That(find.Collected, Is.False);
            // Price the tier from the authored ladder instead of a frozen number.
            player.Wallet.TryCredit(EquipmentProgression.Price(player.Inventory.Level));
            Assert.That(player.Trade.TryUpgrade(player.Trade.OfferUpgrade(EquipmentKind.Inventory)), Is.True);
            Assert.That(player.Inventory.Capacity, Is.EqualTo(15));
            Assert.That(find.TryCollect(player), Is.True);
            Assert.That(player.Inventory.Count, Is.EqualTo(11));
            Assert.That(player.Inventory.Items.Select(i => i.InstanceId).Distinct().Count(), Is.EqualTo(11));
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
        }

        private void Aim(Vector3 origin, Vector3 point)
        {
            player.ViewCamera.transform.position = origin;
            player.ViewCamera.transform.LookAt(point);
            Physics.SyncTransforms();
        }

        private string PickupState(BuriedFind find)
        {
            bool hit = player.TryGetTarget(3, out var targetHit);
            return $"Target={(hit ? targetHit.collider.name : "none")}, camera={player.ViewCamera.transform.position}, find={find.transform.position}, "
                + $"speed={find.GetComponent<Rigidbody>().linearVelocity.magnitude}, exposure={find.Exposure}, "
                + $"cameraInSoil={terrain.IsSolid(player.ViewCamera.transform.position)}, menu={player.Menu}, feedback={player.Feedback}";
        }

        private BuriedFind PrepareUprightFind()
        {
            // Partial-exposure fixtures need a vertical span between first visibility and
            // pickup. A shallow bottle lying flat can legitimately uncover in one stroke.
            var find = field.Finds[0];
            find.transform.rotation = Quaternion.identity;
            find.GetComponent<FindPhysics>().Restore(false);
            Physics.SyncTransforms(); find.RefreshExposure();
            return find;
        }

        private void Expose(BuriedFind find)
        {
            float ring = Mathf.Max(find.WorldBounds.extents.x, find.WorldBounds.extents.z) + 0.12f;
            for (int pass = 0; pass < 12 && !find.Collectible; pass++)
                foreach (var offset in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                {
                    if (find.Collectible) break;
                    DigAbove(find, offset * ring, 0.65f);
                }
            Assert.That(find.Exposure, Is.GreaterThanOrEqualTo(find.RequiredExposure), "Digging around the find should uncover it.");
        }

        private void PrepareDeviceView(BuriedFind find, bool closeToFind = false)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            var position = closeToFind ? find.transform.position + new Vector3(0, .1f, -.3f)
                : new Vector3(find.transform.position.x, 0.01f, find.transform.position.z - 1.05f);
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            player.ViewCamera.transform.localPosition = Vector3.up * 1.6f;
            motor.enabled = true;
            player.Tuning.Gravity = 0;
            LookAt(find.transform.position);
            Physics.SyncTransforms();
        }

        private void LookAt(Vector3 point)
        {
            Vector3 direction = point - player.ViewCamera.transform.position;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            player.Tick(new FpsInputFrame { Look = new Vector2(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), player.Pitch - pitch)
                / player.Tuning.LookSensitivity }, 0.001f);
        }

        // Returns false when the ray only reaches carved ledges that hold no ground.
        private bool DigAbove(BuriedFind find, Vector3 offset, float radius)
        {
            var origin = find.transform.position + offset;
            origin.y = 2;
            // Dense ground puts other finds in the ray, and repeated strokes leave
            // ledges whose hit point is already void: dig the first soil hit that
            // still removes ground instead of failing the stroke.
            var ground = Physics.RaycastAll(origin, Vector3.down, 30, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider.GetComponentInParent<TerrainVolume>() == terrain).OrderBy(h => h.distance).ToArray();
            Assert.That(ground.Length, Is.GreaterThan(0), "The stroke must start on soil.");
            foreach (var hit in ground) if (terrain.TryDig(hit, radius)) return true;
            return false;
        }

        [UnityTest]
        public IEnumerator BuriedPopulationStopsRenderingAndPollingButWakesForDiggingAndRestore()
        {
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(field.Finds.Count(f => !BesideSeededAir(f) && f.GetComponent<MeshRenderer>().enabled), Is.LessThan(field.Finds.Count / 20),
                "Only conservative surface-edge bounds may render in pristine soil.");
            string Named(System.Func<BuriedFind, bool> which) => string.Join("; ", field.Finds.Where(which).Take(4)
                .Select(f => $"{f.SaveContentId} at {f.transform.position:F2}, nearest chest {field.Chests.Min(c => Vector3.Distance(c.transform.position, f.transform.position)):F2}"));
            Assert.That(field.Finds.Where(f => f.WorldBounds.max.y < terrain.SurfaceHeight && !BesideSeededAir(f))
                .All(f => !f.GetComponent<MeshRenderer>().enabled), Is.True,
                "Buried finds render: " + Named(f => f.WorldBounds.max.y < terrain.SurfaceHeight && !BesideSeededAir(f) && f.GetComponent<MeshRenderer>().enabled));
            Assert.That(field.Finds.Where(f => !BesideSeededAir(f)).All(f => !f.GetComponent<FindPhysics>().enabled), Is.True,
                "Buried finds poll: " + Named(f => !BesideSeededAir(f) && f.GetComponent<FindPhysics>().enabled));
            Assert.That(field.Finds.All(f => f.GetComponent<MeshCollider>().enabled), Is.True,
                "Soil-occluded targeting and collision remain available, including tiny slivers.");
            var before = field.Capture();
            var find = field.Finds[0];
            for (int i = 0; i < 8 && find.Exposure == 0; i++) DigAbove(find, Vector3.zero, .65f);
            Assert.That(find.Exposure, Is.GreaterThan(0));
            Assert.That(find.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(find.GetComponent<FindPhysics>().enabled, Is.True, "A cut wakes attachment checking.");
            Assert.That(field.Finds.Count(f => !BesideSeededAir(f) && f.GetComponent<MeshRenderer>().enabled), Is.LessThan(field.Finds.Count / 20),
                "A local excavation must not activate the whole site.");

            terrain.ResetExcavation();
            field.Restore(before, field.Seed);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            var after = field.Capture();
            Assert.That(after.Select(f => f.Item.Id), Is.EqualTo(before.Select(f => f.Item.Id)));
            // What lies loose in a chest settles again on its floor; everything else keeps its exact place.
            var loose = field.Finds.Where(InChest).Select(f => f.Item.InstanceId).ToHashSet();
            Assert.That(after.Where(f => !loose.Contains(f.Item.Id)).Select(f => f.Position), Is.EqualTo(before.Where(f => !loose.Contains(f.Item.Id)).Select(f => f.Position)));
            Assert.That(field.Finds.Where(f => f.WorldBounds.max.y < terrain.SurfaceHeight && !BesideSeededAir(f))
                .All(f => !f.GetComponent<MeshRenderer>().enabled), Is.True);
            Assert.That(field.Finds.Where(f => !BesideSeededAir(f)).All(f => !f.GetComponent<FindPhysics>().enabled), Is.True);
        }

        // Lining a geode's sealed hollow (110), or within a metre of its shell.
        private bool InGeode(BuriedFind find) => terrain.GroundLayout.Geodes.Any(g =>
            Vector3.Distance(terrain.transform.TransformPoint((Vector3)g.Centre), find.transform.position) < g.Reach + 1f);

        // In a cavern's walls (115: its minerals stand half out of the stone, so a lamp shows them) or within a metre of it.
        private bool InCavern(BuriedFind find)
        {
            var local = terrain.transform.InverseTransformPoint(find.transform.position);
            return terrain.GroundLayout.Caverns.Any(c =>
            {
                var bounds = new Bounds(); bounds.SetMinMax((Vector3)c.Min, (Vector3)c.Max); bounds.Expand(2f);
                return bounds.Contains(local);
            });
        }

        private bool BesideSeededAir(BuriedFind find) => BesideChest(find) || InGeode(find) || InCavern(find);

        // In a chest or within a metre of the pocket it stands in (109): seeded air is modified ground, and buried finds
        // beside it wake conservatively (TerrainVolume.MayExpose checks modified samples in coarse blocks).
        private bool BesideChest(BuriedFind find) => field.Chests.Any(chest =>
        {
            var local = chest.transform.InverseTransformPoint(find.transform.position) - chest.Pocket.center;
            var d = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)) - chest.Pocket.extents;
            return Vector3.Max(d, Vector3.zero).magnitude < 1f;
        }) || terrain.GroundLayout.Stashes.Any(stash =>
        {
            // The pocket's uneven dome above the chest (113).
            var dome = TerrainGround.PocketDomeReserve(stash);
            return Vector3.Distance(terrain.transform.TransformPoint((Vector3)dome.centre), find.transform.position) < dome.radius + 1f;
        });

        // Loose in a stash chest's seeded hollow (106).
        private bool InChest(BuriedFind find)
            => field.Chests.Any(chest => chest.Hollow.Contains(chest.transform.InverseTransformPoint(find.transform.position)));

        [Test]
        public void VisibleSliverBetweenExposureSamplesStillRenders()
        {
            var find = field.Finds[0];
            // A valid sparse legacy sample set misses the very top of the mesh.
            // Rendering must follow the actual mesh, never the exposure percentage alone.
            typeof(BuriedFind).GetField("exposureSamples", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(find, new[] { Vector3.zero });
            // Tilting the box diagonal upright inflates the renderer bounds furthest past the rounded mesh.
            var mesh = find.GetComponent<MeshFilter>().sharedMesh;
            var state = find.Capture();
            state.Rotation = Quaternion.FromToRotation(mesh.bounds.extents, Vector3.up);
            find.Restore(state);
            var vertices = mesh.vertices;
            state = find.Capture();
            state.Position.y += terrain.SurfaceHeight - vertices.Max(v => find.transform.TransformPoint(v).y) + .002f;
            find.Restore(state);
            Assert.That(find.Exposure, Is.Zero);
            Assert.That(find.GetComponent<MeshRenderer>().enabled, Is.True, "A 2 mm sliver of the real mesh renders.");
            Assert.That(find.GetComponent<MeshCollider>().enabled, Is.True);
            // Shallow placement keeps the whole vertex envelope under the turf. The tilted renderer
            // box still breaks the surface there, but no part of the mesh can show.
            var scale = find.transform.lossyScale;
            float envelope = vertices.Max(v => v.magnitude) * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            state = find.Capture();
            state.Position.y += terrain.SurfaceHeight - .01f - envelope - find.transform.position.y;
            find.Restore(state);
            Assert.That(find.WorldBounds.max.y, Is.GreaterThan(terrain.SurfaceHeight), "Tilted renderer bounds reach the surface.");
            Assert.That(find.GetComponent<MeshRenderer>().enabled, Is.False, "A fully buried shallow find must not render.");
        }

        [Test]
        public void LocalTerrainNotificationsFollowMovedFindsAndFullResetReburiesThem()
        {
            var find=field.Finds.First(f=>f.Kind==DiscoveryKind.Common);
            var state=find.Capture();
            state.Position=new Vector3(8,terrain.Dimensions.y*terrain.CellSize-7,8);
            find.Restore(state);
            Assert.That(find.Exposure,Is.Zero);
            // No manual SyncTransforms: the notification must query the current pose.
            var center=find.transform.TransformPoint(find.LocalHull.center);
            var half=Vector3.Scale(find.LocalHull.extents,find.transform.lossyScale)+Vector3.one*.3f;
            Assert.That(terrain.ClearLoadSweep(center,center,find.transform.rotation,half),Is.True);
            Assert.That(find.Exposure,Is.EqualTo(1),"A local cut must refresh a moved find, including hidden meshes.");
            terrain.ResetExcavation();
            Assert.That(find.Exposure,Is.Zero,"The saturated whole-world query must not skip buried finds.");
            Assert.That(find.GetComponent<MeshRenderer>().enabled,Is.False);
        }

        [Explicit("Slow end-to-end check; runs with tools/test-changed.ps1 -Full.")]
        [UnityTest]
        public IEnumerator DeeperScrapesRevealFreshFindsAfterEarlierObjectsAreRemoved()
        {
            TestInputPreferences.RestoreGeneratedPopulation(field); // measures the real layout, deep finds included
            var original = field.Finds.ToDictionary(f => f.Item.InstanceId, f => f.WorldBounds);
            var seen = new System.Collections.Generic.HashSet<string>();
            var grid = new ExcavationGrid(terrain.Dimensions, terrain.CellSize);
            int shallow = 0, coal = 0;
            foreach (float depth in new[] { .2f, 1f, 2f, 3f })
            {
                // Remove every earlier reveal from this disposable fixture. Rocks cannot
                // fall into the next photograph and masquerade as newly buried finds.
                foreach (var find in field.Finds.Where(f => seen.Contains(f.Item.InstanceId) && !f.Collected))
                {
                    var state = find.Capture(); state.State = FindState.Collected; find.Restore(state);
                }
                for (float x = 9; x <= 13; x += .3f) for (float z = 3; z <= 7; z += .3f)
                    grid.RemoveSphere(new Vector3(x, SiteLayout.Extent.y - depth + .6f, z), .6f, out _);
                yield return terrain.Restore(grid.Capture(), terrain.ExcavationSeed);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                float floor = terrain.SurfaceHeight - depth;
                var fresh = field.Finds.Where(f => !seen.Contains(f.Item.InstanceId) && f.Exposure > 0
                    && original[f.Item.InstanceId].min.y <= floor + .08f && original[f.Item.InstanceId].max.y >= floor
                    && terrain.transform.InverseTransformPoint(original[f.Item.InstanceId].center).x >= 9
                    && terrain.transform.InverseTransformPoint(original[f.Item.InstanceId].center).x <= 13
                    && terrain.transform.InverseTransformPoint(original[f.Item.InstanceId].center).z >= 3
                    && terrain.transform.InverseTransformPoint(original[f.Item.InstanceId].center).z <= 7).ToArray();
                if (depth < .5f) shallow = fresh.Length;
                else
                {
                    Assert.That(fresh.Length, Is.GreaterThanOrEqualTo(Mathf.Max(8, Mathf.FloorToInt(shallow * .6f))),
                        $"Floor {depth}: new anchored finds must replace earlier reveals.");
                    coal += fresh.Count(f => f.SaveContentId == "mineral_coal");
                }
                foreach (var find in field.Finds.Where(f => f.Exposure > 0)) seen.Add(find.Item.InstanceId);
                Debug.Log($"Fresh floor at {depth}: {fresh.Length} new finds; previous reveals excluded.");
            }
            Assert.That(shallow, Is.GreaterThanOrEqualTo(12));
            Assert.That(coal, Is.GreaterThanOrEqualTo(2), "The first metres should introduce coal while rocks remain common.");
        }
    }
}
#endif
