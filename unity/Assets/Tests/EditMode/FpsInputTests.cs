using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomethingDownThere.Tests
{
    public sealed class FpsInputTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private FpsInput input;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            input = new FpsInput();
            input.Enable();
            InputSystem.Update();
            input.Read();
        }

        public override void TearDown()
        {
            input.Dispose();
            base.TearDown();
        }



        [Test]
        public void WasdDiagonalIsNormalizedAndMouseDeltaIsPreserved()
        {
            Press(keyboard.wKey);
            Press(keyboard.dKey);
            Set(mouse.delta, new Vector2(5, -3));
            var frame = input.Read();
            Assert.That(frame.Move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(frame.Look, Is.EqualTo(new Vector2(5, -3)));
        }

        [Test]
        public void JumpAndInteractAreEdgesButDigAndJetpackRemainHeld()
        {
            Press(keyboard.eKey, queueEventOnly: true);
            Press(keyboard.spaceKey, queueEventOnly: true);
            Press(mouse.leftButton);
            Assert.That(input.Read().InteractPressed, Is.True);
            Assert.That(input.Read().JumpPressed, Is.True);
            Assert.That(input.Read().DigPressed, Is.True);
            InputSystem.Update();
            var held = input.Read();
            Assert.That(held.InteractPressed, Is.False);
            Assert.That(held.JumpPressed, Is.False);
            Assert.That(held.DigHeld, Is.True);
            Assert.That(held.DigPressed, Is.False);
            Assert.That(held.JetpackHeld, Is.True);
        }

        [Test]
        public void ResumeRequiresReleaseOfEachHeldWorldAction()
        {
            Press(keyboard.eKey, queueEventOnly: true);
            Press(keyboard.spaceKey, queueEventOnly: true);
            Press(mouse.leftButton);
            input.SuppressHeldActions();
            var blocked = input.Read();
            Assert.That(blocked.InteractPressed || blocked.DigHeld || blocked.DigPressed || blocked.JumpPressed || blocked.JetpackHeld, Is.False);
            Release(mouse.leftButton);
            input.Read();
            Press(mouse.leftButton);
            var partlyReleased = input.Read();
            Assert.That(partlyReleased.DigHeld, Is.True);
            Assert.That(partlyReleased.DigPressed, Is.True);
            Assert.That(partlyReleased.JumpPressed || partlyReleased.JetpackHeld || partlyReleased.InteractPressed, Is.False);
            Release(keyboard.eKey);
            Release(keyboard.spaceKey);
            input.Read();
            Press(keyboard.eKey, queueEventOnly: true);
            Press(keyboard.spaceKey);
            var rearmed = input.Read();
            Assert.That(rearmed.InteractPressed && rearmed.JumpPressed && rearmed.JetpackHeld, Is.True);
        }

        [Test]
        public void MenuBindingsRemainAvailableWhileWorldActionsAreSuppressed()
        {
            input.SuppressHeldActions();
            Press(keyboard.tabKey);
            Assert.That(input.Read().InventoryPressed, Is.True);
            Press(keyboard.escapeKey);
            Assert.That(input.Read().BackPressed, Is.True);
        }

        [Test]
        public void AdminChordsUseNumberRowAndSinglePressEdges()
        {
            Press(keyboard.leftCtrlKey);
            Press(keyboard.leftShiftKey);
            foreach (var key in new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 })
            {
                Press(keyboard[key]);
                Assert.That(input.Read().AdminLevel, Is.EqualTo(key == Key.Digit0 ? 10 : (int)key - (int)Key.Digit1 + 1));
                InputSystem.Update();
                Assert.That(input.Read().AdminLevel, Is.Zero);
                Release(keyboard[key]);
            }
            Press(keyboard.rKey);
            Assert.That(input.Read().RefillPressed, Is.True);
            Press(keyboard.homeKey);
            Assert.That(input.Read().ReturnPressed, Is.True);
            Press(keyboard.xKey);
            Assert.That(input.Read().XrayPressed, Is.True);
            InputSystem.Update();
            Assert.That(input.Read().RefillPressed || input.Read().ReturnPressed || input.Read().XrayPressed, Is.False);
        }

        [Test]
        public void NumpadCanSelectEveryShovelStrength()
        {
            Press(keyboard.rightCtrlKey);
            Press(keyboard.rightShiftKey);
            foreach (var key in new[] { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9, Key.Numpad0 })
            {
                Press(keyboard[key]);
                Assert.That(input.Read().AdminLevel, Is.EqualTo(key == Key.Numpad0 ? 10 : (int)key - (int)Key.Numpad1 + 1));
                Release(keyboard[key]);
            }
        }

        [Test]
        public void AdminRequiresBothModifiersAndAFreshActionKeyPress()
        {
            foreach (var key in new[] { Key.R, Key.Home, Key.Digit6, Key.Numpad6, Key.F10, Key.X })
            {
                Press(keyboard[key]);
                var plain = input.Read();
                Assert.That(plain.AdminLevel, Is.Zero);
                Assert.That(plain.RefillPressed || plain.ReturnPressed || plain.AdminMenuPressed || plain.XrayPressed, Is.False);
                Press(keyboard.leftCtrlKey);
                Press(keyboard.leftShiftKey);
                var lateModifiers = input.Read();
                Assert.That(lateModifiers.AdminLevel, Is.Zero);
                Assert.That(lateModifiers.RefillPressed || lateModifiers.ReturnPressed || lateModifiers.AdminMenuPressed || lateModifiers.XrayPressed, Is.False);
                Release(keyboard[key]);
                Release(keyboard.leftCtrlKey);
                Release(keyboard.leftShiftKey);
            }
            Press(keyboard.leftCtrlKey);
            Press(keyboard.f10Key);
            Assert.That(input.Read().AdminMenuPressed, Is.False);
            Release(keyboard.f10Key);
            Press(keyboard.rightShiftKey);
            Press(keyboard.f10Key);
            Assert.That(input.Read().AdminMenuPressed, Is.True);
            InputSystem.Update();
            Assert.That(input.Read().AdminMenuPressed, Is.False);
        }

        [Test]
        public void MissingDevicesProduceNeutralInput()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.Update();
            var frame = input.Read();
            Assert.That(frame.Move, Is.EqualTo(Vector2.zero));
            Assert.That(frame.Look, Is.EqualTo(Vector2.zero));
            Assert.That(frame.DigHeld || frame.JetpackHeld || frame.InteractPressed || frame.CrouchHeld, Is.False);
        }

        [Test]
        public void CrouchSamplesOnlyLeftCtrlAcrossReleaseBarriersAndReenable()
        {
            Press(keyboard.leftShiftKey);
            Press(keyboard.rightShiftKey);
            Press(keyboard.rightCtrlKey);
            Assert.That(input.Read().CrouchHeld, Is.False);
            Release(keyboard.leftShiftKey);
            Release(keyboard.rightShiftKey);
            Release(keyboard.rightCtrlKey);
            Press(keyboard.leftCtrlKey);
            Press(keyboard.spaceKey);
            Press(mouse.leftButton);
            input.SuppressHeldActions();
            var resumed = input.Read();
            Assert.That(resumed.CrouchHeld, Is.True);
            Assert.That(resumed.DigHeld || resumed.JetpackHeld, Is.False);
            input.Disable();
            input.Enable();
            InputSystem.Update();
            Assert.That(input.Read().CrouchHeld, Is.True, "Held Ctrl is sampled before movement after enable.");
            Assert.That(input.Read().DigHeld || input.Read().JetpackHeld, Is.False);
            Release(keyboard.leftCtrlKey);
            Assert.That(input.Read().CrouchHeld, Is.False);
        }
    }
}
