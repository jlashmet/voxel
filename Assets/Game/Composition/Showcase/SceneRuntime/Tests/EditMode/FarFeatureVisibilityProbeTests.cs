using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarFeatureVisibilityProbeTests
    {
        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("{\"captures\":[]}", false)]
        [TestCase("{\"renderProbe\":\"unrecognized\"}", false)]
        [TestCase("{\"renderProbe\":\"far-feature-visibility\"}", true)]
        public void ReplayRequiresExplicitOptIn(string json, bool expected) =>
            Assert.That(SceneIssueFarFeatureReplayHarness.IsRequested(json), Is.EqualTo(expected));

        [TestCase(25d, 0)]
        [TestCase(29.99d, 0)]
        [TestCase(30d, 1)]
        [TestCase(35d, 1)]
        [TestCase(40d, 2)]
        [TestCase(45d, 2)]
        [TestCase(55d, 2)]
        public void CaptureWindowsHaveNormalSuppressedAndRestoredPhases(double seconds, int expected) =>
            Assert.That(FarFeatureVisibilityProbe.PhaseAt(seconds), Is.EqualTo(expected));

        [Test]
        public void RealRenderersRestoreOriginalStatesWithoutChangingInstances()
        {
            var first = new GameObject("initially-enabled");
            var second = new GameObject("initially-disabled");
            FarFeatureVisibilityProbe probe = null;
            try
            {
                var enabledRenderer = first.AddComponent<ProceduralFarFeatureRenderer>();
                var disabledRenderer = second.AddComponent<ProceduralFarFeatureRenderer>();
                disabledRenderer.enabled = false;
                enabledRenderer.SetInstances(new[]
                {
                    new FarFeatureInstance(1UL, float3.zero, quaternion.identity, new float3(1f),
                        float3.zero, new float3(1f), "probe-geometry", "probe-style", FarFeatureTier.Mid)
                });
                probe = new FarFeatureVisibilityProbe(new IFarFeatureRenderer[] { enabledRenderer, disabledRenderer });
                int instances = probe.InstanceCount;
                Assert.That(instances, Is.EqualTo(1));
                probe.Apply(true);
                Assert.That(enabledRenderer.enabled, Is.False);
                Assert.That(disabledRenderer.enabled, Is.False);
                Assert.That(probe.InstanceCount, Is.EqualTo(instances));
                probe.Apply(false);
                Assert.That(enabledRenderer.enabled, Is.True);
                Assert.That(disabledRenderer.enabled, Is.False);
                probe.Apply(true);
                probe.Dispose();
                probe.Dispose();
                probe.Apply(true);
                Assert.That(enabledRenderer.enabled, Is.True, "Disposed instrumentation must never suppress again.");
                Assert.That(disabledRenderer.enabled, Is.False);
            }
            finally
            {
                probe?.Dispose();
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void TeardownToleratesDestroyedProductionRenderer()
        {
            var root = new GameObject("destroyed-renderer");
            var renderer = root.AddComponent<ProceduralFarFeatureRenderer>();
            var probe = new FarFeatureVisibilityProbe(new IFarFeatureRenderer[] { renderer });
            probe.Apply(true);
            Object.DestroyImmediate(root);
            Assert.DoesNotThrow(() => probe.Dispose());
        }
    }
}
