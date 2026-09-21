// Unity CLI eval_file preflight. Save intentional project edits and finish any
// active test run before calling (test-changed.ps1 checks the runner status).
// Unsaved Editor scene changes are disposable under the repository's policy.
if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling)
    throw new System.InvalidOperationException("Finish play mode or compilation before preparing tests.");
UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
    UnityEditor.SceneManagement.NewSceneMode.Single);
return "Tests prepared on a clean empty scene; unsaved edits discarded, scene files untouched.";
