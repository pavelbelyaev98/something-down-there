using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    public static class MainGameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainGame.unity";
        private const string MaterialsPath = "Assets/Settings/MainGame";

        [MenuItem("Tools/Something Down There/Create Main Game Scene")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateIfMissing();
        }

        // Run in a closed or isolated project. Existing scene content is never overwritten.
        public static void CreateIfMissing()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            Directory.CreateDirectory(MaterialsPath);
            AssetDatabase.Refresh();
            Material soil = MaterialAsset("Soil", new Color(0.43f, 0.28f, 0.14f));
            Material grass = MaterialAsset("Surface", new Color(0.32f, 0.42f, 0.22f));
            Material rock = MaterialAsset("Bedrock", new Color(0.24f, 0.29f, 0.34f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MainGame";
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.64f, 0.70f);
            var root = new GameObject("MainGameRoot").transform;
            var sun = new GameObject("Sun", typeof(Light));
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(48, -28, 0);
            sun.GetComponent<Light>().type = LightType.Directional;
            sun.GetComponent<Light>().intensity = 1.4f;
            sun.GetComponent<Light>().shadows = LightShadows.None;

            Transform surface = Group("Surface", root);
            Block("South rim", surface, new Vector3(0, -0.5f, -14), new Vector3(32, 1, 4), grass);
            Block("North rim", surface, new Vector3(0, -0.5f, 14), new Vector3(32, 1, 4), grass);
            Block("West rim", surface, new Vector3(-14, -0.5f, 0), new Vector3(4, 1, 24), grass);
            Block("East rim", surface, new Vector3(14, -0.5f, 0), new Vector3(4, 1, 24), grass);

            Transform bedrock = Group("Bedrock", root);
            foreach (string side in new[] { "Floor", "West", "East", "North", "South" })
                Boundary(side, bedrock, Vector3.zero, Vector3.one, rock);
            PlaceBedrock(bedrock);
            // LakebedSiteSetup replaces the temporary rectangular construction rims.

            var terrainRoot = new GameObject("Excavation");
            terrainRoot.SetActive(false);
            terrainRoot.transform.SetParent(root, false);
            terrainRoot.transform.position = SiteLayout.Origin;
            var preview = Block("Untouched preview (edit mode only)", terrainRoot.transform,
                new Vector3(0, -SiteLayout.Extent.y * 0.5f, 0), SiteLayout.Extent, soil);
            terrainRoot.AddComponent<TerrainVolume>().Configure(SiteLayout.Size, SiteLayout.CellSize,
                SiteLayout.ChunkSize, EquipmentProgression.ToolProfiles()[0].Radius, soil, preview);
            terrainRoot.SetActive(true);

            Anchor("RechargeZone", surface, new Vector3(0, 0, -14.5f));
            Anchor("ReturnAnchor", surface, new Vector3(0, 0.1f, -13.5f));

            CreatePlayer(root);
            SurfaceStationSetup.Configure();
            var playerSettings = new SerializedObject(root.GetComponentInChildren<FpsPlayer>());
            playerSettings.FindProperty("excavationTerrain").objectReferenceValue = terrainRoot.GetComponent<TerrainVolume>();
            playerSettings.FindProperty("surfaceReturn").objectReferenceValue = surface.Find("ReturnAnchor");
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
            ConfigureSurfaceRecharge();
            ConfigureDiscoveryContent();
            GroundTextureSetup.Configure();
            NatureEnvironmentSetup.Configure();
            LakebedSiteSetup.Configure();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Main game scene created with untouched terrain and permanent boundaries.");
        }

        // One metre of bedrock just outside every side of the grid. The walls meet the
        // permanent rim underside without overlapping visible faces.
        public static void PlaceBedrock(Transform bedrock)
        {
            var extent = SiteLayout.Extent;
            float centre = (-extent.y + SiteLayout.RimBottom) * .5f, height = extent.y + SiteLayout.RimBottom;
            void Put(string side, Vector3 position, Vector3 size)
            {
                var wall = bedrock.Find(side);
                wall.position = position;
                wall.localScale = size;
                EditorUtility.SetDirty(wall);
            }
            Put("Floor", new Vector3(0, -extent.y - .5f, 0), new Vector3(extent.x + 2, 1, extent.z + 2));
            Put("West", new Vector3(-extent.x * .5f - .5f, centre, 0), new Vector3(1, height, extent.z));
            Put("East", new Vector3(extent.x * .5f + .5f, centre, 0), new Vector3(1, height, extent.z));
            Put("North", new Vector3(0, centre, extent.z * .5f + .5f), new Vector3(extent.x + 2, height, 1));
            Put("South", new Vector3(0, centre, -extent.z * .5f - .5f), new Vector3(extent.x + 2, height, 1));
        }

        // Applies the authored site layout to the existing scene, in place, so the reservoir
        // size is one edit. Safe to re-run: every value is derived from SiteLayout.
        [MenuItem("Tools/Something Down There/Configure Excavation Size")]
        public static void ConfigureExcavationSize()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Configure the MainGame depth outside Play Mode.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath && scene.name != "MainGame")
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var terrain = UnityEngine.Object.FindAnyObjectByType<TerrainVolume>();
            if (terrain == null || terrain.gameObject.scene != scene) throw new InvalidOperationException("MainGame terrain is missing.");
            var root = terrain.transform.parent;
            var settings = new SerializedObject(terrain);
            settings.FindProperty("dimensions").vector3IntValue = SiteLayout.Size;
            settings.FindProperty("cellSize").floatValue = SiteLayout.CellSize;
            settings.ApplyModifiedPropertiesWithoutUndo();
            terrain.transform.position = SiteLayout.Origin;
            var preview = settings.FindProperty("untouchedPreview").objectReferenceValue as GameObject;
            if (preview != null)
            {
                preview.transform.position = new Vector3(0, -SiteLayout.Extent.y * 0.5f, 0);
                preview.transform.localScale = SiteLayout.Extent;
            }
            PlaceBedrock(root.Find("Bedrock"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"MainGame site configured: {SiteLayout.Extent.x} x {SiteLayout.Extent.y} x {SiteLayout.Extent.z} m.");
        }

        [MenuItem("Tools/Something Down There/Configure Surface Recharge")]
        public static void ConfigureSurfaceRecharge()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath && scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode to configure recharge.");
            var root = scene.GetRootGameObjects()[0].transform;
            var anchor = root.Find("Surface/RechargeZone");
            var player = root.GetComponentInChildren<FpsPlayer>();
            var terrain = root.GetComponentInChildren<TerrainVolume>();
            if (anchor == null || player == null || terrain == null)
                throw new InvalidOperationException("MainGame needs its existing recharge anchor, player and terrain.");
            var recharge = anchor.GetComponent<SurfaceRecharge>();
            if (recharge == null) recharge = Undo.AddComponent<SurfaceRecharge>(anchor.gameObject);
            Undo.RecordObject(recharge, "Configure surface recharge");
            recharge.Configure(player, terrain);
            EditorUtility.SetDirty(recharge);
            var settings = new SerializedObject(player);
            settings.FindProperty("surfaceRecharge").objectReferenceValue = recharge;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/Something Down There/Configure Discovery Content")]
        public static void ConfigureDiscoveryContent() => DiscoveryContentSetup.ConfigureScene();

        [MenuItem("Tools/Something Down There/Configure Find Collection")]
        public static void ConfigureFindCollection() => DiscoveryContentSetup.Sync();

        private static void CreatePlayer(Transform parent)
        {
            var player = new GameObject("Player");
            player.SetActive(false);
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(0, 0.1f, -13);
            player.layer = LayerMask.NameToLayer("Ignore Raycast");
            var motor = player.AddComponent<CharacterController>();
            motor.height = 1.8f;
            motor.radius = 0.3f;
            motor.center = new Vector3(0, 0.9f, 0);
            motor.stepOffset = 0.3f;
            motor.minMoveDistance = 0;
            var cameraRoot = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
            cameraRoot.transform.SetParent(player.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0, 1.6f, 0);
            cameraRoot.tag = "MainCamera";
            var camera = cameraRoot.GetComponent<Camera>();
            camera.fieldOfView = 75;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 180f;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.48f, 0.67f, 0.79f);
            player.AddComponent<FpsPlayer>();
            player.AddComponent<FpsHud>();
            player.SetActive(true);
        }

        private static Transform Group(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static Transform Anchor(string name, Transform parent, Vector3 position)
        {
            Transform root = Group(name, parent);
            root.position = position;
            return root;
        }

        private static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size, Material material)
            => Primitive(PrimitiveType.Cube, name, parent, position, size, material);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 size, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static void Boundary(string name, Transform parent, Vector3 position, Vector3 size, Material material)
            => Block(name, parent, position, size, material).AddComponent<PermanentTerrainBoundary>();

        private static Material MaterialAsset(string name, Color color, float smoothness = 0.15f)
        {
            string path = MaterialsPath + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The project requires its URP Lit shader.");
            var material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
