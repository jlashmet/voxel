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
    /// Player-facing acceptance for the Kentridge opening presentation treatment. The generated
    /// pub remains the real scene; this proves the camera looks down into it through a temporary
    /// roof cutaway and that the cutscene fades both in and out around the gameplay handoff.
    /// </summary>
    public sealed class KentridgeOpeningPresentationTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const string PresentationTypeName = "Game.Kentridge.PlayableSlice.KentridgeOpeningPresentation";
        private const string FadeOutCue = "kentridge.pub.opening.fade-out";

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator OpeningUsesOverheadRoofCutawayAndFadesAroundHandoff()
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

            Component driver = FindComponent(_loadedScene, DriverTypeName);
            Component presentation = FindComponent(_loadedScene, PresentationTypeName);
            Assert.That(driver, Is.Not.Null,
                "The production Kentridge playable driver must own this acceptance.");
            Assert.That(presentation, Is.Not.Null,
                "The production scene must include its opening presentation treatment.");

            for (int frame = 0; frame < 1200 && !ReadBoolProperty(driver, "OpeningCutsceneStarted"); frame++)
                yield return null;

            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneStarted"), Is.True,
                "The opening never started after the generated Pub became presentation-ready.");
            yield return null; // let LateUpdate apply the presentation camera once

            Camera camera = driver.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null);
            Assert.That(ReadBoolProperty(presentation, "OpeningOverheadActive"), Is.True,
                "The opening must replace the former interior-height camera with the overhead pub shot.");
            Assert.That(ReadBoolProperty(presentation, "RoofCutawayActive"), Is.True,
                "The overhead opening must remove the generated roof/upper slab presentation-only.");

            Vector3 focus = ReadVector3Property(driver, "OpeningCutsceneCameraFocus");
            Assert.That(camera.transform.position.y - focus.y, Is.GreaterThan(5f),
                "The opening camera should sit clearly above the pub rather than inside its upper floor.");
            Assert.That(Vector3.Dot(camera.transform.forward, Vector3.down), Is.GreaterThan(0.75f),
                "The opening camera should read as a top-down view with only a modest oblique offset.");
            Assert.That(camera.nearClipPlane, Is.GreaterThan(1f),
                "The opening roof cutaway must advance the near plane beyond normal gameplay clipping.");

            // Keep cutscene and fade timing on the same real player clock. Accelerating only
            // Time.deltaTime makes authored waits finish faster than the presentation fade, which
            // can manufacture a handoff failure that cannot occur at normal gameplay timeScale.
            for (int frame = 0; frame < 120 && ReadFloatProperty(presentation, "FadeAlpha") > 0.01f; frame++)
                yield return null;

            Assert.That(ReadFloatProperty(presentation, "FadeAlpha"), Is.LessThanOrEqualTo(0.01f),
                "The opening must fade in from black before the conversation remains visible.");

            int dismissedLines = 0;
            bool sawFadeOutCue = false;
            for (int frame = 0; frame < 1800 && ReadBoolProperty(driver, "OpeningCutsceneCameraActive"); frame++)
            {
                object pending = ReadPendingDialogue(driver);
                if (pending != null)
                {
                    DismissPendingDialogue(driver);
                    dismissedLines++;
                }

                yield return null;

                string cue = ReadStringProperty(presentation, "LastObservedCue");
                if (string.Equals(cue, FadeOutCue, StringComparison.Ordinal))
                {
                    sawFadeOutCue = true;
                    break;
                }
            }

            Assert.That(dismissedLines, Is.EqualTo(31),
                "The presentation transition must occur only after all recovered opening dialogue is dismissed.");
            Assert.That(sawFadeOutCue, Is.True,
                "The recovered opening never reached its explicit closing presentation cue.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.True,
                "The closing fade must begin while the cutscene still owns the overhead pub camera.");

            for (int frame = 0; frame < 120
                && ReadFloatProperty(presentation, "FadeAlpha") < 0.99f; frame++)
                yield return null;

            Assert.That(ReadFloatProperty(presentation, "FadeAlpha"), Is.GreaterThanOrEqualTo(0.99f),
                "The cutscene must reach full black before releasing its camera.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.True,
                "The authored closing hold must keep the cutscene active until fade-out completes.");

            for (int frame = 0; frame < 120 && ReadBoolProperty(driver, "OpeningCutsceneCameraActive"); frame++)
                yield return null;

            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.False,
                "The opening did not release to gameplay after its closing fade hold.");
            Assert.That(ReadBoolProperty(presentation, "RoofCutawayActive"), Is.False,
                "The roof cutaway must be removed as soon as gameplay regains camera ownership.");
            Assert.That(camera.nearClipPlane, Is.EqualTo(0.05f).Within(0.001f),
                "Gameplay must recover the scene camera's normal near clip plane.");

            for (int frame = 0; frame < 120 && ReadFloatProperty(presentation, "FadeAlpha") > 0.01f; frame++)
                yield return null;

            Assert.That(ReadFloatProperty(presentation, "FadeAlpha"), Is.LessThanOrEqualTo(0.01f),
                "Gameplay should fade back in after the cutscene has faded to black.");
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

        private static object ReadPendingDialogue(Component driver)
        {
            object slicePresentation = ReadPrivateField<object>(driver, "_presentation");
            if (slicePresentation == null) return null;
            PropertyInfo property = slicePresentation.GetType().GetProperty(
                "Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "Kentridge presentation must expose its pending dialogue operation.");
            return property.GetValue(slicePresentation);
        }

        private static void DismissPendingDialogue(Component driver)
        {
            object slicePresentation = ReadPrivateField<object>(driver, "_presentation");
            MethodInfo dismiss = slicePresentation.GetType().GetMethod(
                "DismissPending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(dismiss, Is.Not.Null,
                "Kentridge presentation must expose dialogue dismissal.");
            dismiss.Invoke(slicePresentation, null);
        }

        private static Component FindComponent(Scene scene, string fullTypeName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour != null && string.Equals(
                            behaviour.GetType().FullName,
                            fullTypeName,
                            StringComparison.Ordinal))
                        return behaviour;
                }
            }
            return null;
        }

        private static bool ReadBoolProperty(object owner, string name) =>
            ReadProperty<bool>(owner, name);

        private static float ReadFloatProperty(object owner, string name) =>
            ReadProperty<float>(owner, name);

        private static string ReadStringProperty(object owner, string name) =>
            ReadProperty<string>(owner, name);

        private static Vector3 ReadVector3Property(object owner, string name) =>
            ReadProperty<Vector3>(owner, name);

        private static T ReadProperty<T>(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + " is missing property '" + name + "'.");
            return (T)property.GetValue(owner);
        }

        private static T ReadPrivateField<T>(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                owner.GetType().FullName + " is missing runtime field '" + name + "'.");
            return (T)field.GetValue(owner);
        }
    }
}
