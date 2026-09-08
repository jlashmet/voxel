using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceDemandIdentitySnapshotTests
    {
        [Test]
        public void RequestTimeIdentitySurvivesInputImageAdvanceBeforeConsumption()
        {
            var first = new SurfaceLodNodeKey(1, new int3(-3, 0, 2));
            var second = new SurfaceLodNodeKey(1, new int3(5, -1, -4));
            var inputs = new GpuSurfaceLodInputs(64);

            inputs.Update(
                new[] { first },
                new[] { 7 },
                Array.Empty<SurfaceLodNodeKey>(),
                new[] { first });

            var snapshot = new GpuSurfaceDrawDispatcher.DemandIdentitySnapshot(64);
            snapshot.Capture(inputs, settingsVersion: 19u, voxelSize: 0.5f);
            uint requestInputVersion = snapshot.InputVersion;
            uint requestTopologyVersion = snapshot.TopologyVersion;

            inputs.Update(
                new[] { second },
                new[] { 11 },
                Array.Empty<SurfaceLodNodeKey>(),
                new[] { second });

            Assert.That(inputs.KeyAt(0), Is.EqualTo(second),
                "The live input image must advance so this regression discriminates callback-time lookup.");
            Assert.That(inputs.Version, Is.Not.EqualTo(requestInputVersion));
            Assert.That(inputs.TopologyBuildCount, Is.Not.EqualTo(requestTopologyVersion));

            uint rawState = 0x70u | (5u << 7);
            Assert.That(snapshot.TryResolve(0, rawState, out var probe), Is.True);
            Assert.That(probe.Key, Is.EqualTo(first),
                "The GPU readback index must resolve through the request-time key image.");
            Assert.That(probe.RawState, Is.EqualTo(rawState));
            Assert.That(probe.Priority, Is.EqualTo(5u));
            Assert.That(probe.InputVersion, Is.EqualTo(requestInputVersion));
            Assert.That(probe.SettingsVersion, Is.EqualTo(19u));
            Assert.That(probe.TopologyVersion, Is.EqualTo(requestTopologyVersion));
            Assert.That(probe.VoxelSize, Is.EqualTo(0.5f));

            var bounds = probe.BoundsVoxel;
            Assert.That(bounds.x, Is.EqualTo((-3f + 0.5f) * 64f).Within(0.001f));
            Assert.That(bounds.y, Is.EqualTo((0f + 0.5f) * 64f).Within(0.001f));
            Assert.That(bounds.z, Is.EqualTo((2f + 0.5f) * 64f).Within(0.001f));
            Assert.That(bounds.w, Is.EqualTo(33f).Within(0.001f));
        }

        [TestCase(0x70u, true, TestName = "OwnedInBandInFrustumMissingPhysicalIsSelected")]
        [TestCase(0x77u, true, TestName = "LowerCoverageBitsDoNotHideMissingPhysicalCandidate")]
        [TestCase(0x30u, false, TestName = "UnownedCandidateIsExcluded")]
        [TestCase(0x50u, false, TestName = "OffFrustumCandidateIsExcluded")]
        [TestCase(0x60u, false, TestName = "OffBandCandidateIsExcluded")]
        [TestCase(0x78u, false, TestName = "AlreadyPhysicalCandidateIsExcluded")]
        public void MissingPhysicalProbePredicateMatchesExactGpuStateContract(uint rawState, bool expected)
        {
            Assert.That(GpuSurfaceDrawDispatcher.IsMissingPhysicalDemandProbeState(rawState), Is.EqualTo(expected));
        }
    }
}
