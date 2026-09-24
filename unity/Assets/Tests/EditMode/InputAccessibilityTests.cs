using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SomethingDownThere.Tests
{
    public sealed class InputAccessibilityTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private FpsInput input;
        private InputPreferences settings;
        private Store store;
        private sealed class Store : IDevicePreferencesStore
        {
            public string Text;
            public int Writes;
            public bool Fail;
            public string Read() => Text;
            public void Write(string text) { Writes++; if (Fail) throw new IOException(); Text = text; }
        }

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            store = new Store(); settings = new InputPreferences(store);
            input = new FpsInput(settings); input.Enable(); InputSystem.Update(); input.Read();
        }
        public override void TearDown() { input.Dispose(); base.TearDown(); }

        [Test]
        public void OrdinaryBindingsCanUseQWithoutAConflictOrLostBinding()
        {
            Assert.That(settings.CanBind(PlayerBinding.Dig, "<Keyboard>/q", out int conflict), Is.True);
            Assert.That(conflict, Is.EqualTo(-1));
            Assert.That(settings.Bind(PlayerBinding.Dig, "<Keyboard>/q"), Is.True);
            Assert.That(Enumerable.Range(0, InputPreferences.BindingCount).Select(i => settings.Path((PlayerBinding)i)).Distinct().Count(), Is.EqualTo(InputPreferences.BindingCount));
            settings.Flush();
            var restored = new InputPreferences(store);
            Assert.That(restored.Path(PlayerBinding.Dig), Is.EqualTo("<Keyboard>/q"));
        }

        [Test]
        public void AllBindingsRoundTripAndConflictsSwapWithoutLosingAnAction()
        {
            var keys = new[] { "i", "k", "j", "l", "q", "r", "c", "f", "b", "p", "o", "u", "h", "n", "v" };
            for (int i = 0; i < InputPreferences.BindingCount; i++) Assert.That(settings.Bind((PlayerBinding)i, "<Keyboard>/" + keys[i], true), Is.True);
            Assert.That(settings.Bind(PlayerBinding.Dig, "<Keyboard>/r"), Is.False);
            Assert.That(settings.Bind(PlayerBinding.Dig, "<Keyboard>/r", true), Is.True);
            Assert.That(settings.Path(PlayerBinding.Jump), Is.EqualTo("<Keyboard>/q"));
            settings.SetToggleDig(true); Assert.That(settings.Flush(), Is.True);
            var restored = new InputPreferences(store);
            Assert.That(restored.ToggleDig, Is.True);
            for (int i = 0; i < InputPreferences.BindingCount; i++) Assert.That(restored.Path((PlayerBinding)i), Is.EqualTo(settings.Path((PlayerBinding)i)));
            Assert.That(restored.HasUnsavedChanges, Is.False);
            Assert.That(restored.Flush(), Is.True); Assert.That(store.Writes, Is.EqualTo(1));
        }

        [Test]
        public void RetiredBindingFieldsAreIgnoredWithoutStealingThePlayersDigKey()
        {
            settings.Bind(PlayerBinding.Dig, "<Keyboard>/q", true); settings.SetToggleDig(true); settings.Flush();
            store.Text += "cycleMode=<Keyboard>/q\n";
            string old = store.Text;
            var restored = new InputPreferences(store);
            Assert.That(restored.Path(PlayerBinding.Dig), Is.EqualTo("<Keyboard>/q"));
            Assert.That(restored.ToggleDig, Is.True);
            restored.Flush(); Assert.That(store.Text, Is.EqualTo(old));
            input.ConfigurePreferences(restored); InputSystem.Update(); input.Read();
            Press(keyboard.fKey);
            Assert.That(input.Read().DigPressed, Is.False);
        }

        [TestCase("<Gamepad>/buttonSouth")]
        [TestCase("<Keyboard>/anything")]
        [TestCase("<Mouse>/delta")]
        [TestCase("<Keyboard>/*")]
        public void InvalidOrUnsupportedBindingsAreRejected(string path)
        {
            Assert.That(settings.Bind(PlayerBinding.Dig, path), Is.False);
            Assert.That(settings.Path(PlayerBinding.Dig), Is.EqualTo("<Mouse>/leftButton"));
        }

        [Test]
        public void MalformedAndDuplicateFilesKeepACompleteDefaultMapWithoutOverwritingDisk()
        {
            settings.SetToggleDig(true);
            settings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true); settings.Flush();
            string valid = store.Text;
            foreach (string text in new[]
            {
                "nonsense", valid.Replace("version=1", "version=99"),
                valid.Replace("<Keyboard>/w", "<Keyboard>/s"),
                valid.Replace("<Mouse>/rightButton", "<Mouse>/delta"),
                valid.Replace("forward=<Keyboard>/w\n", ""),
                valid.Replace("inventory=<Keyboard>/tab\n", ""),
                valid.Substring(0, valid.IndexOf("inventory=", StringComparison.Ordinal)),
                valid.Replace("sprint=<Keyboard>/leftShift", "sprint=<Mouse>/delta"),
                valid.Replace("grab=<Mouse>/leftButton", "grab=<Mouse>/delta"),
                valid + "dig=<Keyboard>/q\n", valid + "toggleDig=false\n"
            })
            {
                store.Text = text;
                var restored = new InputPreferences(store);
                Assert.That(restored.ToggleDig, Is.False, "A rejected map must restore Hold together with the default keys.");
                for (int i = 0; i < InputPreferences.BindingCount; i++) Assert.That(restored.Path((PlayerBinding)i), Is.EqualTo(InputPreferences.DefaultPath((PlayerBinding)i)));
                Assert.That(restored.HasUnsavedChanges || restored.WriteFailed, Is.False);
                restored.Flush(); Assert.That(store.Text, Is.EqualTo(text)); Assert.That(store.Writes, Is.EqualTo(1));
            }
        }

        [Test]
        public void FailedWriteRetainsSessionValuesAndRetryAndResetOnlyChangeInputPreferences()
        {
            settings.SetToggleDig(true); settings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true);
            store.Fail = true;
            Assert.That(settings.Flush(), Is.False); Assert.That(settings.WriteFailed, Is.True);
            Assert.That(settings.ToggleDig, Is.True); Assert.That(settings.HasUnsavedChanges, Is.True);
            store.Fail = false; Assert.That(settings.Flush(), Is.True);
            Assert.That(new InputPreferences(store).ToggleDig, Is.True);
            settings.Reset();
            var restored = new InputPreferences(store);
            Assert.That(restored.ToggleDig, Is.False);
            Assert.That(restored.Path(PlayerBinding.Dig), Is.EqualTo("<Mouse>/leftButton"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecoveredMapStopsDiggingOnReleaseAndReplacesDamagedFileOnlyAfterAnExplicitEdit(bool reset)
        {
            settings.SetToggleDig(true); settings.Bind(PlayerBinding.Dig, "<Keyboard>/q", true); settings.Flush();
            store.Text = store.Text.Replace("grab=<Mouse>/rightButton", "grab=<Mouse>/delta");
            string damaged = store.Text;
            var restored = new InputPreferences(store);
            input.ConfigurePreferences(restored); InputSystem.Update(); input.Read();
            Press(keyboard.fKey);
            Press(mouse.leftButton); Assert.That(input.Read().DigHeld, Is.True);
            Release(mouse.leftButton); Assert.That(input.Read().DigHeld, Is.False, "Recovered input must stop on release.");
            restored.Flush(); Assert.That(store.Text, Is.EqualTo(damaged)); Assert.That(store.Writes, Is.EqualTo(1));

            if (reset) restored.Reset();
            else restored.Bind(PlayerBinding.Dig, "<Keyboard>/q", true);
            Assert.That(restored.Flush(), Is.True);
            Assert.That(store.Writes, Is.EqualTo(2));
            var reloaded = new InputPreferences(store);
            Assert.That(reloaded.ToggleDig, Is.False);
            Assert.That(reloaded.Path(PlayerBinding.Dig), Is.EqualTo(reset ? "<Mouse>/leftButton" : "<Keyboard>/q"));
            for (int i = 0; i < InputPreferences.BindingCount; i++)
                Assert.That(reloaded.Path((PlayerBinding)i), Is.EqualTo(restored.Path((PlayerBinding)i)));
        }

        [Test]
        public void IncompleteBindingMapsAreRejectedWithoutAPartialToggleState()
        {
            settings.SetToggleDig(true);
            settings.Bind(PlayerBinding.Dig, "<Keyboard>/q");
            settings.Flush();
            store.Text = store.Text.Substring(0, store.Text.IndexOf("rotatePlacement=", StringComparison.Ordinal));
            var restored = new InputPreferences(store);
            Assert.That(restored.ToggleDig, Is.False);
            for (int i = 0; i < InputPreferences.BindingCount; i++)
                Assert.That(restored.Path((PlayerBinding)i), Is.EqualTo(InputPreferences.DefaultPath((PlayerBinding)i)));
        }

        [Test]
        public void SprintUsesCurrentHeldBindingAndMenuStateWithoutALatch()
        {
            Press(keyboard.leftShiftKey); Assert.That(input.Read().SprintHeld, Is.True);
            Assert.That(input.Read(false).SprintHeld, Is.False);
            input.SuppressHeldActions(); Assert.That(input.Read().SprintHeld, Is.True);
            Release(keyboard.leftShiftKey); Assert.That(input.Read().SprintHeld, Is.False);
            settings.Bind(PlayerBinding.Sprint, "<Mouse>/rightButton", true);
            InputSystem.Update(); Press(mouse.rightButton);
            Assert.That(input.Read().SprintHeld, Is.True);
            input.Disable(); input.Enable(); InputSystem.Update();
            Assert.That(input.Read().SprintHeld, Is.True);
            Release(mouse.rightButton); Assert.That(input.Read().SprintHeld, Is.False);
        }

        [Test]
        public void RemappedMovementAndEveryButtonUseTheirActiveBindings()
        {
            settings.Bind(PlayerBinding.Forward, "<Keyboard>/i"); settings.Bind(PlayerBinding.Right, "<Keyboard>/l", true);
            settings.Bind(PlayerBinding.Dig, "<Mouse>/rightButton", true); settings.Bind(PlayerBinding.Jump, "<Mouse>/middleButton");
            settings.Bind(PlayerBinding.Crouch, "<Keyboard>/c"); settings.Bind(PlayerBinding.Interact, "<Mouse>/backButton");
            settings.Bind(PlayerBinding.Inventory, "<Mouse>/forwardButton"); settings.Bind(PlayerBinding.Pause, "<Keyboard>/p");
            InputSystem.Update(); input.Read();
            Press(keyboard.iKey); Press(keyboard.lKey);
            Assert.That(Vector2.Distance(input.Read().Move, new Vector2(1, 1).normalized), Is.LessThan(0.00001f));
            Press(mouse.rightButton); Assert.That(input.Read().DigHeld, Is.True);
            Press(mouse.middleButton); Assert.That(input.Read().JumpPressed, Is.True); Assert.That(input.Read().JetpackHeld, Is.True);
            Press(keyboard.cKey); Assert.That(input.Read().CrouchHeld, Is.True);
            Press(mouse.backButton); Assert.That(input.Read().InteractPressed, Is.True);
            Press(mouse.forwardButton); Assert.That(input.Read().InventoryPressed, Is.True);
            Press(keyboard.pKey); Assert.That(input.Read().BackPressed, Is.True);
            Release(keyboard.pKey); input.Read(false); Press(keyboard.escapeKey);
            Assert.That(input.Read(false).BackPressed, Is.True, "Escape always remains a way back from menus.");
        }

        [Test]
        public void ReboundPauseAndInventoryCannotStealMenuSubmitPointerOrArrowNavigation()
        {
            settings.Bind(PlayerBinding.Pause, "<Keyboard>/enter");
            settings.Bind(PlayerBinding.Inventory, "<Mouse>/leftButton", true);
            InputSystem.Update(); input.Read(false);
            Press(keyboard.enterKey); Assert.That(input.Read(false).BackPressed, Is.False);
            Press(mouse.leftButton); Assert.That(input.Read(false).InventoryPressed, Is.False);
            Release(keyboard.enterKey); Release(mouse.leftButton);
            settings.Bind(PlayerBinding.Pause, "<Keyboard>/downArrow");
            InputSystem.Update(); input.Read(false);
            Press(keyboard.downArrowKey); Assert.That(input.Read(false).BackPressed, Is.False);
            Press(keyboard.escapeKey); Assert.That(input.Read(false).BackPressed, Is.True);
            Release(keyboard.downArrowKey); Release(keyboard.escapeKey); input.Read();
            Press(keyboard.downArrowKey); Assert.That(input.Read().BackPressed, Is.True);
        }

        [Test]
        public void ToggleUsesOneEdgePerInputUpdateAndStopDoesNotDig()
        {
            settings.SetToggleDig(true); InputSystem.Update(); input.Read();
            Press(mouse.leftButton);
            Assert.That(input.Read().DigHeld, Is.True); Assert.That(input.Read().DigHeld, Is.True);
            Release(mouse.leftButton); Assert.That(input.Read().DigHeld, Is.True);
            Press(mouse.leftButton);
            var stopped = input.Read(); Assert.That(stopped.DigHeld || stopped.DigPressed, Is.False);
            Assert.That(input.Read().DigHeld, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MenuAndResumeNeedReleaseAndFreshPressInBothModes(bool toggle)
        {
            settings.SetToggleDig(toggle); InputSystem.Update(); input.Read();
            Press(mouse.leftButton); Assert.That(input.Read().DigHeld, Is.True);
            input.SuppressHeldActions(); Assert.That(input.Read(false).DigHeld, Is.False);
            Assert.That(input.Read().DigHeld, Is.False);
            Release(mouse.leftButton); Assert.That(input.Read().DigHeld, Is.False);
            Press(mouse.leftButton); Assert.That(input.Read().DigHeld, Is.True);
            settings.SetToggleDig(!toggle); InputSystem.Update(); Assert.That(input.Read().DigHeld, Is.False);
        }

        [Test]
        public void CaptureIgnoresOpeningButtonsAndMotionAndQuarantinesTheAcceptedPress()
        {
            var capture = new InputBindingCapture(settings);
            Press(keyboard.enterKey); capture.Begin(PlayerBinding.Dig); capture.Tick();
            Assert.That(capture.State, Is.EqualTo(BindingCaptureState.ReleaseButtons));
            Release(keyboard.enterKey); capture.Tick(); Assert.That(capture.State, Is.EqualTo(BindingCaptureState.Listening));
            Set(mouse.delta, new Vector2(40, 20)); capture.Tick(); Assert.That(capture.State, Is.EqualTo(BindingCaptureState.Listening));
            Press(mouse.middleButton); capture.Tick();
            Assert.That(settings.Path(PlayerBinding.Dig), Is.EqualTo("<Mouse>/middleButton"));
            Assert.That(capture.BlocksInput, Is.True);
            capture.Tick(); Assert.That(capture.BlocksInput, Is.True);
            Release(mouse.middleButton); capture.Tick(); Assert.That(capture.BlocksInput, Is.False);
            Assert.That(input.Read().DigHeld, Is.False);
        }

        [Test]
        public void PlacementBindingsRespectRebindSuppressionAndMenuGates()
        {
            settings.Bind(PlayerBinding.Lamp, "<Keyboard>/q"); InputSystem.Update(); input.Read();
            Press(keyboard.qKey); Assert.That(input.Read().LampPressed, Is.True);
            Assert.That(input.Read(false).LampPressed, Is.False);
            input.SuppressHeldActions(); Assert.That(input.Read().LampPressed, Is.False);
            Release(keyboard.qKey); input.Read(); Press(keyboard.qKey); Assert.That(input.Read().LampPressed, Is.True);
            Press(keyboard.mKey); Assert.That(input.Read().MarkPressed, Is.True);
            Press(keyboard.rKey); Assert.That(input.Read().RotatePlacementPressed, Is.True);
            input.SuppressHeldActions();
            Assert.That(input.Read().MarkPressed || input.Read().RotatePlacementPressed, Is.False);
        }

        [Test]
        public void GrabAndThrowUseFreshBoundPressesAndClearOnSuppression()
        {
            Press(mouse.rightButton); Assert.That(input.Read().GrabPressed, Is.True);
            input.SuppressHeldActions(); Assert.That(input.Read().GrabPressed, Is.False);
            Release(mouse.rightButton); input.Read();
            settings.SetToggleDig(true); InputSystem.Update(); input.Read();
            Press(mouse.leftButton); Assert.That(input.Read().ThrowPressed, Is.True);
            Release(mouse.leftButton); input.Read();
            Press(mouse.leftButton); var stopped = input.Read();
            Assert.That(stopped.DigHeld, Is.False); Assert.That(stopped.ThrowPressed, Is.True, "Throw is a press even when it would stop toggle digging.");
            input.SuppressHeldActions(); Assert.That(input.Read().ThrowPressed, Is.False);
        }

        [Test]
        public void CaptureConflictRequiresExplicitReplaceAndEscapeKeepsTheBinding()
        {
            var capture = new InputBindingCapture(settings);
            capture.Begin(PlayerBinding.Dig); capture.Tick(); Press(keyboard.spaceKey); capture.Tick();
            Assert.That(capture.State, Is.EqualTo(BindingCaptureState.Conflict));
            Assert.That(settings.Path(PlayerBinding.Dig), Is.EqualTo("<Mouse>/leftButton"));
            Release(keyboard.spaceKey); capture.Tick(); capture.Replace();
            Assert.That(settings.Path(PlayerBinding.Dig), Is.EqualTo("<Keyboard>/space"));
            Assert.That(settings.Path(PlayerBinding.Jump), Is.EqualTo("<Mouse>/leftButton"));
            InputSystem.Update(); capture.Tick(); capture.Begin(PlayerBinding.Dig); capture.Tick();
            Press(keyboard.escapeKey); capture.Tick();
            Assert.That(capture.State, Is.EqualTo(BindingCaptureState.Idle));
            Assert.That(settings.Path(PlayerBinding.Dig), Is.EqualTo("<Keyboard>/space"));
        }
    }
}
