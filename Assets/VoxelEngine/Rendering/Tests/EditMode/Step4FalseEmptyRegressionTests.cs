using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class Step4FalseEmptyRegressionTests
    {
        [Test]
        public void OwnedThinFeatureMissedByFourVoxelLatticeMustUseFeaturePreservingFallback()
        {
            var voxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
                // One exact solid voxel lies inside the first 2^3 subcell but on none of the step-4
                // lattice points (0 or 4 on each axis). Exact snapshot classification therefore owns
                // real solid content while the ordinary step-4 faceted/continuous samplers can return
                // zero geometry. The existing 2-voxel feature summary must retain that content.
                int thinFeature = 1 + 8 * (1 + 8 * 1);
                voxels[thinFeature] = 7;
                SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Mixed(voxels, 0);
                Assert.True(summary.IsOccupied(0),
                    "The feature-preserving two-voxel representation lost the exact thin feature.");

                bool hitByFourVoxelLattice = false;
                for (int z = 0; z < 8; z += 4)
                for (int y = 0; y < 8; y += 4)
                for (int x = 0; x < 8; x += 4)
                    hitByFourVoxelLattice |= voxels[x + 8 * (y + 8 * z)] != 0;
                Assert.False(hitByFourVoxelLattice,
                    "Fixture must reproduce a solid that ordinary step-4 point sampling misses.");

                MethodInfo fallbackPolicy = typeof(CpuTransvoxelChunkCache).GetMethod(
                    "RequiresFeaturePreservingFallback",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(fallbackPolicy,
                    "Production has no guard preventing an exact owned step-4 solid with zero ordinary geometry from being published as authoritative empty.");

                Assert.True((bool)fallbackPolicy.Invoke(null, new object[] { 4, true, false, 0, 0 }),
                    "An exact owned step-4 solid with zero ordinary geometry must enter the feature-preserving fallback before empty publication.");
                Assert.False((bool)fallbackPolicy.Invoke(null, new object[] { 4, false, false, 0, 0 }),
                    "A genuinely empty step-4 chunk must remain eligible for normal empty publication.");
                Assert.False((bool)fallbackPolicy.Invoke(null, new object[] { 2, true, false, 0, 0 }),
                    "The step-4 repair must not silently change finer LOD behavior.");
                Assert.False((bool)fallbackPolicy.Invoke(null, new object[] { 4, true, false, 1, 3 }),
                    "A step-4 build that already produced geometry must not run a redundant fallback.");
                Assert.False((bool)fallbackPolicy.Invoke(null, new object[] { 4, true, true, 0, 0 }),
                    "Authored profile geometry is not a false-empty chunk and must not be duplicated by HLOD fallback geometry.");
            }
            finally
            {
                voxels.Dispose();
            }
        }
    }
}
