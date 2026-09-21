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
        public static void RestoreSmallFindFixture(DiscoveryField field)
        {
            var saved = field.Capture();
            var prefab = field.Catalog.Entries.First(e => e.ItemId == "mineral_coal").Prefab;
            for (int i = 0; i < 3; i++)
            {
                saved[i].ContentId = prefab.SaveContentId;
                saved[i].Item.Name = prefab.DisplayName;
                saved[i].Item.Value = prefab.SaleValue;
                saved[i].Scale = prefab.transform.localScale;
            }
            field.Restore(saved, field.Seed);
            var size = typeof(BuriedFind).GetField("size", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < 3; i++) size.SetValue(field.Finds[i], FindSize.Small);
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
