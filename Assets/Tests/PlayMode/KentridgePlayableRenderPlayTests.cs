using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Presentation-path smoke test for the actual Kentridge launch scene. The ordinary scene
    /// acceptance advances gameplay and streaming, but a batch-mode frame is not evidence that the
    /// scene camera submitted the voxel presentation. Explicit offscreen renders keep the real URP
    /// camera and RenderingComposition active while the opening and streaming update.
    /// </summary>
    public sealed class KentridgePlayableRenderPlayTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const int Width = 640;
        private const int Height = 360;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator LaunchScene_RendersWhileOpeningAndStreaming_WithoutErrors()
        {
            _previousActiveScene = SceneManager.GetActiveScene();

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "The Kentridge playable launch scene must be loadable from build settings.");
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True,
                "The Kentridge playable launch scene failed to load.");
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);

            yield return null;

            Camera camera = FindSceneCamera(_loadedScene);
            Assert.That(camera, Is.Not.Null,
                "The playable Kentridge scene must own an enabled camera for the player presentation.");

            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            Assert.That(target.Create(), Is.True, "Failed to create the Kentridge offscreen render target.");

            try
            {
                camera.targetTexture = target;

                // The production opening lasts several seconds. Advance deterministic game time while
                // explicitly submitting the actual camera each frame so surface extraction, uploads,
                // streaming, and presentation overlap as they do in an interactive Game view.
                Time.captureDeltaTime = 0.1f;
                for (var frame = 0; frame < 240; frame++)
                {
                    camera.Render();
                    yield return null;
                }

                // Continue at a gameplay-like step after the opening has released control. This catches
                // presentation/job lifetime failures that only appear after initial generation settles.
                Time.captureDeltaTime = 1f / 60f;
                for (var frame = 0; frame < 180; frame++)
                {
                    camera.Render();
                    yield return null;
                }
            }
            finally
            {
                Time.captureDeltaTime = 0f;
                if (camera != null) camera.targetTexture = previousTarget;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(_previousActiveScene);

            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(_loadedScene);
                if (unload != null)
                    while (!unload.isDone) yield return null;
            }

            _loadedScene = default;
            _previousActiveScene = default;
        }

        private static Camera FindSceneCamera(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Camera[] cameras = roots[rootIndex].GetComponentsInChildren<Camera>(true);
                for (var i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                        return camera;
                }
            }
            return null;
        }
    }
}
