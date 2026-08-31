using System;
using System.Reflection;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldEvidenceSequenceTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void MoordellSettlesBeforeMacroRoadAndLaterTargetsKeepAcceptedOrder()
        {
            Type driverType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldEvidenceDriver",
                throwOnError: true);
            MethodInfo resolveContinuation = driverType.GetMethod("ResolveMoordellContinuation", StaticPrivate);
            MethodInfo buildTargets = driverType.GetMethod("BuildTargetsAndRoadTraversal", InstancePrivate);
            FieldInfo targetsField = driverType.GetField("_targets", InstancePrivate);

            Assert.That(resolveContinuation, Is.Not.Null);
            Assert.That(buildTargets, Is.Not.Null);
            Assert.That(targetsField, Is.Not.Null);

            AssertContinuation(resolveContinuation, targetCaptured: false, macroRoadCaptured: false, roadArrivalCaptured: false, "Survey");
            AssertContinuation(resolveContinuation, targetCaptured: true, macroRoadCaptured: false, roadArrivalCaptured: false, "MacroRoad");
            AssertContinuation(resolveContinuation, targetCaptured: true, macroRoadCaptured: true, roadArrivalCaptured: false, "RoadArrival");
            AssertContinuation(resolveContinuation, targetCaptured: true, macroRoadCaptured: true, roadArrivalCaptured: true, "Advance");

            var host = new GameObject("KentridgeMacroWorldEvidenceSequenceTests");
            host.SetActive(false);
            try
            {
                Component driver = host.AddComponent(driverType);
                buildTargets.Invoke(driver, null);

                var targets = (Array)targetsField.GetValue(driver);
                Assert.That(targets, Is.Not.Null);
                Assert.That(targets.Length, Is.EqualTo(7));

                string[] expected =
                {
                    "moordell",
                    "rossdam",
                    "rossdam-lake-detour",
                    "fairy-village",
                    "orc-village",
                    "southern-ridge-pass",
                    "macro-network-overview"
                };

                for (var i = 0; i < expected.Length; i++)
                {
                    object target = targets.GetValue(i);
                    PropertyInfo labelProperty = target.GetType().GetProperty("Label", BindingFlags.Instance | BindingFlags.Public);
                    Assert.That(labelProperty, Is.Not.Null);
                    Assert.That(labelProperty.GetValue(target), Is.EqualTo(expected[i]), "Unexpected evidence target at index " + i);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MacroDriverRetainsAutomationAndKeepsQueuedMoordellSurveyCamera()
        {
            Type driverType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldEvidenceDriver",
                throwOnError: true);
            MethodInfo retainAutomation = driverType.GetMethod("RetainMacroValidationAutomation", StaticPrivate);
            MethodInfo shouldHoldSurvey = driverType.GetMethod("ShouldHoldMoordellSurveyAfterCapture", StaticPrivate);

            Assert.That(retainAutomation, Is.Not.Null);
            Assert.That(shouldHoldSurvey, Is.Not.Null);

            var host = new GameObject("KentridgeMacroWorldEvidenceOwnershipTests");
            host.SetActive(false);
            try
            {
                var slice = host.AddComponent<KentridgePlayableSlice>();
                slice.AutoSurvey = true;
                slice.AutoRecede = true;

                retainAutomation.Invoke(null, new object[] { slice });

                Assert.That(slice.AutoSurvey, Is.False,
                    "The macro validation driver must override a later generic survey toggle before streaming runs.");
                Assert.That(slice.AutoRecede, Is.False,
                    "The macro validation driver must override a later generic recede toggle before streaming runs.");

                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { true, 0f }), Is.EqualTo(true));
                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { true, 0.09f }), Is.EqualTo(true));
                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { true, 0.11f }), Is.EqualTo(false));
                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { false, 0f }), Is.EqualTo(false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SettlementNearFieldCompositionDolliesAlongExistingSurveyFocusRay()
        {
            Type compositionType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldSettlementSurveyComposition",
                throwOnError: true);
            MethodInfo resolvePosition = compositionType.GetMethod(
                "ResolveReadableSurveyPosition",
                StaticPrivate);
            Assert.That(resolvePosition, Is.Not.Null);

            var start = new Vector3(43f, 100f, 538f);
            var focus = new Vector3(37f, 38f, 532f);
            Vector3 forward = (focus - start).normalized;
            var resolved = (Vector3)resolvePosition.Invoke(null, new object[] { start, forward });

            Assert.That(resolved.y, Is.EqualTo(start.y - 25f).Within(0.001f),
                "The validation composition must move the 70 m settlement survey down by exactly the intended 25 m vertical component.");
            Assert.That(Vector3.Cross(resolved - start, forward).magnitude, Is.LessThan(0.001f),
                "The near-field correction must dolly on the existing semantic focus ray rather than vertically displacing the camera and changing framing.");
            Assert.That(Vector3.Angle(focus - start, focus - resolved), Is.LessThan(0.01f),
                "The authored settlement focus must remain centred after entering the near-field survey distance.");
        }

        private static void AssertContinuation(
            MethodInfo resolveContinuation,
            bool targetCaptured,
            bool macroRoadCaptured,
            bool roadArrivalCaptured,
            string expected)
        {
            object result = resolveContinuation.Invoke(
                null,
                new object[] { targetCaptured, macroRoadCaptured, roadArrivalCaptured });
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToString(), Is.EqualTo(expected));
        }
    }
}
