using System.Reflection;
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
        public void UniformSolidFillsEverySubcellWithoutExpandingSourcePayload()
        {
            SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Uniform(7);
            Assert.AreEqual(ulong.MaxValue, summary.OccupiedSubcells);
            for (int i = 0; i < 64; i++)
                Assert.AreEqual(7, summary.MaterialAt(i), $"Subcell {i} lost the uniform material.");
        }

        [Test]
        public void MixedBlockKeepsIndependentThinFeaturesInSeparateSubcells()
        {
            var voxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
                // Two one-voxel features at opposite corners of the same 8^3 block. The HLOD
                // summary must keep the two 2^3 subcells independent and leave every intervening
                // subcell empty rather than OR-collapsing the whole source block.
                voxels[0] = 7;      // (0,0,0) -> subcell 0
                voxels[511] = 5;    // (7,7,7) -> subcell 63

                SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Mixed(voxels, 0);
                Assert.AreEqual((1UL << 0) | (1UL << 63), summary.OccupiedSubcells);
                Assert.AreEqual(7, summary.MaterialAt(0));
                Assert.AreEqual(5, summary.MaterialAt(63));
                for (int i = 1; i < 63; i++)
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
        public void TwoVoxelOpeningSurvivesSummaryQuantisation()
        {
            var voxels = new NativeArray<byte>(
                SurfaceBlockHlodSummaryBuilder.VoxelsPerBlock,
                Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
                // Fill the left and right 2-voxel slabs while keeping the middle 2-voxel slab
                // empty. This is the minimum architectural opening the step-8 representation
                // promises to preserve.
                for (int z = 0; z < 2; z++)
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        voxels[x + 8 * (y + 8 * z)] = 7;
                    for (int x = 4; x < 6; x++)
                        voxels[x + 8 * (y + 8 * z)] = 7;
                }

                SurfaceBlockHlodSummary summary = SurfaceBlockHlodSummaryBuilder.Mixed(voxels, 0);
                Assert.True(summary.IsOccupied(0));
                Assert.False(summary.IsOccupied(1),
                    "The 2-voxel opening collapsed at the outer production LOD.");
                Assert.True(summary.IsOccupied(2));
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
                Assert.AreEqual(0UL, summary.OccupiedSubcells);
                for (int i = 0; i < 64; i++)
                    Assert.AreEqual(0, summary.MaterialAt(i));
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
            using var mask = new NativeArray<byte>(16, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(32, Allocator.Temp);
            using var indices = new NativeList<uint>(64, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            RunSingleBlockMesh(summaries, mask, vertices, indices, overflow);

            Assert.AreEqual(0, overflow[0]);
            Assert.AreEqual(24, vertices.Length,
                "A solid 4x4x4 HLOD block should greedy-merge to six quads.");
            Assert.AreEqual(36, indices.Length);
        }

        [Test]
        public void GreedyHlodMesherKeepsOppositeSubcellFeaturesDisconnected()
        {
            SurfaceBlockHlodSummary summary = default;
            summary.Set(0, 7);
            summary.Set(63, 5);
            using var summaries = PaddedSingleBlock(summary);
            using var mask = new NativeArray<byte>(16, Allocator.Temp);
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
            using var mask = new NativeArray<byte>(16, Allocator.Temp);
            using var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.Temp);
            using var indices = new NativeList<uint>(6, Allocator.Temp);
            using var overflow = new NativeArray<int>(1, Allocator.Temp);

            RunSingleBlockMesh(summaries, mask, vertices, indices, overflow);

            Assert.AreEqual(1, overflow[0]);
            Assert.LessOrEqual(vertices.Length, vertices.Capacity);
            Assert.LessOrEqual(indices.Length, indices.Capacity);
        }

        [Test]
        public void ProductionWorkspaceCapacityTracksTwoVoxelHlodResolution()
        {
            // The production workspace is intentionally internal, but this is a regression on its
            // sizing contract: the 2-voxel HLOD change doubled linear resolution from the original
            // 4-voxel representation, so face output needs four times the original area budget.
            System.Type workspace = typeof(SurfaceBlockHlodMeshJob).Assembly.GetType(
                "VoxelEngine.Rendering.Runtime.SurfaceExtraction.TransvoxelBuildWorkspace");
            Assert.NotNull(workspace);

            BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            PropertyInfo scale = workspace.GetProperty("HlodSurfaceCapacityScale", flags);
            PropertyInfo vertices = workspace.GetProperty("HlodVertexCapacity", flags);
            PropertyInfo indices = workspace.GetProperty("HlodIndexCapacity", flags);
            Assert.NotNull(scale);
            Assert.NotNull(vertices);
            Assert.NotNull(indices);

            Assert.AreEqual(4, (int)scale.GetValue(null),
                "Doubling HLOD linear resolution must quadruple its surface-output budget.");
            Assert.AreEqual(1_048_576, (int)vertices.GetValue(null));
            Assert.AreEqual(1_572_864, (int)indices.GetValue(null));
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
