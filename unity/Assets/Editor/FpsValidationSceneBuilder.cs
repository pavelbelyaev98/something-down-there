using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    public static class FpsValidationSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/FpsValidation.unity";

        // Batch entrypoint; deterministic generation in the otherwise empty foundation project.
        public static void InitializeProject()
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 1; // Input System only.
            settings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.companyName = "Something Down There";
            PlayerSettings.productName = "Something Down There";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            CreateAndSave();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Something Down There/Create FPS Validation Scene")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            CreateAndSave();
        }

        private static void CreateAndSave()
        {
            // Never overwrite a scene that may have been edited after initial generation.
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "FpsValidation";
            RenderSettings.ambientLight = new Color(0.6f, 0.65f, 0.7f);

            var sun = new GameObject("Sun", typeof(Light));
            sun.GetComponent<Light>().type = LightType.Directional;
            sun.GetComponent<Light>().intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);

            // A closed validation yard; this is not the game's excavation scene.
            Block("Ground", new Vector3(0, -0.5f, 3), new Vector3(18, 1, 18));
            Block("Boundary North", new Vector3(0, 2, 12), new Vector3(18, 5, 1));
            Block("Boundary South", new Vector3(0, 2, -6), new Vector3(18, 5, 1));
            Block("Boundary West", new Vector3(-9, 2, 3), new Vector3(1, 5, 18));
            Block("Boundary East", new Vector3(9, 2, 3), new Vector3(1, 5, 18));
            Block("Ceiling collision check", new Vector3(0, 7, 3), new Vector3(18, 1, 18));

            var player = new GameObject("Player");
            player.layer = LayerMask.NameToLayer("Ignore Raycast");
            player.transform.position = new Vector3(0, 0.1f, 0);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0, 0.9f, 0);
            controller.stepOffset = 0.3f;
            controller.minMoveDistance = 0f;
            var cameraObject = new GameObject("First Person Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.6f, 0);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.fieldOfView = 75;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.65f, 0.8f);
            player.AddComponent<FpsPlayer>();
            player.AddComponent<FpsHud>();

            Block("Dig block - three shovel hits", new Vector3(0, 1, 2.4f), new Vector3(1.5f, 2, 0.7f))
                .AddComponent<ValidationDigTarget>();
            var find = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            find.name = "Old tin - behind dig block";
            find.transform.position = new Vector3(0, 1.4f, 3.2f);
            find.transform.localScale = Vector3.one * 0.45f;
            find.AddComponent<ValidationFind>();
            // Enough finite pickups to exercise the full-inventory rejection.
            for (int i = 0; i < 11; i++)
            {
                var extra = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                extra.name = "Inventory capacity check " + (i + 1);
                extra.transform.position = new Vector3(3 + i % 4, 0.4f, 3 + i / 4);
                extra.transform.localScale = Vector3.one * 0.45f;
                extra.AddComponent<ValidationFind>();
            }
            var sell = Block("Selling station", new Vector3(-3, 1, 2), new Vector3(1.5f, 2, 1));
            sell.AddComponent<ValidationStation>().Kind = ValidationStation.StationKind.Sell;
            var upgrade = Block("Upgrade station", new Vector3(-5.5f, 1, 2), new Vector3(1.5f, 2, 1));
            upgrade.AddComponent<ValidationStation>().Kind = ValidationStation.StationKind.Upgrade;
            var recharge = new GameObject("Surface recharge", typeof(BoxCollider), typeof(Rigidbody), typeof(ValidationRecharge));
            recharge.transform.position = new Vector3(-4.2f, 1, 0.8f);
            recharge.GetComponent<BoxCollider>().size = new Vector3(5, 2, 2);
            recharge.GetComponent<BoxCollider>().isTrigger = true;
            recharge.GetComponent<Rigidbody>().isKinematic = true;
            recharge.GetComponent<Rigidbody>().useGravity = false;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("FPS validation scene generated: " + ScenePath);
        }

        private static GameObject Block(string name, Vector3 position, Vector3 scale)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            return block;
        }
    }
}
