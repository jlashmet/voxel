using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TransvoxelChunkBoundaryOwnershipTests
    {
        [Test]
        public void SceneIssue20260823013834177BoundaryCellBelongsToInsideDensitySide()
        {
            const int cellsPerAxis = 64;
            int3 negativeChunkCell = new(63, 20, 20);
            int3 positiveChunkShellCell = new(-1, 20, 20);

            Assert.False(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                negativeChunkCell, selectedInsideCorner: 1, cellsPerAxis));
            Assert.True(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                positiveChunkShellCell, selectedInsideCorner: 1, cellsPerAxis));
            Assert.True(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                negativeChunkCell, selectedInsideCorner: 0, cellsPerAxis));
            Assert.False(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                positiveChunkShellCell, selectedInsideCorner: 0, cellsPerAxis));
        }

        [Test]
        public void SceneIssueOwnershipIgnoresDominantMaterialOnOutsideCorner()
        {
            float[] density = { -0.40f, 0.30f };
            byte[] dominantMaterial = { 1, 1 };
            int selectedByMaterial = -1;
            int selectedByDensity = -1;
            for (int i = 0; i < 2; i++)
            {
                if (selectedByMaterial < 0 && dominantMaterial[i] != 0) selectedByMaterial = i;
                if (selectedByDensity < 0 && density[i] >= 0f) selectedByDensity = i;
            }

            Assert.AreEqual(0, selectedByMaterial);
            Assert.AreEqual(1, selectedByDensity);
            Assert.True(TransvoxelTopologyJob.OwnsSelectedInsideSample(
                new int3(-1, 20, 20), selectedByDensity, 64));
        }

        [Test]
        public void BoundaryLaneCompactsMultipleCellRecordsWithoutLosingIndices()
        {
            using var stream = new NativeStream(1, Allocator.TempJob);
            NativeStream.Writer writer = stream.AsWriter();
            writer.BeginForEachIndex(0);

            var first = new SmoothSurfaceVertex { Material = 101u };
            writer.Write((byte)0);
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write(first);
            writer.Write((byte)0);

            var second = new SmoothSurfaceVertex { Material = 202u };
            writer.Write((byte)0);
            writer.Write((byte)1);
            writer.Write((byte)1);
            writer.Write(second);
            writer.Write((byte)0);
            writer.EndForEachIndex();

            using var vertices = new NativeList<SmoothSurfaceVertex>(4, Allocator.TempJob);
            using var indices = new NativeList<uint>(4, Allocator.TempJob);
            using var overflow = new NativeArray<int>(1, Allocator.TempJob);
            new TransvoxelCompactJob
            {
                Input = stream.AsReader(),
                Vertices = vertices,
                Indices = indices,
                OverflowCell = overflow,
            }.Execute();

            Assert.AreEqual(-1, overflow[0]);
            Assert.AreEqual(2, vertices.Length);
            Assert.AreEqual(101u, vertices[0].Material);
            Assert.AreEqual(202u, vertices[1].Material);
            Assert.AreEqual(2, indices.Length);
            Assert.AreEqual(0u, indices[0]);
            Assert.AreEqual(1u, indices[1]);
        }

        [Test]
        public void MinimumFaceWithNoSolidHaloFallsBackFromGpu()
        {
            const int edge = 5;
            const int padding = 1;
            const int coreEdge = 3;
            var bricks = new NativeArray<TransvoxelDensityBrick>(edge * edge * edge, Allocator.TempJob);
            var mixed = new NativeArray<byte>(1, Allocator.TempJob);
            try
            {
                int y = 2;
                int z = 2;
                int core = 1 + edge * (y + edge * z);
                int halo = 0 + edge * (y + edge * z);
                bricks[core] = new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = 1,
                    MixedOffset = 0,
                };

                Assert.True(ExactSnapshotClassificationJob.LowBoundaryTouchesNoSolidHalo(
                    bricks, mixed, 1, y, z, edge, padding, coreEdge),
                    "A solid minimum-face brick with an all-air predecessor needs the CPU negative shell.");

                bricks[halo] = new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = 1,
                    MixedOffset = 0,
                };
                Assert.False(ExactSnapshotClassificationJob.LowBoundaryTouchesNoSolidHalo(
                    bricks, mixed, 1, y, z, edge, padding, coreEdge),
                    "A solid predecessor can publish the shared crossing, so GPU extraction remains eligible.");

                Assert.False(ExactSnapshotClassificationJob.LowBoundaryTouchesNoSolidHalo(
                    bricks, mixed, 2, y, z, edge, padding, coreEdge),
                    "Interior bricks must never trigger the boundary fallback.");
            }
            finally
            {
                mixed.Dispose();
                bricks.Dispose();
            }
        }
    }
}
