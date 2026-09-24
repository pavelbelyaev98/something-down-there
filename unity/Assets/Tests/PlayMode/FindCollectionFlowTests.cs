#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SomethingDownThere.Tests
{
    public sealed partial class FindPhysicsIntegrationTests
    {
        [TestCase(true)] [TestCase(false)]
        public void FullBagKeepsCuttingThroughAnAimedFindAndCollectsWhenSpaceReturns(bool shaving)
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0; player.SelectAdminLevel(4);
            if (player.ShavingEnabled != shaving) player.ToggleAdminShaving();
            PlacePickupCutFixture(find, false); AimPickupCutFixture(find, 0);
            while (!player.Inventory.IsFull) player.Inventory.TryAdd(new InventoryItem("fill-" + player.Inventory.Count, "Carried", 1));
            int count = player.Inventory.Count, strokes = player.SuccessfulStrokes;
            string identity = find.Item.InstanceId; float charge = player.Battery.Charge;
            Assert.That(player.TryPrimaryAction(), Is.True);
            Assert.That(player.TryPrimaryAction(), Is.False, "A full bag must still respect the cutting cadence.");
            player.Tick(new FpsInputFrame { DigHeld = true }, player.EffectiveDigInterval);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 2));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - 2 * player.EffectiveDigEnergy).Within(.001f));
            Assert.That(player.Inventory.Count, Is.EqualTo(count));
            Assert.That(find.Collected, Is.False); Assert.That(find.Item.InstanceId, Is.EqualTo(identity));
            Assert.That(find.GetComponent<Collider>().enabled, Is.True);
            player.Inventory.TryRemove(player.Inventory.Items[0].InstanceId, out _);
            Assert.That(player.TryPrimaryAction(), Is.True, "Pickup resumes immediately, even during cut cooldown.");
            Assert.That(find.Collected, Is.True);
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == identity), Is.EqualTo(1));
        }

        [Test]
        public void FullBagDiggingStillStopsAtAnInterveningWall()
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0; player.SelectAdminLevel(4);
            Place(find, .65f); AimPickupCutFixture(find, 0);
            while (!player.Inventory.IsFull) player.Inventory.TryAdd(new InventoryItem("fill-" + player.Inventory.Count, "Carried", 1));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = find.WorldBounds.center + Vector3.down * .4f;
                wall.transform.localScale = new Vector3(3, .05f, 3); Physics.SyncTransforms();
                Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var hit), Is.True);
                Assert.That(hit.collider.GetComponentInParent<BuriedFind>(), Is.SameAs(find));
                int revision = terrain.Revision; float charge = player.Battery.Charge;
                Assert.That(player.TryPrimaryAction(), Is.False);
                Assert.That(terrain.Revision, Is.EqualTo(revision));
                Assert.That(player.Battery.Charge, Is.EqualTo(charge)); Assert.That(find.Collected, Is.False);
            }
            finally { Object.DestroyImmediate(wall); }
        }

        [TestCase(true, false)] [TestCase(false, false)]
        [TestCase(true, true)] [TestCase(false, true)]
        public void PickupKeepsTheReadyCutInTheSameHeldFrame(bool shaving, bool nearby)
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0;
            player.SelectAdminLevel(4);
            if (player.ShavingEnabled != shaving) player.ToggleAdminShaving();
            PlacePickupCutFixture(find, nearby);
            AimPickupCutFixture(find, nearby ? .7f : 0);
            Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<BuriedFind>() == find, Is.EqualTo(!nearby));
            int strokes = player.SuccessfulStrokes, revision = terrain.Revision;
            float charge = player.Battery.Charge;
            player.Tick(new FpsInputFrame { DigHeld = true }, .001f);
            Assert.That(find.Collected, Is.True);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 1), "Pickup cannot consume a ready cutting frame.");
            Assert.That(terrain.Revision, Is.EqualTo(revision + 1));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - player.EffectiveDigEnergy).Within(.001f));
            Assert.That(player.TryPrimaryAction(), Is.False, "Collection must not allow a second cut before its cadence.");
            player.Tick(new FpsInputFrame { DigHeld = true }, player.EffectiveDigInterval);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 2), "Continue while the pickup visual is active.");
        }

        [TestCase(true)] [TestCase(false)]
        public void PickupKeepsAnExistingCutDeadline(bool shaving)
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0;
            player.SelectAdminLevel(4);
            if (player.ShavingEnabled != shaving) player.ToggleAdminShaving();
            PlacePickupCutFixture(find, false);
            AimPickupCutFixture(find, 1.1f);
            Assert.That(player.TryPrimaryAction(), Is.True);
            int strokes = player.SuccessfulStrokes;
            float interval = player.EffectiveDigInterval;
            player.Tick(default, interval * .5f);
            AimPickupCutFixture(find, 0);
            player.Tick(new FpsInputFrame { DigHeld = true }, .001f);
            Assert.That(find.Collected, Is.True);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes), "Pickup must not bypass the in-progress cooldown.");
            player.Tick(new FpsInputFrame { DigHeld = true }, interval * .5f);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 1), "Pickup must not reset the remaining cooldown.");
        }

        private void PlacePickupCutFixture(BuriedFind find, bool nearby)
        {
            if (nearby) { Place(find, .65f); return; }
            // Partial exposure permits aimed collection but excludes proximity pickup.
            for (float height = -.1f; height < .3f; height += .001f)
            {
                Place(find, height);
                if (find.Collectible && find.Exposure < .8f) return;
            }
            Assert.Fail("Could not place a collectible find with retained soil.");
        }

        private void AimPickupCutFixture(BuriedFind find, float horizontalOffset)
        {
            var cameraPosition = find.transform.position + new Vector3(0, 1.7f, -1.2f);
            var target = find.WorldBounds.center + Vector3.up * find.WorldBounds.extents.y * .7f
                + Vector3.right * horizontalOffset;
            AimCutFixture(cameraPosition, target);
        }

        private void AimCutFixture(Vector3 cameraPosition, Vector3 target)
        {
            Vector3 direction = target - cameraPosition;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.SetPositionAndRotation(cameraPosition - Vector3.up * 1.6f, Quaternion.Euler(0, yaw, 0));
            player.ViewCamera.transform.localPosition = Vector3.up * 1.6f;
            player.ViewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            typeof(FpsPlayer).GetField("pitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(player, pitch);
            motor.enabled = true;
            Physics.SyncTransforms();
        }

        [Test]
        public void ShavingUncoversAndCollectsOneAimedIdentityForItsActualFuelCost()
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0; player.SelectAdminLevel(EquipmentProgression.DrillLevel);
            player.enabled = false;
            HalfCover(find); AimVisible(find);
            int strokes = player.SuccessfulStrokes; float charge = player.Battery.Charge;
            for (int i = 0; i < 80 && !find.Collected; i++)
            {
                AimVisible(find);
                player.Tick(new FpsInputFrame { DigHeld = true }, 1f);
            }
            Assert.That(find.Collected, Is.True);
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - (player.SuccessfulStrokes - strokes) * player.EffectiveDigEnergy).Within(.001f));
        }

        [Test]
        public void ShavingRevealsAnOffAimFindWithoutCollectingOrBypassingAFullBag()
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0; player.SelectAdminLevel(EquipmentProgression.DrillLevel);
            HalfCover(find);
            AimCutFixture(find.transform.position + Vector3.up * 2f,
                find.transform.position + Vector3.right * (player.EffectiveShovel.Radius * .7f));
            Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<TerrainVolume>(), Is.SameAs(terrain));
            Assert.That(player.TryPrimaryAction(), Is.True);
            for (int i = 0; i < 80 && !find.Collectible; i++)
            {
                Assert.That(find.Collected, Is.False, "The wide edge never grants off-aim collection.");
                player.Tick(new FpsInputFrame { DigHeld = true }, 1f);
            }
            Assert.That(find.Collectible, Is.True); Assert.That(find.Collected, Is.False);
            while (!player.Inventory.IsFull) player.Inventory.TryAdd(new InventoryItem("fill-" + player.Inventory.Count, "Carried", 1));
            AimPickupCutFixture(find, 0); float charge = player.Battery.Charge;
            Assert.That(player.TryGetTarget(player.EffectiveDigReach, out hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<BuriedFind>(), Is.SameAs(find));
            Assert.That(player.TryPrimaryAction(), Is.False); Assert.That(find.Collected, Is.False);
            player.Inventory.TryRemove(player.Inventory.Items[0].InstanceId, out _);
            Assert.That(player.TryPrimaryAction(), Is.True); Assert.That(find.Collected, Is.True);
            Assert.That(player.Battery.Charge, Is.EqualTo(charge));
        }

        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void AimedHalfCoveredFindCollectsOnItsRevealingStroke(bool rock, bool automatic)
        {
            var find = field.Finds.First(f => (f.Size == FindSize.Large) == rock);
            player.Tuning.Gravity = 0; player.SelectAdminLevel(EquipmentProgression.DrillLevel);
            HalfCover(find); AimVisible(find);
            int strokes = player.SuccessfulStrokes; float charge = player.Battery.Charge;
            var pose = find.Capture();
            for (int i = 0; i < 80 && !find.Collected; i++)
            {
                AimVisible(find);
                player.Tick(new FpsInputFrame { DigHeld = true, DigPressed = !automatic && i == 0 }, player.EffectiveDigInterval);
                Assert.That(find.Collected, Is.EqualTo(find.Exposure >= find.RequiredExposure),
                    "The exact shave that reaches eligibility must also collect the aimed find.");
            }
            Assert.That(find.Exposure, Is.GreaterThanOrEqualTo(find.RequiredExposure));
            Assert.That(find.Collected, Is.True, "The already-aimed revealing stroke must complete pickup.");
            Assert.That(player.SuccessfulStrokes, Is.GreaterThan(strokes));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - (player.SuccessfulStrokes - strokes) * player.EffectiveDigEnergy).Within(.001f));
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
            Assert.That(find.TryCollect(player), Is.False);
            Assert.That(find.Capture().Position, Is.EqualTo(pose.Position), "Cosmetic travel must not mutate the saved find pose.");
            var proxy = player.transform.Find("Pickup visual");
            Assert.That(proxy, Is.Not.Null); Assert.That(proxy.gameObject.activeSelf, Is.True);
            Assert.That(proxy.GetComponent<Collider>(), Is.Null); Assert.That(proxy.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(proxy.GetComponent<BuriedFind>(), Is.Null);
            Assert.That(proxy.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(find.GetComponent<MeshFilter>().sharedMesh));
        }

        [TestCase(false)] [TestCase(true)]
        public void RevealingStrokeLeavesAFullBagFindAndFailedDigDoesNotCollect(bool full)
        {
            var find = field.Finds[0]; player.Tuning.Gravity = 0; player.SelectAdminLevel(EquipmentProgression.DrillLevel);
            HalfCover(find); AimVisible(find);
            if (full) while (!player.Inventory.IsFull) player.Inventory.TryAdd(new InventoryItem("fill-" + player.Inventory.Count,"Carried",1));
            else player.Battery.TrySpend(player.Battery.Charge);
            int strokes = player.SuccessfulStrokes;
            Assert.That(player.TryPrimaryAction(), Is.EqualTo(full));
            Assert.That(find.Collected, Is.False); Assert.That(find.gameObject.activeSelf, Is.True);
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + (full ? 1 : 0)));
            Assert.That(player.transform.Find("Pickup visual"), Is.Null);
        }

        [UnityTest]
        public IEnumerator WalkingCollectsEveryUncoveredAppearanceWithoutAimOrDig()
        {
            var variants = field.Finds.Where(f => f.Kind == DiscoveryKind.Common).GroupBy(f => f.SaveContentId).Select(g => g.First()).ToArray();
            foreach (var find in variants)
            {
                Place(find,.65f); yield return WaitForSimulation(1.5f);
                Assert.That(find.Collectible, Is.True, find.SaveContentId);
                float charge = player.Battery.Charge; int strokes = player.SuccessfulStrokes;
                WalkTo(find);
                Assert.That(find.Collected, Is.True, $"Walk-over {find.SaveContentId}, exposed={find.Exposure}, feet={player.FeetPosition}, bounds={find.WorldBounds}");
                Assert.That(player.Inventory.Items.Count(i => i.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
                Assert.That(player.Battery.Charge, Is.EqualTo(charge)); Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes));
                foreach (var item in player.Inventory.Items.ToArray()) player.Inventory.TryRemove(item.InstanceId, out _);
            }
        }

        [UnityTest]
        public IEnumerator WalkOverRejectsPartialAndFullBagThenCollectsWhenSpaceReturns()
        {
            var find = field.Finds[0]; HalfCover(find); WalkTo(find);
            Assert.That(find.Collected, Is.False);
            Place(find,.65f); yield return WaitForSimulation(1.5f);
            while (!player.Inventory.IsFull) player.Inventory.TryAdd(new InventoryItem("fill-" + player.Inventory.Count,"Carried",1));
            player.ShowFeedback("Existing feedback"); WalkTo(find);
            Assert.That(find.Collected, Is.False); Assert.That(player.Feedback, Is.EqualTo("Existing feedback"));
            player.Inventory.TryRemove(player.Inventory.Items[0].InstanceId,out _);
            WalkTo(find); Assert.That(find.Collected, Is.True);
        }

        [UnityTest]
        public IEnumerator WalkOverDoesNotCollectThroughCoverOrDuringFocusLoss()
        {
            var find = field.Finds[0]; Place(find,.65f); yield return WaitForSimulation(1.5f);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                cover.transform.position = find.WorldBounds.center + Vector3.up * .16f;
                cover.transform.localScale = new Vector3(2,.05f,2); Physics.SyncTransforms();
                WalkTo(find); Assert.That(find.Collected, Is.False, "A floor/cover between the feet and item must block pickup.");
            }
            finally { Object.DestroyImmediate(cover); }
            player.SetApplicationFocus(false); WalkTo(find);
            Assert.That(find.Collected, Is.False);
            player.SetApplicationFocus(true); if (player.IsMenuOpen) player.CloseMenu();
            yield return null;
            WalkTo(find); Assert.That(find.Collected, Is.True);
        }

        [UnityTest]
        public IEnumerator NearbyWalkingCollectsElevatedFindsButNotAirborneMovement()
        {
            var find=field.Finds[0]; Place(find,1.2f);
            WalkTo(find); Assert.That(find.Collected, Is.True, "Automatic collection must include clear finds above foot height.");
            find=field.Finds[1];
            Place(find,.65f); yield return WaitForSimulation(1.5f);
            var motor=player.GetComponent<CharacterController>(); motor.enabled=false;
            player.transform.position=new Vector3(find.transform.position.x,.025f,find.transform.position.z-.15f);
            motor.enabled=true; Physics.SyncTransforms();
            player.Tick(new FpsInputFrame(),.02f); Assert.That(motor.isGrounded, Is.True);
            player.Tick(new FpsInputFrame{Move=Vector2.up,JumpPressed=true},.01f);
            Assert.That(motor.isGrounded, Is.False);
            Assert.That(find.Collected, Is.False, "Airborne horizontal movement does not count as walking over loot.");
            for(int i=0;i<100;i++)player.Tick(new FpsInputFrame(),.02f);
            Assert.That(find.Collected, Is.False, "Standing/landing without walking must leave the item available.");
            WalkTo(find); Assert.That(find.Collected, Is.True);
        }

        [UnityTest]
        public IEnumerator ExplicitDropRequiresLeavingAndReturningBeforeWalkPickup()
        {
            var find = field.Finds[0]; Place(find,.65f); yield return WaitForSimulation(1.5f);
            player.Tuning.Gravity = 0; AimRock(find);
            Assert.That(player.TryGrabOrDrop(), Is.True);
            Assert.That(player.TryGrabOrDrop(), Is.True);
            player.Tuning.Gravity = -20;
            WalkTo(find, .55f); Assert.That(find.Collected, Is.False, "Walking over the just-dropped find must not undo handling.");
            WalkTo(find, 4.5f); Assert.That(find.Collected, Is.True, "Leaving automatic range and returning re-enables pickup.");
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)]
        public void HeldDigCollectsNearbyOffCentreFindAtEyeHeightUnlessOccluded(bool blocked, bool beyondRange)
        {
            var find=field.Finds[0]; Place(find,1.5f);
            player.Tuning.Gravity=0;
            var motor=player.GetComponent<CharacterController>(); motor.enabled=false;
            player.transform.SetPositionAndRotation(find.WorldBounds.center+Vector3.back*(beyondRange?3f:2f)-Vector3.up*1.65f, Quaternion.identity);
            player.ViewCamera.transform.localPosition=Vector3.up*1.65f;
            motor.enabled=true;
            LookRock(find.WorldBounds.center+Vector3.right*.8f);
            Assert.That(Vector3.Distance(find.GetComponent<Collider>().ClosestPoint(player.ViewCamera.transform.position),
                player.ViewCamera.transform.position), beyondRange ? Is.GreaterThan(2.25f) : Is.InRange(1.5f,2.25f));
            GameObject wall=null;
            if(blocked)
            {
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position=find.WorldBounds.center+Vector3.back*1.2f;
                wall.transform.localScale=new Vector3(2,2,.15f);
            }
            try
            {
                Physics.SyncTransforms();
                int strokes=player.SuccessfulStrokes; float charge=player.Battery.Charge;
                player.Tick(new FpsInputFrame{DigHeld=true},.01f);
                Assert.That(find.Collected, Is.EqualTo(!blocked && !beyondRange));
                Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes));
                Assert.That(player.Battery.Charge, Is.EqualTo(charge));
            }
            finally { if(wall!=null) Object.DestroyImmediate(wall); }
        }

        [UnityTest]
        public IEnumerator PickupVisualPullsQuicklyWithLittleShrinkFreezesAndClearsOnReturn()
        {
            var find = field.Finds[0]; Place(find,.65f); player.Tuning.Gravity = 0; AimRock(find);
            Assert.That(find.TryCollect(player), Is.True);
            var proxy = player.transform.Find("Pickup visual"); Vector3 start = proxy.position; float scale = proxy.localScale.magnitude;
            Quaternion rotation = proxy.rotation;
            player.enabled = true; player.SetApplicationFocus(true);
            yield return WaitForSimulation(.06f);
            Assert.That(proxy.gameObject.activeSelf, Is.True);
            Assert.That(proxy.localScale.magnitude, Is.InRange(scale*.85f, scale));
            Assert.That(Quaternion.Angle(proxy.rotation, rotation), Is.LessThan(.01f), "Pickup must not add a decorative spin.");
            Assert.That(Vector3.Distance(proxy.position,start), Is.GreaterThan(.025f));
            player.OpenMenu(PlayerMenu.Pause); var pausedPosition = proxy.position; var pausedScale = proxy.localScale;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(proxy.position, Is.EqualTo(pausedPosition)); Assert.That(proxy.localScale, Is.EqualTo(pausedScale));
            player.CloseMenu(); yield return WaitForSimulation(.16f);
            Assert.That(proxy.gameObject.activeSelf, Is.False); Assert.That(find.Collected, Is.True);
            var next = field.Finds[1]; Place(next,.65f); AimRock(next); Assert.That(next.TryCollect(player), Is.True);
            player.AdminReturnToSurface(); Assert.That(proxy.gameObject.activeSelf, Is.False);
            Assert.That(player.Inventory.Count, Is.EqualTo(2));
        }

        private void HalfCover(BuriedFind find)
        {
            for (float y=-.25f;y<.3f;y+=.001f)
            {
                Place(find,y);
                if (find.Exposure >= .48f && find.Exposure <= .55f) return;
            }
            Assert.Fail("Could not prepare a half-exposed real model: " + find.SaveContentId);
        }

        private void AimVisible(BuriedFind find)
        {
            AimRock(find);
            LookRock(find.WorldBounds.center + Vector3.up * find.WorldBounds.extents.y * .7f);
            Assert.That(player.TryGetTarget(3,out var hit), Is.True);
            Assert.That(hit.collider, Is.SameAs(find.GetComponent<Collider>()), "Reproduce direct aim on the visible item surface.");
        }

        private void WalkTo(BuriedFind find, float approach=1f)
        {
            var motor = player.GetComponent<CharacterController>(); motor.enabled=false;
            player.transform.SetPositionAndRotation(new Vector3(find.transform.position.x,.025f,find.transform.position.z-approach),Quaternion.identity);
            player.ViewCamera.transform.localPosition=new Vector3(0,1.65f,0);
            motor.enabled=true; Physics.SyncTransforms();
            LookRock(player.ViewCamera.transform.position+Vector3.forward*4+Vector3.up);
            for (int i=0;i<Mathf.CeilToInt((approach+.55f)/.08f);i++)
                player.Tick(new FpsInputFrame{Move=Vector2.up},.02f);
        }
    }
}
#endif
