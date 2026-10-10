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
        // The crane's set-down spots (119): a yard beside the workshop (local x, z), most on its side toward the mast (-X),
        // clear of the walk from the camp to the door, each about 2.8 m from the next.
        private static readonly Vector2[] YardLocal =
        {
            new Vector2(-6.6f, -5.2f), new Vector2(-6.6f, -2.4f), new Vector2(-6.6f, .4f), new Vector2(-6.6f, 3.2f),
            new Vector2(-9.4f, -3.8f), new Vector2(-9.4f, -1f), new Vector2(-9.4f, 1.8f),
        };
        private const string ConcreteDetail = "Assets/TowerCrane/Textures/concrete_Normal.png";
        // The bought Survival Workshop Garage (Tirgames, art/survival-workshop-garage) on trial behind the workshop, its
        // door end toward the camp centre, on the flattest clear ground near it (the yard and the crane's swing stay free).
        private const string GarageVendor = "Assets/TirgamesAssets/SurvivalWorkshop/Models/";
        private const string GarageFolder = "Assets/Content/Camp/Garage";
        private static readonly Vector3 GarageSpot = new Vector3(-19, 0, -35);
        // Floor half-extents of the shell (its long axis X, door end +X) and the demo scene's poses round its floor centre.
        private static readonly Vector2 GarageHalf = new Vector2(6.47f, 3.95f);
        private static readonly (string prefab, Vector3 position, float yaw)[] GarageDoors =
        {
            ("PBRGarage/Prefabs/GarageDoor01Animated", new Vector3(6.18f, 0, .03f), 270),
            ("PBRGarage/Prefabs/GarageDoor02Animated", new Vector3(6.36f, 0, 3.18f), 90),
        };
        private static readonly float[] GarageLampX = { -4.64f, -1.6f, 1.5f, 4.6f }, GarageLampZ = { -1.86f, 1.87f };
        private const float GarageLampY = 3.14f;

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
            AddStash(workshop, surface.parent.GetComponentInChildren<DiscoveryField>());
            ClearGrass(scene, workshop);
            var garage = BuildBoughtGarage(surface);
            ClearGrass(scene, garage);
            SurfaceStationSetup.Configure();  // the computer moves in with the workshop
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Camp workshop configured at " + workshop.position + ", computer at " + surface.Find("ComputerStation").position
                + ", bought garage on trial at " + garage.position);
        }

        // Rim-layout ground positions of the crane's set-down spots in the workshop's yard.
        public static Vector3[] YardSpots()
        {
            var workshop = WorkshopPose();
            return YardLocal.Select(p => workshop.position + workshop.rotation * new Vector3(p.x, 0, p.y))
                .Select(w => new Vector3(w.x, 0, w.z)).ToArray();
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
            // Racking as its boards and uprights (workshop_geometry.py), so finds on a shelf can be aimed at and set down.
            foreach (var (x0, x1) in new[] { (-3.3f, -1.3f), (-1.2f, .8f) })
            {
                float z0 = hd - .55f, z1 = hd - .1f;
                foreach (float x in new[] { x0, x1 })
                    foreach (float z in new[] { z0, z1 })
                        Box(c, "Rack upright", new Vector3(x, l.slab_m + 1.05f, z), new Vector3(.04f, 2.1f, .04f));
                foreach (float top in new[] { .168f, .768f, 1.368f, 1.968f })
                    Box(c, "Rack shelf", new Vector3((x0 + x1) / 2, l.slab_m + top - .029f, (z0 + z1) / 2), new Vector3(x1 - x0 + .04f, .058f, z1 - z0 + .05f));
            }

            // Two fluorescent battens under the collar ties, cool white.
            var lamps = Group(root, "Lamps");
            foreach (float z in new[] { -1.5f, 1.5f })
                DownLight(lamps, "Batten light", new Vector3(0, l.plate_m + 1.0f - .21f, z), 150, 7.5f, 2.2f, new Color(.96f, .98f, 1f));
            return root;
        }

        // The bought garage, bare: its shell, the roller door rolled up and the side door shut (their legacy-input script,
        // prompt canvas and animation dropped), the demo's eight ceiling lamps as fittings and four soft-shadowed down-lights
        // like the workshop's battens, on Excavation Lit copies of its Built-in materials.
        private static Transform BuildBoughtGarage(Transform surface)
        {
            var previous = surface.Find("BoughtGarage");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            if (!AssetDatabase.IsValidFolder(GarageFolder)) AssetDatabase.CreateFolder("Assets/Content/Camp", "Garage");
            var away = GarageSpot - CampCentre; away.y = 0;
            var rotation = Quaternion.LookRotation(away.normalized) * Quaternion.Euler(0, 90, 0);  // door end (+X) to the camp
            var root = new GameObject("BoughtGarage").transform;
            root.SetParent(surface, false);
            // Its floor on the highest ground under it, so no terrain shows through.
            root.SetPositionAndRotation(new Vector3(GarageSpot.x, HighestGround(GarageSpot, rotation, GarageHalf) + .02f, GarageSpot.z), rotation);
            var materials = new Dictionary<Material, Material>();
            GaragePart(root, "PBRGarage/Prefabs/Garage01", Vector3.zero, 0, materials);
            foreach (var (prefab, position, yaw) in GarageDoors)
            {
                var door = GaragePart(root, prefab, position, yaw, materials);
                PrefabUtility.UnpackPrefabInstance(door.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var animation = door.GetComponent<Animation>();
                // The roller door takes its rolled-up pose; the side door keeps its prefab pose, which is shut (the pack's
                // side-door clips are named the wrong way round: "Close" holds it open).
                if (prefab.Contains("Door01")) animation.GetClip("Opened").SampleAnimation(door.gameObject, 0);
                UnityEngine.Object.DestroyImmediate(door.GetComponent("GarageDoor"));
                UnityEngine.Object.DestroyImmediate(animation);
                foreach (var trigger in door.GetComponents<BoxCollider>().Where(c => c.isTrigger)) UnityEngine.Object.DestroyImmediate(trigger);
                UnityEngine.Object.DestroyImmediate(door.Find("UI").gameObject);
            }
            var lamps = Group(root, "Lamps");
            foreach (float x in GarageLampX)
            {
                foreach (float z in GarageLampZ)
                {
                    var lamp = GaragePart(lamps, "PBRIndustrialLights/Prefabs/CeilingLamp01On", new Vector3(x, GarageLampY, z), 0, materials);
                    foreach (var light in lamp.GetComponentsInChildren<Light>()) light.enabled = false;
                }
                DownLight(lamps, "Down light", new Vector3(x, GarageLampY - .05f, 0), 150, 7.5f, 2.2f, new Color(1f, .97f, .9f));
            }
            return root;
        }

        private static Transform GaragePart(Transform parent, string prefab, Vector3 position, float yaw, Dictionary<Material, Material> materials)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GarageVendor + prefab + ".prefab");
            if (asset == null) throw new InvalidOperationException("Missing the bought garage's " + prefab);
            var part = ((GameObject)PrefabUtility.InstantiatePrefab(asset, parent)).transform;
            part.localPosition = position; part.localRotation = Quaternion.Euler(0, yaw, 0);
            foreach (var renderer in part.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    m == null ? null : materials.TryGetValue(m, out var copy) ? copy : materials[m] = FromStandard(m)).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            return part;
        }

        // An Excavation Lit copy of a Built-in Standard (metallic) material: same maps, its smoothness from the metallic
        // map's alpha (scaled) or the plain value, emission only where the vendor's glows.
        private static Material FromStandard(Material vendor)
        {
            if (vendor.shader.name != "Standard") throw new InvalidDataException("Expected a Standard material: " + vendor.name);
            var material = MaterialAsset(GarageFolder, vendor.name);
            void Keyword(string keyword, bool on) { if (on) material.EnableKeyword(keyword); else material.DisableKeyword(keyword); }
            material.SetTexture("_BaseMap", vendor.GetTexture("_MainTex")); material.SetColor("_BaseColor", vendor.GetColor("_Color"));
            material.SetTextureScale("_BaseMap", vendor.GetTextureScale("_MainTex")); material.SetTextureOffset("_BaseMap", vendor.GetTextureOffset("_MainTex"));
            var normal = vendor.GetTexture("_BumpMap");
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", vendor.GetFloat("_BumpScale")); Keyword("_NORMALMAP", normal != null);
            var gloss = vendor.GetTexture("_MetallicGlossMap");
            material.SetTexture("_MetallicGlossMap", gloss); Keyword("_METALLICSPECGLOSSMAP", gloss != null);
            material.SetFloat("_Metallic", vendor.GetFloat("_Metallic"));
            material.SetFloat("_Smoothness", gloss != null ? vendor.GetFloat("_GlossMapScale") : vendor.GetFloat("_Glossiness"));
            bool albedoAlpha = vendor.GetFloat("_SmoothnessTextureChannel") > .5f;
            material.SetFloat("_SmoothnessTextureChannel", albedoAlpha ? 1 : 0); Keyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", albedoAlpha);
            var occlusion = vendor.GetTexture("_OcclusionMap");
            material.SetTexture("_OcclusionMap", occlusion); material.SetFloat("_OcclusionStrength", vendor.GetFloat("_OcclusionStrength"));
            Keyword("_OCCLUSIONMAP", occlusion != null);
            var glow = vendor.GetColor("_EmissionColor");
            bool glows = vendor.IsKeywordEnabled("_EMISSION") && glow.maxColorComponent > 0;
            material.SetTexture("_EmissionMap", glows ? vendor.GetTexture("_EmissionMap") : null);
            material.SetColor("_EmissionColor", glows ? glow : Color.black); Keyword("_EMISSION", glows);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Highest terrain under a rectangle (half-extents along the pose's X and Z).
        private static float HighestGround(Vector3 centre, Quaternion rotation, Vector2 half)
        {
            float high = float.MinValue;
            for (int i = -4; i <= 4; i++)
                for (int j = -4; j <= 4; j++)
                {
                    var p = centre + rotation * new Vector3(half.x * i / 4, 0, half.y * j / 4);
                    foreach (var terrain in Terrain.activeTerrains)
                    {
                        var corner = terrain.transform.position; var size = terrain.terrainData.size;
                        if (p.x >= corner.x && p.z >= corner.z && p.x <= corner.x + size.x && p.z <= corner.z + size.z)
                            high = Mathf.Max(high, terrain.SampleHeight(p) + corner.y);
                    }
                }
            if (high == float.MinValue) throw new InvalidOperationException("No terrain under the bought garage.");
            return high;
        }

        // One spot per carry unique (art/stash-uniques/catalog.json): where its base stands, turned its way, with a glowing
        // see-through copy of the unique standing exactly as it will be set down, and an aim box filling that shape, both
        // off until that unique is carried (StashSpot).
        private static void AddStash(Transform workshop, DiscoveryField field)
        {
            if (field == null) throw new InvalidOperationException("MainGame has no discovery field for the stash spots.");
            var ghost = Clear(WorkshopFolder, "Workshop_StashGhost", new Color(1f, .93f, .8f, .22f), .2f, null, 1, new Color(1f, .86f, .62f) * .9f);
            ghost.SetFloat("_ZWrite", 1);  // only the nearest surface shows, so the copy reads as one solid shape
            EditorUtility.SetDirty(ghost);
            var group = Group(workshop, "Stash");
            foreach (var unique in StashUniqueSetup.Load().uniques)
            {
                string id = unique.find.content_id;
                var prefab = StashUniqueSetup.Prefab(id);
                var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                var scale = prefab.transform.localScale;
                var size = Vector3.Scale(mesh.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                // BuriedFind.PlaceAt stands the unique's bounds on the spot, centred over it.
                var stand = Vector3.up * (size.y / 2 + .002f);
                var spot = new GameObject(unique.find.display_name + " spot").transform;
                spot.SetParent(group, false);
                spot.localPosition = unique.stash.position;
                spot.localRotation = Quaternion.Euler(0, unique.stash.yaw, 0);
                var copy = new GameObject("Ghost", typeof(MeshFilter), typeof(MeshRenderer));
                copy.transform.SetParent(spot, false);
                copy.transform.localPosition = stand - Vector3.Scale(mesh.bounds.center, scale);
                copy.transform.localScale = scale;
                copy.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = copy.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = Enumerable.Repeat(ghost, mesh.subMeshCount).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.enabled = false;
                var aim = spot.gameObject.AddComponent<BoxCollider>();
                aim.center = stand; aim.size = size; aim.enabled = false;
                var stash = spot.gameObject.AddComponent<StashSpot>();
                using var data = new SerializedObject(stash);
                data.FindProperty("contentId").stringValue = id;
                data.FindProperty("discoveries").objectReferenceValue = field;
                data.FindProperty("ghost").objectReferenceValue = renderer;
                data.FindProperty("aim").objectReferenceValue = aim;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
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
