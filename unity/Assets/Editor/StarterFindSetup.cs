using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Editor
{
    // Repeatable source -> import -> centred mesh/surface samples -> stable prefab/catalog.
    // The editable JSON owns selection and references; this tool owns no prices or model list.
    public static class StarterFindSetup
    {
        public const string Folder = "Assets/Content/StarterFinds";
        public const string CatalogPath = Folder + "/StarterFindCatalog.asset";
        private static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/starter-finds"));
        [Serializable] public sealed class SourceCatalog { public int schema_version; public SourceEntry[] variants; public Alias[] legacy_aliases; }
        [Serializable] public sealed class Alias { public string old_id, current_id; }
        [Serializable] public sealed class Maps { public string BaseColor, Normal, Masks; }
        [Serializable] public sealed class SourceEntry
        {
            public string content_id, display_name, atlas_group, tier, fbx, collision_fbx;
            public float[] dimensions_m;
            public int instances, shallow_instances, sale_value, slots;
            public bool detector_eligible, lay_on_side;
            public float required_exposure;
            public float throw_speed;
            // Small finds keep the entry carpet: coal ships this way.
            public bool small;
            public float minimum_depth_m, maximum_depth_m, core_minimum_depth_m, core_maximum_depth_m, core_share;
            public int deep_instances;
            public float deep_minimum_depth_m, deep_maximum_depth_m;
            // Authored shrink applied to the prefab: smaller finds read as ordinary junk and
            // their soil envelope shrinks with them, so the entry layer holds more of them.
            public float model_scale;
            public Maps textures;
        }

        [MenuItem("Tools/Something Down There/Sync Starter Find Models")]
        public static void Sync()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Sync finds outside Play Mode.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MainGameSceneBuilder.ScenePath) throw new InvalidOperationException("Open the saved MainGame scene first.");
            var source = JsonUtility.FromJson<SourceCatalog>(File.ReadAllText(Path.Combine(Source, "catalog.json")));
            ValidateSource(source);
            EnsureFolder(Folder);
            foreach (string name in new[] { "Models", "Textures", "Materials", "Meshes", "Prefabs" }) EnsureFolder(Folder + "/" + name);
            CopySourceIfPresent("LICENSE.txt", Folder + "/LICENSE.txt");
            var materials = new Dictionary<string, Material>();
            foreach (var entry in source.variants)
                if (!materials.ContainsKey(entry.atlas_group)) materials.Add(entry.atlas_group, MaterialFor(entry));
            var entries = new List<DiscoveryCatalog.Entry>();
            foreach (var entry in source.variants)
            {
                float scale = ModelScale(entry);
                string modelPath = Folder + "/Models/" + entry.content_id + ".fbx";
                CopySource(entry.fbx, modelPath);
                ImportModel(modelPath, false);
                string collisionPath = Folder + "/Models/" + entry.content_id + "_collision.fbx";
                CopySource(entry.collision_fbx, collisionPath); ImportModel(collisionPath, true);
                Mesh mesh = CentreModel(modelPath, entry, "", Folder, scale);
                Mesh collision = CentreModel(collisionPath, entry, "_collision", Folder, scale);
                if (collision.triangles.Length / 3 > 220 || collision.vertices.Distinct().Count() > 112)
                    throw new InvalidDataException("Bottle collision hull exceeds its authored 112-point/220-triangle budget.");
                var prefab = UpdatePrefab(entry, mesh, collision, materials[entry.atlas_group], Folder, false, .4f, scale);
                entries.Add(new DiscoveryCatalog.Entry { ItemId = entry.content_id, Prefab = prefab, Count = entry.instances,
                    ShallowCount = entry.shallow_instances, LayOnSide = entry.lay_on_side });
            }
            var catalog = AssetDatabase.LoadAssetAtPath<DiscoveryCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<DiscoveryCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            PhotoRockSetup.AppendToCatalog(entries);
            MineralSetup.AppendToCatalog(entries);
            catalog.Entries = entries.ToArray();
            catalog.LegacyAliases = source.legacy_aliases.Select(a => new DiscoveryCatalog.LegacyAlias { OldId = a.old_id, CurrentId = a.current_id }).ToArray();
            catalog.Validate(); EditorUtility.SetDirty(catalog);
            ConfigureScene(catalog);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            PruneRetiredAssets(source);
            Debug.Log($"Starter find trial synced: {entries.Count} stable prefabs, {catalog.TotalCount} new-game instances. Existing saves are not opened or rewritten by this tool.");
        }

        // Headless entry point (`-executeMethod ...SyncBatch`) for shells and CI where no
        // Editor is open: open the shipped scene, then run the same menu sync.
        public static void SyncBatch()
        {
            EditorSceneManager.OpenScene(MainGameSceneBuilder.ScenePath, OpenSceneMode.Single);
            Sync();
        }

        // Shared deterministic import mechanics; each batch retains isolated source/output ownership.
        internal static BuriedFind ImportAppearance(SourceEntry entry, string sourceRoot, string folder, bool large, float mass)
        {
            EnsureFolder(folder);
            foreach (string name in new[] { "Models", "Textures", "Materials", "Meshes", "Prefabs" }) EnsureFolder(folder + "/" + name);
            CopySource("LICENSE.txt", folder + "/LICENSE.txt", sourceRoot);
            float scale = ModelScale(entry);
            var material = MaterialFor(entry, folder, sourceRoot);
            string visualPath = folder + "/Models/" + entry.content_id + ".fbx";
            string collisionPath = folder + "/Models/" + entry.content_id + "_collision.fbx";
            CopySource(entry.fbx, visualPath, sourceRoot); ImportModel(visualPath, false);
            CopySource(entry.collision_fbx, collisionPath, sourceRoot); ImportModel(collisionPath, true);
            var visual = CentreModel(visualPath, entry, "", folder, scale);
            var collision = CentreModel(collisionPath, entry, "_collision", folder, scale);
            if (collision.triangles.Length / 3 > 220 || collision.vertices.Distinct().Count() > 112)
                throw new InvalidDataException("Discovery hull exceeds its 112-point/220-triangle budget.");
            return UpdatePrefab(entry, visual, collision, material, folder, large, mass, scale);
        }

        // Missing model_scale keeps the authored size; the envelope check scales with it.
        internal static float ModelScale(SourceEntry entry)
        {
            float scale = entry.model_scale > 0 ? entry.model_scale : 1f;
            if (!float.IsFinite(scale) || scale < .1f || scale > 1.5f)
                throw new InvalidDataException("Model scale must sit between 0.1 and 1.5.");
            return scale;
        }

        private static void PruneRetiredAssets(SourceCatalog source)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in source.variants)
            {
                keep.Add(Folder + "/Models/" + e.content_id + ".fbx");
                keep.Add(Folder + "/Models/" + e.content_id + "_collision.fbx");
                keep.Add(Folder + "/Meshes/" + e.content_id + ".asset");
                keep.Add(Folder + "/Meshes/" + e.content_id + "_collision.asset");
                keep.Add(Folder + "/Prefabs/" + e.content_id + ".prefab");
                keep.Add(Folder + "/Materials/" + e.atlas_group + ".mat");
                foreach (string map in new[] { "BaseColor", "Normal", "MetallicOcclusionSmoothness" })
                    keep.Add(Folder + "/Textures/" + e.atlas_group + "_" + map + ".png");
            }
            // These five folders contain only derived catalog outputs, never user source files.
            foreach (string group in new[] { "Models", "Meshes", "Prefabs", "Materials", "Textures" })
                foreach (string path in Directory.GetFiles(Folder + "/" + group).Select(p => p.Replace('\\', '/')))
                    if (!path.EndsWith(".meta", StringComparison.Ordinal) && !keep.Contains(path))
                        if (!AssetDatabase.DeleteAsset(path)) throw new IOException("Could not remove retired generated asset: " + path);
            // The retired bottle trial owned these two files; with no variants left the
            // whole starter-find batch is gone and old saves resolve through aliases.
            if (source.variants.Length == 0)
            {
                foreach (string retired in new[] { Folder + "/BottleContact.physicMaterial", Folder + "/LICENSE.txt" })
                    if (File.Exists(retired)) AssetDatabase.DeleteAsset(retired);
            }
        }

        public static void ConfigureScene(DiscoveryCatalog catalog = null)
        {
            catalog = catalog != null ? catalog : AssetDatabase.LoadAssetAtPath<DiscoveryCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("Sync the approved starter catalog first.");
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || (scene.path != MainGameSceneBuilder.ScenePath && scene.name != "MainGame"))
                throw new InvalidOperationException("Configure the saved MainGame scene outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot").transform;
            var terrain = root.GetComponentInChildren<TerrainVolume>(); var player = root.GetComponentInChildren<FpsPlayer>();
            var fieldRoot = root.Find("Discoveries");
            if (fieldRoot == null) { fieldRoot = new GameObject("Discoveries").transform; fieldRoot.SetParent(root, false); }
            var field = GetOrAdd<DiscoveryField>(fieldRoot.gameObject);
            var data = new SerializedObject(field);
            data.FindProperty("catalog").objectReferenceValue = catalog;
            data.FindProperty("prefabs").arraySize = 0;
            data.FindProperty("terrain").objectReferenceValue = terrain;
            data.FindProperty("count").intValue = catalog.TotalCount;
            data.FindProperty("developmentContent").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(player);settings.FindProperty("discoveries").objectReferenceValue = field;
            settings.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ValidateSource(SourceCatalog source)
        {
            // The base catalog may be intentionally empty (the bottle trial was retired);
            // the merged catalog still validates the active rock/mineral content.
            if (source == null || source.schema_version != 1 || source.variants == null || source.legacy_aliases == null)
                throw new InvalidDataException("Unsupported or missing starter source catalog.");
            var ids = new HashSet<string>(); var groups = new Dictionary<string, string>();
            int total = 0, shallow = 0;
            foreach (var e in source.variants)
            {
                if (!Regex.IsMatch(e.content_id ?? "", "^[a-z][a-z0-9_]+$") || !ids.Add(e.content_id)
                    || !Regex.IsMatch(e.atlas_group ?? "", "^[A-Za-z][A-Za-z0-9_]+$") || string.IsNullOrWhiteSpace(e.display_name)
                    || e.dimensions_m == null || e.dimensions_m.Length != 3 || e.dimensions_m.Any(n => float.IsNaN(n) || float.IsInfinity(n) || n <= 0)
                    || e.instances < 0 || e.shallow_instances < 0 || e.shallow_instances > e.instances || e.sale_value < 0
                    || e.slots != 1 || e.tier != "common" || e.detector_eligible || Mathf.Abs(e.required_exposure - .6f) > .0001f)
                    throw new InvalidDataException("Invalid starter entry; preserve common-item collection rules.");
                SourcePath(e.fbx); SourcePath(e.collision_fbx); SourcePath(e.textures.BaseColor); SourcePath(e.textures.Normal); SourcePath(e.textures.Masks);
                string maps = JsonUtility.ToJson(e.textures);
                if (groups.TryGetValue(e.atlas_group, out string previous) && previous != maps) throw new InvalidDataException("One atlas group cannot point to different texture sets.");
                groups[e.atlas_group] = maps; total += e.instances; shallow += e.shallow_instances;
            }
            // A retained batch may be disabled; the merged catalog validates active shallow content.
            if (total > DiscoveryField.MaximumPopulation) throw new InvalidDataException("Invalid starter allocation.");
            var oldIds = new HashSet<string>();
            foreach (var alias in source.legacy_aliases)
            {
                // Targets are validated against the merged catalog, which also owns the
                // retired bottle ids now mapped onto the rock entry.
                if (string.IsNullOrWhiteSpace(alias.old_id) || ids.Contains(alias.old_id) || !oldIds.Add(alias.old_id)
                    || string.IsNullOrWhiteSpace(alias.current_id))
                    throw new InvalidDataException("Invalid legacy mapping.");
            }
        }

        private static Material MaterialFor(SourceEntry entry, string folder = Folder, string sourceRoot = null)
        {
            string prefix = folder + "/Textures/" + entry.atlas_group;
            CopySource(entry.textures.BaseColor, prefix + "_BaseColor.png", sourceRoot);
            CopySource(entry.textures.Normal, prefix + "_Normal.png", sourceRoot);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                source.LoadImage(File.ReadAllBytes(SourcePath(entry.textures.Masks, sourceRoot)));
                var pixels = source.GetPixels32();
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(pixels[i].r, pixels[i].g, 0, (byte)(255 - pixels[i].b));
                source.SetPixels32(pixels); source.Apply();
                WriteIfChanged(prefix + "_MetallicOcclusionSmoothness.png", source.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            var color = ImportTexture(prefix + "_BaseColor.png", true, false);
            var normal = ImportTexture(prefix + "_Normal.png", false, true);
            var masks = ImportTexture(prefix + "_MetallicOcclusionSmoothness.png", false, false);
            string path = folder + "/Materials/" + entry.atlas_group + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", color);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1);
            material.SetTexture("_MetallicGlossMap", masks); material.SetTexture("_OcclusionMap", masks);
            material.SetFloat("_Smoothness", 1); material.SetFloat("_Metallic", 1); material.SetFloat("_OcclusionStrength", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0); material.SetFloat("_Surface", 0);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(material); return material;
        }

        private static Texture2D ImportTexture(string path, bool srgb, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true; importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true,
                maxTextureSize = 2048, format = normal ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7, compressionQuality = 100 });
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ImportModel(string path, bool collision)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = collision ? ModelImporterTangents.None : ModelImporterTangents.CalculateMikk;
            importer.SetPreBakeCollisionMesh(isConvex: false, preBake: false);
            importer.SetPreBakeCollisionMesh(isConvex: true, preBake: collision);
            importer.isReadable = true; importer.SaveAndReimport();
        }

        private static Mesh CentreModel(string path, SourceEntry entry, string suffix = "", string folder = Folder, float scale = 1f)
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                var filters = model.GetComponentsInChildren<MeshFilter>();
                if (filters.Length != 1 || filters[0].sharedMesh.subMeshCount != 1) throw new InvalidDataException("Starter models must export one mesh and one atlas material.");
                var mesh = UnityEngine.Object.Instantiate(filters[0].sharedMesh);
                var matrix = filters[0].transform.localToWorldMatrix;
                var vertices = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                var bounds = new Bounds(vertices[0], Vector3.zero); foreach (var v in vertices) bounds.Encapsulate(v);
                var expected = new Vector3(entry.dimensions_m[0], entry.dimensions_m[2], entry.dimensions_m[1]);
                if ((bounds.size - expected).magnitude > .0002f) throw new InvalidDataException($"{entry.content_id}: imported dimensions {bounds.size:F4} differ from catalog {expected:F4}.");
                if (bounds.extents.magnitude * scale > DiscoveryField.MaximumFindRadius) throw new InvalidDataException("Replacement exceeds placement clearance; review its size first.");
                for (int i = 0; i < vertices.Length; i++) vertices[i] -= bounds.center;
                for (int i = 0; i < normals.Length; i++) normals[i] = matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
                for (int i = 0; i < tangents.Length; i++) { var t = matrix.MultiplyVector(tangents[i]).normalized; tangents[i] = new Vector4(t.x,t.y,t.z,tangents[i].w); }
                mesh.vertices = vertices; mesh.normals = normals; mesh.tangents = tangents; mesh.RecalculateBounds();
                mesh.name = entry.content_id + suffix;
                string output = folder + "/Meshes/" + entry.content_id + suffix + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(output);
                if (existing == null) { AssetDatabase.CreateAsset(mesh, output); return mesh; }
                EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        private static BuriedFind UpdatePrefab(SourceEntry entry, Mesh mesh, Mesh collisionMesh, Material material, string folder = Folder, bool large = false, float mass = .4f, float scale = 1f)
        {
            string path = folder + "/Prefabs/" + entry.content_id + ".prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(entry.content_id);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one * scale;
                GetOrAdd<MeshFilter>(root).sharedMesh = mesh;
                var renderer = GetOrAdd<MeshRenderer>(root); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                var collider = GetOrAdd<MeshCollider>(root); collider.sharedMesh = collisionMesh; collider.convex = true;
                collider.contactOffset = .002f;
                string contactPath = folder + (large ? "/RockContact.physicMaterial" : "/BottleContact.physicMaterial");
                var contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(contactPath);
                if (contact == null) { contact = new PhysicsMaterial(large ? "Rock contact" : "Bottle contact"); AssetDatabase.CreateAsset(contact, contactPath); }
                contact.dynamicFriction = .8f; contact.staticFriction = .9f; contact.bounciness = 0;
                contact.frictionCombine = PhysicsMaterialCombine.Maximum; contact.bounceCombine = PhysicsMaterialCombine.Minimum;
                EditorUtility.SetDirty(contact); collider.sharedMaterial = contact;
                var body = GetOrAdd<Rigidbody>(root);
                // Mass stays authored: shrinking the model is a look change, and lighter
                // bodies only buy solver jitter where finds have to settle.
                body.isKinematic = true; body.useGravity = false; body.mass = mass;
                body.linearDamping = .4f; body.angularDamping = 2;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.maxLinearVelocity = 12; body.maxAngularVelocity = 8; body.maxDepenetrationVelocity = 1.5f;
                body.solverIterations = 8; body.solverVelocityIterations = 2;
                var find = GetOrAdd<BuriedFind>(root);
                var handling = new SerializedObject(GetOrAdd<FindPhysics>(root));
                if (!float.IsFinite(entry.throw_speed) || entry.throw_speed < 1 || entry.throw_speed > 12)
                    throw new InvalidDataException("Find throw speed must be between 1 and 12 m/s.");
                handling.FindProperty("throwSpeed").floatValue = entry.throw_speed;
                handling.ApplyModifiedPropertiesWithoutUndo();
                var data = new SerializedObject(find);
                data.FindProperty("saveContentId").stringValue = entry.content_id; data.FindProperty("displayName").stringValue = entry.display_name;
                data.FindProperty("saleValue").intValue = entry.sale_value; data.FindProperty("size").enumValueIndex = (int)(large ? FindSize.Large : FindSize.Small);
                data.FindProperty("minor").boolValue = true; data.FindProperty("detectorEligible").boolValue = false;
                data.FindProperty("collectionThreshold").floatValue = entry.required_exposure;
                var samples = SurfaceSamples(mesh, 256, !large); var serialized = data.FindProperty("exposureSamples"); serialized.arraySize = samples.Length;
                for (int i = 0; i < samples.Length; i++) serialized.GetArrayElementAtIndex(i).vector3Value = samples[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BuriedFind>();
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else UnityEngine.Object.DestroyImmediate(root); }
        }

        public static Vector3[] SurfaceSamples(Mesh mesh, int count, bool exteriorFacingOnly = true)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            var faces = new List<int>(); var cumulative = new List<float>(); float area = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i+1]], c = vertices[triangles[i+2]];
                Vector3 cross = Vector3.Cross(b-a,c-a);
                // Exclude inward bottle walls; exposure samples describe the exterior silhouette.
                if ((exteriorFacingOnly && Vector3.Dot(cross, (a+b+c)/3 - mesh.bounds.center) < -.00000001f) || cross.magnitude < .000000001f) continue;
                area += cross.magnitude * .5f; faces.Add(i); cumulative.Add(area);
            }
            if (area <= 0) throw new InvalidDataException("No outward surface in the source mesh.");
            var result = new Vector3[count]; int face = 0;
            for (int n = 0; n < count; n++)
            {
                float target = area * (n + .5f) / count;
                while (face < cumulative.Count - 1 && cumulative[face] < target) face++;
                int i = faces[face]; float u = Mathf.Sqrt((n * .61803398875f + .37f) % 1); float v = (n * .754877666f + .19f) % 1;
                result[n] = vertices[triangles[i]]*(1-u) + vertices[triangles[i+1]]*(u*(1-v)) + vertices[triangles[i+2]]*(u*v);
            }
            return result;
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static string SourcePath(string relative, string sourceRoot = null)
        {
            if (string.IsNullOrWhiteSpace(relative)) throw new InvalidDataException("Missing source path.");
            string source = sourceRoot ?? Source;
            string path = Path.GetFullPath(Path.Combine(source, relative));
            if (!path.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new InvalidDataException("Missing source file or a path outside the owned starter source folder: " + relative);
            return path;
        }
        private static void CopySource(string relative, string output, string sourceRoot = null) => WriteIfChanged(output, File.ReadAllBytes(SourcePath(relative, sourceRoot)));
        private static void CopySourceIfPresent(string relative, string output, string sourceRoot = null)
        {
            string path = Path.GetFullPath(Path.Combine(sourceRoot ?? Source, relative));
            if (File.Exists(path)) WriteIfChanged(output, File.ReadAllBytes(path));
        }
        private static void WriteIfChanged(string output, byte[] bytes)
        {
            if (!File.Exists(output) || !File.ReadAllBytes(output).SequenceEqual(bytes)) File.WriteAllBytes(output, bytes);
            AssetDatabase.ImportAsset(output, ImportAssetOptions.ForceSynchronousImport);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
