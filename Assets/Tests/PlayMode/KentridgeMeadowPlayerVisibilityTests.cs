using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Regression for the player-visible failure that global meadow counts missed. The production
    /// Kentridge ecology budget must reach the actual opening player camera rather than being
    /// exhausted behind it by deterministic scan order.
    /// </summary>
    public sealed class KentridgeMeadowPlayerVisibilityTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string PlayerCameraName = "Kentridge Player Camera";
        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator OpeningPlayerCamera_FrustumContainsDenseProductionGrass()
        {
            _previousActiveScene = SceneManager.GetActiveScene();
            AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "Kentridge playable scene must be present in build settings.");
            while (!load.isDone) yield return null;

            _loadedScene = SceneManager.GetSceneByName(SceneName);
            Assert.That(_loadedScene.IsValid() && _loadedScene.isLoaded, Is.True);
            Assert.That(SceneManager.SetActiveScene(_loadedScene), Is.True);
            yield return null;

            KentridgePlayableSlice driver = FindInScene<KentridgePlayableSlice>(_loadedScene);
            Assert.That(driver, Is.Not.Null, "Exact Kentridge scene must contain its production driver.");

            KentridgeRegionLife life = ReadPrivateField<KentridgeRegionLife>(driver, "_life");
            Assert.That(life, Is.Not.Null, "Production Kentridge region life must be populated.");

            Camera camera = FindNamedCamera(_loadedScene, PlayerCameraName);
            Assert.That(camera, Is.Not.Null,
                $"Exact replay camera '{PlayerCameraName}' must exist in the production scene.");

            List<VegetationInstance> instances = ReadPrivateField<List<VegetationInstance>>(life, "_undergrowth");
            Assert.That(instances, Is.Not.Null.And.Not.Empty);

            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
            Vector3 cameraPosition = camera.transform.position;
            Vector3 cameraForward = camera.transform.forward;
            int grassTotal = 0;
            int grassInFront = 0;
            int grassInFrustum = 0;
            float furthestForwardMetres = float.NegativeInfinity;
            float maxGrassZ = float.NegativeInfinity;

            for (int i = 0; i < instances.Count; i++)
            {
                VegetationInstance instance = instances[i];
                if (instance.Kind != VegetationKind.Grass) continue;

                grassTotal++;
                Vector3 root = new Vector3(
                    instance.PositionMetres.x,
                    instance.PositionMetres.y,
                    instance.PositionMetres.z);
                float forwardMetres = Vector3.Dot(cameraForward, root - cameraPosition);
                if (forwardMetres > 0f) grassInFront++;
                if (forwardMetres > furthestForwardMetres) furthestForwardMetres = forwardMetres;
                if (root.z > maxGrassZ) maxGrassZ = root.z;

                // Bounds cover the full packed ribbon cluster generated around one semantic root.
                var bladeClusterBounds = new Bounds(
                    root + Vector3.up * 0.35f,
                    new Vector3(1.4f, 1.2f, 1.4f));
                if (GeometryUtility.TestPlanesAABB(frustum, bladeClusterBounds))
                    grassInFrustum++;
            }

            Debug.Log($"KENTRIDGE_MEADOW_PLAYER_VISIBILITY cameraPos={cameraPosition} "
                    + $"cameraForward={cameraForward} grassTotal={grassTotal} "
                    + $"grassInFront={grassInFront} grassInFrustum={grassInFrustum} "
                    + $"furthestForwardMetres={furthestForwardMetres:F2} maxGrassZ={maxGrassZ:F2}");

            Assert.That(grassTotal, Is.GreaterThanOrEqualTo(300),
                "The production scene must contain a substantive semantic grass population.");
            Assert.That(furthestForwardMetres, Is.GreaterThan(20f),
                "The bounded ecology budget must extend at least 20 m into the opening camera's forward view; scan-order truncation behind the camera is a regression.");
            Assert.That(grassInFront, Is.GreaterThanOrEqualTo(256),
                "Hundreds of production grass roots must lie in front of the required opening player camera.");
            Assert.That(grassInFrustum, Is.GreaterThanOrEqualTo(128),
                "The required player-height replay must actually contain dense production grass geometry; global blade counts outside the frustum are insufficient.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_loadedScene.IsValid() && _loadedScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(_loadedScene);
                if (unload != null)
                    while (!unload.isDone) yield return null;
            }

            if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(_previousActiveScene);
        }

        private static Camera FindNamedCamera(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
                for (int j = 0; j < cameras.Length; j++)
                    if (cameras[j].name == name) return cameras[j];
            }
            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"Expected production field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }
    }
}
