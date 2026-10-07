using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // The caves' and geodes' crystal lights (115, 110): CavernScenery on MainGame's terrain, which gives each crystal in a
    // hollow a small light of its own. Run with the saved MainGame scene open.
    public static class CavernSetup
    {
        [MenuItem("Tools/Something Down There/Configure Caverns")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindAnyObjectByType<TerrainVolume>();
            if (scene.path != "Assets/Scenes/MainGame.unity" || terrain == null) throw new InvalidOperationException("Open the saved MainGame scene first.");
            var field = UnityEngine.Object.FindAnyObjectByType<DiscoveryField>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("MainGame has no discovery field.");
            var scenery = terrain.GetComponent<CavernScenery>() ?? Undo.AddComponent<CavernScenery>(terrain.gameObject);
            using (var data = new SerializedObject(scenery))
            {
                data.FindProperty("terrain").objectReferenceValue = terrain;
                data.FindProperty("field").objectReferenceValue = field;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Caverns configured: crystal lights.");
        }
    }
}
