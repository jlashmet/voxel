using System.Collections.Generic;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Bounded output for the compute mesher.
    ///
    /// The CPU path knew how much geometry a chunk produced because it had already produced it. A
    /// compute mesher must reserve before it writes, from a shader that cannot grow a buffer, so
    /// the sizing policy is load-bearing in a way it never was before.
    /// </summary>
    public sealed class GpuMeshletPageArenaTests
    {
        [Test]
        public void PageCountRoundsUpAndZeroNeedsNone()
        {
            var arena = new GpuMeshletPageArena(pageCount: 16, verticesPerPage: 100);

            Assert.AreEqual(0, arena.PagesFor(0));
            Assert.AreEqual(1, arena.PagesFor(1));
            Assert.AreEqual(1, arena.PagesFor(100));
            Assert.AreEqual(2, arena.PagesFor(101));
        }

        [Test]
        public void ReservingGivesDistinctPages()
        {
            var arena = new GpuMeshletPageArena(pageCount: 8, verticesPerPage: 10);

            Assert.AreEqual(GpuPageReservation.Granted,
                arena.TryReserve(chunkId: 1, vertexCount: 25, out IReadOnlyList<int> pages));
            Assert.AreEqual(3, pages.Count);
            CollectionAssert.AllItemsAreUnique(pages);
            Assert.AreEqual(3, arena.ReservedPages);
            Assert.AreEqual(5, arena.FreePages);
        }

        [Test]
        public void AFailedReservationLeavesThePreviousGeometryStanding()
        {
            var arena = new GpuMeshletPageArena(pageCount: 4, verticesPerPage: 10);
            arena.TryReserve(1, 20, out IReadOnlyList<int> first);
            var firstPages = new List<int>(first);

            // Wants 3 more pages than the arena has left.
            GpuPageReservation result = arena.TryReserve(1, 60, out _);

            Assert.AreEqual(GpuPageReservation.Exhausted, result);
            Assert.IsTrue(arena.TryGetPages(1, out IReadOnlyList<int> stillHeld));
            CollectionAssert.AreEqual(firstPages, stillHeld,
                "Half a chunk's triangles is a hole with extra steps. A build that cannot be "
              + "placed must change nothing, so the previous representation keeps covering.");
            Assert.AreEqual(1UL, arena.ExhaustedCount);
        }

        [Test]
        public void RegrowingAChunkKeepsThePagesItAlreadyHeld()
        {
            var arena = new GpuMeshletPageArena(pageCount: 8, verticesPerPage: 10);
            arena.TryReserve(1, 15, out IReadOnlyList<int> before);
            var kept = new List<int>(before);

            Assert.AreEqual(GpuPageReservation.Granted, arena.TryReserve(1, 35, out IReadOnlyList<int> after));

            Assert.AreEqual(4, after.Count);
            foreach (int page in kept)
                CollectionAssert.Contains(after, page,
                    "An edit that grows a chunk should extend its reservation rather than move it; "
                  + "moving would force the arena to repack.");
        }

        [Test]
        public void ShrinkingAChunkReturnsTheDifference()
        {
            var arena = new GpuMeshletPageArena(pageCount: 8, verticesPerPage: 10);
            arena.TryReserve(1, 40, out _);
            Assert.AreEqual(4, arena.ReservedPages);

            arena.TryReserve(1, 10, out IReadOnlyList<int> after);

            Assert.AreEqual(1, after.Count);
            Assert.AreEqual(1, arena.ReservedPages);
            Assert.AreEqual(7, arena.FreePages);
        }

        [Test]
        public void EmptyingAChunkReleasesEverythingItHeld()
        {
            var arena = new GpuMeshletPageArena(pageCount: 8, verticesPerPage: 10);
            arena.TryReserve(1, 30, out _);

            Assert.AreEqual(GpuPageReservation.Empty, arena.TryReserve(1, 0, out _));

            Assert.AreEqual(0, arena.ReservedPages,
                "A chunk meshed away to nothing must give its pages back, or destruction leaks "
              + "the arena one cleared chunk at a time.");
            Assert.IsFalse(arena.TryGetPages(1, out _));
        }

        [Test]
        public void OneChunkCannotConsumeTheWholeArena()
        {
            var arena = new GpuMeshletPageArena(pageCount: 1000, verticesPerPage: 10,
                                                maxPagesPerChunk: 4);

            Assert.AreEqual(GpuPageReservation.TooLarge, arena.TryReserve(1, 500, out _),
                "A degenerate density field must not starve every other chunk in view.");
            Assert.AreEqual(0, arena.ReservedPages);
            Assert.AreEqual(1UL, arena.RefusedTooLargeCount);
        }

        [Test]
        public void ReleasedPagesAreHandedOutAgain()
        {
            var arena = new GpuMeshletPageArena(pageCount: 4, verticesPerPage: 10);
            arena.TryReserve(1, 40, out _);
            Assert.AreEqual(GpuPageReservation.Exhausted, arena.TryReserve(2, 10, out _));

            arena.Release(1);

            Assert.AreEqual(GpuPageReservation.Granted, arena.TryReserve(2, 10, out _));
        }

        [Test]
        public void PageOffsetsDoNotOverlap()
        {
            var arena = new GpuMeshletPageArena(pageCount: 4, verticesPerPage: 256);

            Assert.AreEqual(0, arena.PageVertexOffset(0));
            Assert.AreEqual(256, arena.PageVertexOffset(1));
            Assert.AreEqual(768, arena.PageVertexOffset(3));
            Assert.AreEqual(1024, arena.VertexCapacity);
        }

        [Test]
        public void ClearReturnsEveryPage()
        {
            var arena = new GpuMeshletPageArena(pageCount: 8, verticesPerPage: 10);
            arena.TryReserve(1, 30, out _);
            arena.TryReserve(2, 30, out _);

            arena.Clear();

            Assert.AreEqual(8, arena.FreePages);
            Assert.AreEqual(0, arena.ChunkCount);
        }
    }
}
