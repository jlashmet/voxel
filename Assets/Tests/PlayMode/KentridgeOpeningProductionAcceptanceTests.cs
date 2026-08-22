using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused production-scene acceptance for the recovered Kentridge opening. The broader
    /// KentridgePlayableScenePlayTests also walks through the generated doorway and exercises the
    /// later destination interaction; this regression deliberately stops at the opening handoff so
    /// opening fidelity failures cannot be hidden by an unrelated downstream traversal failure.
    /// </summary>
    public sealed class KentridgeOpeningProductionAcceptanceTests
    {
        private const string SceneName = "KentridgePlayableSlice";
        private const string DriverTypeName = "Game.Kentridge.PlayableSlice.KentridgePlayableSlice";
        private const float DecimetresToMetres = 0.1f;

        private Scene _loadedScene;
        private Scene _previousActiveScene;

        [UnityTest]
        public IEnumerator RecoveredOpening_CompletesProductionCameraMovementDialogueAndStoryHandoff()
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
                "The production KentridgePlayableSlice driver must own the opening acceptance.");
            Assert.That(ReadBoolProperty(driver, "GameplayControlEnabled"), Is.False,
                "The opening must own player control when the production scene begins.");
            Assert.That(ReadBoolProperty(driver, "HasExitedPub"), Is.False,
                "The production opening must begin inside the generated pub.");

            for (var frame = 0; frame < 1200 && !ReadBoolProperty(driver, "OpeningCutsceneStarted"); frame++)
                yield return null;

            Assert.That(ReadBoolProperty(driver, "OpeningPresentationReady"), Is.True,
                "The generated pub never reached complete published near-surface coverage.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneStarted"), Is.True,
                "The authored opening never started after the generated pub became presentation-ready.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.True,
                "The recovered opening must use its fixed establishing camera.");

            CharacterMotor motor = ReadPrivateField<CharacterMotor>(driver, "_motor");
            Camera openingCamera = driver.GetComponent<Camera>();
            Assert.That(openingCamera, Is.Not.Null,
                "The production Kentridge driver must own the camera used for the opening shot.");
            Vector3 openingFocus = ReadVector3Property(driver, "OpeningCutsceneCameraFocus");
            Vector3 openingCameraPosition = driver.transform.position;
            Quaternion openingCameraRotation = driver.transform.rotation;
            object pubAccessAtOpening = ReadPrivateField<object>(driver, "_pubAccess");
            Vector3 openingEntrance = ReadRealizedPoint(pubAccessAtOpening, "Entrance");
            Vector3 openingInward = ReadInt2Direction(pubAccessAtOpening, "Inward");
            float firstSlabBottom = openingEntrance.y
                + (KentridgeDefinition.Theme.FloorHeightDm - 3) * DecimetresToMetres;

            Assert.That(openingCameraPosition.y, Is.LessThan(firstSlabBottom - 0.1f),
                "The opening camera must remain below the generated pub's first intermediate floor instead of photographing through the roof.");
            Assert.That(Vector3.Dot(openingCameraPosition - openingEntrance, openingInward), Is.GreaterThan(0.5f),
                "The opening establishing camera must remain physically inside the generated pub entrance plane.");
            Assert.That(openingCameraPosition.y - openingFocus.y, Is.GreaterThan(1.3f),
                "The opening camera must remain elevated above the pub conversation group while staying below the upper floor.");
            Assert.That(Vector3.Dot(driver.transform.forward, Vector3.down), Is.GreaterThan(0.35f),
                "The opening camera must look downward as an overhead ensemble shot.");
            Assert.That(GameObject.Find("Weldon"), Is.Not.Null,
                "The production opening must realize a visible Weldon body under the ensemble camera.");

            Vector3 leadStart = motor.Position;
            Vector3 previousLead = leadStart;
            int leadMovingFrames = 0;
            Time.captureDeltaTime = 0.1f;

            for (var frame = 0; frame < 100 && !HasPendingDialogue(driver); frame++)
            {
                yield return null;
                Vector3 leadNow = motor.Position;
                if (HorizontalDistance(leadNow, previousLead) > 0.01f)
                {
                    leadMovingFrames++;
                    previousLead = leadNow;
                }

                AssertFixedCamera(driver, openingCameraPosition, openingCameraRotation,
                    "while Weldon enters the pub conversation");
            }

            Assert.That(HasPendingDialogue(driver), Is.True,
                "The production opening never reached recovered dialogue line 1 after Weldon entered.");
            Assert.That(HorizontalDistance(motor.Position, leadStart), Is.GreaterThan(0.5f),
                "Weldon must physically move from the opening spawn into the pub conversation.");
            Assert.That(leadMovingFrames, Is.GreaterThanOrEqualTo(5),
                "Weldon's recovered entrance must remain visible movement rather than a teleport.");

            object madelineActor = FindNpcActor(driver, "madeline");
            object stevenActor = FindNpcActor(driver, "steven");
            AssertActorReadable(openingCamera, motor.Position, "Weldon at dialogue line 1");
            AssertActorReadable(openingCamera, ReadActorRootPosition(madelineActor), "Madeline at dialogue line 1");
            AssertActorReadable(openingCamera, ReadActorRootPosition(stevenActor), "Steven at dialogue line 1");

            object loganActor = FindNpcActor(driver, "logan");
            Vector3 loganStart = ReadActorRootPosition(loganActor);
            Vector3 previousLogan = loganStart;
            int loganMovingFrames = 0;
            int dismissedDialogueBeats = 0;
            bool provedFourActorFraming = false;

            for (var frame = 0; frame < 400 && !ReadBoolProperty(driver, "GameplayControlEnabled"); frame++)
            {
                if (!provedFourActorFraming
                    && HasPendingDialogue(driver)
                    && string.Equals(ReadPendingSpeaker(driver), "Logan", StringComparison.Ordinal))
                {
                    AssertActorReadable(openingCamera, motor.Position, "Weldon when Logan first speaks");
                    AssertActorReadable(openingCamera, ReadActorRootPosition(madelineActor), "Madeline when Logan first speaks");
                    AssertActorReadable(openingCamera, ReadActorRootPosition(stevenActor), "Steven when Logan first speaks");
                    AssertActorReadable(openingCamera, ReadActorRootPosition(loganActor), "Logan on his first dialogue line");
                    provedFourActorFraming = true;
                }

                if (DismissPendingDialogue(driver))
                    dismissedDialogueBeats++;

                yield return null;

                Vector3 loganNow = ReadActorRootPosition(loganActor);
                if (HorizontalDistance(loganNow, previousLogan) > 0.01f)
                {
                    loganMovingFrames++;
                    previousLogan = loganNow;
                }

                if (ReadBoolProperty(driver, "OpeningCutsceneCameraActive"))
                    AssertFixedCamera(driver, openingCameraPosition, openingCameraRotation,
                        "while the recovered opening remains active");
            }
            Time.captureDeltaTime = 0f;

            Assert.That(provedFourActorFraming, Is.True,
                "The production opening never proved that all four actors are readable when Logan first speaks.");
            Assert.That(dismissedDialogueBeats, Is.EqualTo(31),
                "The production scene must present every recovered Kentridge opening dialogue beat exactly once.");
            Assert.That(loganMovingFrames, Is.GreaterThanOrEqualTo(5),
                "Logan's recovered entrance must remain visible movement rather than a teleport.");
            Assert.That(HorizontalDistance(ReadActorRootPosition(loganActor), loganStart), Is.GreaterThan(0.5f),
                "Logan must physically move from the public entrance toward the pub group.");
            Assert.That(ReadBoolProperty(driver, "GameplayControlEnabled"), Is.True,
                "The production opening never returned gameplay control after all 31 recovered lines.");
            Assert.That(ReadBoolProperty(driver, "OpeningCutsceneCameraActive"), Is.False,
                "The fixed ensemble camera must release when the opening hands control back.");
            Assert.That(GameObject.Find("Weldon"), Is.Null,
                "The cutscene-only Weldon body must hide when first-person gameplay resumes.");
            Assert.That(ReadBoolProperty(driver, "TravelObjectiveActive"), Is.True,
                "Completing the recovered opening must activate the main-story travel objective.");
            Assert.That(ReadBoolProperty(driver, "TravelObjectiveCompleted"), Is.False,
                "The travel objective must remain incomplete at the opening handoff.");

            object pubAccess = ReadPrivateField<object>(driver, "_pubAccess");
            Vector3 entrance = ReadRealizedPoint(pubAccess, "Entrance");
            Vector3 interiorApproach = ReadRealizedPoint(pubAccess, "InteriorApproach");
            Vector3 inward = ReadInt2Direction(pubAccess, "Inward");

            Assert.That(HorizontalDistance(motor.Position, interiorApproach), Is.LessThanOrEqualTo(0.05f),
                "The opening must hand gameplay back at the architecture-owned interior pub approach.");
            Assert.That(Vector3.Dot(motor.Position - entrance, inward), Is.GreaterThan(0.5f),
                "The player must still be physically inside the generated pub when the opening releases control.");
            Assert.That(ReadBoolProperty(driver, "HasExitedPub"), Is.False,
                "The scene must not report Kentridge town until the player physically crosses the generated doorway.");
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

        private static void AssertFixedCamera(
            Component driver,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            string phase)
        {
            Assert.That(Vector3.Distance(driver.transform.position, expectedPosition), Is.LessThanOrEqualTo(0.01f),
                "The establishing camera moved " + phase + ".");
            Assert.That(Quaternion.Angle(driver.transform.rotation, expectedRotation), Is.LessThanOrEqualTo(0.05f),
                "The establishing camera rotated " + phase + ".");
        }

        private static void AssertActorReadable(Camera camera, Vector3 feet, string actor)
        {
            Vector3 centre = camera.WorldToViewportPoint(feet + Vector3.up * 0.9f);
            Vector3 bottom = camera.WorldToViewportPoint(feet + Vector3.up * 0.05f);
            Vector3 top = camera.WorldToViewportPoint(feet + Vector3.up * 1.75f);
            float projectedHeight = Mathf.Abs(top.y - bottom.y);

            Assert.That(centre.z, Is.GreaterThan(0f), actor + " must be in front of the opening camera.");
            Assert.That(centre.x, Is.InRange(0.05f, 0.95f),
                actor + " must remain horizontally inside the fixed ensemble frame; viewport x=" + centre.x.ToString("0.###"));
            Assert.That(centre.y, Is.InRange(0.08f, 0.92f),
                actor + " must remain vertically inside the fixed ensemble frame; viewport y=" + centre.y.ToString("0.###"));
            Assert.That(projectedHeight, Is.GreaterThanOrEqualTo(0.12f),
                actor + " must occupy a readable share of the frame instead of becoming a tiny distant figure; projected height=" + projectedHeight.ToString("0.###"));
        }

        private static bool HasPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            PropertyInfo property = presentation.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Kentridge presentation must expose its pending dialogue operation.");
            return property.GetValue(presentation) != null;
        }

        private static string ReadPendingSpeaker(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            PropertyInfo pendingProperty = presentation.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(pendingProperty, Is.Not.Null, "Kentridge presentation must expose its pending dialogue operation.");
            object pending = pendingProperty.GetValue(presentation);
            Assert.That(pending, Is.Not.Null, "A pending dialogue speaker was requested with no pending line.");
            PropertyInfo speakerProperty = pending.GetType().GetProperty("Speaker", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(speakerProperty, Is.Not.Null, "Kentridge pending dialogue must expose its speaker.");
            return (string)speakerProperty.GetValue(pending);
        }

        private static bool DismissPendingDialogue(Component driver)
        {
            object presentation = ReadPrivateField<object>(driver, "_presentation");
            PropertyInfo property = presentation.GetType().GetProperty("Pending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Kentridge presentation must expose its pending dialogue operation.");
            if (property.GetValue(presentation) == null) return false;

            MethodInfo dismiss = presentation.GetType().GetMethod("DismissPending", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(dismiss, Is.Not.Null, "Kentridge presentation must expose dialogue dismissal.");
            dismiss.Invoke(presentation, null);
            return true;
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

        private static Vector3 ReadActorRootPosition(object actor)
        {
            FieldInfo rootField = actor.GetType().GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null, "Cutscene NPC actor is missing its visual root.");
            var root = rootField.GetValue(actor) as GameObject;
            Assert.That(root, Is.Not.Null, "Cutscene NPC visual root must exist while the opening is running.");
            return root.transform.position;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
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

        private static Vector3 ReadVector3Property(Component driver, string name)
        {
            PropertyInfo property = driver.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Playable scene driver is missing public property '" + name + "'.");
            return (Vector3)property.GetValue(driver);
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

        private static Vector3 ReadInt2Direction(object owner, string propertyName)
        {
            object direction = ReadProperty(owner, propertyName);
            return new Vector3(ReadIntField(direction, "X"), 0f, ReadIntField(direction, "Y")).normalized;
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
