using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    public static class WorksiteToolsSetup
    {
        public const string Folder = "Assets/Content/WorksiteTools";
        [MenuItem("Tools/Something Down There/Configure Work Lamps and Markings")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            foreach (string name in new[] { "WorkLamp", "Arrow", "Home", "ReturnHere" })
            {
                var importer = AssetImporter.GetAtPath(Folder + "/Models/" + name + ".fbx") as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing original worksite art: " + name);
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = importer.importCameras = importer.importLights = false;
                importer.bakeAxisConversion = true; importer.isReadable = true; importer.SaveAndReimport();
            }
            var paint = Material("Yellow housing", new Color(.83f, .50f, .08f));
            var steel = Material("Dark steel", new Color(.12f, .15f, .17f));
            var rubber = Material("Rubber", new Color(.035f, .038f, .04f));
            var lens = Material("Warm lens", new Color(.75f, .78f, .76f), true);
            lens.SetColor("_EmissionColor", new Color(.45f, .47f, .44f));
            var valid = Material("Valid placement", new Color(.15f, .8f, 1), true);
            var invalid = Material("Invalid placement", new Color(1, .22f, .15f), true);
            var chalk = Material("Chalk", new Color(.90f, .88f, .79f));
            var template = new GameObject("Portable work lamp");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/WorkLamp.fbx"), template.transform);
                model.transform.localRotation = Quaternion.Euler(0, 180, 0) * model.transform.localRotation;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    string name = renderer.name.Split('.')[0];
                    renderer.sharedMaterial = name == "Lens" || name == "Status lens" ? lens : name.Contains("Yellow") ? paint
                        : name.Contains("Rubber") || name.Contains("Black") ? rubber : steel;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                // A kit is repeated content: publish one mesh per finish instead of one
                // renderer per screw/guard/handle, retaining the original Blender model.
                foreach (var group in model.GetComponentsInChildren<MeshRenderer>().GroupBy(r => r.sharedMaterial))
                {
                    var mesh = new Mesh { name = "Lamp " + group.Key.name };
                    mesh.CombineMeshes(group.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh,
                        transform = template.transform.worldToLocalMatrix * r.transform.localToWorldMatrix }).ToArray(), true, true);
                    string path = Folder + "/Lamp " + group.Key.name + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
                    var part = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(template.transform, false); part.GetComponent<MeshFilter>().sharedMesh = saved;
                    var renderer = part.GetComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key; renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                UnityEngine.Object.DestroyImmediate(model);
                var box = template.AddComponent<BoxCollider>(); box.center = WorksiteTools.LampCenter; box.size = WorksiteTools.LampHalfSize * 2;
                var body = template.AddComponent<Rigidbody>(); body.mass = 3; body.linearDamping = .8f; body.angularDamping = 2;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; body.interpolation = RigidbodyInterpolation.Interpolate; body.isKinematic = true;
                var light = new GameObject("Diffuse lantern light", typeof(Light), typeof(UniversalAdditionalLightData)).GetComponent<Light>();
                light.transform.SetParent(template.transform, false); light.transform.localPosition = new Vector3(0, .24f, 0);
                light.type = LightType.Point; light.range = WorksiteTools.LightRange; light.intensity = 8;
                light.color = new Color(.91f, .96f, 1);
                light.shadows = LightShadows.Soft; light.shadowBias = .015f; light.shadowNormalBias = .04f; light.shadowNearPlane = .04f;
                var data = light.GetComponent<UniversalAdditionalLightData>(); data.usePipelineSettings = false;
                using (var serialized = new SerializedObject(data))
                {
                    serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 1;
                    serialized.FindProperty("m_SoftShadowQuality").intValue = 2;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var lamp = template.AddComponent<WorkLamp>(); Set(lamp, "workLight", light);
                PrefabUtility.SaveAsPrefabAsset(template, Folder + "/WorkLamp.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(template); }
            var root = scene.GetRootGameObjects().Single(go => go.name == "MainGameRoot");
            var player = root.GetComponentInChildren<FpsPlayer>(); var terrain = root.GetComponentInChildren<TerrainVolume>();
            var existing = root.transform.Find("WorksiteTools");
            var tools = existing != null ? existing.GetComponent<WorksiteTools>() : new GameObject("WorksiteTools", typeof(WorksiteTools)).GetComponent<WorksiteTools>();
            tools.transform.SetParent(root.transform, false);
            Set(tools, "player", player); Set(tools, "terrain", terrain); Set(tools, "lampPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/WorkLamp.prefab").GetComponent<WorkLamp>());
            Set(tools, "markMaterial", chalk); Set(tools, "validPreview", valid); Set(tools, "invalidPreview", invalid);
            using (var serialized = new SerializedObject(tools))
            {
                var stencils = serialized.FindProperty("stencils"); stencils.arraySize = 3;
                string[] names = { "Arrow", "Home", "ReturnHere" };
                for (int i = 0; i < names.Length; i++)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Models/" + names[i] + ".fbx");
                    var filter = model.GetComponentInChildren<MeshFilter>();
                    var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                    mesh.name = names[i] + " stencil";
                    mesh.vertices = mesh.vertices.Select(v => filter.transform.localToWorldMatrix.MultiplyPoint3x4(v)).ToArray();
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string path = Folder + "/" + names[i] + ".asset";
                    var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existingMesh == null) { AssetDatabase.CreateAsset(mesh, path); existingMesh = mesh; }
                    else { EditorUtility.CopySerialized(mesh, existingMesh); UnityEngine.Object.DestroyImmediate(mesh); }
                    stencils.GetArrayElementAtIndex(i).objectReferenceValue = existingMesh;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Set(player, "worksiteTools", tools);
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            using (var serialized = new SerializedObject(pipeline))
            {
                serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                // Eight point lights need six faces each; this atlas fits every face
                // at the selected tier without URP silently shrinking shadow tiles.
                serialized.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 4096;
                serialized.FindProperty("m_AdditionalLightsShadowResolutionTierMedium").intValue = 512;
                serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
                serialized.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = 8;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Reusable work lamps and navigation markings configured in MainGame.");
        }

        private static Material Material(string name, Color color, bool emissive = false)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Something Down There/Excavation Lit");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .2f); material.SetFloat("_Cull", 0);
            if (emissive) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 1.3f); }
            EditorUtility.SetDirty(material); return material;
        }
        private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
        { using var serialized = new SerializedObject(owner); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
