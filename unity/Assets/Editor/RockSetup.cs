using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    // The plain rock (art/plain-rock/rock.json): four of the Mountains pack's stones in the old rock's colour map
    // (BuriedPropsSetup.Stones; user, 2026-10-08: new shapes, "not the poop like form", the colours kept). Its looks bake
    // like any prop find (PropFindSetup); the source also sets the shallow layer under the surface, where it lies thickest.
    public static class RockSetup
    {
        public const string Folder = MineralSetup.Folder;
        private static string Source => Path.GetFullPath(Path.Combine(Application.dataPath, "../../art/plain-rock/rock.json"));
        [Serializable] private sealed class RockSource
        {
            public int schema_version;
            public string prefab;
            public float mass_kg, shallow_minimum_cover_m, shallow_maximum_cover_m;
            public DiscoveryContentSetup.SourceEntry find;
        }

        [MenuItem("Tools/Something Down There/Sync Rock Models")]
        public static void Sync() => DiscoveryContentSetup.Sync();

        internal static void AppendToCatalog(List<DiscoveryCatalog.Entry> entries)
        {
            var source = JsonUtility.FromJson<RockSource>(File.ReadAllText(Source));
            var e = source?.find;
            if (source == null || source.schema_version != 1 || e == null || e.content_id != "common_rock" || string.IsNullOrEmpty(source.prefab)
                || e.tier != "common" || e.recovery != "bag" || e.slots != 1 || e.detector_eligible || e.required_exposure != .6f
                || e.instances < 0 || e.shallow_instances < 0 || e.shallow_instances > e.instances || e.sale_value < 0
                || string.IsNullOrWhiteSpace(e.display_name) || !(source.mass_kg > 0 && source.mass_kg <= 50)
                || !float.IsFinite(e.minimum_depth_m) || !float.IsFinite(e.maximum_depth_m)
                || e.minimum_depth_m < .6f || e.maximum_depth_m <= e.minimum_depth_m || e.maximum_depth_m > 31.2f)
                throw new InvalidDataException("Invalid plain rock source contract.");
            if (!float.IsFinite(e.core_minimum_depth_m) || !float.IsFinite(e.core_maximum_depth_m)
                || !float.IsFinite(e.core_share) || e.core_share <= 0 || e.core_share > 1
                || e.core_minimum_depth_m < e.minimum_depth_m || e.core_maximum_depth_m > e.maximum_depth_m
                || e.core_maximum_depth_m <= e.core_minimum_depth_m)
                throw new InvalidDataException("Invalid plain rock core band.");
            if (!(source.shallow_minimum_cover_m >= 0 && source.shallow_maximum_cover_m >= source.shallow_minimum_cover_m))
                throw new InvalidDataException("Invalid plain rock shallow cover.");
            foreach (string suffix in new[] { "", "/Meshes", "/Prefabs" }) RetroComputerSetup.EnsureFolder(Folder + suffix);
            e.prefab = source.prefab;
            var looks = PropFindSetup.ImportLooks(e, Folder, false, source.mass_kg);
            entries.Add(new DiscoveryCatalog.Entry { ItemId = e.content_id, Prefab = looks[0], AppearanceVariants = looks.Skip(1).ToArray(),
                Count = e.instances, ShallowCount = e.shallow_instances, MinDepth = e.minimum_depth_m,
                ShallowMinCover = source.shallow_minimum_cover_m, ShallowMaxCover = source.shallow_maximum_cover_m,
                MaxDepth = e.maximum_depth_m, CoreMinDepth = e.core_minimum_depth_m, CoreMaxDepth = e.core_maximum_depth_m, CoreShare = e.core_share,
                HostGrounds = DiscoveryContentSetup.HostGrounds(e.host_grounds, e.host_weights), HostWeights = DiscoveryContentSetup.HostWeights(e.host_grounds, e.host_weights),
                RandomOrientation = true });
        }
    }
}
