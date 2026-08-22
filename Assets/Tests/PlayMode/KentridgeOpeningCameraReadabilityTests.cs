using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

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
        private const float MinimumBodyViewportHeight = 0.075f;
        private const float ViewportMargin = 0.04f;
        private const float ApproximateBodyHeightMetres = 1.7f;

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

            CharacterMotor motor = ReadPrivateField<CharacterMotor>(driver, "_motor");
            object madelineActor = FindNpcActor(driver, "madeline");
            object stevenActor = FindNpcActor(driver, "steven");
            object loganActor = FindNpcActor(driver, "logan");

            Time.captureDeltaTime = 0.1f;
            for (var frame = 0; frame < 100 && !HasPendingDialogue(driver); frame++)
                yield return null;

            Assert.That(HasPendingDialogue(driver), Is.True,
                "The production opening never reached recovered dialogue line 1.");

            AssertReadable(openingCamera, motor.Position, "Weldon at line 1");
            AssertReadable(openingCamera, ReadActorRootPosition(madelineActor), "Madeline at line 1");
            AssertReadable(openingCamera, ReadActorRootPosition(stevenActor), "Steven at line 1");

            var dismissed = 0;
            for (var frame = 0; frame < 160 && dismissed < 10; frame++)
            {
                if (DismissPendingDialogue(driver))
                    dismissed++;
                yield return null;
            }

            Assert.That(dismissed, Is.EqualTo(10),
                "The acceptance must reach Logan's recovered line-11 entrance beat.");

            for (var frame = 0; frame < 40 && !HasPendingDialogue(driver); frame++)
                yield return null;

            Assert.That(HasPendingDialogue(driver), Is.True,
                "Logan's recovered line 11 never became pending after the first ten beats.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.True,
                "The fixed ensemble camera must still own the view when Logan first speaks.");

            AssertReadable(openingCamera, motor.Position, "Weldon at Logan's entrance");
            AssertReadable(openingCamera, ReadActorRootPosition(madelineActor), "Madeline at Logan's entrance");
            AssertReadable(openingCamera, ReadActorRootPosition(stevenActor), "Steven at Logan's entrance");
            AssertReadable(openingCamera, ReadActorRootPosition(loganActor), "Logan at line 11");
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

        private static void AssertReadable(Camera camera, Vector3 rootPosition, string participant)
        {
            Vector3 feet = camera.WorldToViewportPoint(rootPosition);
            Vector3 head = camera.WorldToViewportPoint(rootPosition + Vector3.up * ApproximateBodyHeightMetres);

            Assert.That(feet.z, Is.GreaterThan(0f), participant + " must remain in front of the opening camera.");
            Assert.That(feet.x, Is.InRange(ViewportMargin, 1f - ViewportMargin),
                participant + " must remain comfortably inside the horizontal frame.");
            Assert.That(feet.y, Is.InRange(ViewportMargin, 1f - ViewportMargin),
                participant + " must remain comfortably inside the vertical frame.");
            Assert.That(head.x, Is.InRange(ViewportMargin, 1f - ViewportMargin),
                participant + " head must remain comfortably inside the horizontal frame.");
            Assert.That(head.y, Is.InRange(ViewportMargin, 1f - ViewportMargin),
                participant + " head must remain comfortably inside the vertical frame.");

            float bodyViewportHeight = Mathf.Abs(head.y - feet.y);
            Assert.That(bodyViewportHeight, Is.GreaterThanOrEqualTo(MinimumBodyViewportHeight),
                participant + " is technically visible but too small for the intended ensemble shot. " +
                "Approximate body viewport height=" + bodyViewportHeight.ToString("F3") + ".");
        }

        private static bool HasPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            PropertyInfo property = presentation.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Kentridge presentation must expose its pending dialogue operation.");
            return property.GetValue(presentation) != null;
        }

        private static bool DismissPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            PropertyInfo property = presentation.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Kentridge presentation must expose its pending dialogue operation.");
            if (property.GetValue(presentation) == null) return false;

            MethodInfo dismiss = presentation.GetType().GetMethod("DismissPending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(dismiss, Is.Not.Null, "Kentridge presentation must expose dialogue dismissal.");
            dismiss.Invoke(presentation, null);
            return true;
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

        private static Vector3 ReadActorRootPosition(object actor)
        {
            FieldInfo rootField = actor.GetType().GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null, "Cutscene NPC actor is missing its visual root.");
            var root = rootField.GetValue(actor) as GameObject;
            Assert.That(root, Is.Not.Null, "Cutscene NPC visual root must exist while the opening is running.");
            return root.transform.position;
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
