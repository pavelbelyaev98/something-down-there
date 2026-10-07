using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // The great caves' crystal trophies (116; user, 2026-10-07: "each cave has one crystal to excavate and it becomes a unique
    // people look at"): uniques the generator seats on a great cave's floor (DiscoveryCatalog.SeatTrophies), one for each
    // zone's cave, dug free and sent up with the crane to stand at camp. Each is a glowing big formation from the Crystal
    // Caverns pack (BuriedPropsSetup.Trophies), baked like a prop find (PropBake) but without the size cap of buried finds,
    // scaled to its height_m (art/pure-nature-crystal-caverns/trophy.json).
    public static class CrystalTrophySetup
    {
        private const string SourcePath = "art/pure-nature-crystal-caverns/trophy.json", Folder = "Assets/Content/Discoveries/Trophy";
        [Serializable] private sealed class Source { public int schema_version; public Trophy[] trophies; }
        [Serializable] private sealed class Trophy { public string prefab; public float height_m, mass_kg; public DiscoveryContentSetup.SourceEntry find; }

        internal static void AppendToCatalog(List<DiscoveryCatalog.Entry> entries)
        {
            foreach (string suffix in new[] { "", "/Meshes", "/Prefabs" }) RetroComputerSetup.EnsureFolder(Folder + suffix);
            var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../" + SourcePath))));
            if (source == null || source.schema_version != 1 || source.trophies == null || source.trophies.Length == 0)
                throw new InvalidDataException("Invalid crystal trophy source.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trophy in source.trophies)
            {
                var e = trophy?.find;
                if (e == null || string.IsNullOrWhiteSpace(e.content_id) || !identities.Add(e.content_id)
                    || e.tier != "unique" || e.recovery != "rope" || e.instances != 1 || e.shallow_instances != 0 || e.slots != 0
                    || e.sale_value != 0 || !e.detector_eligible || string.IsNullOrWhiteSpace(e.lore)
                    || e.required_exposure <= 0 || e.required_exposure > 1 || !(trophy.height_m >= 1 && trophy.height_m <= 3)
                    || !(trophy.mass_kg > 0) || !(e.maximum_depth_m > e.minimum_depth_m))
                    throw new InvalidDataException("Invalid crystal trophy policy: " + e?.content_id);
                var (mesh, hull, material) = PropBake.Bake(trophy.prefab, e.content_id, Folder + "/Meshes", 1, false, true);
                float scale = trophy.height_m / mesh.bounds.size.y;
                var find = DiscoveryContentSetup.UpdatePrefab(e, mesh, hull, material, Folder, true, trophy.mass_kg, scale);
                var path = UnityEditor.AssetDatabase.GetAssetPath(find);
                var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                try
                {
                    using (var data = new UnityEditor.SerializedObject(root.GetComponent<BuriedFind>()))
                    {
                        data.FindProperty("liftsByCrown").boolValue = true;
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    find = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<BuriedFind>();
                }
                finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
                DiscoveryContentSetup.GenerateDetailLevels(mesh);
                entries.Add(new DiscoveryCatalog.Entry { ItemId = e.content_id, Prefab = find, Count = 1, CaveTrophy = true,
                    MinDepth = e.minimum_depth_m, MaxDepth = e.maximum_depth_m });
            }
        }
    }
}
