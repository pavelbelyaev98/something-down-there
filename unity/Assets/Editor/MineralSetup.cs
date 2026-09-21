using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // Only the approved, source-owned mineral batch is imported here.
    public static class MineralSetup
    {
        public const string Folder = "Assets/Content/Minerals";
        private static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/minerals"));

        [MenuItem("Tools/Something Down There/Sync Mineral Models")]
        public static void Sync() => DiscoveryContentSetup.Sync();

        internal static void AppendToCatalog(List<DiscoveryCatalog.Entry> entries)
        {
            var source = JsonUtility.FromJson<DiscoveryContentSetup.SourceCatalog>(File.ReadAllText(Path.Combine(Source, "catalog.json")));
            string[] names = { "Coal", "Copper", "Iron", "Silver", "Gold", "Emerald", "Ruby", "Diamond" };
            if (source == null || source.schema_version != 1 || source.variants == null || source.variants.Length != names.Length)
                throw new InvalidDataException("Invalid approved mineral source catalog.");
            for (int i = 0; i < names.Length; i++)
            {
                var e = source.variants[i];
                if (e == null || e.content_id != "mineral_" + names[i].ToLowerInvariant() || e.display_name != names[i]
                    || e.instances < 0 || e.shallow_instances < 0 || e.shallow_instances > e.instances
                    || e.sale_value <= 0 || e.slots != 1 || e.detector_eligible || e.tier != "common"
                    || e.required_exposure != .6f || !float.IsFinite(e.minimum_depth_m) || !float.IsFinite(e.maximum_depth_m)
                    || e.minimum_depth_m < .6f || e.maximum_depth_m <= e.minimum_depth_m || e.maximum_depth_m > SiteLayout.Extent.y - .8f
                    || e.deep_instances < 0 || e.deep_instances > e.instances - e.shallow_instances
                    || !float.IsFinite(e.deep_minimum_depth_m) || !float.IsFinite(e.deep_maximum_depth_m)
                    || e.deep_minimum_depth_m < 0 || e.deep_maximum_depth_m < 0
                    || (e.deep_instances > 0 && (e.deep_minimum_depth_m < e.maximum_depth_m
                        || e.deep_maximum_depth_m <= e.deep_minimum_depth_m || e.deep_maximum_depth_m > SiteLayout.Extent.y - .8f))
                    || !float.IsFinite(e.core_minimum_depth_m) || !float.IsFinite(e.core_maximum_depth_m)
                    || !float.IsFinite(e.core_share) || e.core_share <= 0 || e.core_share > 1
                    || e.core_minimum_depth_m < e.minimum_depth_m || e.core_maximum_depth_m > e.maximum_depth_m
                    || e.core_maximum_depth_m <= e.core_minimum_depth_m
                    || e.dimensions_m == null || e.dimensions_m.Length != 3 || e.dimensions_m.Any(v => !float.IsFinite(v) || v <= 0))
                    throw new InvalidDataException("Invalid mineral identity, placement or collection contract.");
                if (i > 0 && (e.sale_value <= source.variants[i - 1].sale_value
                    || e.core_minimum_depth_m <= source.variants[i - 1].core_minimum_depth_m))
                    throw new InvalidDataException("Mineral value and core depth must increase in the selected order.");
                var prefab = DiscoveryContentSetup.ImportAppearance(e, Source, Folder, !e.small, i < 5 ? 4 : 2);
                entries.Add(new DiscoveryCatalog.Entry { ItemId = e.content_id, Prefab = prefab, Count = e.instances,
                    ShallowCount = e.shallow_instances, MinDepth = e.minimum_depth_m, MaxDepth = e.maximum_depth_m,
                    CoreMinDepth = e.core_minimum_depth_m, CoreMaxDepth = e.core_maximum_depth_m, CoreShare = e.core_share,
                    DeepCount = e.deep_instances, DeepMinDepth = e.deep_minimum_depth_m, DeepMaxDepth = e.deep_maximum_depth_m,
                    RandomOrientation = true });
            }
        }
    }
}
