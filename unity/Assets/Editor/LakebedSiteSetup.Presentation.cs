using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    public static partial class LakebedSiteSetup
    {
        public const string WaterShaderPath = Folder + "/LakebedWater.shader";
        public const string WaterShaderName = "Something Down There/Lakebed Water";
        public const string LakeMaterialPath = Folder + "/LakeWater.mat";
        public const string RiverMaterialPath = Folder + "/RiverWater.mat";
        public const string TrickleMaterialPath = Folder + "/TrickleWater.mat";
        private const string VendorWaterFolder = "Assets/BK/PureNature_Highlands/Textures/Water/Materials/";
        private const string VendorSplashMaterial = "Assets/BK/PureNature_Highlands/Textures/Fx/Materials/Watersplash.mat";

        // Rebind presentation without regenerating terrain, scenery or authored placement.
        [MenuItem("Tools/Something Down There/Refresh Lakebed Lighting and Water")]
        public static void ConfigurePresentation()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MainGame.unity")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            SunPresentationSetup.Configure();
            ConfigureWater(root);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureWater(Transform root)
        {
            EnsureFolder();
            var shader = ConfigureWaterShader();
            var lake = WaterMaterial("Ocean.mat", LakeMaterialPath, shader, false);
            var river = WaterMaterial("River.mat", RiverMaterialPath, shader, true);
            var water = root.Find("Environment/Water");
            // The publisher's splash material ships without its textures: its lake-sized
            // horizontal billboards render as solid white sheets below the waterfalls.
            foreach (var splash in water.GetComponentsInChildren<ParticleSystemRenderer>(true)
                .Where(r => AssetDatabase.GetAssetPath(r.sharedMaterial) == VendorSplashMaterial)
                .Select(r => PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject) ?? r.gameObject).Distinct().ToArray())
                Undo.DestroyObjectImmediate(splash);
            var trickle = TrickleMaterial(river);
            foreach (var renderer in water.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("Trickle", StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = trickle;
                    EditorUtility.SetDirty(renderer);
                    continue;
                }
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    string path = AssetDatabase.GetAssetPath(materials[i]);
                    if (path == VendorWaterFolder + "Ocean.mat") { materials[i] = lake; changed = true; }
                    else if (path == VendorWaterFolder + "River.mat") { materials[i] = river; changed = true; }
                }
                if (!changed) continue;
                Undo.RecordObject(renderer, "Bind lakebed water");
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
            }
        }

        // Keep the publisher's animation, normals, URP lighting and reflection passes.
        // The opaque texture already contains lighting: transmit it through emission rather
        // than using it as diffuse albedo and lighting the shoreline a second time.
        public static Shader ConfigureWaterShader()
        {
            EnsureFolder();
            string source = File.ReadAllText("Assets/BK/Pure_Common/Shaders/BK_Water.shader");
            string Replace(string from, string to, int count)
            {
                if (source.Split(new[] { from }, StringSplitOptions.None).Length != count + 1)
                    throw new InvalidOperationException("BK water changed; review the lakebed water integration: " + from);
                return source.Replace(from, to);
            }
            source = Replace("Shader \"BK/Water\"", "Shader \"" + WaterShaderName + "\"", 1);
            source = Replace("float temp_output_6_0 = ( 1.0 - ( temp_output_368_0 * distanceDepth4 ) );",
                "float temp_output_6_0 = saturate(1.0 - temp_output_368_0 * distanceDepth4);", 4);
            source = Replace("float3 Refraction60 = saturate( (fetchOpaqueVal20).rgb );",
                "float3 Refraction60 = max(fetchOpaqueVal20.rgb, 0.0);", 4);
            source = Replace("float3 BaseColor = clampResult109;",
                "// SDT: the sampled scene is already lit; foam replaces colour instead of adding white.\n" +
                "\t\t\t\tfloat transmission = saturate(Depth98) * (1.0 - Edges62);\n" +
                "\t\t\t\tfloat3 BaseColor = lerp(Albedo58, float3(0.7, 0.78, 0.78), Edges62) * (1.0 - transmission);", 4);
            source = Replace("float3 Emission = 0;", "float3 Emission = Refraction60 * transmission;", 3);
            source = Replace("float Smoothness = _SmoothnessPower;",
                "float Smoothness = lerp(_SmoothnessPower, 0.35, Edges62);", 2);
            // This is a generated code adaptation, not an editable Amplify graph.
            int graph = source.IndexOf("/*ASEBEGIN", StringComparison.Ordinal);
            if (graph >= 0) source = source.Substring(0, graph);
            if (!File.Exists(WaterShaderPath) || File.ReadAllText(WaterShaderPath) != source)
            {
                File.WriteAllText(WaterShaderPath, source);
                AssetDatabase.ImportAsset(WaterShaderPath, ImportAssetOptions.ForceSynchronousImport);
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(WaterShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Lakebed water shader must compile before binding materials.");
            return shader;
        }

        // A shallow run of the same silty water, with finer ripples and a thinner foam line.
        private static Material TrickleMaterial(Material river)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TrickleMaterialPath);
            if (material == null)
            {
                material = new Material(river) { name = "TrickleWater" };
                material.SetFloat("_FoamDistance", .2f);
                material.SetFloat("_FoamPower", .25f);
                material.SetFloat("_EdgesFade", .15f);
                material.SetFloat("_RefractionPower", .15f);
                material.SetFloat("_NormalScale", 4);
                AssetDatabase.CreateAsset(material, TrickleMaterialPath);
            }
            // Subsequent setup runs preserve the user's Inspector tuning.
            material.shader = river.shader;
            // Drawn before the lake: its depth write hides the lake plane under the flooded mouth.
            // URP's material validation derives the queue from _QueueOffset, so set both.
            material.SetFloat("_QueueOffset", -1);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material WaterMaterial(string vendorName, string path, Shader shader, bool flowing)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var original = AssetDatabase.LoadAssetAtPath<Material>(VendorWaterFolder + vendorName);
                if (original == null) throw new InvalidOperationException("Missing approved Highlands water: " + vendorName);
                material = new Material(original) { name = Path.GetFileNameWithoutExtension(path) };
                material.SetFloat("_FoamDistance", flowing ? .65f : .4f);
                material.SetFloat("_FoamPower", flowing ? .35f : .18f);
                material.SetFloat("_SmoothnessPower", .94f);
                material.SetFloat("_RefractionPower", .3f);
                material.SetFloat("_EdgesFade", .25f);
                Silt(material);
                AssetDatabase.CreateAsset(material, path);
            }
            // Subsequent setup runs preserve the user's Inspector tuning.
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Silty water off the drained bed: slate grey-green that hides the bottom within a few
        // decimetres. _Depth is the depth-fade length, so shorter reads as murkier. Lake, river
        // and trickles share it so a trickle runs into the lake without a seam.
        private static void Silt(Material material)
        {
            material.SetFloat("_Depth", .3f);
            material.SetColor("_ShallowColor", new Color(.47f, .5f, .45f, 1));
            material.SetColor("_CausticsColor", new Color(.5f, .52f, .47f, 1));
            material.SetColor("_DepthColor", new Color(.37f, .42f, .42f, .8f));
        }
    }
}
