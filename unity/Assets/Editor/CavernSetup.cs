using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // The caves' and geodes' crystal lights (115, 110) and the caves' puddles: CavernScenery on MainGame's terrain, which
    // gives each crystal in a hollow a small light of its own and lays water in the caves' low spots. Run with the saved
    // MainGame scene open.
    public static class CavernSetup
    {
        // The puddles' water: muddy and see-through (PuddleColour's alpha), so the floor shows darkened under it and the
        // waterline reads as a wet edge, not a hole (an opaque dark first try read as a black cut-out); glossy, its
        // highlights kept whole, so lamps and crystals glint in it; rippled by Crystal Caverns' own water normal map over
        // PuddleRippleMetres. It uses the excavation's own Lit (daylight faded underground) directly rather than being
        // adapted at runtime: a build keeps only the shader variants its materials use, so an adapted copy lost its
        // transparency in the player and drew near-black (user, 2026-10-08: "I saw no cave puddles").
        public const string PuddlePath = "Assets/Content/Environment/CavePuddle.mat";
        private const string PuddleShader = "Assets/Runtime/Terrain/ExcavationLit.shader";
        private const string PuddleRipples = "Assets/BK/PureNature_CrystalCaverns/Textures/Water/Water_n.png";
        private static readonly Color PuddleColour = new Color(.08f, .07f, .058f, .7f);
        private const float PuddleGloss = .88f, PuddleRipple = .3f, PuddleRippleMetres = 1.6f;

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
                data.FindProperty("puddleMaterial").objectReferenceValue = PuddleMaterial();
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Caverns configured: crystal lights and puddles.");
        }

        private static Material PuddleMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PuddlePath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(PuddleShader) ?? throw new InvalidOperationException("Missing " + PuddleShader);
            if (material == null)
            {
                material = new Material(shader) { name = "CavePuddle" };
                AssetDatabase.CreateAsset(material, PuddlePath);
            }
            material.shader = shader;
            var ripples = AssetDatabase.LoadAssetAtPath<Texture2D>(PuddleRipples)
                ?? throw new InvalidOperationException("Missing " + PuddleRipples + " (reimport Crystal Caverns).");
            material.SetColor("_BaseColor", PuddleColour);
            material.SetColor("_Color", PuddleColour);
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_BumpMap", ripples); material.SetFloat("_BumpScale", PuddleRipple); material.EnableKeyword("_NORMALMAP");
            material.mainTextureScale = Vector2.one / PuddleRippleMetres;
            material.SetFloat("_WorkflowMode", 1); material.DisableKeyword("_SPECULAR_SETUP");
            material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", PuddleGloss);
            // URP Lit's transparent surface, premultiplied with its specular preserved.
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_BlendModePreserveSpecular", 1);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
