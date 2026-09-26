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
    public static partial class LakebedSiteSetup
    {
        private const string TreesFolder = Folder + "/Trees";
        private const string FoliageFolder = TreesFolder + "/Materials";
        // Grass, props and scenery in and this far around the play area never disappear from
        // anywhere the player can stand or fly.
        public const float SurroundingMargin = 40;

        // Farthest camera-to-ground distance across the play area, from the flight ceiling.
        public static float PlayViewDistance()
        {
            var outline = PlayArea();
            float span = 0;
            foreach (var a in outline) foreach (var b in outline) span = Mathf.Max(span, Vector2.Distance(a, b));
            return Mathf.Sqrt(span * span + (FlightCeiling + 2) * (FlightCeiling + 2));
        }

        // Camera distance an object must stay visible to: from the farthest point of the play area,
        // for objects in or near it; zero leaves the vendor culling for the far backdrop.
        private static float PlayVisibleDistance(Vector2[] outline, Vector3 centre, float size)
        {
            var point = new Vector2(centre.x, centre.z);
            float nearest = InPlayArea(outline, point) ? 0 : outline.Min(o => Vector2.Distance(o, point));
            if (nearest > SurroundingMargin) return 0;
            return outline.Max(o => Vector3.Distance(new Vector3(o.x, FlightCeiling + 2, o.y), centre)) + size;
        }

        [MenuItem("Tools/Something Down There/Refresh Lakebed Performance")]
        public static void ConfigurePerformance()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/MainGame.unity")
                throw new InvalidOperationException("Open MainGame outside Play Mode.");
            ConfigurePerformance(scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
        }

        // Authoring only: no distance-checking MonoBehaviour or per-frame scene traversal.
        private static void ConfigurePerformance(Transform root)
        {
            StaticOcclusionCulling.Clear();
            var environment = root.Find("Environment");
            var terrain = environment.GetComponentInChildren<Terrain>();
            Undo.RecordObject(terrain, "Tune lakebed backdrop detail");
            terrain.heightmapPixelError = 6;
            terrain.basemapDistance = 150;
            // Grass and pebbles cover the whole play area and its surroundings from any position.
            terrain.detailObjectDistance = Mathf.Ceil((PlayViewDistance() + SurroundingMargin) / 10) * 10;
            terrain.drawInstanced = true;
            EditorUtility.SetDirty(terrain);

            // Preserve the current quality tier and user MSAA preference. The demo's
            // doubled LOD bias spends geometry on inaccessible scenery and trees.
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var bias = levels.GetArrayElementAtIndex(i).FindPropertyRelative("lodBias");
                bias.floatValue = Mathf.Min(bias.floatValue, 1);
            }
            quality.ApplyModifiedProperties();
            QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 1);

            var outline = PlayArea();
            var footprint = new Bounds(new Vector3(outline[0].x, 0, outline[0].y), Vector3.zero);
            foreach (var point in outline) footprint.Encapsulate(new Vector3(point.x, 0, point.y));
            // Keep a generous transition outside the boundary, including noon-shadow
            // projection from tall nearby cliffs. Distant mountain shadows never reach play.
            footprint.Expand(new Vector3(50, 2000, 50));
            foreach (string groupName in Groups.Where(g => g != "Water"))
            {
                var group = environment.Find(groupName);
                foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                    if (source == null) continue;
                    Undo.RecordObject(renderer, "Omit backdrop shadow casters");
                    renderer.shadowCastingMode = footprint.Intersects(renderer.bounds) ? source.shadowCastingMode : ShadowCastingMode.Off;
                    UseProjectFoliage(renderer);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
            foreach (var lod in environment.GetComponentsInChildren<LODGroup>(true))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(lod);
                if (source == null || source.lodCount != lod.lodCount || lod.lodCount == 0) continue;
                var scale = lod.transform.lossyScale;
                float size = lod.size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                Undo.RecordObject(lod, "Keep lakebed detail changes distant");
                DistantDetail(lod, source.GetLODs(), size, PlayVisibleDistance(outline, lod.transform.TransformPoint(lod.localReferencePoint), size));
                PrefabUtility.RecordPrefabInstancePropertyModifications(lod);
            }
            ConfigureTreeDetail(terrain, footprint);
            ConfigureOcclusion(root, footprint);
            SunPresentationSetup.ConfigureShadowBudget(root.Find("Sun").GetComponent<Light>());
        }

        private static void ConfigureTreeDetail(Terrain terrain, Bounds nearGround)
        {
            if (!AssetDatabase.IsValidFolder(TreesFolder)) AssetDatabase.CreateFolder(Folder, "Trees");
            var data = terrain.terrainData;
            var old = data.treePrototypes;
            var instances = data.treeInstances;
            var prototypes = new List<TreePrototype>();
            var mapping = new Dictionary<(GameObject, bool), int>();
            for (int i = 0; i < instances.Length; i++)
            {
                var tree = instances[i];
                var prefab = old[tree.prototypeIndex].prefab;
                // Vendor trees are themselves variants of model prefabs. Stop at the
                // vendor prefab, not OriginalSource (which would skip straight to FBX).
                var source = AssetDatabase.GetAssetPath(prefab).StartsWith(TreesFolder + "/", StringComparison.Ordinal)
                    ? PrefabUtility.GetCorrespondingObjectFromSource(prefab) ?? prefab : prefab;
                bool backdrop = !nearGround.Contains(terrain.transform.position + Vector3.Scale(tree.position, data.size));
                var key = (source, backdrop);
                if (!mapping.TryGetValue(key, out int index))
                {
                    index = prototypes.Count;
                    // Only the approved imported trees belong to this setup; retain custom prototypes.
                    var tuned = AssetDatabase.GetAssetPath(source).StartsWith(VendorPrefabs + "Trees/", StringComparison.Ordinal)
                        ? TreeVariant(source, backdrop) : prefab;
                    prototypes.Add(new TreePrototype { prefab = tuned, bendFactor = old[tree.prototypeIndex].bendFactor });
                    mapping.Add(key, index);
                }
                tree.prototypeIndex = index;
                instances[i] = tree;
            }
            Undo.RecordObject(data, "Tune terrain tree representations");
            data.SetTreeInstances(Array.Empty<TreeInstance>(), false);
            data.treePrototypes = prototypes.ToArray();
            data.SetTreeInstances(instances, false);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            terrain.Flush();
        }

        // Detail changes stay far from the player: no mesh switch nearer than these
        // camera distances even at the widest permitted FOV, and the coarsest mesh or
        // tree impostor only on the far backdrop. Farther vendor switches are kept.
        public static readonly float[] DetailSwitchDistances = { 50, 80, 120 };
        public const float CoarsestDetailDistance = 200;

        public static float SwitchDistance(float worldSize, float screenHeight) =>
            worldSize / (2 * Mathf.Tan(CameraPreferences.MaximumFov * .5f * Mathf.Deg2Rad) * screenHeight);

        private static void DistantDetail(LODGroup lod, LOD[] vendor, float worldSize, float neverCullWithin = 0)
        {
            var levels = lod.GetLODs();
            int last = levels.Length - 1;
            float atOneMetre = worldSize / (2 * Mathf.Tan(CameraPreferences.MaximumFov * .5f * Mathf.Deg2Rad));
            // Horizon silhouettes are never culled sooner than the vendor intended, and nothing the
            // play area can see is culled at all from inside it.
            levels[last].screenRelativeTransitionHeight = vendor[last].screenRelativeTransitionHeight * .5f;
            if (neverCullWithin > 0)
                levels[last].screenRelativeTransitionHeight = Mathf.Min(levels[last].screenRelativeTransitionHeight, atOneMetre / neverCullWithin);
            bool blend = true;
            for (int i = last - 1; i >= 0; i--)
            {
                float distance = i == last - 1 ? CoarsestDetailDistance : DetailSwitchDistances[Mathf.Min(i, DetailSwitchDistances.Length - 1)];
                float height = Mathf.Min(vendor[i].screenRelativeTransitionHeight, atOneMetre / distance);
                levels[i].screenRelativeTransitionHeight = Mathf.Max(height, levels[i + 1].screenRelativeTransitionHeight + .0001f);
            }
            for (int i = 0; i <= last; i++)
            {
                // A narrow band only matters where a renderer path ignores animated fades.
                levels[i].fadeTransitionWidth = .1f;
                foreach (var renderer in levels[i].renderers)
                    if (renderer != null)
                        blend &= renderer.sharedMaterials.All(m => m != null && m.shader.keywordSpace.FindKeyword("LOD_FADE_CROSSFADE").isValid);
            }
            lod.SetLODs(levels);
            // A timed dither during each switch replaces the vendor trees' permanent
            // half-band dither, which speckled mid-distance foliage.
            lod.fadeMode = blend ? LODFadeMode.CrossFade : LODFadeMode.None;
            lod.animateCrossFading = blend;
        }

        // Vendor foliage dithers leaf cards across a wide band of viewing angles to hide
        // edge-on cards; its Bayer pattern speckles whole canopies at every distance.
        // Project copies still hide those cards, over a narrow band.
        private static void UseProjectFoliage(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            var foliage = materials.Select(ProjectFoliage).ToArray();
            if (!foliage.SequenceEqual(materials)) renderer.sharedMaterials = foliage;
        }

        private static Material ProjectFoliage(Material vendor)
        {
            if (vendor == null || !vendor.IsKeywordEnabled("_HIDESIDES_ON") || !vendor.HasProperty("_HidePower")
                || !AssetDatabase.GetAssetPath(vendor).StartsWith("Assets/BK/", StringComparison.Ordinal))
                return vendor;
            string path = FoliageFolder + "/" + vendor.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material; // Retain Inspector tuning.
            if (!AssetDatabase.IsValidFolder(TreesFolder)) AssetDatabase.CreateFolder(Folder, "Trees");
            if (!AssetDatabase.IsValidFolder(FoliageFolder)) AssetDatabase.CreateFolder(TreesFolder, "Materials");
            material = new Material(vendor) { name = vendor.name };
            material.SetFloat("_HidePower", 8);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject TreeVariant(GameObject source, bool backdrop)
        {
            string path = TreesFolder + "/" + source.name + (backdrop ? " Backdrop" : " Near") + ".prefab";
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                var lod = instance.GetComponent<LODGroup>();
                if (lod != null && lod.lodCount > 0)
                    DistantDetail(lod, lod.GetLODs(), lod.size, backdrop ? 0 : PlayViewDistance() + SurroundingMargin);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    UseProjectFoliage(renderer);
                    if (backdrop) renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static void ConfigureOcclusion(Transform root, Bounds footprint)
        {
            // Exclude the editable excavation AND its solid edit-mode preview. Terrain
            // holes and alpha-cutout plants must never become solid baked blockers.
            const StaticEditorFlags flags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(item.gameObject, GameObjectUtility.GetStaticEditorFlags(item.gameObject) & ~flags);
            var environment = root.Find("Environment");
            foreach (string groupName in Groups.Where(g => g != "Water"))
                foreach (var renderer in environment.Find(groupName).GetComponentsInChildren<MeshRenderer>(true))
                {
                    var settings = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.OccludeeStatic;
                    if (groupName != "Trees" && !groupName.StartsWith("Rubble", StringComparison.Ordinal)
                        && renderer.bounds.size.magnitude >= 8)
                        settings |= StaticEditorFlags.OccluderStatic;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, settings & ~StaticEditorFlags.BatchingStatic);
                }
            void Volume(string name, Vector3 centre, Vector3 size)
            {
                var child = environment.Find(name);
                if (child == null) { child = new GameObject(name).transform; child.SetParent(environment, false); }
                var area = child.GetComponent<OcclusionArea>();
                if (area == null) area = child.gameObject.AddComponent<OcclusionArea>();
                area.center = centre; area.size = size;
                var settings = new SerializedObject(area);
                settings.FindProperty("m_IsViewVolume").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            Volume("Surface visibility volume", new Vector3(footprint.center.x, FlightCeiling * .5f, footprint.center.z),
                new Vector3(footprint.size.x, FlightCeiling + 12, footprint.size.z));
            Volume("Excavation visibility volume", new Vector3(0, -SiteLayout.Extent.y * .5f, 0),
                SiteLayout.Extent + Vector3.one * 4);
            StaticOcclusionCulling.smallestOccluder = 4;
            StaticOcclusionCulling.smallestHole = .25f;
            StaticOcclusionCulling.backfaceThreshold = 100;
            root.GetComponentInChildren<Camera>().useOcclusionCulling = true;
        }
    }
}
