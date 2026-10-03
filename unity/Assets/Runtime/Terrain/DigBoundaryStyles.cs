using UnityEngine;

namespace SomethingDownThere
{
    // The dig plot's boundary dressing (LakebedSiteSetup): one style shows, the first by default; the
    // Developer admin steps through the others for a session to compare them.
    public sealed class DigBoundaryStyles : MonoBehaviour
    {
        [SerializeField] private GameObject[] styles = new GameObject[0];
        public int Count => styles.Length;
        public int Current { get; private set; }
        public string CurrentName => Current < styles.Length && styles[Current] != null ? styles[Current].name : "";

        public void Show(int index)
        {
            if (styles.Length == 0) return;
            Current = (index % styles.Length + styles.Length) % styles.Length;
            for (int i = 0; i < styles.Length; i++)
                if (styles[i] != null) styles[i].SetActive(i == Current);
        }
    }
}
