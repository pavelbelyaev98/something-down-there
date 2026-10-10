using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace SomethingDownThere
{
    [DisallowMultipleComponent, RequireComponent(typeof(FpsPlayer))]
    public sealed class FpsHud : MonoBehaviour
    {
        private FpsPlayer player;
        private GameObject eventRoot;
        private GameUiDocument document;
        private TitleView title;
        private InputActionAsset uiActions;
        private readonly List<InputActionReference> uiReferences = new List<InputActionReference>();
        public GameMenuView Menus { get; private set; }
        public GameHudView View { get; private set; }

        private void Start()
        {
            player = GetComponent<FpsPlayer>();
            BuildInput();
            document = new GameUiDocument(transform);
            Menus = new GameMenuView(document.Root, player);
            View = new GameHudView(document.Root, player);
            title = new TitleView(transform, player);
            Menus.Tick();
            View.Tick();
        }

        private void LateUpdate()
        {
            if (player == null || player.Battery == null) return;
            Menus.Tick();
            View.Tick();
            title.Tick();
            // The EventSystem's panel object is registered after Start. An element
            // can already have focus while its panel still has no keyboard route.
            var events = EventSystem.current;
            if (player.IsMenuOpen && events != null && events.currentSelectedGameObject == null
                && document.Root.panel is IRuntimePanel panel && panel.selectableGameObject != null)
                events.SetSelectedGameObject(panel.selectableGameObject);
        }

        private void BuildInput()
        {
            if (EventSystem.current == null)
            {
                eventRoot = new GameObject("FPS Event System");
                eventRoot.SetActive(false);
                eventRoot.transform.SetParent(transform, false);
                eventRoot.AddComponent<EventSystem>();
                var module = eventRoot.AddComponent<InputSystemUIInputModule>();
                module.deselectOnBackgroundClick = false;
                uiActions = ScriptableObject.CreateInstance<InputActionAsset>();
                var ui = uiActions.AddActionMap("UI");
                var navigate = ui.AddAction("Navigate", InputActionType.Value);
                navigate.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
                module.actionsAsset = uiActions;
                module.move = Reference(navigate);
                module.submit = Reference(ui.AddAction("Submit", InputActionType.Button, "<Keyboard>/enter"));
                module.point = Reference(ui.AddAction("Point", InputActionType.PassThrough, "<Mouse>/position"));
                module.leftClick = Reference(ui.AddAction("Click", InputActionType.PassThrough, "<Mouse>/leftButton"));
                module.scrollWheel = Reference(ui.AddAction("Scroll", InputActionType.PassThrough, "<Mouse>/scroll"));
                module.cancel = null; // Escape belongs exclusively to the player's menu state.
                eventRoot.SetActive(true);
            }
        }

        private InputActionReference Reference(InputAction action)
        {
            var reference = InputActionReference.Create(action);
            uiReferences.Add(reference);
            return reference;
        }

        private void OnDestroy()
        {
            Menus?.Dispose();
            title?.Dispose();
            document?.Dispose();
            if (eventRoot != null) Destroy(eventRoot);
            foreach (var reference in uiReferences) if (reference != null) Destroy(reference);
            if (uiActions != null)
            {
                uiActions.Disable();
                Destroy(uiActions);
            }
        }
    }
}
