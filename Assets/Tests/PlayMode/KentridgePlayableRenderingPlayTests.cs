using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Rendering regression for the real player launch scene. The ordinary scene acceptance proves
    /// campaign progression and collision, but batch-mode PlayMode does not guarantee that the Game
    /// view camera is actually submitted. Force the production camera through the same render path
    /// used by the visual capture tests while Kentridge generation and streaming are active.
    /// </summary>
    public sealed class KentridgePlayableRenderingPlayTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const int Width = 640;
        private const int Height = 360;

        private Scene _loadedScene;
        private Scene _previousActiveScene;
        private Camera _camera;
        private RenderTexture _renderTarget;
        private RenderTexture _previousTarget;

        [UnityTest]
        public IEnumerator LaunchScene_ProductionCameraRendersWhileKentridgeStreamsWithoutErrors()
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

            Component driver = FindDriver(_loadedScene);
            Assert.That(driver, Is.Not.Null,
                "The launch scene must contain the production KentridgePlayableSlice driver.");

            _camera = FindCamera(_loadedScene);
            Assert.That(_camera, Is.Not.Null,
                "The launch scene must contain its production player camera.");
            Assert.That(_camera.enabled, Is.True,
                "The Kentridge production player camera must be enabled.");

            _renderTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "KentridgePlayableRenderingPlayTests"
            };
            Assert.That(_renderTarget.Create(), Is.True,
                "The Kentridge render regression could not create its offscreen target.");
            _previousTarget = _camera.targetTexture;
            _camera.targetTexture = _renderTarget;

            // Advance deterministic game time while explicitly submitting the real camera. This is
            // the path the previous acceptance missed: generation/streaming Update work interleaved
            // with the renderer's presentation callbacks and geometry jobs.
            Time.captureDeltaTime = 0.05f;
            var sawGameplayControl = false;
            for (var frame = 0; frame < 240; frame++)
            {
                yield return null;
                _camera.Render();
                sawGameplayControl |= ReadBoolProperty(driver, "GameplayControlEnabled");
            }
            Time.captureDeltaTime = 0f;

            Assert.That(sawGameplayControl, Is.True,
                "The forced-render regression never reached gameplay control after the opening cutscene.");
            Assert.That(_renderTarget.IsCreated(), Is.True,
                "The production camera lost its render target while Kentridge was streaming.");
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_camera != null)
                _camera.targetTexture = _previousTarget;

            if (_renderTarget != null)
            {
                _renderTarget.Release();
                UnityEngine.Object.DestroyImmediate(_renderTarget);
            }

            _camera = null;
            _renderTarget = null;
            _previousTarget = null;

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

        private static Component FindDriver(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (var i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour != null && string.Equals(
                            behaviour.GetType().FullName,
                            DriverTypeName,
                            StringComparison.Ordinal))
                        return behaviour;
                }
            }

            return null;
        }

        private static Camera FindCamera(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                Camera camera = roots[i].GetComponentInChildren<Camera>(true);
                if (camera != null) return camera;
            }

            return null;
        }

        private static bool ReadBoolProperty(Component driver, string name)
        {
            PropertyInfo property = driver.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Playable scene driver is missing public property '" + name + "'.");
            return (bool)property.GetValue(driver);
        }
    }
}
