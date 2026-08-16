using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VoxelBrushBudgetTests
    {
        [Test]
        public void CoatingSlowWritesRespectHardWriteBudget()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(128, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);

                brush.FillColumnBulk(8, 8, 10, 8, Mat.Stone);
                Assert.IsTrue(brush.IsSolid(8, 8, 8));
                Assert.IsTrue(brush.IsSolid(8, 9, 8));
                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Bulk setup must not consume the slow-write budget.");

                brush.Coat(8, 8, 8, Coatings.Moss);
                Assert.AreEqual(1, brush.VoxelsWritten);
                Assert.AreEqual(Coatings.Moss, brush.GetCoating(8, 8, 8));
                Assert.IsFalse(brush.BudgetExceeded);

                brush.Coat(8, 9, 8, Coatings.Moss);

                Assert.AreEqual(1, brush.VoxelsWritten,
                    "The second slow coating write must be dropped at the hard budget.");
                Assert.AreEqual(Coatings.None, brush.GetCoating(8, 9, 8));
                Assert.IsTrue(brush.BudgetExceeded,
                    "Rejected coating writes must latch the same budget signal as Set/SetStyled.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
