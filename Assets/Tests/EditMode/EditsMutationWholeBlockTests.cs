using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class EditsMutationWholeBlockTests
    {
        [Test]
        public void SameUniformMaterialIsNoOpUntilMetadataChanges()
        {
            var table = new RegionTable(1, Allocator.Persistent);
            var pool = new BrickPool(8, Allocator.Persistent);
            try
            {
                table.LoadRegion(int3.zero);
                var storage = new RegionMutationStore(in table, in pool);

                Assert.IsTrue(storage.SetWholeBlock(int3.zero, 5, false));
                Assert.IsFalse(storage.SetWholeBlock(int3.zero, 5, false));
                Assert.AreEqual(0, pool.AllocatedCount);

                Assert.IsTrue(storage.SetWholeBlock(int3.zero, 5, true),
                    "Adding hard-surface semantics must count as an authoritative change.");
                Assert.IsFalse(storage.SetWholeBlock(int3.zero, 5, true),
                    "Reapplying identical material and metadata must be a no-op.");
                Assert.AreEqual(0, pool.AllocatedCount);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
