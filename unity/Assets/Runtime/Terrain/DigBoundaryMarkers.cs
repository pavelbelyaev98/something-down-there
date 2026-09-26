using System;
using UnityEngine;

namespace SomethingDownThere
{
    // Development comparison of the markers that outline the dig plot. Every option is authored into
    // the scene; one is shown, and Developer admin cycles them for the session only. The markers are
    // visual: they carry no colliders, so digging, aiming and the winch cable pass through.
    public sealed class DigBoundaryMarkers : MonoBehaviour
    {
        [SerializeField] private GameObject[] options = Array.Empty<GameObject>();

        public static DigBoundaryMarkers Active { get; private set; }
        public int Current { get; private set; }
        public string CurrentName => options.Length > 0 && options[Current] != null ? options[Current].name : "";

        public void Configure(GameObject[] markers)
        {
            options = markers;
            Show(0);
        }

        private void OnEnable()
        {
            if (FpsPlayer.AdminBuild && options.Length > 1) Active = this;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        public void Next()
        {
            if (options.Length > 1) Show((Current + 1) % options.Length);
        }

        private void Show(int index)
        {
            Current = index;
            for (int i = 0; i < options.Length; i++)
                if (options[i] != null) options[i].SetActive(i == index);
        }
    }
}
