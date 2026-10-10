using System;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SomethingDownThere
{
    public sealed class GameUiDocument : IDisposable
    {
        private readonly GameObject documentRoot;
        private readonly PanelSettings panel;
        private readonly PanelTextSettings textSettings;
        public VisualElement Root { get; }

        public GameUiDocument(Transform parent)
        {
            textSettings = ScriptableObject.CreateInstance<PanelTextSettings>();
            textSettings.hideFlags = HideFlags.DontSave;
            panel = Object.Instantiate(Resources.Load<PanelSettings>("GameMenus/MenuPanel"));
            panel.hideFlags = HideFlags.DontSave;
            panel.textSettings = textSettings;
            documentRoot = new GameObject("Game screen UI");
            documentRoot.transform.SetParent(parent, false);
            var document = documentRoot.AddComponent<UIDocument>();
            document.panelSettings = panel;
            Root = document.rootVisualElement;
            Root.pickingMode = PickingMode.Ignore;
            Resources.Load<VisualTreeAsset>("GameMenus/GameHud").CloneTree(Root);
            Resources.Load<VisualTreeAsset>("GameMenus/GameMenus").CloneTree(Root);
        }

        public void Dispose()
        {
            Object.Destroy(documentRoot);
            Object.Destroy(panel);
            Object.Destroy(textSettings);
        }
    }
}
