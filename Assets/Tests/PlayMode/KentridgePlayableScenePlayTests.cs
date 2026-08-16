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
    /// Scene-level acceptance for the actual player launch path. Unlike the lower-level pub-exit
    /// collision test, this test loads the same scene a player launches, lets its real campaign
    /// runtime own the opening, waits for gameplay control to return, then drives the scene's real
    /// character motor through the generated pub doorway and observes the scene report Kentridge.
    /// </summary>
    public sealed class KentridgePlayableScenePlayTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const float DecimetresToMetres = 0.1f;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator LaunchScene_NewGameCutscene_ReleasesControl_AndPlayerWalksOutIntoKentridge()
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
            Assert.That(ReadBoolProperty(driver, "GameplayControlEnabled"), Is.False,
                "The opening cutscene must own player control when the playable scene begins.");
            Assert.That(ReadBoolProperty(driver, "HasExitedPub"), Is.False,
                "The player must begin inside the pub, not already in Kentridge town.");

            // Advance actual Unity frames through the authored opening. Time scale shortens wall-clock
            // test duration only; the scene still executes its normal Update -> actor tick -> story
            // runtime tick path on every frame.
            Time.timeScale = 20f;
            for (var frame = 0; frame < 240 && !ReadBoolProperty(driver, "GameplayControlEnabled"); frame++)
                yield return null;
            Time.timeScale = 1f;

            Assert.That(ReadBoolProperty(driver, "GameplayControlEnabled"), Is.True,
                "The actual launch scene never returned gameplay control after the opening cutscene.");

            CharacterMotor motor = ReadPrivateField<CharacterMotor>(driver, "_motor");
            ShowcaseWorld world = ReadPrivateField<ShowcaseWorld>(driver, "_world");
            object pubAccess = ReadPrivateField<object>(driver, "_pubAccess");

            Vector3 entrance = ReadRealizedPoint(pubAccess, "Entrance");
            Vector3 exteriorTarget = ReadRealizedPoint(pubAccess, "ExteriorApproach");
            Vector3 inward = ReadInt2Direction(pubAccess, "Inward");

            float initialDepth = Vector3.Dot(motor.Position - entrance, inward);
            Assert.That(initialDepth, Is.GreaterThan(0.5f),
                "When gameplay control returns, the actual scene player must still be physically inside the generated pub.");

            // Feed movement to the exact CharacterMotor owned by the loaded scene. The production
            // scene continues running each frame, including streaming and its normal exit detection.
            // No teleport or semantic location mutation is used: success requires voxel collision
            // to permit the physical crossing and the scene itself to observe the exterior position.
            for (var frame = 0; frame < 600 && !ReadBoolProperty(driver, "HasExitedPub"); frame++)
            {
                Vector3 delta = exteriorTarget - motor.Position;
                delta.y = 0f;
                Vector3 wish = delta.sqrMagnitude <= 1e-6f ? Vector3.zero : delta.normalized;
                motor.Step(world, wish, sprint: false, jumpHeld: false, dt: 1f / 60f);
                yield return null;
            }

            Assert.That(ReadBoolProperty(driver, "HasExitedPub"), Is.True,
                "The launched game scene did not allow the player to walk through the generated pub doorway into Kentridge.");

            float exteriorDepth = Vector3.Dot(motor.Position - entrance, inward);
            Assert.That(exteriorDepth, Is.LessThanOrEqualTo(-0.75f),
                "The player must finish on the Kentridge-town side of the generated pub entrance.");
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Time.timeScale = 1f;
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
            Assert.That(property, Is.Not.Null, "Playable scene driver is missing public property '" + name + "'.");
            return (bool)property.GetValue(driver);
        }

        private static T ReadPrivateField<T>(Component driver, string name)
        {
            FieldInfo field = driver.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
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

        private static Vector3 ReadInt2Direction(object owner, string propertyName)
        {
            object direction = ReadProperty(owner, propertyName);
            return new Vector3(
                ReadIntField(direction, "X"),
                0f,
                ReadIntField(direction, "Y")).normalized;
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
