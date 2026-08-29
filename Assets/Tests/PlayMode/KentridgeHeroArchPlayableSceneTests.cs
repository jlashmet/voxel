using System;
using System.Collections;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Behavioral acceptance for the reopened ArchLookdev integration issue. This deliberately loads
    /// the same player scene a user launches, resolves a landmark through the production settlement
    /// plan/public-access path, and then drives the Game-owned player host through that generated
    /// entrance. Catalogue primitive counts alone cannot satisfy this test.
    /// </summary>
    public sealed class KentridgeHeroArchPlayableSceneTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const float WalkDeltaTime = 1f / 60f;
        private const float WaypointToleranceMetres = 0.4f;
        private const int MaxWalkFramesPerLeg = 720;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator GeneratedWarehouseHeroArch_IsReachableThroughProductionPlayerHost()
        {
            _previousActiveScene = SceneManager.GetActiveScene();
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True,
                "The exact Kentridge playable scene must load for hero-arch acceptance.");
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);
            yield return null;

            Component driver = FindDriver(_loadedScene);
            Assert.That(driver, Is.Not.Null,
                "KentridgePlayableSlice must own the production generated-world runtime.");

            Time.captureDeltaTime = 0.1f;
            for (int frame = 0; frame < 300 && !ReadBoolProperty(driver, "GameplayControlEnabled"); frame++)
            {
                DismissPendingDialogue(driver);
                yield return null;
            }
            Time.captureDeltaTime = 0f;
            Assert.That(ReadBoolProperty(driver, "GameplayControlEnabled"), Is.True,
                "The normal opening flow must return gameplay control before landmark traversal.");

            SettlementPlan plan = ReadPrivateField<SettlementPlan>(driver, "_kentridgePlan");
            ShowcaseWorld world = ReadPrivateField<ShowcaseWorld>(driver, "_world");
            KentridgeCharacterHost motor = ReadPrivateField<KentridgeCharacterHost>(driver, "_motor");
            Assert.That(plan, Is.Not.Null);
            Assert.That(world, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);

            Assert.That(KentridgeGameplaySiteAccessResolver.TryResolve(
                    plan,
                    (int)KentridgeRole.Warehouse,
                    1,
                    out KentridgeGameplaySiteAccess access),
                Is.True,
                "The generated landmark warehouse must connect its hero entrance to public circulation.");

            Vector3 exterior = ToMetres(access.ExteriorApproach);
            Vector3 entrance = ToMetres(access.Entrance);
            Vector3 interior = ToMetres(access.InteriorApproach);

            // Relocate only to bound test duration. From here onward the production motor must do the
            // actual approach and doorway crossing against the exact scene's generated voxel world.
            motor.Position = exterior;
            motor.Velocity = Vector3.zero;
            for (int frame = 0; frame < 180; frame++) yield return null;
            motor.SnapToGround(world, exterior);

            Time.captureDeltaTime = WalkDeltaTime;
            yield return WalkMotorTo(motor, world, entrance, "warehouse hero-arch threshold");
            yield return WalkMotorTo(motor, world, interior, "warehouse interior through hero arch");
            Time.captureDeltaTime = 0f;

            Vector3 inward = new Vector3(access.Inward.X, 0f, access.Inward.Y).normalized;
            float signedDepth = Vector3.Dot(motor.Position - entrance, inward);
            Assert.That(signedDepth, Is.GreaterThan(0.75f),
                "The visible hero treatment must remain non-destructive: the production player body " +
                "must clear the landmark opening and reach its interior approach.");
            Assert.That(Vector3.Distance(motor.EyePosition, motor.Position), Is.GreaterThan(1f),
                "Acceptance must use the normal player-height host/camera geometry, not an overhead survey proxy.");
        }

        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Time.captureDeltaTime = 0f;
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
        }

        private static IEnumerator WalkMotorTo(
            KentridgeCharacterHost motor,
            ShowcaseWorld world,
            Vector3 target,
            string waypointName)
        {
            Vector3 start = motor.Position;
            int frame = 0;
            for (; frame < MaxWalkFramesPerLeg; frame++)
            {
                Vector3 delta = target - motor.Position;
                delta.y = 0f;
                float remaining = delta.magnitude;
                if (remaining <= WaypointToleranceMetres) break;
                Vector3 wish = delta.sqrMagnitude <= 1e-6f ? Vector3.zero : delta.normalized;
                motor.Step(world, wish, sprint: false, jumpHeld: false, dt: WalkDeltaTime);
                yield return null;
            }

            float finalDistance = HorizontalDistance(motor.Position, target);
            Assert.That(finalDistance, Is.LessThanOrEqualTo(WaypointToleranceMetres),
                "Production player could not physically reach " + waypointName +
                ". start=" + start + ", final=" + motor.Position + ", target=" + target +
                ", remaining=" + finalDistance.ToString("F3") + "m, frames=" + frame + ".");
        }

        private static Vector3 ToMetres(RealizedWorldPoint point)
        {
            float scale = 0.1f / point.UnitsPerDecimetre;
            return new Vector3(
                point.Position.X * scale,
                point.Position.Y * scale,
                point.Position.Z * scale);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Component FindDriver(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
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
                name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Playable driver is missing property '" + name + "'.");
            return (bool)property.GetValue(driver);
        }

        private static T ReadPrivateField<T>(Component driver, string name)
        {
            FieldInfo field = driver.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Playable driver is missing runtime field '" + name + "'.");
            return (T)field.GetValue(driver);
        }

        private static void DismissPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            if (presentation == null) return;
            PropertyInfo pending = presentation.GetType().GetProperty(
                "Pending", BindingFlags.Instance | BindingFlags.Public);
            if (pending == null || pending.GetValue(presentation) == null) return;
            MethodInfo dismiss = presentation.GetType().GetMethod(
                "DismissPending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(dismiss, Is.Not.Null);
            dismiss.Invoke(presentation, null);
        }
    }
}
