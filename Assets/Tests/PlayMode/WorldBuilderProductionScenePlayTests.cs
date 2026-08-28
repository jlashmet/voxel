using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Architecture-focused production-scene regression for WorldBuilder composition. This stops
    /// once the real Kentridge launch scene has published its generated environment; camera and
    /// story choreography belong to separate presentation acceptance tests.
    /// </summary>
    public sealed class WorldBuilderProductionScenePlayTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const float DecimetresToMetres = 0.1f;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator KentridgePlayableScene_PublishesWorldBuilderEnvironmentWithoutPresentationCoupling()
        {
            _previousActiveScene = SceneManager.GetActiveScene();

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null,
                "The production Kentridge scene must remain loadable from build settings.");
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True,
                "The production Kentridge scene failed to load.");
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);

            yield return null;

            Component driver = FindDriver(_loadedScene);
            Assert.That(driver, Is.Not.Null,
                "The production scene must contain the KentridgePlayableSlice bootstrap.");

            for (var frame = 0; frame < 1200
                && !ReadBoolProperty(driver, "OpeningPresentationReady"); frame++)
                yield return null;

            Assert.That(ReadBoolProperty(driver, "OpeningPresentationReady"), Is.True,
                "The WorldBuilder-authored Kentridge environment never reached published near-surface coverage.");

            ShowcaseWorld world = ReadPrivateField<ShowcaseWorld>(driver, "_world");
            Assert.That(world, Is.Not.Null,
                "The production bootstrap must realize the semantic environment into the shared ShowcaseWorld runtime.");
            Assert.That(ReadPrivateField<object>(driver, "_kentridgePlan"), Is.Not.Null,
                "The production scene must retain the WorldBuilder-authored Kentridge settlement plan.");
            Assert.That(ReadPrivateField<object>(driver, "_hightownPlan"), Is.Not.Null,
                "The production composition must retain its distinct Hightown settlement plan.");
            Assert.That(ReadPrivateField<object>(driver, "_themes"), Is.Not.Null,
                "The production composition must realize its shared region theme map.");
            Assert.That(ReadPrivateField<object>(driver, "_corridorPlan"), Is.Not.Null,
                "The production composition must realize the inter-settlement corridor plan.");
            Assert.That(ReadPrivateField<object>(driver, "_life"), Is.Not.Null,
                "Vegetation/ambient-life realization must be present in the production environment.");

            object pubAccess = ReadPrivateField<object>(driver, "_pubAccess");
            Vector3 entrance = ReadRealizedPoint(pubAccess, "Entrance");
            Vector3 interior = ReadRealizedPoint(pubAccess, "InteriorApproach");
            Vector3 exterior = ReadRealizedPoint(pubAccess, "ExteriorApproach");

            Assert.That(world.IsGenerated(ShowcaseWorld.RegionAt(entrance)), Is.True,
                "The generated pub entrance region must be resident before the environment is declared ready.");
            Assert.That(world.IsGenerated(ShowcaseWorld.RegionAt(interior)), Is.True,
                "The generated pub interior approach must be resident before presentation starts.");
            Assert.That(world.IsGenerated(ShowcaseWorld.RegionAt(exterior)), Is.True,
                "The generated pub exterior approach must be resident so the settlement is physically reachable.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
            PropertyInfo property = driver.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "Playable scene driver is missing public property '" + name + "'.");
            return (bool)property.GetValue(driver);
        }

        private static T ReadPrivateField<T>(Component driver, string name)
        {
            FieldInfo field = driver.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "Playable scene driver is missing runtime field '" + name + "'.");
            return (T)field.GetValue(driver);
        }

        private static Vector3 ReadRealizedPoint(object owner, string propertyName)
        {
            object point = ReadProperty(owner, propertyName);
            object position = ReadProperty(point, "Position");
            int unitsPerDecimetre = (int)ReadProperty(point, "UnitsPerDecimetre");
            float scale = DecimetresToMetres / unitsPerDecimetre;
            return new Vector3(
                ReadIntField(position, "X") * scale,
                ReadIntField(position, "Y") * scale,
                ReadIntField(position, "Z") * scale);
        }

        private static object ReadProperty(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + " is missing property '" + name + "'.");
            return property.GetValue(owner);
        }

        private static int ReadIntField(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null,
                owner.GetType().FullName + " is missing field '" + name + "'.");
            return (int)field.GetValue(owner);
        }
    }
}
