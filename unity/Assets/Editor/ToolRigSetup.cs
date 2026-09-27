using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // Places the original Blender tool rig (art/tool-rig) under the MainGame player camera and gives
    // each part its project material from the name suffix (`__Steel`, `__Paint`, ...).
    public static class ToolRigSetup
    {
        public const string Folder = "Assets/Content/ToolRig";
        public const string ModelPath = Folder + "/Models/ToolRig.fbx";
        private static readonly Dictionary<string, (Color colour, float metallic, float smoothness)> Materials = new()
        {
            ["Wood"] = (new Color(.42f, .27f, .15f), 0f, .25f),
            ["Steel"] = (new Color(.55f, .56f, .57f), .75f, .45f),
            ["DarkSteel"] = (new Color(.18f, .19f, .2f), .6f, .3f),
            ["Paint"] = (new Color(.72f, .43f, .07f), .1f, .35f),
            ["Rubber"] = (new Color(.05f, .05f, .05f), 0f, .2f),
            ["Battery"] = (new Color(.22f, .3f, .38f), .2f, .45f),
            ["Cable"] = (new Color(.45f, .08f, .05f), 0f, .3f)
        };

        [MenuItem("Tools/Something Down There/Configure Tool Rig")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing the Blender tool rig; run art/tool-rig/create_assets.py.");
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.bakeAxisConversion = true; importer.SaveAndReimport();
            var materials = Materials.ToDictionary(pair => pair.Key, pair => Material(pair.Key, pair.Value));

            var player = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").GetComponentInChildren<FpsPlayer>();
            var camera = player.GetComponentInChildren<Camera>(true).transform;
            var rig = camera.Find("ToolRig");
            if (rig == null) { rig = new GameObject("ToolRig").transform; rig.SetParent(camera, false); }
            var old = rig.Find("Model");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), rig);
            model.name = "Model";
            foreach (var transform in model.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 2;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string key = renderer.name.Split(new[] { "__" }, StringSplitOptions.None).Last();
                key = new string(key.TakeWhile(char.IsLetter).ToArray());
                if (!materials.TryGetValue(key, out var material)) throw new InvalidOperationException("No rig material for " + renderer.name);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
            var presenter = rig.TryGetComponent<ToolRigPresenter>(out var existing) ? existing : rig.gameObject.AddComponent<ToolRigPresenter>();
            using (var data = new SerializedObject(presenter))
            {
                data.FindProperty("player").objectReferenceValue = player;
                data.FindProperty("model").objectReferenceValue = model.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Tool rig configured under the MainGame player camera.");
        }

        private static Material Material(string name, (Color colour, float metallic, float smoothness) look)
        {
            string path = Folder + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.SetColor("_BaseColor", look.colour); result.SetFloat("_Metallic", look.metallic); result.SetFloat("_Smoothness", look.smoothness);
            EditorUtility.SetDirty(result);
            return result;
        }
    }
}
