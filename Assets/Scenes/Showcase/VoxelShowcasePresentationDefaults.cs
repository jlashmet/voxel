using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Restores scene-owned presentation defaults when the production VoxelShowcase is loaded.
    /// Diagnostic harnesses may still override these values after scene load for an explicit run.
    /// </summary>
    internal static class VoxelShowcasePresentationDefaults
    {
        internal const string SceneName = "VoxelShowcase";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Enter-play-mode without a domain reload can preserve static event subscriptions.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) =>
            RestoreForScene(scene.name);

        internal static void RestoreForScene(string sceneName)
        {
            if (sceneName == SceneName)
                RenderingComposition.SetWaterRenderEnabled(true);
        }
    }
}
