using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceBlockHlodSummaryTests
    {
        [Test]
        public void UniformSolidFillsEverySubcellWithoutExpandingPayload()
        {
            SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Uniform(7);
            Assert.AreEqual(byte.MaxValue, summary.OccupiedSubcells);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(7, summary.MaterialAt(i));
        }

        [Test]
        public void MixedBlockKeepsIndependentThinFeaturesInSeparateSubcells()
        {
            using var voxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock,
                Allocator.Temp, NativeArrayOptions.ClearMemory);

            // Two one-voxel features at opposite corners of the same 8^3 block. An any-solid
            // block summary collapses these into one filled coarse sample; the HLOD summary must
            // keep the two 4^3 subcells independent and leave the six intervening subcells empty.
            voxels[0] = 7;      // (0,0,0) -> subcell 0
            voxels[511] = 5;    // (7,7,7) -> subcell 7

            SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Mixed(voxels, 0);
            Assert.AreEqual((1 << 0) | (1 << 7), summary.OccupiedSubcells);
            Assert.AreEqual(7, summary.MaterialAt(0));
            Assert.AreEqual(5, summary.MaterialAt(7));
            for (int i = 1; i < 7; i++)
            {
                Assert.False(summary.IsOccupied(i), $"Subcell {i} should remain an opening.");
                Assert.AreEqual(0, summary.MaterialAt(i));
            }
        }

        [Test]
        public void LiquidOnlySubcellsDoNotBecomeSolidHlodGeometry()
        {
            using var voxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            voxels[0] = 11;
            voxels[511] = 16;

            SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Mixed(voxels, 0);
            Assert.AreEqual(0, summary.OccupiedSubcells);
            Assert.AreEqual(0UL, summary.PackedMaterials);
        }
    }
}
