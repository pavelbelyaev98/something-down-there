using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // Uniques small enough for the bag (119; art/stash-uniques/catalog.json): each is buried once at its authored pose,
    // taken by hand when exposed (no slot, never sold) and set down at its own spot in the camp workshop. Baked from a
    // bought prop like a prop find (PropBake); Configure Camp Workshop builds the spots from the same file.
    public static class StashUniqueSetup
    {
        public const string SourcePath = "art/stash-uniques/catalog.json", Folder = "Assets/Content/Discoveries/Stash";

        [Serializable] public sealed class Source { public int schema_version; public Unique[] uniques; }
        [Serializable] public sealed class Spot { public Vector3 position; public float yaw; }
        [Serializable] public sealed class Unique
        {
            public string prefab;
            public float mass_kg;
            public Vector3 position, euler;
            public Spot stash;
            public DiscoveryContentSetup.SourceEntry find;
        }

        public static Source Load()
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../" + SourcePath))));
            if (source == null || source.schema_version != 1 || source.uniques == null || source.uniques.Length == 0)
                throw new InvalidDataException("Invalid stash unique source.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var unique in source.uniques)
            {
                var e = unique?.find;
                if (e == null || string.IsNullOrWhiteSpace(e.content_id) || !identities.Add(e.content_id)
                    || e.tier != "unique" || e.recovery != "carry" || e.instances != 1 || e.shallow_instances != 0 || e.slots != 0
                    || e.sale_value != 0 || !e.detector_eligible || string.IsNullOrWhiteSpace(e.lore)
                    || e.required_exposure <= 0 || e.required_exposure > 1 || !(unique.mass_kg > 0 && unique.mass_kg <= 50) || unique.stash == null)
                    throw new InvalidDataException("Invalid stash unique policy: " + e?.content_id);
            }
            return source;
        }

        internal static void AppendToCatalog(List<DiscoveryCatalog.Entry> entries)
        {
            foreach (string suffix in new[] { "", "/Meshes", "/Prefabs" }) RetroComputerSetup.EnsureFolder(Folder + suffix);
            foreach (var unique in Load().uniques)
            {
                var e = unique.find;
                e.prefab = unique.prefab;
                float scale = DiscoveryContentSetup.ModelScale(e);
                var (mesh, hull, material) = PropBake.Bake(unique.prefab, e.content_id, Folder + "/Meshes", scale, true);
                var find = DiscoveryContentSetup.UpdatePrefab(e, mesh, hull, material, Folder, true, unique.mass_kg, scale);
                DiscoveryContentSetup.GenerateDetailLevels(mesh);
                entries.Add(new DiscoveryCatalog.Entry { ItemId = e.content_id, Prefab = find, Count = 1,
                    AuthoredPlacement = true, AuthoredPosition = unique.position, AuthoredEuler = unique.euler });
            }
        }

        // The unique's baked find (built by Sync Discovery Models).
        public static BuriedFind Prefab(string contentId)
        {
            var find = UnityEditor.AssetDatabase.LoadAssetAtPath<BuriedFind>(Folder + "/Prefabs/" + contentId + ".prefab");
            if (find == null) throw new InvalidOperationException("Run Sync Discovery Models first: missing stash unique " + contentId);
            return find;
        }
    }
}
