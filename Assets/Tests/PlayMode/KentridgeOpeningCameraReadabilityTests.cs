using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Player-facing composition regression for the recovered Kentridge opening. Presence alone is
    /// insufficient: the fixed camera must keep the active conversation participants comfortably
    /// inside the viewport and large enough to read as characters in a real-player frame.
    /// </summary>
    public sealed class KentridgeOpeningCameraReadabilityTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const float MinimumBodyViewportHeight = 0.12f;
        private const float ViewportMargin = 0.04f;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator OpeningCameraKeepsConversationParticipantsReadable()
        {
            _previousActiveScene = SceneManager.GetActiveScene();

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "The Kentridge playable scene must be loadable from build settings.");
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True,
                "The Kentridge playable scene failed to load.");
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);
            yield return null;

            Component driver = FindDriver(_loadedScene);
            Assert.That(driver, Is.Not.Null, "The production Kentridge playable driver must own this acceptance.");

            for (var frame = 0; frame < 1200 && !ReadBoolProperty(driver, "OpeningCutsceneStarted"); frame++)
                yield return null;

            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneStarted"), Is.True,
                "The opening never started after the generated Pub became presentation-ready.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.True,
                "The recovered opening must use its fixed establishing camera.");

            Camera openingCamera = driver.GetComponent<Camera>();
            Assert.That(openingCamera, Is.Not.Null,
                "The production opening driver must share the camera whose transform it owns.");

            object madelineActor = FindNpcActor(driver, "madeline");
            object stevenActor = FindNpcActor(driver, "steven");
            object loganActor = FindNpcActor(driver, "logan");

            Time.captureDeltaTime = 0.1f;
            for (var frame = 0; frame < 100 && !HasPendingDialogue(driver); frame++)
                yield return null;

            Assert.That(HasPendingDialogue(driver), Is.True,
                "The production opening never reached recovered dialogue line 1.");

            AssertReadable(openingCamera, GameObject.Find("Weldon"), "Weldon at line 1");
            AssertReadable(openingCamera, ReadActorRoot(madelineActor), "Madeline at line 1");
            AssertReadable(openingCamera, ReadActorRoot(stevenActor), "Steven at line 1");

            int dismissedBeforeLogan = 0;
            for (var frame = 0; frame < 240; frame++)
            {
                object pending = ReadPendingDialogue(driver);
                if (pending != null)
                {
                    string speaker = ReadStringProperty(pending, "Speaker");
                    if (string.Equals(speaker, "Logan", StringComparison.OrdinalIgnoreCase))
                        break;

                    DismissPendingDialogue(driver);
                    dismissedBeforeLogan++;
                }

                yield return null;
            }

            object loganLine = ReadPendingDialogue(driver);
            Assert.That(loganLine, Is.Not.Null,
                "Logan's recovered line 11 never became pending after the preceding dialogue beats.");
            Assert.That(ReadStringProperty(loganLine, "Speaker"), Is.EqualTo("Logan").IgnoreCase,
                "The opening must stop on Logan's recovered line-11 entrance beat.");
            Assert.That(dismissedBeforeLogan, Is.EqualTo(10),
                "Exactly the recovered first ten dialogue beats must precede Logan's line 11.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.True,
                "The fixed ensemble camera must still own the view when Logan first speaks.");

            AssertReadable(openingCamera, GameObject.Find("Weldon"), "Weldon at Logan's entrance");
            AssertReadable(openingCamera, ReadActorRoot(madelineActor), "Madeline at Logan's entrance");
            AssertReadable(openingCamera, ReadActorRoot(stevenActor), "Steven at Logan's entrance");
            AssertReadable(openingCamera, ReadActorRoot(loganActor), "Logan at line 11");
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

        private static void AssertReadable(Camera camera, GameObject visualRoot, string participant)
        {
            Assert.That(visualRoot, Is.Not.Null, participant + " must have a realized visual body.");

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            bool foundBounds = false;
            Bounds bounds = default;
            for (var i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!foundBounds)
                {
                    bounds = renderer.bounds;
                    foundBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Assert.That(foundBounds, Is.True,
                participant + " must have at least one enabled renderer in the opening.");

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var corner = new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                Assert.That(viewport.z, Is.GreaterThan(0f),
                    participant + " renderer bounds must remain in front of the opening camera.");
                minX = Mathf.Min(minX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxX = Mathf.Max(maxX, viewport.x);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            float viewportHeight = maxY - minY;
            float viewportWidth = maxX - minX;
            Debug.Log(
                "KENTRIDGE_CAMERA_READABILITY participant=" + participant
                + " viewportHeight=" + viewportHeight.ToString("F3")
                + " viewportWidth=" + viewportWidth.ToString("F3")
                + " bounds=(" + minX.ToString("F3") + "," + minY.ToString("F3")
                + ")-(" + maxX.ToString("F3") + "," + maxY.ToString("F3") + ")");

            Assert.That(minX, Is.GreaterThanOrEqualTo(ViewportMargin),
                participant + " is clipped against the left edge of the fixed opening shot.");
            Assert.That(maxX, Is.LessThanOrEqualTo(1f - ViewportMargin),
                participant + " is clipped against the right edge of the fixed opening shot.");
            Assert.That(minY, Is.GreaterThanOrEqualTo(ViewportMargin),
                participant + " is clipped against the bottom edge of the fixed opening shot.");
            Assert.That(maxY, Is.LessThanOrEqualTo(1f - ViewportMargin),
                participant + " is clipped against the top edge of the fixed opening shot.");
            Assert.That(viewportHeight, Is.GreaterThanOrEqualTo(MinimumBodyViewportHeight),
                participant + " is technically visible but too small for the intended ensemble shot. "
                + "Rendered viewport height=" + viewportHeight.ToString("F3") + ".");
        }

        private static bool HasPendingDialogue(Component driver) =>
            ReadPendingDialogue(driver) != null;

        private static object ReadPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            PropertyInfo property = presentation.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Kentridge presentation must expose its pending dialogue operation.");
            return property.GetValue(presentation);
        }

        private static void DismissPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            MethodInfo dismiss = presentation.GetType().GetMethod("DismissPending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(dismiss, Is.Not.Null, "Kentridge presentation must expose dialogue dismissal.");
            dismiss.Invoke(presentation, null);
        }

        private static string ReadStringProperty(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, owner.GetType().FullName + " is missing property '" + name + "'.");
            return property.GetValue(owner) as string;
        }

        private static object FindNpcActor(Component driver, string nameFragment)
        {
            object actors = ReadPrivateField<object>(driver, "_actors");
            FieldInfo npcsField = actors.GetType().GetField("_npcs", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(npcsField, Is.Not.Null, "Actor host is missing its authoritative NPC registry.");
            var npcs = npcsField.GetValue(actors) as IDictionary;
            Assert.That(npcs, Is.Not.Null, "Actor host NPC registry must be enumerable for production acceptance.");

            foreach (DictionaryEntry entry in npcs)
            {
                if (entry.Key != null
                    && entry.Key.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return entry.Value;
            }

            Assert.Fail("Could not find opening NPC actor containing '" + nameFragment + "'.");
            return null;
        }

        private static GameObject ReadActorRoot(object actor)
        {
            FieldInfo rootField = actor.GetType().GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null, "Cutscene NPC actor is missing its visual root.");
            var root = rootField.GetValue(actor) as GameObject;
            Assert.That(root, Is.Not.Null, "Cutscene NPC visual root must exist while the opening is running.");
            return root;
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

        private static bool ReadBoolProperty(Component driver, string name)
        {
            PropertyInfo property = driver.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Playable scene driver is missing public property '" + name + "'.");
            return (bool)property.GetValue(driver);
        }

        private static T ReadPrivateField<T>(Component driver, string name)
        {
            FieldInfo field = driver.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Playable scene driver is missing runtime field '" + name + "'.");
            return (T)field.GetValue(driver);
        }
    }
}
