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
        [Test]
        public void ScoopUncoversAndCollectsOneAimedIdentityForItsActualFuelCost()
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0; player.SelectAdminLevel(6);
            player.enabled = false;
            HalfCover(find); AimVisible(find);
            int strokes = player.SuccessfulStrokes; float charge = player.Battery.Charge;
            for (int i = 0; i < 16 && !find.Collected; i++)
            {
                AimVisible(find);
                player.Tick(new FpsInputFrame { DigHeld = true }, 1f);
            }
            Assert.That(find.Collected, Is.True);
            Assert.That(player.Inventory.Items.Count(i => i.InstanceId == find.Item.InstanceId), Is.EqualTo(1));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - (player.SuccessfulStrokes - strokes) * player.EffectiveDigEnergy).Within(.001f));
        }

        [Test]
        public void ScoopRevealsAnOffAimFindWithoutCollectingOrBypassingAFullBag()
        {
            var find = field.Finds.First(f => f.SaveContentId == "mineral_coal");
            player.Tuning.Gravity = 0; player.SelectAdminLevel(6);
            HalfCover(find);
            player.ViewCamera.transform.position = find.transform.position + Vector3.up * 2f;
            player.ViewCamera.transform.rotation = Quaternion.LookRotation(
                find.transform.position + Vector3.right * (player.EffectiveShovel.Radius * .7f)
                - player.ViewCamera.transform.position, Vector3.forward);
            Physics.SyncTransforms();
            Assert.That(player.TryGetTarget(player.EffectiveDigReach, out var hit), Is.True);
            Assert.That(hit.collider.GetComponentInParent<TerrainVolume>(), Is.SameAs(terrain));
            Assert.That(player.TryPrimaryAction(), Is.True);
            for (int i = 0; i < 4 && !find.Collectible; i++)
            {
                Assert.That(find.Collected, Is.False, "The wide edge never grants off-aim collection.");
                player.Tick(new FpsInputFrame { DigHeld = true }, 1f);
            }
            Assert.That(find.Collectible, Is.True); Assert.That(find.Collected, Is.False);
            while (!player.Inventory.IsFull) player.Inventory.TryAdd(new InventoryItem("fill-" + player.Inventory.Count, "Carried", 1));
            AimRock(find); float charge = player.Battery.Charge;
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
            player.Tuning.Gravity = 0; player.SelectAdminLevel(6);
            HalfCover(find); AimVisible(find);
            int strokes = player.SuccessfulStrokes; float charge = player.Battery.Charge;
            var pose = find.Capture();
            player.Tick(new FpsInputFrame { DigHeld = true, DigPressed = !automatic }, .01f);
            Assert.That(find.Exposure, Is.GreaterThanOrEqualTo(find.RequiredExposure));
            Assert.That(find.Collected, Is.True, "The already-aimed revealing stroke must complete pickup.");
            Assert.That(player.SuccessfulStrokes, Is.EqualTo(strokes + 1));
            Assert.That(player.Battery.Charge, Is.EqualTo(charge - player.Tuning.DigEnergy));
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
            var find = field.Finds[0]; player.Tuning.Gravity = 0; player.SelectAdminLevel(6);
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
            var variants = field.Finds.GroupBy(f => f.SaveContentId).Select(g => g.First()).ToArray();
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
        public IEnumerator WalkOverRejectsElevatedFindsAndAirborneMovement()
        {
            var find=field.Finds[0]; Place(find,1.2f);
            WalkTo(find); Assert.That(find.Collected, Is.False, "Walking beneath a raised find does not collect it.");
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
            WalkTo(find, 2f); Assert.That(find.Collected, Is.True, "Leaving the item and returning re-enables walk pickup.");
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
