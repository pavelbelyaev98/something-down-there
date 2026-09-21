using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    public static class RoundSiteSetup
    {
        public const string Folder = "Assets/Content/Site";
        public const string ApronMeshPath = Folder + "/WalkingApron.asset";
        public const string ApronMaterialPath = Folder + "/DryGravel.mat";

        [MenuItem("Tools/Something Down There/Configure Round Excavation Site")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "MainGame")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            foreach (string name in new[] { "Environment", "Perimeter", "Clouds" }) Remove(root.Find(name));
            var surface = root.Find("Surface");
            foreach (string side in new[] { "South", "North", "West", "East" }) Remove(surface.Find(side + " rim"));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content", "Site");
            var mesh = BuildApron();
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(ApronMeshPath);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, ApronMeshPath); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            var apron = surface.Find("Walking apron");
            if (apron == null) { apron = new GameObject("Walking apron").transform; apron.SetParent(surface, false); }
            Get<MeshFilter>(apron.gameObject).sharedMesh = saved;
            Get<MeshRenderer>(apron.gameObject).sharedMaterial = Gravel();
            Get<MeshCollider>(apron.gameObject).sharedMesh = saved;
            Get<PermanentTerrainBoundary>(apron.gameObject);
            float depth = SiteLayout.Extent.y;
            foreach (string side in new[] { "West", "East", "North", "South" })
            {
                var wall = root.Find("Bedrock/" + side);
                var position = wall.position; position.y = (-depth + SiteLayout.ApronBottom) * .5f; wall.position = position;
                var scale = wall.localScale; scale.y = depth + SiteLayout.ApronBottom; wall.localScale = scale;
            }
            var terrain = root.GetComponentInChildren<TerrainVolume>();
            var grass = new SerializedObject(terrain.GetComponent<SurfaceGrassRenderer>());
            grass.FindProperty("surfaceRadius").floatValue = SiteLayout.OpeningRadius;
            grass.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponentInChildren<Camera>().farClipPlane = 500;
            GroundTextureSetup.ConfigureMeadowMaterials(root);
            SunPresentationSetup.Configure();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        private static void Remove(Transform item) { if (item != null) Undo.DestroyObjectImmediate(item.gameObject); }
        private static T Get<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static Material Gravel()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ApronMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "DryGravel" };
                AssetDatabase.CreateAsset(material, ApronMaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/BK/PureNature_Mountains/Textures/Surfaces/Gravel_a.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/BK/PureNature_Mountains/Textures/Surfaces/Gravel_n.png"));
            if (material.GetTexture("_BaseMap") == null) throw new InvalidOperationException("Approved gravel texture is missing.");
            material.SetColor("_BaseColor", new Color(.7f, .68f, .63f));
            material.SetFloat("_BumpScale", .3f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", 0);
            material.SetFloat("_SpecularHighlights", 0);
            material.SetFloat("_EnvironmentReflections", 0);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        // Closed annular slab: one render/collision mesh, no collider over the hole.
        // The voxel grid extends underneath for lateral excavation.
        private static Mesh BuildApron()
        {
            const int segments = 256;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                foreach (var v in new[] { a, b, c, d }) uv.Add(new Vector2(v.x, v.z) / 3f);
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector3 Direction(float angle) => new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 ia = Direction(a) * SiteLayout.OpeningRadius, ib = Direction(b) * SiteLayout.OpeningRadius;
                Vector3 oa = Direction(a) * SiteLayout.ApronRadius, ob = Direction(b) * SiteLayout.ApronRadius;
                Vector3 top = Vector3.up * SiteLayout.ApronTop, bottom = Vector3.up * SiteLayout.ApronBottom;
                Quad(ia + top, ib + top, ob + top, oa + top);
                Quad(ia + bottom, oa + bottom, ob + bottom, ib + bottom);
                Quad(ia + top, ia + bottom, ib + bottom, ib + top);
                Quad(oa + top, ob + top, ob + bottom, oa + bottom);
            }
            var mesh = new Mesh { name = "Permanent circular walking apron" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            mesh.SetPreBakeCollisionMesh(false, true);
            return mesh;
        }
    }
}
