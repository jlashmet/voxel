using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;

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
        private const float MinimumBodyViewportHeight = 0.18f;
        private const float ViewportMargin = 0.04f;
        private static readonly Vector3 CapturedCameraPosition = new Vector3(137.2f, 29.5f, 74.8f);
        private static readonly Quaternion CapturedCameraRotation =
            new Quaternion(0.37510243f, -0.5994149f, 0.37510243f, 0.59941494f);

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

            object madelineActor = FindNpcActor(driver, "madeline");
            object stevenActor = FindNpcActor(driver, "steven");
            object loganActor = FindNpcActor(driver, "logan");

            Time.captureDeltaTime = 0.1f;
            for (var frame = 0; frame < 100 && !HasPendingDialogue(driver); frame++)
                yield return null;

            Assert.That(HasPendingDialogue(driver), Is.True,
                "The production opening never reached recovered dialogue line 1.");

            AssertPhaseReadable(
                openingCamera,
                "line 1",
                new[] { "Weldon", "Madeline", "Steven" },
                new[] { GameObject.Find("Weldon"), ReadActorRoot(madelineActor), ReadActorRoot(stevenActor) });

            int dismissedBeforeLogan = 0;
            for (var frame = 0; frame < 240; frame++)
            {
                object pending = ReadPendingDialogue(driver);
                if (pending != null)
                {
                    string speaker = ReadStringProperty(pending, "Speaker");
                    if (string.Equals(speaker, "Logan", StringComparison.OrdinalIgnoreCase))
                        break;

                    DismissPendingDialogue(driver);
                    dismissedBeforeLogan++;
                }

                yield return null;
            }

            object loganLine = ReadPendingDialogue(driver);
            Assert.That(loganLine, Is.Not.Null,
                "Logan's recovered line 11 never became pending after the preceding dialogue beats.");
            Assert.That(ReadStringProperty(loganLine, "Speaker"), Is.EqualTo("Logan").IgnoreCase,
                "The opening must stop on Logan's recovered line-11 entrance beat.");
            Assert.That(dismissedBeforeLogan, Is.EqualTo(10),
                "Exactly the recovered first ten dialogue beats must precede Logan's line 11.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.True,
                "The fixed ensemble camera must still own the view when Logan first speaks.");

            AssertPhaseReadable(
                openingCamera,
                "Logan line 11",
                new[] { "Weldon", "Madeline", "Steven", "Logan" },
                new[]
                {
                    GameObject.Find("Weldon"),
                    ReadActorRoot(madelineActor),
                    ReadActorRoot(stevenActor),
                    ReadActorRoot(loganActor)
                });
        }

        [UnityTest]
        public IEnumerator CapturedOpeningCameraHasAuthoritativeLineOfSightToInitialCast()
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

            Time.captureDeltaTime = 0.1f;
            for (var frame = 0; frame < 100 && !HasPendingDialogue(driver); frame++)
                yield return null;
            Assert.That(HasPendingDialogue(driver), Is.True,
                "The production opening never reached the captured first dialogue beat.");

            Camera openingCamera = driver.GetComponent<Camera>();
            Assert.That(openingCamera, Is.Not.Null,
                "The production opening driver must share the captured camera.");
            openingCamera.transform.SetPositionAndRotation(
                CapturedCameraPosition,
                CapturedCameraRotation);
            openingCamera.fieldOfView = 58f;
            Assert.That(Vector3.Distance(openingCamera.transform.position, CapturedCameraPosition),
                Is.LessThan(0.001f), "The line-of-sight regression must use the saved issue position.");
            Assert.That(Quaternion.Angle(openingCamera.transform.rotation, CapturedCameraRotation),
                Is.LessThan(0.01f), "The line-of-sight regression must use the saved issue rotation.");
            Assert.That(openingCamera.fieldOfView, Is.EqualTo(58f).Within(0.001f),
                "The line-of-sight regression must use the saved issue field of view.");

            object madelineActor = FindNpcActor(driver, "madeline");
            object stevenActor = FindNpcActor(driver, "steven");
            Component openingPresentation = driver.GetComponent("KentridgeOpeningPresentation");
            Assert.That(openingPresentation, Is.Not.Null,
                "The production opening must own its bounded voxel cutaway.");

            AssertTorsoLineOfSight(
                openingCamera,
                ReadPrivateField<ShowcaseWorld>(driver, "_world"),
                ReadPrivateField<Vector3>(openingPresentation, "_cutawayMinVoxel"),
                ReadPrivateField<Vector3>(openingPresentation, "_cutawayMaxVoxel"),
                new[] { "Weldon", "Madeline", "Steven" },
                new[]
                {
                    GameObject.Find("Weldon"),
                    ReadActorRoot(madelineActor),
                    ReadActorRoot(stevenActor)
                });
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

        private static void AssertPhaseReadable(
            Camera camera,
            string phase,
            string[] participants,
            GameObject[] visualRoots)
        {
            Assert.That(participants.Length, Is.EqualTo(visualRoots.Length));
            var diagnostics = new List<string>(participants.Length);
            var failures = new List<string>();

            for (var i = 0; i < participants.Length; i++)
            {
                ViewportMeasurement measurement = Measure(camera, visualRoots[i], participants[i] + " at " + phase);
                diagnostics.Add(participants[i] + "=" + measurement.Describe());

                if (measurement.MinX < ViewportMargin)
                    failures.Add(participants[i] + " left=" + measurement.MinX.ToString("F3"));
                if (measurement.MaxX > 1f - ViewportMargin)
                    failures.Add(participants[i] + " right=" + measurement.MaxX.ToString("F3"));
                if (measurement.MinY < ViewportMargin)
                    failures.Add(participants[i] + " bottom=" + measurement.MinY.ToString("F3"));
                if (measurement.MaxY > 1f - ViewportMargin)
                    failures.Add(participants[i] + " top=" + measurement.MaxY.ToString("F3"));
                if (measurement.Height < MinimumBodyViewportHeight)
                    failures.Add(participants[i] + " height=" + measurement.Height.ToString("F3"));
            }

            string envelope = "KENTRIDGE_CAMERA_READABILITY phase=" + phase + " " + string.Join("; ", diagnostics);
            Debug.Log(envelope);
            if (failures.Count > 0)
            {
                Assert.Fail(
                    "Opening camera readability failed at " + phase + ": " + string.Join(", ", failures)
                    + ". Full envelope: " + string.Join("; ", diagnostics));
            }
        }

        private static ViewportMeasurement Measure(Camera camera, GameObject visualRoot, string participant)
        {
            Assert.That(visualRoot, Is.Not.Null, participant + " must have a realized visual body.");

            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            bool foundBounds = false;
            Bounds bounds = default;
            for (var i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!foundBounds)
                {
                    bounds = renderer.bounds;
                    foundBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Assert.That(foundBounds, Is.True,
                participant + " must have at least one enabled renderer in the opening.");

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var corner = new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z);
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                Assert.That(viewport.z, Is.GreaterThan(0f),
                    participant + " renderer bounds must remain in front of the opening camera.");
                minX = Mathf.Min(minX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxX = Mathf.Max(maxX, viewport.x);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            return new ViewportMeasurement(minX, minY, maxX, maxY);
        }

        private static void AssertTorsoLineOfSight(
            Camera camera,
            ShowcaseWorld world,
            Vector3 cutawayMinVoxel,
            Vector3 cutawayMaxVoxel,
            string[] participants,
            GameObject[] roots)
        {
            Assert.That(participants.Length, Is.EqualTo(roots.Length));
            for (int i = 0; i < participants.Length; i++)
            {
                Vector3 target = roots[i].transform.position + Vector3.up * 0.9f;
                Vector3 delta = target - camera.transform.position;
                float distance = delta.magnitude;
                Vector3 direction = delta / distance;
                int3 previousVoxel = new int3(int.MinValue);

                for (float travelled = 0.1f; travelled < distance - 0.1f; travelled += 0.025f)
                {
                    Vector3 point = camera.transform.position + direction * travelled;
                    Vector3 voxelPoint = point * 10f;
                    int3 voxel = (int3)math.floor((float3)voxelPoint);
                    if (math.all(voxel == previousVoxel)) continue;
                    previousVoxel = voxel;

                    bool cutAway = voxelPoint.x >= cutawayMinVoxel.x
                                && voxelPoint.y >= cutawayMinVoxel.y
                                && voxelPoint.z >= cutawayMinVoxel.z
                                && voxelPoint.x <= cutawayMaxVoxel.x
                                && voxelPoint.y <= cutawayMaxVoxel.y
                                && voxelPoint.z <= cutawayMaxVoxel.z;
                    if (cutAway) continue;

                    if (world.SurfaceQuery.TryRead(voxel, out VoxelCell cell)
                        && cell.BaseMaterialId != VoxelGrid.MaterialEmpty)
                    {
                        Assert.Fail(
                            participants[i] + " torso is hidden by authoritative material "
                            + cell.BaseMaterialId + " at voxel " + voxel
                            + " outside the opening cutaway; camera="
                            + camera.transform.position.ToString("F3")
                            + " target=" + target.ToString("F3") + ".");
                    }
                }
            }
        }

        private readonly struct ViewportMeasurement
        {
            public readonly float MinX;
            public readonly float MinY;
            public readonly float MaxX;
            public readonly float MaxY;

            public float Height => MaxY - MinY;
            public float Width => MaxX - MinX;

            public ViewportMeasurement(float minX, float minY, float maxX, float maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public string Describe() =>
                "h=" + Height.ToString("F3")
                + " w=" + Width.ToString("F3")
                + " bounds=(" + MinX.ToString("F3") + "," + MinY.ToString("F3")
                + ")-(" + MaxX.ToString("F3") + "," + MaxY.ToString("F3") + ")";
        }

        private static bool HasPendingDialogue(Component driver) =>
            ReadPendingDialogue(driver) != null;

        private static object ReadPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            PropertyInfo property = presentation.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Kentridge presentation must expose its pending dialogue operation.");
            return property.GetValue(presentation);
        }

        private static void DismissPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            MethodInfo dismiss = presentation.GetType().GetMethod("DismissPending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(dismiss, Is.Not.Null, "Kentridge presentation must expose dialogue dismissal.");
            dismiss.Invoke(presentation, null);
        }

        private static string ReadStringProperty(object owner, string name)
        {
            PropertyInfo property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, owner.GetType().FullName + " is missing property '" + name + "'.");
            return property.GetValue(owner) as string;
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

        private static GameObject ReadActorRoot(object actor)
        {
            FieldInfo rootField = actor.GetType().GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null, "Cutscene NPC actor is missing its visual root.");
            var root = rootField.GetValue(actor) as GameObject;
            Assert.That(root, Is.Not.Null, "Cutscene NPC visual root must exist while the opening is running.");
            return root;
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
