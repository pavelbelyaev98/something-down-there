using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    public static class SurfaceStationSetup
    {
        public const string ComputerPrefabPath = "Assets/Cosmic_Retro_Computer_1_FREE/Prefabs/Cosmic_Retro_Computer_3.prefab";

        [MenuItem("Tools/Something Down There/Configure Surface Computer")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath && scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode to configure its computer.");
            var surface = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform.Find("Surface");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ComputerPrefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing approved retro computer prefab.");
            var anchor = surface.Find("ComputerStation");
            if (anchor == null)
            {
                anchor = new GameObject("ComputerStation").transform;
                anchor.SetParent(surface, false);
                anchor.localPosition = LakebedSiteSetup.Camp(new Vector3(-3, 0, -14));
                Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Install surface computer");
            }
            var previous = anchor.Find("Station visual");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            visual.name = "Station visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var bounds = LocalBounds(anchor, visual);
            visual.transform.localScale *= 2.1f / bounds.size.y;
            bounds = LocalBounds(anchor, visual);
            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = LocalBounds(anchor, visual);
            if (anchor.GetComponent<ComputerStation>() == null) Undo.AddComponent<ComputerStation>(anchor.gameObject);
            var collider = anchor.GetComponent<BoxCollider>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider>(anchor.gameObject);
            Undo.RecordObject(collider, "Configure computer interaction bounds");
            collider.center = bounds.center;
            collider.size = bounds.size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Bounds LocalBounds(Transform anchor, GameObject visual)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh.bounds;
                var matrix = anchor.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var point = matrix.MultiplyPoint3x4(corner);
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            if (first || bounds.size.y <= 0) throw new InvalidOperationException("Computer prefab has no usable mesh.");
            return bounds;
        }
    }
}
