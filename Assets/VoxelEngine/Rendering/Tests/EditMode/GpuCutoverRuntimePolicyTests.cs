using System;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GpuCutoverRuntimePolicyTests
    {
        [Test]
        public void RetiredCpuSwitchCannotDisableSolidGpuRendering()
        {
            string previous = Environment.GetEnvironmentVariable("VOXEL_DISABLE_GPU_CUTOVER");
            try
            {
                Environment.SetEnvironmentVariable("VOXEL_DISABLE_GPU_CUTOVER", "1");
                foreach (int step in new[] { 1, 2, 4, 8 })
                {
                    using var cache = new GpuSolidChunkCache(step);
                    Assert.True(cache.GpuCutoverAvailable);
                    Assert.False(cache.GpuBackendResident, "Compute scratch remains lazy.");
                }
            }
            finally { Environment.SetEnvironmentVariable("VOXEL_DISABLE_GPU_CUTOVER", previous); }
        }
    }
}
