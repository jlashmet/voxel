using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Composes the dedicated Multiplayer scene without putting networking UI or transport state
    /// back into every showcase. The scene is intentionally presentation-only on disk, so its
    /// playable showcase driver is attached at runtime and then opts into multiplayer explicitly.
    /// </summary>
    public static class MultiplayerSceneBootstrap
    {
        internal const string SceneName = "Multiplayer";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (!string.Equals(scene.name, SceneName, StringComparison.Ordinal)) return;

            VoxelShowcase showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
            if (showcase == null)
            {
                Camera camera = Camera.main;
                if (camera == null)
                {
                    Debug.LogError("Multiplayer scene requires a MainCamera for the showcase driver.");
                    return;
                }

                showcase = camera.gameObject.AddComponent<VoxelShowcase>();
            }

            showcase.EnableMultiplayer();
        }
    }
}
