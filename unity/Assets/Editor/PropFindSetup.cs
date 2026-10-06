using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // Finds made from bought prop prefabs (BuriedPropsSetup): rubbish (106: art/tv-set, art/big-old-tv), which lies loose
    // in the soil in its depth band like any find, and a chest's treasure (113: the ingots in art/mining-pack and the gem
    // crystals in art/pure-nature-crystal-caverns that the old chests hold, taken by hand), and pyrite, the lake sediment's
    // fool's gold (114: ground.json beside them). Each source names its props and their find policy; a find can take further
    // appearances, each its own prop. The props bake to centred meshes (PropBake); the find prefab, surface samples and
    // detail levels come from the shared discovery import.
    public static class PropFindSetup
    {
        private static readonly (string path, string folder, bool boxHull)[] Sources =
        {
            ("art/tv-set/catalog.json", "Assets/Content/Discoveries/Junk", true),
            ("art/big-old-tv/catalog.json", "Assets/Content/Discoveries/Junk", true),
            ("art/mining-pack/catalog.json", "Assets/Content/Discoveries/Treasure", false),
            ("art/pure-nature-crystal-caverns/catalog.json", "Assets/Content/Discoveries/Treasure", false),
            ("art/pure-nature-crystal-caverns/ground.json", "Assets/Content/Minerals", false),
        };
        [Serializable] private sealed class Source { public int schema_version; public PropFind[] finds; }
        [Serializable] private sealed class PropFind { public string prefab; public float mass_kg; public DiscoveryContentSetup.SourceEntry find; }

        internal static void AppendToCatalog(List<DiscoveryCatalog.Entry> entries)
        {
            foreach (var (path, folder, boxHull) in Sources)
            {
                foreach (string suffix in new[] { "", "/Meshes", "/Prefabs" }) RetroComputerSetup.EnsureFolder(folder + suffix);
                var source = JsonUtility.FromJson<Source>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../../" + path))));
                if (source == null || source.schema_version != 1 || source.finds == null || source.finds.Length == 0)
                    throw new InvalidDataException("Invalid prop find source: " + path);
                foreach (var prop in source.finds)
                {
                    var e = prop?.find;
                    if (e == null || string.IsNullOrWhiteSpace(e.content_id) || e.tier != "common" || e.recovery != "bag"
                        || e.instances < 1 || e.shallow_instances != 0 || e.slots != 1 || e.sale_value < 1 || e.detector_eligible
                        || e.required_exposure <= 0 || e.required_exposure > 1 || !(prop.mass_kg > 0 && prop.mass_kg <= 50))
                        throw new InvalidDataException("Invalid prop find policy: " + e?.content_id);
                    e.prefab = prop.prefab;
                    var looks = ImportLooks(e, folder, boxHull, prop.mass_kg);
                    entries.Add(new DiscoveryCatalog.Entry { ItemId = e.content_id, Prefab = looks[0], AppearanceVariants = looks.Skip(1).ToArray(),
                        Count = e.instances, MinDepth = e.minimum_depth_m, MaxDepth = e.maximum_depth_m,
                        HostGrounds = DiscoveryContentSetup.HostGrounds(e.host_grounds, e.host_weights),
                        HostWeights = DiscoveryContentSetup.HostWeights(e.host_grounds, e.host_weights) });
                }
            }
        }

        // The find's look from its prop prefab, then each further look (prop_looks) under its own key.
        internal static List<BuriedFind> ImportLooks(DiscoveryContentSetup.SourceEntry e, string folder, bool boxHull, float mass)
        {
            float scale = DiscoveryContentSetup.ModelScale(e);
            var looks = new List<BuriedFind> { Import(e, e.prefab, folder, boxHull, mass, scale) };
            foreach (var other in e.prop_looks ?? Array.Empty<DiscoveryContentSetup.PropLook>())
            {
                if (string.IsNullOrWhiteSpace(other?.content_id) || !other.content_id.StartsWith(e.content_id + "_", StringComparison.Ordinal))
                    throw new InvalidDataException(e.content_id + ": each further look needs its own key under the find's.");
                var look = JsonUtility.FromJson<DiscoveryContentSetup.SourceEntry>(JsonUtility.ToJson(e));
                look.content_id = other.content_id;
                looks.Add(Import(look, other.prefab, folder, boxHull, mass, scale));
            }
            return looks;
        }

        private static BuriedFind Import(DiscoveryContentSetup.SourceEntry e, string prefab, string folder, bool boxHull, float mass, float scale)
        {
            var (mesh, hull, material) = PropBake.Bake(prefab, e.content_id, folder + "/Meshes", scale, boxHull);
            var find = DiscoveryContentSetup.UpdatePrefab(e, mesh, hull, material, folder, true, mass, scale);
            DiscoveryContentSetup.GenerateDetailLevels(mesh);
            return find;
        }
    }
}
