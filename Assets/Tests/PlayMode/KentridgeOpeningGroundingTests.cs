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
    /// Regression for SceneIssues/20260826-140058-860-KentridgePlayableSlice.
    /// The captured opening showed imported humanoid feet below the generated pub stage plane.
    /// Actor root positions are semantic stage coordinates, so visible renderer bottoms must meet
    /// those roots without changing story/collision placement.
    /// </summary>
    public sealed class KentridgeOpeningGroundingTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const float FootPlaneToleranceMetres = 0.025f;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator InitialOpeningCastRendererFeetRestOnSemanticStagePlane()
        {
            _previousActiveScene = SceneManager.GetActiveScene();

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "The captured Kentridge scene must be loadable from build settings.");
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True,
                "The captured Kentridge playable scene failed to load.");
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);
            yield return null;

            Component driver = FindDriver(_loadedScene);
            Assert.That(driver, Is.Not.Null,
                "The production Kentridge driver must own the captured opening acceptance.");
            Assert.That(driver.GetComponent("KentridgeCutsceneFootGrounding"), Is.Not.Null,
                "The playable scene must own its Kentridge-only visual foot grounding adapter.");

            for (var frame = 0; frame < 1200 && !ReadBoolProperty(driver, "OpeningCutsceneStarted"); frame++)
                yield return null;
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneStarted"), Is.True,
                "The opening never started after the generated Pub became presentation-ready.");

            Time.captureDeltaTime = 0.1f;
            for (var frame = 0; frame < 120 && !HasPendingDialogue(driver); frame++)
                yield return null;
            Assert.That(HasPendingDialogue(driver), Is.True,
                "The production opening never reached the first captured dialogue beat.");

            // Allow the Kentridge-only late presentation pass to normalize every active cast body.
            yield return null;

            AssertFootContact(FindSceneRoot("Weldon"), "Weldon");
            AssertFootContact(FindSceneRoot("Madeline"), "Madeline");
            AssertFootContact(FindSceneRoot("Steven"), "Steven");
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

        private void AssertFootContact(GameObject root, string actor)
        {
            Assert.That(root, Is.Not.Null, actor + " must have a realized opening body.");
            Assert.That(root.activeInHierarchy, Is.True, actor + " must be visible at the captured opening beat.");

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            Bounds bounds = default;
            for (var i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Assert.That(found, Is.True, actor + " must have enabled visible renderer bounds.");
            float delta = bounds.min.y - root.transform.position.y;
            Debug.Log(
                "KENTRIDGE_FOOT_CONTACT actor=" + actor
                + " stageY=" + root.transform.position.y.ToString("F3")
                + " rendererMinY=" + bounds.min.y.ToString("F3")
                + " delta=" + delta.ToString("F3"));
            Assert.That(Mathf.Abs(delta), Is.LessThanOrEqualTo(FootPlaneToleranceMetres),
                actor + " visible soles must rest on the architecture-realized stage plane instead of passing through it.");
        }

        private GameObject FindSceneRoot(string name)
        {
            GameObject[] roots = _loadedScene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (string.Equals(roots[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return roots[i];
            }
            return null;
        }

        private static Component FindDriver(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                Component[] components = roots[i].GetComponentsInChildren<Component>(true);
                for (var j = 0; j < components.Length; j++)
                {
                    Component component = components[j];
                    if (component != null
                        && string.Equals(component.GetType().FullName, DriverTypeName, StringComparison.Ordinal))
                        return component;
                }
            }
            return null;
        }

        private static bool ReadBoolProperty(Component target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, "Missing Kentridge driver property " + propertyName + ".");
            return (bool)property.GetValue(target);
        }

        private static bool HasPendingDialogue(Component driver)
        {
            FieldInfo presentationField = driver.GetType().GetField(
                "_presentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presentationField, Is.Not.Null, "Kentridge driver must retain its production presentation.");
            object presentation = presentationField.GetValue(driver);
            if (presentation == null) return false;

            PropertyInfo pending = presentation.GetType().GetProperty(
                "Pending",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(pending, Is.Not.Null, "Kentridge presentation must expose pending dialogue.");
            return pending.GetValue(presentation) != null;
        }
    }
}
