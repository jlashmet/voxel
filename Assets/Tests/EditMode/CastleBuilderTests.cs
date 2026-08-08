using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Structures;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuilderTests
    {
        [Test]
        public void PlanHeightMatchesItsFloorStack()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                var plan = CastleBuilder.Plan(int3.zero, seed);
                Assert.AreEqual(plan.Floors * plan.FloorHeight, plan.KeepHeight,
                    $"Seed {seed} produced a keep shell that disagrees with its floors.");
            }
        }

        [Test]
        public void BulkColumnPreservesNeighboursAndCollapsesUniformBricks()
        {
            var table = new RegionTable(4, Allocator.Persistent);
            var pool = new BrickPool(32, Allocator.Persistent);

            try
            {
                var brush = new VoxelBrush(table, pool, writeBudget: 1);

                for (int z = 0; z < VoxelDimensions.BrickEdge; z++)
                for (int x = 0; x < VoxelDimensions.BrickEdge; x++)
                    brush.FillColumnBulk(x, 0, VoxelDimensions.BrickEdge, z, Mat.Stone);

                Assert.AreEqual(Mat.Stone, brush.Get(3, 4, 5));
                Assert.AreEqual(Mat.Empty, brush.Get(8, 4, 5),
                    "A column batch must not overwrite the neighbouring brick.");
                Assert.AreEqual(0, brush.Pool.AllocatedCount,
                    "A completely filled brick must collapse back to a uniform reference.");
                Assert.IsFalse(brush.BudgetExceeded,
                    "Batched column writes must not consume the slow per-voxel budget.");
                Assert.AreEqual(512, brush.BulkVoxelsWritten);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
