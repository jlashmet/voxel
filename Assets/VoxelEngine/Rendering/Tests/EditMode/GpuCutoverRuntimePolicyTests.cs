using System;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuCutoverRuntimePolicyTests
    {
        [Test]
        public void CacheUsesConfiguredBackendInsteadOfHardCodedCpuGate()
        {
            // The cache samples the startup environment once. This checks the actual cache
            // value, not a source string or a disconnected policy helper. GPU module players
            // separately require dispatch/publication and reject fallback at runtime.
            bool explicitlyDisabled = Environment.GetEnvironmentVariable(
                "VOXEL_DISABLE_GPU_CUTOVER") == "1";
            Assert.That(CpuTransvoxelChunkCache.GpuCutoverDisabled,
                Is.EqualTo(explicitlyDisabled),
                "The production chunk cache must not silently hard-code the CPU backend.");
        }
    }
}
