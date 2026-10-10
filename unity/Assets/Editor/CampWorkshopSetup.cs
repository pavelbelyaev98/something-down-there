using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // The camp workshop (art/camp-workshop, original Blender art): a corrugated-iron shed beside the camp with its
    // roller door toward it, the surface computer standing inside. Solid to the player (slab, walls, roof, fittings),
    // lit inside by its own battens; terrain grass under it is cleared here and again whenever the lakebed site is
    // rebuilt (its station footprints). Blender X stays X and Blender Y (front at -Y) becomes Z after the FBX axis
    // conversion.
    public static class CampWorkshopSetup
    {
        public const string WorkshopFolder = "Assets/Content/Camp/Workshop";
        // Rim-layout position (LakebedSiteSetup.Camp lifts it onto the lakebed ground); the door faces the camp centre.
        public static readonly Vector3 WorkshopSpot = new Vector3(-6.5f, 0, -24.5f);
        public static readonly Vector3 CampCentre = new Vector3(0, 0, -13);
        // The surface computer on the slab at the back right, clear of the racking, its screen toward the door.
        private static readonly Vector2 ComputerOnSlab = new Vector2(2.6f, 2.3f);
        private const string ConcreteDetail = "Assets/TowerCrane/Textures/concrete_Normal.png";

        [Serializable]
        private class WorkshopLayout
        {
            public float sheet_atlas_m, sheet_in_atlas_m, skylight_atlas_m, rib_pitch_m, width_m, depth_m, slab_m, plate_m,
                door_half_m, door_top_m, ridge_m, pitch, eave_overhang_m, gable_overhang_m;
            public AtlasScale atlas_m;
        }

        [Serializable]
        private class AtlasScale { public float Timber, Concrete, Metal; }

        [MenuItem("Tools/Something Down There/Configure Camp Workshop")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open MainGame outside Play Mode to configure the camp workshop.");
            var surface = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform.Find("Surface");
            var workshop = BuildWorkshop(surface);
            ClearGrass(scene, workshop);
            SurfaceStationSetup.Configure();  // the computer moves in with the workshop
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Camp workshop configured at " + workshop.position + ", computer at " + surface.Find("ComputerStation").position);
        }

        // Where the surface computer stands: inside the workshop, on its slab, facing the door.
        public static Pose ComputerPose()
        {
            var layout = JsonUtility.FromJson<WorkshopLayout>(File.ReadAllText(WorkshopFolder + "/Workshop.json"));
            var workshop = WorkshopPose();
            var facing = workshop.rotation * Quaternion.Euler(0, 180, 0);
            return new Pose(workshop.position + workshop.rotation * new Vector3(ComputerOnSlab.x, layout.slab_m, ComputerOnSlab.y), facing);
        }

        private static Pose WorkshopPose()
        {
            var spot = LakebedSiteSetup.Camp(WorkshopSpot);
            var away = spot - LakebedSiteSetup.Camp(CampCentre); away.y = 0;
            return new Pose(spot, Quaternion.LookRotation(away.normalized));
        }

        private static Transform BuildWorkshop(Transform surface)
        {
            var l = JsonUtility.FromJson<WorkshopLayout>(File.ReadAllText(WorkshopFolder + "/Workshop.json"));
            var ribs = Texture(WorkshopFolder + "/Textures/Workshop_Ribs_Normal.png", TextureImporterType.NormalMap, false, 256);
            float ribTiling(float atlas) => atlas / (4 * l.rib_pitch_m);
            var materials = new Dictionary<string, Material>
            {
                ["Workshop_SheetOut"] = Baked(WorkshopFolder, "Workshop_SheetOut", ribs, ribTiling(l.sheet_atlas_m), 1f),
                ["Workshop_SheetIn"] = Baked(WorkshopFolder, "Workshop_SheetIn", ribs, ribTiling(l.sheet_in_atlas_m), 1f),
                ["Workshop_Timber"] = Baked(WorkshopFolder, "Workshop_Timber", null, 0, 0),
                ["Workshop_Concrete"] = Baked(WorkshopFolder, "Workshop_Concrete", VendorNormal(ConcreteDetail), l.atlas_m.Concrete / 2.5f, .6f),
                ["Workshop_Metal"] = Baked(WorkshopFolder, "Workshop_Metal", null, 0, 0),
                ["Workshop_Skylight"] = Clear(WorkshopFolder, "Workshop_Skylight", new Color(.88f, .88f, .82f, .84f), .7f, ribs, ribTiling(l.skylight_atlas_m), new Color(.5f, .51f, .47f)),
                ["Workshop_Glass"] = Clear(WorkshopFolder, "Workshop_Glass", new Color(.32f, .34f, .33f, .32f), .86f, null, 1, Color.black),
                ["Workshop_TubeLamp"] = Glow(WorkshopFolder, "Workshop_TubeLamp", new Color(.96f, .98f, 1f) * 2.4f),
            };
            // Clear sheets and glass let the sun in; the lamp tubes are the light itself.
            var root = Place(surface, "CampWorkshop", WorkshopPose(), WorkshopFolder + "/Models/Workshop.fbx", materials,
                new[] { "Workshop_Skylight", "Workshop_Glass", "Workshop_TubeLamp" });

            var c = Group(root, "Colliders");
            float hw = l.width_m / 2, hd = l.depth_m / 2, wall = .12f, plate = l.plate_m;
            Box(c, "Slab", new Vector3(0, l.slab_m - .25f, 0), new Vector3(l.width_m, .5f, l.depth_m));
            float apronSlope = Mathf.Atan2(l.slab_m - .025f, .9f) * Mathf.Rad2Deg;
            Box(c, "Apron", new Vector3(0, (l.slab_m + .025f) / 2 - .1f, -hd - .45f), new Vector3((l.door_half_m + .35f) * 2, .2f, .95f), -apronSlope);
            foreach (float s in new[] { -1f, 1f })
            {
                Box(c, "Side wall", new Vector3(s * (hw - wall / 2 + .01f), plate / 2, 0), new Vector3(wall, plate, l.depth_m));
                float jamb = hw - l.door_half_m - .05f;
                Box(c, "Front wall", new Vector3(s * (l.door_half_m + .05f + jamb / 2), plate / 2, -hd + wall / 2), new Vector3(jamb, plate, wall));
            }
            Box(c, "Back wall", new Vector3(0, plate / 2, hd - wall / 2), new Vector3(l.width_m, plate, wall));
            Box(c, "Door head", new Vector3(0, (l.door_top_m + l.ridge_m) / 2, -hd + wall / 2), new Vector3(l.door_half_m * 2 + .2f, l.ridge_m - l.door_top_m, wall));
            Box(c, "Back gable", new Vector3(0, (plate + l.ridge_m) / 2, hd - wall / 2), new Vector3(l.width_m * .6f, l.ridge_m - plate, wall));
            float run = hw + l.eave_overhang_m, roll = Mathf.Atan(l.pitch) * Mathf.Rad2Deg, slope = run / Mathf.Cos(Mathf.Atan(l.pitch));
            foreach (float s in new[] { -1f, 1f })
                Box(c, "Roof", new Vector3(s * run / 2, l.ridge_m - run / 2 * l.pitch - .05f, 0), new Vector3(slope, .1f, l.depth_m + 2 * l.gable_overhang_m), 0, -s * roll);
            Box(c, "Workbench", new Vector3(-(hw - .5f), l.slab_m + .46f, -1.2f), new Vector3(.8f, .92f, 2.05f));
            Box(c, "Racking", new Vector3(-1.25f, l.slab_m + 1.05f, hd - .32f), new Vector3(4.2f, 2.1f, .5f));

            // Two fluorescent battens under the collar ties, cool white.
            var lamps = Group(root, "Lamps");
            foreach (float z in new[] { -1.5f, 1.5f })
                DownLight(lamps, "Batten light", new Vector3(0, l.plate_m + 1.0f - .21f, z), 150, 7.5f, 2.2f, new Color(.96f, .98f, 1f));
            return root;
        }

        // Imports the model, replaces any previous copy under Surface, poses it and assigns materials by part name.
        private static Transform Place(Transform surface, string name, Pose pose, string model, Dictionary<string, Material> materials, string[] noShadow)
        {
            if (!(AssetImporter.GetAtPath(model) is ModelImporter importer)) throw new InvalidOperationException("Missing the shelter model " + model);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = importer.importCameras = importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.addCollider = false; importer.isReadable = false;
            importer.SaveAndReimport();

            var previous = surface.Find(name);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root = new GameObject(name).transform;
            root.SetParent(surface, false);
            root.SetPositionAndRotation(pose.position, pose.rotation);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model), root);
            visual.name = name + " model";
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
            {
                string part = renderer.name.Split('.')[0];
                if (!materials.TryGetValue(part, out var material)) throw new InvalidDataException("Unexpected shelter part " + renderer.name);
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = noShadow.Contains(part) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            return root;
        }

        private static Transform Group(Transform root, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }

        private static void Box(Transform group, string name, Vector3 centre, Vector3 size, float pitchDeg = 0, float rollDeg = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(group, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(pitchDeg, 0, rollDeg);
            go.AddComponent<BoxCollider>().size = size;
        }

        // A downward spot with soft shadows from everything, so finds set down inside cast real shadows.
        private static void DownLight(Transform group, string name, Vector3 position, float angle, float range, float intensity, Color color)
        {
            var light = new GameObject(name, typeof(Light), typeof(UniversalAdditionalLightData)).GetComponent<Light>();
            light.transform.SetParent(group, false);
            light.transform.localPosition = position;
            light.transform.localRotation = Quaternion.Euler(90, 0, 0);
            light.type = LightType.Spot; light.spotAngle = angle; light.innerSpotAngle = angle * .6f;
            light.range = range; light.intensity = intensity; light.color = color;
            light.shadows = LightShadows.Soft; light.shadowBias = .02f; light.shadowNormalBias = .3f; light.shadowNearPlane = .1f;
        }

        private static Material Baked(string folder, string name, Texture2D detailNormal, float detailTiling, float detailScale, int size = 2048)
        {
            var material = MaterialAsset(folder, name);
            material.SetTexture("_BaseMap", Texture(folder + "/Textures/" + name + "_Albedo.png", TextureImporterType.Default, true, size));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", Texture(folder + "/Textures/" + name + "_Normal.png", TextureImporterType.NormalMap, false, size));
            material.SetFloat("_BumpScale", 1); material.EnableKeyword("_NORMALMAP");
            // One mask: metallic R, occlusion G, smoothness A.
            var mask = Texture(folder + "/Textures/" + name + "_Mask.png", TextureImporterType.Default, false, size);
            material.SetTexture("_MetallicGlossMap", mask); material.SetFloat("_Smoothness", 1); material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetTexture("_OcclusionMap", mask); material.SetFloat("_OcclusionStrength", 1); material.EnableKeyword("_OCCLUSIONMAP");
            if (detailNormal != null)
            {
                material.SetTexture("_DetailNormalMap", detailNormal); material.SetFloat("_DetailNormalMapScale", detailScale);
                material.SetTextureScale("_DetailAlbedoMap", new Vector2(detailTiling, detailTiling));
                material.EnableKeyword("_DETAIL_MULX2");
            }
            else { material.SetTexture("_DetailNormalMap", null); material.DisableKeyword("_DETAIL_MULX2"); }
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        // Transparent sheets; glow is the daylight that milky polycarbonate passes through (the sun stands at noon).
        private static Material Clear(string folder, string name, Color color, float smoothness, Texture2D normal, float tiling, Color glow)
        {
            var material = MaterialAsset(folder, name);
            if (glow.maxColorComponent > 0) { material.SetColor("_EmissionColor", glow); material.EnableKeyword("_EMISSION"); }
            else material.DisableKeyword("_EMISSION");
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", 0);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1); material.EnableKeyword("_NORMALMAP");
                material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            }
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("DepthOnly", false); material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Glow(string folder, string name, Color emission)
        {
            var material = MaterialAsset(folder, name);
            material.SetColor("_BaseColor", new Color(.95f, .95f, .93f)); material.SetFloat("_Smoothness", .6f);
            material.SetColor("_EmissionColor", emission); material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        // A purchased pack's normal map, used as it was imported (vendor import settings stay untouched).
        private static Texture2D VendorNormal(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.textureType != TextureImporterType.NormalMap)
                throw new InvalidOperationException("Expected the purchased normal map " + path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MaterialAsset(string folder, string name)
        {
            string materials = folder + "/Materials";
            if (!AssetDatabase.IsValidFolder(materials)) AssetDatabase.CreateFolder(folder, "Materials");
            string path = materials + "/" + name + ".mat";
            // Authored on the excavation daylight's Lit shader, so ExcavationDaylight leaves it alone: a runtime copy
            // would lose the transparent queue, and a build keeps only the variants its material assets use.
            var shader = Shader.Find("Something Down There/Excavation Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            return material;
        }

        private static Texture2D Texture(string path, TextureImporterType type, bool colour, int size)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) throw new InvalidOperationException("Missing texture " + path);
            importer.textureType = type; importer.sRGBTexture = colour; importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4; importer.maxTextureSize = size;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true, maxTextureSize = size,
                format = type == TextureImporterType.NormalMap ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7, compressionQuality = 100 });
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Terrain grass under the workshop goes now; LakebedSiteSetup's station footprints keep it clear on rebuilds.
        private static void ClearGrass(Scene scene, Transform shelter)
        {
            var terrain = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Terrain>()).FirstOrDefault();
            if (terrain == null) return;
            var data = terrain.terrainData;
            int cells = data.detailWidth;
            float cell = data.size.x / cells;
            var corner = terrain.transform.position;
            var bounds = shelter.GetComponentsInChildren<Renderer>().Where(r => r.bounds.min.y <= 1).Select(r => r.bounds).ToArray();
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                var map = data.GetDetailLayer(0, 0, cells, cells, layer);
                bool changed = false;
                foreach (var b in bounds)
                {
                    int u0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - .7f - corner.x) / cell)), u1 = Mathf.Min(cells - 1, Mathf.FloorToInt((b.max.x + .7f - corner.x) / cell));
                    int v0 = Mathf.Max(0, Mathf.FloorToInt((b.min.z - .7f - corner.z) / cell)), v1 = Mathf.Min(cells - 1, Mathf.FloorToInt((b.max.z + .7f - corner.z) / cell));
                    for (int v = v0; v <= v1; v++)
                    for (int u = u0; u <= u1; u++)
                        if (map[v, u] != 0) { map[v, u] = 0; changed = true; }
                }
                if (changed) data.SetDetailLayer(0, 0, layer, map);
            }
            EditorUtility.SetDirty(data);
        }
    }
}
