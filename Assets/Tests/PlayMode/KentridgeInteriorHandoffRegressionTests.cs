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
    /// Regression for the captured opening handoff that placed the player on the pub roof while
    /// preserving the authored interior X/Z. This uses the production scene and generated pub so
    /// stacked floor/roof occupancy participates in the same grounding query as real gameplay.
    /// </summary>
    public sealed class KentridgeInteriorHandoffRegressionTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const float DecimetresToMetres = 0.1f;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator OpeningRelease_StaysAtAuthoredInteriorElevationUnderPubRoof()
        {
            _previousActiveScene = SceneManager.GetActiveScene();

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "The Kentridge playable scene must be loadable.");
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True,
                "The Kentridge playable scene failed to load.");
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);
            yield return null;

            Component driver = FindDriver(_loadedScene);
            Assert.That(driver, Is.Not.Null,
                "The production KentridgePlayableSlice driver must own the handoff regression.");

            CharacterMotor motor = ReadPrivateField<CharacterMotor>(driver, "_motor");
            object pubAccess = ReadPrivateField<object>(driver, "_pubAccess");
            Vector3 interiorApproach = ReadRealizedPoint(pubAccess, "InteriorApproach");

            MethodInfo release = driver.GetType().GetMethod(
                "ReleasePlayerForGameplay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(release, Is.Not.Null,
                "The production Kentridge driver is missing its opening gameplay handoff.");
            release.Invoke(driver, null);

            Vector3 handedOff = motor.Position;
            Assert.That(Mathf.Abs(handedOff.x - interiorApproach.x), Is.LessThanOrEqualTo(0.05f),
                "The opening handoff must preserve the architecture-owned interior X coordinate.");
            Assert.That(Mathf.Abs(handedOff.z - interiorApproach.z), Is.LessThanOrEqualTo(0.05f),
                "The opening handoff must preserve the architecture-owned interior Z coordinate.");
            Assert.That(Mathf.Abs(handedOff.y - interiorApproach.y), Is.LessThanOrEqualTo(0.5f),
                "The opening handoff must remain at the authored interior elevation instead of snapping to the pub roof.");
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

        private static Component FindDriver(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours = roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    MonoBehaviour behaviour = behaviours[i];
                    if (behaviour != null
                        && string.Equals(behaviour.GetType().FullName, DriverTypeName, StringComparison.Ordinal))
                        return behaviour;
                }
            }

            return null;
        }

        private static T ReadPrivateField<T>(Component driver, string name)
        {
            FieldInfo field = driver.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Playable scene driver is missing runtime field '" + name + "'.");
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
            PropertyInfo property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, owner.GetType().FullName + " is missing property '" + name + "'.");
            return property.GetValue(owner);
        }

        private static int ReadIntField(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, owner.GetType().FullName + " is missing field '" + name + "'.");
            return (int)field.GetValue(owner);
        }
    }
}
