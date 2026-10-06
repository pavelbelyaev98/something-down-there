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
    // C4 (026): the armed charge's prefab and the blast preview's material, wired into MainGame's WorksiteTools. No owned
    // pack has an explosive, so the charge is a stand-in from owned art (art/c4-charge): the Mining pack's flat ingot as
    // a demolition block in olive drab, with a small red arming LED and its blinking light. Run after Configure Work
    // Lamps and Markings, with MainGame open outside Play Mode.
    public static class ChargeSetup
    {
        public const string Folder = "Assets/Content/C4";
        private const string IngotModel = "Assets/REAL_DEDICATED/MiningTools_Ore_Ingots/Models/SM_Ingot_Flat.fbx";
        private const string IngotNormal = "Assets/REAL_DEDICATED/MiningTools_Ore_Ingots/Textures/T_Ingots_Silver_N.tga";
        // A demolition block's length, and how much thicker than the ingot it stands (a block is about 4 cm thick); olive
        // drab, a little worn and dull; the LED's glow and its light.
        private const float BlockLength = .2f, BlockThickening = 2f, BlockGloss = .28f, LedGlow = 6f, LedLight = .35f;
        private static readonly Color Olive = new Color(.29f, .3f, .2f), Led = new Color(1f, .06f, .03f);

        [MenuItem("Tools/Something Down There/Configure C4 Charges")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "C4");
            var ingot = AssetDatabase.LoadAssetAtPath<GameObject>(IngotModel) ?? throw new InvalidOperationException("Missing " + IngotModel);
            var mesh = BlockMesh(ingot.GetComponentsInChildren<MeshFilter>(true).OrderBy(f => f.name.Contains("LOD0") ? 0 : 1).First().sharedMesh);
            var block = Lit("C4 Block", Olive, BlockGloss);
            block.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(IngotNormal)); block.EnableKeyword("_NORMALMAP");
            block.SetFloat("_BumpScale", .6f);
            var led = Lit("C4 LED", Led * .4f, .6f);
            led.SetColor("_EmissionColor", Led * LedGlow); led.EnableKeyword("_EMISSION");
            led.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            var preview = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/C4 Blast Preview.mat");
            var shader = Shader.Find("Something Down There/Blast Preview") ?? throw new InvalidOperationException("Missing blast preview shader.");
            if (preview == null) { preview = new Material(shader); AssetDatabase.CreateAsset(preview, Folder + "/C4 Blast Preview.mat"); }
            preview.shader = shader; EditorUtility.SetDirty(preview);

            var template = new GameObject("C4 charge");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                // The ingot lies flat, its length along the charge's forward and its base on the charge's origin.
                var bounds = mesh.bounds;
                bool lengthAlongX = bounds.size.x > bounds.size.z;
                float scale = BlockLength / Mathf.Max(bounds.size.x, bounds.size.z);
                var body = new GameObject("Block", typeof(MeshFilter), typeof(MeshRenderer));
                body.transform.SetParent(template.transform, false);
                body.transform.localRotation = lengthAlongX ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
                body.transform.localScale = new Vector3(scale, scale * BlockThickening, scale);
                body.transform.localPosition = -(body.transform.localRotation * Vector3.Scale(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                    new Vector3(1, BlockThickening, 1))) * scale;
                body.GetComponent<MeshFilter>().sharedMesh = mesh;
                body.GetComponent<MeshRenderer>().sharedMaterial = block;
                float height = bounds.size.y * scale * BlockThickening, width = (lengthAlongX ? bounds.size.z : bounds.size.x) * scale;

                var lamp = new GameObject("Arming LED", typeof(MeshFilter), typeof(MeshRenderer));
                lamp.transform.SetParent(template.transform, false);
                lamp.transform.localPosition = new Vector3(0, height, BlockLength * .32f);
                lamp.transform.localScale = new Vector3(.012f, .008f, .012f);
                lamp.GetComponent<MeshFilter>().sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
                var ledRenderer = lamp.GetComponent<MeshRenderer>(); ledRenderer.sharedMaterial = led; ledRenderer.shadowCastingMode = ShadowCastingMode.Off;
                var light = new GameObject("Arming light", typeof(Light), typeof(UniversalAdditionalLightData)).GetComponent<Light>();
                light.transform.SetParent(template.transform, false); light.transform.localPosition = new Vector3(0, height + .03f, BlockLength * .32f);
                light.type = LightType.Point; light.color = Led; light.range = .8f; light.intensity = LedLight; light.shadows = LightShadows.None;

                var box = template.AddComponent<BoxCollider>();
                box.center = new Vector3(0, height * .5f, 0); box.size = new Vector3(width, height, BlockLength);
                var rigidbody = template.AddComponent<Rigidbody>(); rigidbody.mass = .6f; rigidbody.linearDamping = .5f; rigidbody.angularDamping = 1.5f;
                rigidbody.maxDepenetrationVelocity = 1; rigidbody.isKinematic = true;
                rigidbody.interpolation = RigidbodyInterpolation.None; rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var charge = template.AddComponent<PlacedCharge>();
                using (var data = new SerializedObject(charge))
                {
                    data.FindProperty("armedLight").objectReferenceValue = light;
                    data.FindProperty("armedLed").objectReferenceValue = ledRenderer;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(template, Folder + "/C4Charge.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(template); UnityEngine.Object.DestroyImmediate(cube); }

            var root = scene.GetRootGameObjects().Single(go => go.name == "MainGameRoot");
            var tools = root.GetComponentInChildren<WorksiteTools>(true) ?? throw new InvalidOperationException("Run Configure Work Lamps and Markings first.");
            using (var data = new SerializedObject(tools))
            {
                data.FindProperty("chargePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/C4Charge.prefab").GetComponent<PlacedCharge>();
                data.FindProperty("blastPreview").objectReferenceValue = preview;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log($"C4 charges configured: block {BlockLength} m, preview {preview.name}.");
        }

        // A project copy of the ingot's full-detail mesh, so the charge depends on no vendor model (its embedded materials
        // are the pack's HDRP ones). Refilled in place, never copied over (a serialized copy scrambles the triangles).
        private static Mesh BlockMesh(Mesh source)
        {
            string path = Folder + "/C4 Block.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = UnityEngine.Object.Instantiate(source); AssetDatabase.CreateAsset(mesh, path); }
            else
            {
                mesh.Clear(); mesh.indexFormat = source.indexFormat;
                mesh.SetVertices(source.vertices); mesh.SetNormals(source.normals); mesh.SetTangents(source.tangents);
                mesh.SetUVs(0, source.uv); mesh.SetTriangles(source.triangles, 0); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
            }
            mesh.name = "C4 Block";
            return mesh;
        }

        private static Material Lit(string name, Color colour, float gloss)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", colour); material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", gloss);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
