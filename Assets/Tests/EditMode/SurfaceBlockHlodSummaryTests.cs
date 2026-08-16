using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
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
            var voxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
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
            finally
            {
                voxels.Dispose();
            }
        }

        [Test]
        public void LiquidOnlySubcellsDoNotBecomeSolidHlodGeometry()
        {
            var voxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
                voxels[0] = 11;
                voxels[511] = 16;

                SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Mixed(voxels, 0);
                Assert.AreEqual(0, summary.OccupiedSubcells);
                Assert.AreEqual(0UL, summary.PackedMaterials);
            }
            finally
            {
                voxels.Dispose();
            }
        }

        [Test]
        public void GreedyHlodMesherMergesUniformBlockIntoSixQuads()
        {
            using var summaries = PaddedSingleBlock(SurfaceBlockHlodSummaryBuilder.Uniform(7));
            using var mask = new NativeArray<byte>(4, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(32, Allocator.Temp);
            using var indices = new NativeList<uint>(64, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            RunSingleBlockMesh(summaries, mask, vertices, indices, overflow);

            Assert.AreEqual(0, overflow[0]);
            Assert.AreEqual(24, vertices.Length,
                "A solid 2x2x2 HLOD block should greedy-merge to six quads.");
            Assert.AreEqual(36, indices.Length);
        }

        [Test]
        public void GreedyHlodMesherKeepsOppositeSubcellFeaturesDisconnected()
        {
            var summary = new SurfaceBlockHlodSummary
            {
                OccupiedSubcells = (1 << 0) | (1 << 7),
                PackedMaterials = 7UL | (5UL << (7 * 8)),
            };
            using var summaries = PaddedSingleBlock(summary);
            using var mask = new NativeArray<byte>(4, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(64, Allocator.Temp);
            using var indices = new NativeList<uint>(128, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            RunSingleBlockMesh(summaries, mask, vertices, indices, overflow);

            Assert.AreEqual(0, overflow[0]);
            Assert.AreEqual(48, vertices.Length,
                "Two diagonal HLOD subcells must remain two disconnected cubes (12 quads). ");
            Assert.AreEqual(72, indices.Length);
        }

        [Test]
        public void HlodMesherReportsCapacityOverflowInsteadOfGrowingOutput()
        {
            using var summaries = PaddedSingleBlock(SurfaceBlockHlodSummaryBuilder.Uniform(7));
            using var mask = new NativeArray<byte>(4, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Temp);
            using var indices = new NativeList<uint>(6, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            RunSingleBlockMesh(summaries, mask, vertices, indices, overflow);

            Assert.AreEqual(1, overflow[0]);
            Assert.LessOrEqual(vertices.Length, vertices.Capacity);
            Assert.LessOrEqual(indices.Length, indices.Capacity);
        }

        private static NativeArray<SurfaceBlockHlodSummary> PaddedSingleBlock(
            SurfaceBlockHlodSummary centre)
        {
            var summaries = new NativeArray<SurfaceBlockHlodSummary>(27, Allocator.Temp,
                NativeArrayOptions.ClearMemory);
            summaries[1 + 3 * (1 + 3)] = centre;
            return summaries;
        }

        private static void RunSingleBlockMesh(
            NativeArray<SurfaceBlockHlodSummary> summaries,
            NativeArray<byte> mask,
            NativeList<SmoothSurfaceVertex> vertices,
            NativeList<uint> indices,
            NativeArray<int> overflow)
        {
            var job = new SurfaceBlockHlodMeshJob
            {
                Summaries = summaries,
                SummaryGridEdge = 3,
                PaddingBricks = 1,
                CoreBrickEdge = 1,
                CoreOriginVoxel = int3.zero,
                VoxelSize = 0.1f,
                MaskScratch = mask,
                Vertices = vertices,
                Indices = indices,
                Overflow = overflow,
            };
            job.Execute();
        }
    }
}
