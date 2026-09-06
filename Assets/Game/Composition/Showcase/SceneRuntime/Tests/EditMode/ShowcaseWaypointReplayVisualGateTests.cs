using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseWaypointReplayVisualGateTests
    {
        private static readonly Type HarnessType = typeof(VoxelEngine.Showcase.ShowcaseFarFeatureRuntime)
            .Assembly.GetType("VoxelEngine.Showcase.ShowcaseWaypointReplayHarness", throwOnError: true);

        [Test]
        public void ErrorMagentaCounterMatchesUnityErrorColorSignature()
        {
            MethodInfo count = HarnessType.GetMethod(
                "CountErrorMagentaPixels",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(count, Is.Not.Null);

            var pixels = new[]
            {
                new Color32(255, 0, 255, 255),
                new Color32(240, 16, 240, 255),
                new Color32(239, 0, 255, 255),
                new Color32(255, 17, 255, 255),
                new Color32(255, 0, 239, 255)
            };

            Assert.That((int)count.Invoke(null, new object[] { pixels }), Is.EqualTo(2));
        }

        [Test]
        public void VisualGateRejectsOnlySubstantialErrorMagentaPopulation()
        {
            MethodInfo substantial = HarnessType.GetMethod(
                "IsSubstantialErrorMagenta",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(substantial, Is.Not.Null);

            const int pixelsAt1600x900 = 1_440_000;
            Assert.That(
                (bool)substantial.Invoke(null, new object[] { 1_440, pixelsAt1600x900 }),
                Is.True);
            Assert.That(
                (bool)substantial.Invoke(null, new object[] { 1_439, pixelsAt1600x900 }),
                Is.False);
            Assert.That(
                (bool)substantial.Invoke(null, new object[] { 64, 32_000 }),
                Is.True);
            Assert.That(
                (bool)substantial.Invoke(null, new object[] { 63, 32_000 }),
                Is.False);
        }
    }
}
