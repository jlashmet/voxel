using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused production-scene acceptance for the Kentridge meadow feature. The standalone-player
    /// replay is the visual authority; this test makes the same scene expose durable density,
    /// exclusion, grounding, and cost diagnostics before the visual capture is trusted.
    /// </summary>
    public sealed class KentridgeMeadowAcceptanceTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator BuiltKentridge_ReportsDenseConnectedGrassOnlyMeadowWithNoExcludedLeakage()
        {
            _previousActiveScene = SceneManager.GetActiveScene();

            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "The Kentridge playable scene must be present in build settings.");
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True);
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);
            yield return null;

            KentridgePlayableSlice driver = FindDriver(_loadedScene);
            Assert.That(driver, Is.Not.Null,
                "The exact Kentridge playable scene must contain its production driver.");

            KentridgeRegionLife life = ReadPrivateField<KentridgeRegionLife>(driver, "_life");
            Assert.That(life, Is.Not.Null,
                "Kentridge must realize the WorldBuilder ecology policy in the production scene.");

            Assert.That(life.UndergrowthCount, Is.EqualTo(life.GrassCount),
                "The current Kentridge countryside allowlist is grass-only; no flowers, nettles, shrubs, or legacy accents may be mixed in.");
            Assert.That(life.TreeCount, Is.EqualTo(0),
                "The current countryside ecology tree allowlist is intentionally empty.");
            Assert.That(life.ClusterCount, Is.EqualTo(0),
                "Ambient animal life is intentionally disabled by the current ecology policy.");
            Assert.That(life.PrimaryMeadowGrassCount, Is.GreaterThan(0));
            Assert.That(life.PrimaryMeadowBladeCount, Is.GreaterThanOrEqualTo(3000),
                "One connected production meadow must contain at least 3,000 packed procedural grass blades.");
            Assert.That(life.GrassBladeCount, Is.GreaterThanOrEqualTo(life.PrimaryMeadowBladeCount));
            Assert.That(life.GrassMeshChunkCount, Is.GreaterThan(0),
                "Packed grass must be represented by spatial mesh chunks, not thousands of GameObjects.");
            Assert.That(life.ExcludedSurfaceGrassCount, Is.EqualTo(0),
                "No generated grass may leak back onto cells rejected by route/building/water/slope/invalid-surface policy.");
            Assert.That(life.RouteExclusionCount, Is.GreaterThan(0),
                "The production meadow must actively clear the authored traversal route rather than merely missing it by chance.");

            ShowcaseWorld world = ReadPrivateField<ShowcaseWorld>(driver, "_world");
            Assert.That(world, Is.Not.Null,
                "The production Kentridge world must be available to verify semantic grass grounding.");
            List<VegetationInstance> instances = ReadPrivateField<List<VegetationInstance>>(life, "_undergrowth");
            Assert.That(instances, Is.Not.Null.And.Not.Empty);

            int checkedGrassRoots = 0;
            for (int i = 0; i < instances.Count && checkedGrassRoots < 256; i++)
            {
                VegetationInstance instance = instances[i];
                if (instance.Kind != VegetationKind.Grass) continue;

                int vx = (int)math.floor(instance.PositionMetres.x / ShowcaseWorld.VoxelSize);
                int vz = (int)math.floor(instance.PositionMetres.z / ShowcaseWorld.VoxelSize);
                float exposedTopFace = (world.SurfaceHeight(vx, vz) + 1) * ShowcaseWorld.VoxelSize;
                Assert.That(instance.PositionMetres.y, Is.EqualTo(exposedTopFace).Within(0.0001f),
                    $"Grass root at ({instance.PositionMetres.x:F2},{instance.PositionMetres.z:F2}) must sit on the exposed top face, not inside the top occupied voxel.");
                checkedGrassRoots++;
            }
            Assert.That(checkedGrassRoots, Is.GreaterThanOrEqualTo(64),
                "Grounding regression must inspect a representative set of production meadow roots.");

            Debug.Log($"KENTRIDGE_MEADOW_ACCEPTANCE grassInstances={life.GrassCount} "
                    + $"grassBlades={life.GrassBladeCount} "
                    + $"primaryMeadowInstances={life.PrimaryMeadowGrassCount} "
                    + $"primaryMeadowBlades={life.PrimaryMeadowBladeCount} "
                    + $"meshChunks={life.GrassMeshChunkCount} "
                    + $"excludedLeakage={life.ExcludedSurfaceGrassCount} "
                    + $"groundRootsChecked={checkedGrassRoots} "
                    + $"routeRejected={life.RouteExclusionCount} "
                    + $"builtRejected={life.BuiltContentExclusionCount} "
                    + $"waterRejected={life.WaterExclusionCount} "
                    + $"cultivatedRejected={life.CultivatedExclusionCount} "
                    + $"steepRejected={life.SteepOrCliffExclusionCount} "
                    + $"invalidRejected={life.OtherInvalidExclusionCount}");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(_previousActiveScene);
                AsyncOperation unload = SceneManager.UnloadSceneAsync(_loadedScene);
                if (unload != null)
                    while (!unload.isDone) yield return null;
            }
            _loadedScene = default;
            _previousActiveScene = default;
        }

        private static KentridgePlayableSlice FindDriver(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                KentridgePlayableSlice driver = roots[i].GetComponentInChildren<KentridgePlayableSlice>(true);
                if (driver != null) return driver;
            }
            return null;
        }

        private static T ReadPrivateField<T>(object instance, string name) where T : class
        {
            FieldInfo field = instance.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {name} on {instance.GetType().Name}.");
            return field.GetValue(instance) as T;
        }
    }
}
