using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VoxelWallRasterizerTests
    {
        [Test]
        public void DiagonalWallSegmentIsContinuousAndUsesBulkColumns()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(512, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);

                var start = new int2(8, 8);
                var end = new int2(48, 48);
                VoxelWallRasterizer.FillSegment(
                    ref brush, start, end, baseY: 4, height: 24, thickness: 7,
                    material: Mat.Stone);

                for (int offset = 0; offset <= 40; offset++)
                {
                    int x = 8 + offset;
                    int z = 8 + offset;
                    Assert.AreEqual(Mat.Stone, brush.Get(x, 4, z),
                        $"missing diagonal wall base at offset {offset}");
                    Assert.AreEqual(Mat.Stone, brush.Get(x, 27, z),
                        $"missing diagonal wall top at offset {offset}");
                }

                for (int offset = 8; offset <= 32; offset += 4)
                {
                    int x = 8 + offset;
                    int z = 8 + offset;
                    Assert.AreEqual(Mat.Stone, brush.Get(x + 2, 12, z - 2),
                        $"wall lost requested thickness at offset {offset}");
                    Assert.AreEqual(Mat.Empty, brush.Get(x + 5, 12, z - 5),
                        $"wall footprint spread too far at offset {offset}");
                }

                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Wall rasterization must not fall back to slow per-voxel writes.");
                Assert.Greater(brush.BulkVoxelsWritten, 0);
                Assert.IsFalse(brush.BudgetExceeded,
                    "Bulk wall columns must not consume the slow write budget.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void JoinedSegmentsSealTheirSharedCorner()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(512, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);

                var a = new int2(10, 12);
                var corner = new int2(34, 28);
                var b = new int2(24, 54);
                VoxelWallRasterizer.FillSegment(
                    ref brush, a, corner, baseY: 3, height: 16, thickness: 6,
                    material: Mat.Stone);
                VoxelWallRasterizer.FillSegment(
                    ref brush, corner, b, baseY: 3, height: 16, thickness: 6,
                    material: Mat.Stone);

                Assert.AreEqual(Mat.Stone, brush.Get(corner.x, 3, corner.y));
                Assert.AreEqual(Mat.Stone, brush.Get(corner.x, 18, corner.y));

                for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                    Assert.AreEqual(Mat.Stone, brush.Get(corner.x + dx, 10, corner.y + dz),
                        $"shared corner contains a crack at ({dx}, {dz})");

                Assert.AreEqual(0, brush.VoxelsWritten);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
