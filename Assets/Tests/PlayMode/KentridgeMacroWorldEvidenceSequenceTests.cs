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
        public void SettlementNearFieldCompositionWidensLensWithoutChangingNormalLens()
        {
            Type compositionType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldSettlementSurveyComposition",
                throwOnError: true);
            MethodInfo resolveFieldOfView = compositionType.GetMethod(
                "ResolveReadableSurveyFieldOfView",
                StaticPrivate);
            Assert.That(resolveFieldOfView, Is.Not.Null);

            float widened = (float)resolveFieldOfView.Invoke(null, new object[] { 58f });
            float alreadyWide = (float)resolveFieldOfView.Invoke(null, new object[] { 80f });

            Assert.That(widened, Is.EqualTo(72f).Within(0.001f),
                "The validation settlement survey must widen the production 58-degree lens enough to contain the four-plot envelope without moving its authored camera/focus pose.");
            Assert.That(alreadyWide, Is.EqualTo(80f).Within(0.001f),
                "The validation composition must not narrow a camera that already has a wider lens.");

            const float genericHalfXMetres = 25.8f;
            const float genericHalfZMetres = 24.2f;
            float diagonalEnvelopeHalfSpan =
                (genericHalfXMetres + genericHalfZMetres) / Mathf.Sqrt(2f);
            const float flatTerrainCameraToFocusHeight = 62f;
            float normalHalfSpan = flatTerrainCameraToFocusHeight * Mathf.Tan(58f * 0.5f * Mathf.Deg2Rad);
            float readableHalfSpan = flatTerrainCameraToFocusHeight * Mathf.Tan(widened * 0.5f * Mathf.Deg2Rad);

            Assert.That(normalHalfSpan, Is.LessThan(diagonalEnvelopeHalfSpan),
                "The minimal repro must retain the demonstrated root cause: the normal scene lens cannot fully contain the generic settlement envelope on the diagonal survey axis.");
            Assert.That(readableHalfSpan, Is.GreaterThan(diagonalEnvelopeHalfSpan + 5f),
                "The widened validation lens must provide explicit containment margin rather than another edge-intersection-only framing.");
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
