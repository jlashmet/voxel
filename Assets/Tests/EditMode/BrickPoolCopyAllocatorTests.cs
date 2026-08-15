using NUnit.Framework;
using Unity.Collections;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class BrickPoolCopyAllocatorTests
    {
        [Test]
        public void CopiesShareAllocatorCursorAndFreeList()
        {
            var owner = new BrickPool(16, Allocator.Persistent);
            try
            {
                var copyA = owner;
                var copyB = owner;

                int first = copyA.Allocate();
                int second = copyB.Allocate();
                int third = owner.Allocate();

                Assert.AreEqual(0, first);
                Assert.AreEqual(1, second);
                Assert.AreEqual(2, third);
                Assert.AreEqual(3, owner.AllocatedCount);
                Assert.AreEqual(3, copyA.AllocatedCount);
                Assert.AreEqual(3, copyB.AllocatedCount,
                    "BrickPool copies must observe one shared allocator cursor.");

                copyA.Free(second);
                Assert.AreEqual(2, owner.AllocatedCount,
                    "Free-list mutations through a copy must remain visible to the owner.");

                int recycled = copyB.Allocate();
                Assert.AreEqual(second, recycled,
                    "A second copy must recycle the slot freed through the first copy.");
                Assert.AreEqual(3, owner.AllocatedCount);

                int next = owner.Allocate();
                Assert.AreEqual(3, next,
                    "Recycling a free slot must not rewind or fork the shared high-water cursor.");
            }
            finally
            {
                owner.Dispose();
            }
        }
    }
}
