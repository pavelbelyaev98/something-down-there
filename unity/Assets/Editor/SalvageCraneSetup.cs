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
    // Builds the salvage crane from the purchased tower crane pack and wires it into MainGame. The
    // vendor files stay untouched: the crane is a project prefab variant with URP copies of the
    // pack's materials, its controller scripts removed (TowerCraneRig drives the same rig), the
    // hook's colliders off and interpolated motion. Its straight hoist cable is stripped from the jib
    // mesh: CraneRopeView draws the crane's one rope, which also serves as the smart rope. Rerunnable:
    // existing material copies, the rope settings asset and SalvageCrane tuning are kept.
    public static class SalvageCraneSetup
    {
        private const string Folder = "Assets/Content/Salvage";
        private const string MaterialFolder = Folder + "/Crane";
        private const string ModelFolder = Folder + "/Models";
        public const string LiftingEyePath = ModelFolder + "/LiftingEye.fbx";
        public const string VendorPrefab = "Assets/TowerCrane/Prefabs/TC_Old/TC_old_Base_small.prefab";
        public const string VariantPath = Folder + "/SalvageCrane.prefab";
        // The pack's smallest crane (user choice). Its mast stands south-east of the plot beside the
        // camp, where its 27 m trolley reaches about three quarters of the plot, every authored unique
        // and the set-down spots east of the camp. Its jib rests over those spots, so the swing out to
        // a hole is seen from the dig.
        public static readonly Vector3 Mast = new Vector3(15, 0, -14);
        public static readonly Vector3[] SetDownSpots = { new Vector3(6.2f, 0, -12.6f), new Vector3(8.4f, 0, -15.2f), new Vector3(6.6f, 0, -17.8f) };
        // The base footing sits slightly into the lakebed, whose ground dips under its edges.
        private const float BaseSink = .25f;
        // The trolley stops this far short of the pack's physical stop colliders.
        private const float StopMargin = .3f;
        // The crane's one rope, a little thicker than one of the pack's two cable strands.
        private const float CableWidth = .03f;
        // The pack's speed_General, doubled, and the rig's anti-sway assist (1/s²): the crane swings over
        // briskly and its hook settles over a hole instead of swinging on its long rope.
        private const float PackSpeed = .02f, SwayStiffness = 2f;
        // Metal of the small hook's bowl under the point a ring rests on.
        private const float HookThickness = .035f;
        private static Vector3 Storage => SetDownSpots.Aggregate(Vector3.zero, (sum, spot) => sum + spot) / SetDownSpots.Length;

        [MenuItem("Tools/Something Down There/Configure Salvage Crane")]
        public static void Configure()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != MainGameSceneBuilder.ScenePath) throw new InvalidOperationException("Open MainGame outside Play Mode.");
            var variant = BuildVariant();
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            var surface = root.Find("Surface");
            var terrain = root.GetComponentInChildren<TerrainVolume>(); var field = root.GetComponentInChildren<DiscoveryField>(); var player = root.GetComponentInChildren<FpsPlayer>();
            var lakebed = root.GetComponentsInChildren<Terrain>().Single();
            var station = Child(surface, "SalvageCrane");
            station.SetPositionAndRotation(LakebedSiteSetup.Camp(Vector3.zero), Quaternion.identity);
            var crane = station.Find("Crane");
            if (crane != null && PrefabUtility.GetCorrespondingObjectFromSource(crane.gameObject) != variant) { UnityEngine.Object.DestroyImmediate(crane.gameObject); crane = null; }
            if (crane == null) { crane = ((GameObject)PrefabUtility.InstantiatePrefab(variant, station)).transform; crane.name = "Crane"; }
            Vector3 rest = Storage - Mast; rest.y = 0;
            crane.SetPositionAndRotation(new Vector3(Mast.x, SiteLayout.GroundTop - BaseSink, Mast.z), Quaternion.LookRotation(rest));
            var spots = new Transform[SetDownSpots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                spots[i] = Child(station, "SetDownSpot " + (i + 1));
                Vector3 spot = SetDownSpots[i];
                spot.y = Mathf.Max(SiteLayout.GroundTop, lakebed.SampleHeight(spot) + lakebed.transform.position.y);
                spots[i].SetPositionAndRotation(spot, Quaternion.identity);
            }
            var settings = AssetDatabase.LoadAssetAtPath<SalvageRopeSettings>(Folder + "/RopeSettings.asset");
            if (settings == null) { settings = ScriptableObject.CreateInstance<SalvageRopeSettings>(); AssetDatabase.CreateAsset(settings, Folder + "/RopeSettings.asset"); }
            var ropeRoot = Child(station, "Rope");
            var view = Get<CraneRopeView>(ropeRoot.gameObject);
            var line = Get<LineRenderer>(ropeRoot.gameObject);
            line.useWorldSpace = true; line.sharedMaterial = CableMaterial();
            // Its collision radius stays in the rope settings.
            line.startWidth = line.endWidth = CableWidth; line.numCapVertices = 3; line.numCornerVertices = 2;
            line.generateLightingData = true; line.positionCount = 0; line.enabled = false;
            Set(view, "rope", line); Set(view, "rig", crane.GetComponent<TowerCraneRig>());
            var salvage = Get<SalvageCrane>(station.gameObject);
            Set(salvage, "terrain", terrain); Set(salvage, "discoveries", field); Set(salvage, "player", player);
            Set(salvage, "rig", crane.GetComponent<TowerCraneRig>()); Set(salvage, "markMaterial", RecoveryMark());
            Set(salvage, "settings", settings); Set(salvage, "ropeView", view);
            Set(salvage, "liftingEye", LiftingEye());
            using (var tuning = new SerializedObject(salvage))
            {
                // The idle hook rests over the set-down spots.
                tuning.FindProperty("restYaw").floatValue = 0; tuning.FindProperty("restReach").floatValue = rest.magnitude;
                tuning.ApplyModifiedPropertiesWithoutUndo();
            }
            Set(salvage, "soilChipsMaterial", SoilBreak("SoilCrumbs", false)); Set(salvage, "soilDustMaterial", SoilBreak("SoilDust", true));
            SetArray(salvage, "setDownSpots", spots);
            Set(player, "crane", salvage);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Salvage crane configured in MainGame.");
        }

        private static GameObject BuildVariant()
        {
            var vendor = AssetDatabase.LoadAssetAtPath<GameObject>(VendorPrefab);
            if (vendor == null) throw new InvalidOperationException("Missing the purchased tower crane pack: " + VendorPrefab);
            RetroComputerSetup.EnsureFolder(MaterialFolder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(vendor);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                // The vendor controller names the rig's moving bodies (the pack names them per model).
                // It reads legacy key input (unavailable here) and Hook only probes the demo's cargo
                // triggers; both live in the vendor's global assembly, so they are matched by name.
                var controller = instance.GetComponentsInChildren<MonoBehaviour>(true).Single(s => s != null && s.GetType().Name == "Controller_TC");
                Rigidbody cabin, truck, hook, swivel;
                using (var vendorRig = new SerializedObject(controller))
                {
                    cabin = Body(vendorRig, "boom_point_Rotation"); truck = Body(vendorRig, "truck");
                    hook = Body(vendorRig, "hook"); swivel = Body(vendorRig, "hook_point_Rotation");
                }
                foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script != null && (script.GetType().Name == "Controller_TC" || script.GetType().Name == "Hook"))
                        UnityEngine.Object.DestroyImmediate(script);
                foreach (var body in new[] { cabin, truck, hook, swivel }) body.interpolation = RigidbodyInterpolation.Interpolate;
                // Trolley travel ends at the cabin block behind it and the jib-end stop in front.
                float truckHalf = truck.GetComponent<BoxCollider>().size.z * .5f;
                float back = Stop(cabin.transform, true, "collider", "collider (1)"), front = Stop(cabin.transform, false, "jib_partEnd/collider");
                var swivelBox = swivel.GetComponent<BoxCollider>();
                float tip = swivel.position.y - swivel.transform.TransformPoint(swivelBox.center - Vector3.up * swivelBox.size.y * .5f).y;
                // The hook is a carrier: the clearance check owns safety, so it must not jam in a
                // narrow shaft, rest on the player or catch the flight ceiling.
                foreach (var collider in hook.GetComponents<Collider>().Concat(swivel.GetComponents<Collider>())) collider.enabled = false;
                // The slewing crane sits above the flight ceiling, out of reach: its colliders keep their
                // physical role (trolley stops) while aim, digging, lamp and rope rays pass them wherever
                // the jib points.
                int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
                foreach (var collider in cabin.GetComponentsInChildren<Collider>(true)) collider.gameObject.layer = ignoreRaycast;
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(UrpCopy).ToArray();
                var (sheave, ropeEntry) = StripCable(instance, truck.transform, hook.transform);
                Vector3 seat = HookSeat(instance, swivel.transform);
                var rig = instance.GetComponent<TowerCraneRig>() ?? instance.AddComponent<TowerCraneRig>();
                using (var data = new SerializedObject(rig))
                {
                    data.FindProperty("cabin").objectReferenceValue = cabin; data.FindProperty("truck").objectReferenceValue = truck;
                    data.FindProperty("hook").objectReferenceValue = hook; data.FindProperty("swivel").objectReferenceValue = swivel;
                    data.FindProperty("hoist").objectReferenceValue = hook.GetComponent<ConfigurableJoint>();
                    data.FindProperty("minimumReach").floatValue = back + truckHalf + StopMargin;
                    data.FindProperty("maximumReach").floatValue = front - truckHalf - StopMargin;
                    data.FindProperty("maximumRope").floatValue = truck.position.y + SiteLayout.Extent.y + 5;
                    data.FindProperty("hookDrop").floatValue = truck.position.y - hook.position.y;
                    data.FindProperty("swivelDrop").floatValue = hook.position.y - swivel.position.y;
                    data.FindProperty("tipDrop").floatValue = tip;
                    data.FindProperty("speedGeneral").floatValue = PackSpeed;
                    data.FindProperty("swayStiffness").floatValue = SwayStiffness;
                    data.FindProperty("sheave").vector3Value = sheave; data.FindProperty("ropeEntry").vector3Value = ropeEntry;
                    data.FindProperty("seat").vector3Value = seat;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                return PrefabUtility.SaveAsPrefabAsset(instance, VariantPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        // The pack's hoist cable is stretched between the trolley and hook-block bones of the jib's
        // skinned mesh, so it can only hang straight: the variant uses a copy of the mesh without its
        // triangles (the only ones joining those two bones). Returns the cable's top and bottom in the
        // trolley's and the hook block's local space, where the drawn rope starts and ends.
        private static (Vector3 sheave, Vector3 entry) StripCable(GameObject instance, Transform truck, Transform hook)
        {
            var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.bones.Contains(truck) && s.bones.Contains(hook));
            int top = Array.IndexOf(skin.bones, truck), bottom = Array.IndexOf(skin.bones, hook);
            var source = skin.sharedMesh;
            var weights = source.boneWeights; var vertices = source.vertices; var bind = source.bindposes;
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = source.name + " without hoist cable";
            var cable = new HashSet<int>();
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var triangles = source.GetTriangles(sub); var kept = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    bool toTop = false, toBottom = false;
                    for (int k = 0; k < 3; k++) { int bone = weights[triangles[i + k]].boneIndex0; toTop |= bone == top; toBottom |= bone == bottom; }
                    if (toTop && toBottom) { for (int k = 0; k < 3; k++) cable.Add(triangles[i + k]); continue; }
                    kept.Add(triangles[i]); kept.Add(triangles[i + 1]); kept.Add(triangles[i + 2]);
                }
                mesh.SetTriangles(kept, sub);
            }
            Vector3 sheave = Vector3.zero, entry = Vector3.zero; int tops = 0, bottoms = 0;
            foreach (int v in cable)
            {
                int bone = weights[v].boneIndex0;
                if (bone == top) { sheave += bind[top].MultiplyPoint3x4(vertices[v]); tops++; }
                else if (bone == bottom) { entry += bind[bottom].MultiplyPoint3x4(vertices[v]); bottoms++; }
            }
            if (tops == 0 || bottoms == 0) throw new InvalidOperationException("The tower crane rig has changed: no hoist cable between its trolley and hook.");
            string path = MaterialFolder + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; }
            skin.sharedMesh = mesh;
            return (sheave / tops, entry / bottoms);
        }

        // The inside bottom of the small hook's bowl in the swivel's local space: just above the lowest
        // vertex skinned to it (the bowl's outside), by the hook's metal thickness.
        private static Vector3 HookSeat(GameObject instance, Transform swivel)
        {
            var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s => s.bones.Contains(swivel));
            int bone = Array.IndexOf(skin.bones, swivel);
            var mesh = skin.sharedMesh; var weights = mesh.boneWeights; var vertices = mesh.vertices; var bind = mesh.bindposes[bone];
            var lowest = Enumerable.Range(0, vertices.Length).Where(v => weights[v].boneIndex0 == bone)
                .Select(v => bind.MultiplyPoint3x4(vertices[v])).OrderBy(v => v.y).First();
            return lowest + Vector3.up * HookThickness / swivel.lossyScale.y;
        }

        // The lifting eye's model (art/lifting-eye): a mesh only, its material slots remapped to project
        // URP materials and no collider, so it blocks neither targeting nor the rope.
        private static GameObject LiftingEye()
        {
            if (!(AssetImporter.GetAtPath(LiftingEyePath) is ModelImporter importer)) throw new InvalidOperationException("Missing the lifting eye model " + LiftingEyePath);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false; importer.importBlendShapes = false;
            importer.animationType = ModelImporterAnimationType.None; importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "LiftingEyePaint"), EyeMaterial("LiftingEyePaint", new Color(.85f, .6f, .08f), 0, .4f));
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "LiftingEyeSteel"), EyeMaterial("LiftingEyeSteel", new Color(.5f, .51f, .53f), .8f, .45f));
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(LiftingEyePath);
        }

        private static Material EyeMaterial(string name, Color color, float metallic, float smoothness)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static Rigidbody Body(SerializedObject vendorRig, string field)
        {
            var body = vendorRig.FindProperty(field)?.objectReferenceValue as Rigidbody;
            if (body == null) throw new InvalidOperationException("The tower crane rig has changed: Controller_TC." + field + " is missing");
            return body;
        }

        // Jib-space Z of a stop collider's face toward the trolley (the pack names it per model).
        private static float Stop(Transform cabin, bool behind, params string[] paths)
        {
            var box = paths.Select(p => cabin.Find(p)?.GetComponent<BoxCollider>()).FirstOrDefault(b => b != null);
            if (box == null) throw new InvalidOperationException("The tower crane rig has changed: missing stop " + string.Join(" or ", paths));
            Vector3 face = box.transform.TransformPoint(box.center + Vector3.forward * box.size.z * (behind ? .5f : -.5f));
            return cabin.InverseTransformPoint(face).z;
        }

        // URP Lit copy of a vendor Built-in Standard material (the vendor shader renders magenta
        // under URP). An existing copy is kept, so Inspector tuning survives reruns.
        private static Material UrpCopy(Material vendor)
        {
            if (vendor == null) return null;
            string path = MaterialFolder + "/" + vendor.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = vendor.name };
            material.SetTexture("_BaseMap", vendor.GetTexture("_MainTex"));
            material.SetTextureScale("_BaseMap", vendor.GetTextureScale("_MainTex")); material.SetTextureOffset("_BaseMap", vendor.GetTextureOffset("_MainTex"));
            material.SetColor("_BaseColor", vendor.GetColor("_Color"));
            if (vendor.GetTexture("_BumpMap") is Texture normal)
            { material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", vendor.GetFloat("_BumpScale")); material.EnableKeyword("_NORMALMAP"); }
            if (vendor.GetTexture("_MetallicGlossMap") is Texture metallic)
            {
                material.SetTexture("_MetallicGlossMap", metallic); material.SetFloat("_Smoothness", vendor.GetFloat("_GlossMapScale"));
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else { material.SetFloat("_Metallic", vendor.GetFloat("_Metallic")); material.SetFloat("_Smoothness", vendor.GetFloat("_Glossiness")); }
            material.SetFloat("_SmoothnessTextureChannel", 0);
            if (vendor.GetTexture("_OcclusionMap") is Texture occlusion)
            { material.SetTexture("_OcclusionMap", occlusion); material.SetFloat("_OcclusionStrength", vendor.GetFloat("_OcclusionStrength")); material.EnableKeyword("_OCCLUSIONMAP"); }
            if (vendor.IsKeywordEnabled("_DETAIL_MULX2") && vendor.GetTexture("_DetailNormalMap") is Texture detail)
            {
                material.SetTexture("_DetailNormalMap", detail); material.SetFloat("_DetailNormalMapScale", vendor.GetFloat("_DetailNormalMapScale"));
                material.SetTextureScale("_DetailAlbedoMap", vendor.GetTextureScale("_DetailAlbedoMap"));
                material.SetTextureOffset("_DetailAlbedoMap", vendor.GetTextureOffset("_DetailAlbedoMap"));
                material.EnableKeyword("_DETAIL_MULX2");
            }
            if (vendor.IsKeywordEnabled("_EMISSION"))
            {
                material.SetColor("_EmissionColor", vendor.GetColor("_EmissionColor")); material.SetTexture("_EmissionMap", vendor.GetTexture("_EmissionMap"));
                material.EnableKeyword("_EMISSION"); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            // Standard modes 2 (fade) and 3 (transparent, premultiplied): the cabin glass and lamps.
            if (vendor.GetFloat("_Mode") >= 2)
            {
                bool premultiply = vendor.GetFloat("_Mode") >= 3;
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", premultiply ? 1 : 0);
                material.SetFloat("_SrcBlend", (float)(premultiply ? BlendMode.One : BlendMode.SrcAlpha));
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (premultiply) material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("DepthOnly", false); material.SetShaderPassEnabled("ShadowCaster", false);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material CableMaterial()
        {
            string path = Folder + "/Cable.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.22f, .23f, .24f)); material.SetFloat("_Metallic", .3f); material.SetFloat("_Smoothness", .3f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        // Rope ruptures and the gravel pour throw the same soil crumbs and dust (GroundTextureSetup wires the pour).
        private static Material SoilBreak(string name, bool dust)
        {
            string path = GroundTextureSetup.Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Something Down There/Soil Break");
                if (shader == null) throw new InvalidOperationException("Missing soil-break shader.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_Dust", dust ? 1 : 0); EditorUtility.SetDirty(material);
            return material;
        }

        private static Material RecoveryMark()
        {
            string path = Folder + "/RecoveryMark.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Something Down There/Recovery Mark");
            if (shader == null) throw new InvalidOperationException("Missing recovery-mark shader.");
            material = new Material(shader); AssetDatabase.CreateAsset(material, path); return material;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = parent.Find(name); if (child != null) return child;
            child = new GameObject(name).transform; child.SetParent(parent, false); return child;
        }
        private static T Get<T>(GameObject go) where T : Component => go.TryGetComponent<T>(out var component) ? component : go.AddComponent<T>();
        private static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
        { using var data = new SerializedObject(owner); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetArray(UnityEngine.Object owner, string name, UnityEngine.Object[] values)
        {
            using var data = new SerializedObject(owner); var property = data.FindProperty(name); property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
