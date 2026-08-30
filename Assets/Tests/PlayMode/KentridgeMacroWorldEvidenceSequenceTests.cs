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
