using System.Linq;
using UnityEngine.SceneManagement;

namespace SomethingDownThere.Tests
{
    internal sealed class TestInputPreferences : IDevicePreferencesStore
    {
        public string Contents;
        public string Read() => Contents;
        public void Write(string contents) => Contents = contents;
        // Exercise the generic small-find branch with approved coal meshes in
        // known shallow positions. These are test-only instance settings; the
        // production catalog currently classifies all its finds as large.
        // Physics tests place their finds explicitly, so they restore a compact population:
        // the three coal finds first, three of every common appearance and each unique.
        // Generating and replacing all ~12k finds per test cost most of the class's time.
        private static FindSnapshot[] compactPopulation;
        private static int compactSeed;
        private static System.Collections.Generic.HashSet<string> coalFixture = new System.Collections.Generic.HashSet<string>();
        // The three retagged shallow coal finds (smaller meshes than the rocks around them).
        public static bool IsCoalFixture(BuriedFind find) => coalFixture.Contains(find.Item.InstanceId);
        public static void RestoreSmallFindFixture(DiscoveryField field)
        {
            if (compactPopulation == null)
            {
                if (field.Finds.Count == 0) field.InitializePopulation();
                var saved = field.Capture();
                var prefab = field.Catalog.Entries.First(e => e.ItemId == "mineral_coal").Prefab;
                for (int i = 0; i < 3; i++)
                {
                    saved[i].ContentId = prefab.SaveContentId;
                    saved[i].Item.Name = prefab.DisplayName;
                    saved[i].Item.Value = prefab.SaleValue;
                    saved[i].Scale = prefab.transform.localScale;
                }
                var uniques = field.Catalog.Entries.Where(e => e.Prefab.Kind == DiscoveryKind.Unique).Select(e => e.Prefab.SaveContentId).ToArray();
                compactPopulation = saved.Take(3).Concat(saved.Skip(3).Where(s => !uniques.Contains(s.ContentId))
                    .GroupBy(s => s.ContentId).SelectMany(g => g.Take(3))).Concat(saved.Where(s => uniques.Contains(s.ContentId))).ToArray();
                compactSeed = field.Seed;
                coalFixture = new System.Collections.Generic.HashSet<string>(compactPopulation.Take(3).Select(s => s.Item.Id));
            }
            field.Restore(compactPopulation, compactSeed);
        }

        // Discovery checks need the real shallow layer and a few of every find type, not all
        // ~12k finds. Tests that measure the deep layout restore the generated population.
        private static FindSnapshot[] layerPopulation;
        public static FindSnapshot[] GeneratedPopulation { get; private set; }
        // The stash chests (106) and, in the layer, everything they hold.
        public static ChestSnapshot[] GeneratedChests { get; private set; }
        public static void RestoreLayerFixture(DiscoveryField field)
        {
            if (layerPopulation == null)
            {
                if (field.Finds.Count == 0) field.InitializePopulation();
                GeneratedPopulation = field.Capture();
                GeneratedChests = field.CaptureChests();
                compactSeed = field.Seed;
                int shallow = field.Catalog.ShallowCount;
                var uniques = field.Catalog.Entries.Where(e => e.Prefab.Kind == DiscoveryKind.Unique).Select(e => e.Prefab.SaveContentId).ToArray();
                float reach = field.Catalog.Chest != null ? field.Catalog.Chest.Radius : 0;
                bool held(FindSnapshot s) => GeneratedChests.Any(c => UnityEngine.Vector3.Distance(c.Position, s.Position) < reach);
                layerPopulation = GeneratedPopulation.Take(shallow).Concat(GeneratedPopulation.Skip(shallow).Where(s => !uniques.Contains(s.ContentId))
                    .GroupBy(s => s.ContentId).SelectMany(g => g.Take(3))).Concat(GeneratedPopulation.Where(s => uniques.Contains(s.ContentId)))
                    .Concat(GeneratedPopulation.Where(held)).GroupBy(s => s.Item.Id).Select(g => g.First()).ToArray();
            }
            field.Restore(layerPopulation, compactSeed, GeneratedChests);
        }

        public static void RestoreGeneratedPopulation(DiscoveryField field) => field.Restore(GeneratedPopulation, compactSeed, GeneratedChests);

        public static void ConfigureLayerFinds(Scene scene, LoadSceneMode mode)
        {
            Configure(scene, mode);
            if (layerPopulation == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var field in root.GetComponentsInChildren<DiscoveryField>(true)) field.DeferGeneration();
        }

        // Suites that never touch finds skip generating the ~12k population.
        public static void ConfigureWithoutFinds(Scene scene, LoadSceneMode mode)
        {
            Configure(scene, mode);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var field in root.GetComponentsInChildren<DiscoveryField>(true)) field.DeferGeneration();
        }

        // Once the compact population exists, later scenes skip full generation entirely.
        public static void ConfigureCompactFinds(Scene scene, LoadSceneMode mode)
        {
            Configure(scene, mode);
            if (compactPopulation == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var field in root.GetComponentsInChildren<DiscoveryField>(true)) field.DeferGeneration();
        }

        public static void Configure(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var player in root.GetComponentsInChildren<FpsPlayer>(true))
                {
                    player.ConfigureInputPreferences(new TestInputPreferences());
                    player.ConfigureGamePreferences(new TestInputPreferences());
                }
        }
    }
}
