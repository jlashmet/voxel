using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepShellRealizerTests
    {
        [Test]
        public void ShellBuildsMasonryAndClearsInteriorOnBulkPath()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(4096, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);
                int baseY = 30;
                var min = new int3(80, baseY, 96);
                var size = new int3(72, 92, 64);

                CastleKeepShellRealizer.Build(ref brush, min, size, baseY);

                Assert.AreEqual(Mat.DarkStone, brush.Get(min.x - 3, baseY - 10, min.z - 3));
                Assert.AreEqual(Mat.Stone, brush.Get(min.x, baseY + 20, min.z + 20));
                Assert.AreEqual(Mat.Empty, brush.Get(min.x + 20, baseY + 20, min.z + 20));
                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Keep shell realization should remain on bulk authored writes.");
                Assert.Greater(brush.BulkVoxelsWritten, 0);
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
