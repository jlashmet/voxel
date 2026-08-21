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
        private const float WaypointToleranceMetres = 0.35f;
        private const int MaxWalkFramesPerLeg = 600;
        private const float WalkDeltaTime = 1f / 60f;

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

            // Batch-mode frame rate is intentionally uncapped, so a frame count alone is not a
            // deterministic amount of game time. Force each rendered test frame to advance 100 ms;
            // the scene still executes its normal Update -> actor tick -> story runtime tick path.
            Time.captureDeltaTime = 0.1f;
            for (var frame = 0; frame < 240 && !ReadBoolProperty(driver, "GameplayControlEnabled"); frame++)
                yield return null;
            Time.captureDeltaTime = 0f;

            Assert.That(ReadBoolProperty(driver, "GameplayControlEnabled"), Is.True,
                "The actual launch scene never returned gameplay control after the opening cutscene.");

            CharacterMotor motor = ReadPrivateField<CharacterMotor>(driver, "_motor");
            ShowcaseWorld world = ReadPrivateField<ShowcaseWorld>(driver, "_world");
            object pubAccess = ReadPrivateField<object>(driver, "_pubAccess");

            Assert.That(ReadPrivateField<object>(driver, "_farTerrain"), Is.Not.Null,
                "The playable integration scene must install the far-terrain handoff.");
            Assert.That(ReadPrivateField<object>(driver, "_themes"), Is.Not.Null,
                "The playable integration scene must retain the generated region theme map.");
            Assert.That(ReadPrivateField<object>(driver, "_corridorPlan"), Is.Not.Null,
                "The playable integration scene must retain the generated inter-town corridor plan.");
            Assert.That(ReadPrivateField<object>(driver, "_hightownPlan"), Is.Not.Null,
                "The playable integration scene must compose Hightown with Kentridge.");
            Assert.That(ReadPrivateField<object>(driver, "_actors"), Is.Not.Null,
                "The playable integration scene must install the campaign actor host.");

            object life = ReadPrivateField<object>(driver, "_life");
            Assert.That(life, Is.Not.Null,
                "The playable integration scene must realize vegetation and ambient life.");
            Assert.That(ReadIntProperty(life, "TreeCount"), Is.LessThanOrEqualTo(900));
            Assert.That(ReadIntProperty(life, "UndergrowthCount"), Is.LessThanOrEqualTo(12000));
            Assert.That(ReadIntProperty(life, "ClusterCount"), Is.LessThanOrEqualTo(110));

            Vector3 entrance = ReadRealizedPoint(pubAccess, "Entrance");
            Vector3 interiorApproach = ReadRealizedPoint(pubAccess, "InteriorApproach");
            Vector3 exteriorTarget = ReadRealizedPoint(pubAccess, "ExteriorApproach");
            Vector3 inward = ReadInt2Direction(pubAccess, "Inward");

            Assert.That(HorizontalDistance(motor.Position, interiorApproach),
                Is.LessThanOrEqualTo(0.05f),
                "Gameplay control must be handed back on the architecture-owned interior " +
                "approach, not teleported across the generated doorway. " +
                PositionDiagnostic(motor.Position, interiorApproach, entrance, inward));

            float initialDepth = Vector3.Dot(motor.Position - entrance, inward);
            Assert.That(initialDepth, Is.GreaterThan(0.5f),
                "When gameplay control returns, the player must still be physically inside the generated pub.");
            Assert.That(ReadBoolProperty(driver, "HasExitedPub"), Is.False,
                "The scene must not report Kentridge town before the player crosses the public entrance.");

            // This is the integration invariant: the production scene-owned CharacterMotor, not a
            // teleport or semantic location mutation, must physically cross the generated doorway.
            Time.captureDeltaTime = WalkDeltaTime;
            yield return WalkMotorTo(
                motor,
                world,
                exteriorTarget,
                "generated pub exterior approach through the public doorway");
            Time.captureDeltaTime = 0f;

            float exteriorDepth = Vector3.Dot(motor.Position - entrance, inward);
            Assert.That(exteriorDepth, Is.LessThanOrEqualTo(-0.75f),
                "The production motor did not cross onto the town side of the generated pub entrance. " +
                PositionDiagnostic(motor.Position, exteriorTarget, entrance, inward));
            Assert.That(ReadBoolProperty(driver, "HasExitedPub"), Is.True,
                "The launch scene must report Kentridge only after physical doorway traversal.");

            // Prove the player remains free after the seam itself is crossed.
            Vector3 freeMovementTarget = exteriorTarget - inward * 2f;
            Time.captureDeltaTime = WalkDeltaTime;
            yield return WalkMotorTo(
                motor,
                world,
                freeMovementTarget,
                "free town-side movement after crossing the generated pub doorway");
            Time.captureDeltaTime = 0f;

            Vector3 beforeRescue = motor.Position;
            MethodInfo rescue = driver.GetType().GetMethod(
                "RescuePlayerToY100",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(rescue, Is.Not.Null,
                "The playable slice must expose its player-facing Y=100 rescue action.");
            rescue.Invoke(driver, null);
            Assert.That(motor.Position.x, Is.EqualTo(beforeRescue.x).Within(0.001f));
            Assert.That(motor.Position.z, Is.EqualTo(beforeRescue.z).Within(0.001f));
            Assert.That(motor.Position.y, Is.EqualTo(100f).Within(0.001f),
                "The rescue action must set the player's feet to world Y=100 exactly.");
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

        private static IEnumerator WalkMotorTo(
            CharacterMotor motor,
            ShowcaseWorld world,
            Vector3 target,
            string waypointName)
        {
            Vector3 start = motor.Position;
            var frame = 0;
            for (; frame < MaxWalkFramesPerLeg; frame++)
            {
                float remaining = HorizontalDistance(motor.Position, target);
                if (remaining <= WaypointToleranceMetres) break;
                StepToward(motor, world, target);
                yield return null;
            }

            float finalDistance = HorizontalDistance(motor.Position, target);
            Assert.That(finalDistance, Is.LessThanOrEqualTo(WaypointToleranceMetres),
                "Player could not physically reach " + waypointName +
                ". start=" + FormatVector(start) +
                ", final=" + FormatVector(motor.Position) +
                ", target=" + FormatVector(target) +
                ", remainingHorizontalMetres=" + finalDistance.ToString("F3") +
                ", frames=" + frame + ".");
        }

        private static void StepToward(CharacterMotor motor, ShowcaseWorld world, Vector3 target)
        {
            Vector3 delta = target - motor.Position;
            delta.y = 0f;
            Vector3 wish = delta.sqrMagnitude <= 1e-6f ? Vector3.zero : delta.normalized;
            motor.Step(world, wish, sprint: false, jumpHeld: false, dt: WalkDeltaTime);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static string PositionDiagnostic(
            Vector3 position,
            Vector3 target,
            Vector3 entrance,
            Vector3 inward)
        {
            float signedDepth = Vector3.Dot(position - entrance, inward);
            return "position=" + FormatVector(position) +
                   ", target=" + FormatVector(target) +
                   ", remainingHorizontalMetres=" + HorizontalDistance(position, target).ToString("F3") +
                   ", signedEntranceDepthMetres=" + signedDepth.ToString("F3") + ".";
        }

        private static string FormatVector(Vector3 value) =>
            "(" + value.x.ToString("F3") + ", " + value.y.ToString("F3") + ", " + value.z.ToString("F3") + ")";

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

        private static int ReadIntProperty(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + " is missing property '" + name + "'.");
            return (int)property.GetValue(owner);
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
