// Unity CLI eval_file preflight. Save intentional project edits and finish any
// active test run before calling (test-changed.ps1 checks the runner status).
// Unsaved Editor scene changes are disposable under the repository's policy.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling)
    throw new System.InvalidOperationException("Finish play mode or compilation before preparing tests.");
var scenes = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
    .Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt).ToArray();
var empty = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
    UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(empty);
foreach (var scene in scenes)
    if (!UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true))
        throw new System.InvalidOperationException("Could not close scene for tests: " + scene.name);
return "Tests prepared on a clean empty scene; unsaved edits discarded, scene files untouched.";
