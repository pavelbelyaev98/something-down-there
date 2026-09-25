using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    public static class SurfacePerformanceBuild
    {
        [MenuItem("Tools/Something Down There/Validation/Build Surface Performance Player")]
        public static void Build() => Build(false);

        [MenuItem("Tools/Something Down There/Validation/Build Environment Performance Player")]
        public static void BuildEnvironment() => Build(true);

        private static void Build(bool environment)
        {
            const string fixture = "Assets/Scenes/SurfacePerformanceValidation.unity";
            if (File.Exists(fixture)) throw new InvalidOperationException("Validation scene path is already occupied.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save scene work before building validation.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool frameTimings = PlayerSettings.enableFrameTimingStats;
            try
            {
                if (environment)
                {
                    // Single-scene boot is required for normal lighting and baked occlusion.
                    var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity", OpenSceneMode.Single);
                    var root = scene.GetRootGameObjects().Single(o => o.name == "MainGameRoot");
                    UnityEngine.Object.DestroyImmediate(root.GetComponentInChildren<WorldSaveController>());
                    var validation = new GameObject("Surface performance validation").AddComponent<SurfacePerformanceFixture>();
                    var prototypes = root.GetComponentInChildren<Terrain>().terrainData.treePrototypes;
                    validation.EnvironmentReferenceTrees = prototypes.Select(p =>
                        PrefabUtility.GetCorrespondingObjectFromSource(p.prefab) ?? p.prefab).ToArray();
                    validation.EnvironmentReferenceLodHeights = SurfacePerformanceFixture.EnvironmentScenery
                        .SelectMany(n => root.transform.Find("Environment/" + n).GetComponentsInChildren<LODGroup>(true))
                        .SelectMany(l => (PrefabUtility.GetCorrespondingObjectFromSource(l) ?? l).GetLODs()
                            .Select(level => level.screenRelativeTransitionHeight)).ToArray();
                    // Save the disposable copy only; never write the removed save owner to MainGame.
                    EditorSceneManager.SaveScene(scene, fixture, true);
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    EditorSceneManager.OpenScene(fixture, OpenSceneMode.Single);
                }
                else
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    new GameObject("Surface performance validation", typeof(SurfacePerformanceFixture));
                    EditorSceneManager.SaveScene(scene, fixture);
                }
                PlayerSettings.enableFrameTimingStats = true;
                string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../builds/validation/surface/SurfacePerformance.exe"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = environment ? new[] { fixture } : new[] { fixture, "Assets/Scenes/MainGame.unity" }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Surface validation build failed.");
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = frameTimings;
                try
                {
                    if (Array.Exists(setup, s => s.isLoaded && s.isActive && !string.IsNullOrEmpty(s.path)))
                        EditorSceneManager.RestoreSceneManagerSetup(setup);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally
                {
                    AssetDatabase.DeleteAsset(fixture);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
