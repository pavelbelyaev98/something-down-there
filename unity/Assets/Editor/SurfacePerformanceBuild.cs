using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SomethingDownThere.Editor
{
    public static class SurfacePerformanceBuild
    {
        [MenuItem("Tools/Something Down There/Validation/Build Surface Performance Player")]
        public static void Build()
        {
            const string fixture = "Assets/Scenes/SurfacePerformanceValidation.unity";
            if (File.Exists(fixture)) throw new InvalidOperationException("Validation scene path is already occupied.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save scene work before building validation.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool frameTimings = PlayerSettings.enableFrameTimingStats;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Surface performance validation", typeof(SurfacePerformanceFixture));
                EditorSceneManager.SaveScene(scene, fixture);
                PlayerSettings.enableFrameTimingStats = true;
                string output = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../builds/validation/surface/SurfacePerformance.exe"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { fixture, "Assets/Scenes/MainGame.unity" }, locationPathName = output,
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
