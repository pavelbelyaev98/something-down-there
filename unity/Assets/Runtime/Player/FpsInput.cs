using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SomethingDownThere
{
    public struct FpsInputFrame
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool DigHeld;
        public bool DigPressed;
        public bool GrabPressed;
        public bool ThrowPressed;
        public bool JumpPressed;
        public bool JetpackHeld;
        public bool CrouchHeld;
        public bool SprintHeld;
        public bool InteractPressed;
        public bool InventoryPressed;
        public bool BackPressed;
        public bool AdminMenuPressed;
        public int AdminLevel;
        public bool RefillPressed;
        public bool ReturnPressed;
        public bool XrayPressed;
    }

    public sealed class FpsInput : IDisposable
    {
        private readonly InputActionMap actions = new InputActionMap("FPS");
        private readonly InputAction move, look, dig, grab, jetpack, crouch, sprint, interact, inventory, back, escape;
        private readonly InputAction refill, returnToSurface, adminMenu, adminCtrl, adminShift, xray;
        private readonly InputAction[] adminLevels = new InputAction[6];
        private bool digArmed, grabArmed, jetpackArmed, interactArmed, inventoryArmed, backArmed, escapeArmed, toggleIntent;
        private InputPreferences preferences;
        private int preferenceRevision = -1;
        private uint lastToggleUpdate = uint.MaxValue;

        public FpsInput(InputPreferences preferences = null)
        {
            move = actions.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            look = actions.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            dig = actions.AddAction("Dig", InputActionType.Button, "<Mouse>/leftButton");
            grab = actions.AddAction("Grab", InputActionType.Button, "<Mouse>/rightButton");
            jetpack = actions.AddAction("JumpAndJetpack", InputActionType.Button, "<Keyboard>/space");
            crouch = actions.AddAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            crouch.wantsInitialStateCheck = true;
            sprint = actions.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprint.wantsInitialStateCheck = true;
            interact = actions.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            inventory = actions.AddAction("Inventory", InputActionType.Button, "<Keyboard>/tab");
            back = actions.AddAction("Back", InputActionType.Button, "<Keyboard>/escape");
            escape = actions.AddAction("MenuEscape", InputActionType.Button, "<Keyboard>/escape");
            refill = actions.AddAction("AdminRefill", InputActionType.Button, "<Keyboard>/r");
            returnToSurface = actions.AddAction("AdminReturn", InputActionType.Button, "<Keyboard>/home");
            adminMenu = actions.AddAction("AdminMenu", InputActionType.Button, "<Keyboard>/f10");
            xray = actions.AddAction("AdminXray", InputActionType.Button, "<Keyboard>/x");
            adminCtrl = actions.AddAction("AdminCtrl", InputActionType.Button, "<Keyboard>/ctrl");
            adminShift = actions.AddAction("AdminShift", InputActionType.Button, "<Keyboard>/shift");
            for (int i = 0; i < adminLevels.Length; i++)
            {
                adminLevels[i] = actions.AddAction("AdminLevel" + (i + 1), InputActionType.Button, "<Keyboard>/" + (i + 1));
                adminLevels[i].AddBinding("<Keyboard>/numpad" + (i + 1));
            }
            if (preferences != null) ConfigurePreferences(preferences);
        }

        public void ConfigurePreferences(InputPreferences settings)
        {
            if (preferences != null) preferences.Changed -= ApplyPreferences;
            preferences = settings;
            preferenceRevision = -1;
            preferences.Changed += ApplyPreferences;
            ApplyPreferences();
        }

        private void ApplyPreferences()
        {
            if (preferenceRevision == preferences.Revision) return;
            bool enabled = actions.enabled;
            actions.Disable();
            for (int i = 0; i < 4; i++) move.ApplyBindingOverride(i + 1, preferences.Path((PlayerBinding)i));
            dig.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Dig));
            grab.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Grab));
            jetpack.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Jump));
            crouch.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Crouch));
            sprint.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Sprint));
            interact.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Interact));
            inventory.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Inventory));
            back.ApplyBindingOverride(0, preferences.Path(PlayerBinding.Pause));
            preferenceRevision = preferences.Revision;
            SuppressHeldActions();
            if (enabled) actions.Enable();
        }

        public void Enable()
        {
            SuppressHeldActions();
            actions.Enable();
        }

        public void Disable() { SuppressHeldActions(); actions.Disable(); }

        public void SuppressHeldActions()
        {
            digArmed = grabArmed = jetpackArmed = interactArmed = false;
            inventoryArmed = !inventory.IsPressed();
            backArmed = !back.IsPressed();
            escapeArmed = !escape.IsPressed();
            toggleIntent = false;
            lastToggleUpdate = InputState.updateCount;
        }

        public FpsInputFrame Read(bool gameplayActive = true)
        {
            bool digHeld = dig.IsPressed();
            bool jetpackHeld = jetpack.IsPressed();
            bool interactHeld = interact.IsPressed();
            if (!digHeld) digArmed = true;
            if (!grab.IsPressed()) grabArmed = true;
            if (!jetpackHeld) jetpackArmed = true;
            if (!interactHeld) interactArmed = true;
            if (!inventory.IsPressed()) inventoryArmed = true;
            if (!back.IsPressed()) backArmed = true;
            if (!escape.IsPressed()) escapeArmed = true;
            bool toggle = preferences != null && preferences.ToggleDig;
            bool digPressed = digArmed && dig.WasPressedThisFrame();
            if (!gameplayActive) toggleIntent = false;
            else if (toggle && digPressed && lastToggleUpdate != InputState.updateCount) toggleIntent = !toggleIntent;
            lastToggleUpdate = InputState.updateCount;
            int adminLevel = 0;
            // Require modifiers before the action-key edge. Pressing Ctrl/Shift after
            // an ordinary key is held must never turn that old press into an admin action.
            bool adminChord = FpsPlayer.AdminBuild && adminCtrl.IsPressed() && adminShift.IsPressed();
            for (int i = 0; i < adminLevels.Length; i++)
                if (adminChord && adminLevels[i].WasPressedThisFrame()) adminLevel = i + 1;

            return new FpsInputFrame
            {
                Move = move.ReadValue<Vector2>(),
                Look = look.ReadValue<Vector2>(),
                DigHeld = gameplayActive && (toggle ? toggleIntent : digArmed && digHeld),
                DigPressed = gameplayActive && digPressed && (!toggle || toggleIntent),
                GrabPressed = gameplayActive && grabArmed && grab.WasPressedThisFrame(),
                ThrowPressed = gameplayActive && digPressed,
                JumpPressed = jetpackArmed && jetpack.WasPressedThisFrame(),
                JetpackHeld = jetpackArmed && jetpackHeld,
                CrouchHeld = crouch.IsPressed(),
                SprintHeld = gameplayActive && sprint.IsPressed(),
                InteractPressed = interactArmed && interact.WasPressedThisFrame(),
                InventoryPressed = inventoryArmed && inventory.WasPressedThisFrame() && (gameplayActive || MenuShortcutAvailable(inventory, true)),
                BackPressed = (backArmed && back.WasPressedThisFrame() && (gameplayActive || MenuShortcutAvailable(back, false)))
                    || (!gameplayActive && escapeArmed && escape.WasPressedThisFrame()),
                AdminMenuPressed = adminChord && adminMenu.WasPressedThisFrame(),
                AdminLevel = adminLevel,
                RefillPressed = adminChord && refill.WasPressedThisFrame(),
                ReturnPressed = adminChord && returnToSurface.WasPressedThisFrame(),
                XrayPressed = adminChord && xray.WasPressedThisFrame()
            };
        }

        public void Dispose()
        {
            if (preferences != null) preferences.Changed -= ApplyPreferences;
            actions.Dispose();
        }

        private static bool MenuShortcutAvailable(InputAction action, bool inventory)
        {
            string path = action.bindings[0].effectivePath;
            // Gameplay remaps cannot steal fixed menu submit, navigation or pointer
            // activation. Escape and visible Back/Close remain available in every map.
            return path != "<Mouse>/leftButton" && path != "<Keyboard>/enter" && path != "<Keyboard>/numpadEnter"
                && path != "<Keyboard>/space" && path != "<Keyboard>/leftArrow" && path != "<Keyboard>/rightArrow"
                && path != "<Keyboard>/upArrow" && path != "<Keyboard>/downArrow" && (inventory || path != "<Keyboard>/tab");
        }
    }
}
