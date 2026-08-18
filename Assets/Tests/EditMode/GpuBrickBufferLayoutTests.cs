using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// The mirror's buffer arithmetic.
    ///
    /// A structured ComputeBuffer only takes a 32-bit stride, so every authoritative channel has to
    /// divide evenly into words for a brick's payload to be reinterpreted in place rather than
    /// copied through a staging array. If any of these stops dividing evenly the upload silently
    /// becomes a copy, or worse, misaligns and publishes one brick's voxels at another's offset.
    /// </summary>
    public sealed class GpuBrickBufferLayoutTests
    {
        [Test]
        public void EveryChannelDividesEvenlyIntoWords()
        {
            Assert.AreEqual(VoxelReadGrid.VoxelsPerBlock / 4,
                GpuBrickBufferLayout.MaterialWordsPerBrick, "512 material bytes are 128 words");
            Assert.AreEqual(VoxelReadGrid.VoxelsPerBlock / 2,
                GpuBrickBufferLayout.SurfaceWordsPerBrick, "512 ushorts are 256 words");
            Assert.AreEqual(VoxelReadGrid.VoxelsPerBlock / 4,
                GpuBrickBufferLayout.BoundaryWordsPerBrick, "512 boundary bytes are 128 words");
            Assert.AreEqual(VoxelReadGrid.OccupancyWordsPerBlock * 2,
                GpuBrickBufferLayout.OccupancyGpuWordsPerBrick, "8 ulongs are 16 words");
        }

        [Test]
        public void MirroredBrickCostsTheSameAsTheAuthoritativeOne()
        {
            // 512 material + 1024 surface + 512 boundary + 64 occupancy.
            Assert.AreEqual(2112, GpuBrickBufferLayout.BytesPerMixedBrick,
                "The mirror keeps the authoritative pool's shape, so a mixed brick costs the same "
              + "on both sides. A divergence here means a channel was dropped or padded.");
        }

        [Test]
        public void SlotOffsetsDoNotOverlap()
        {
            for (int slot = 0; slot < 8; slot++)
            {
                Assert.AreEqual(slot * GpuBrickBufferLayout.MaterialWordsPerBrick,
                    GpuBrickBufferLayout.MaterialWordOffset(slot));
                Assert.AreEqual(slot * GpuBrickBufferLayout.SurfaceWordsPerBrick,
                    GpuBrickBufferLayout.SurfaceWordOffset(slot));
                Assert.AreEqual(slot * GpuBrickBufferLayout.BoundaryWordsPerBrick,
                    GpuBrickBufferLayout.BoundaryWordOffset(slot));
                Assert.AreEqual(slot * GpuBrickBufferLayout.OccupancyGpuWordsPerBrick,
                    GpuBrickBufferLayout.OccupancyWordOffset(slot));
            }

            Assert.AreEqual(GpuBrickBufferLayout.MaterialWordsPerBrick,
                GpuBrickBufferLayout.MaterialWordOffset(1) - GpuBrickBufferLayout.MaterialWordOffset(0),
                "Consecutive slots are exactly one brick apart, with no padding to drift over.");
        }

        [Test]
        public void CommittedBytesScaleLinearlyWithSlots()
        {
            Assert.AreEqual(2112L * 1000, GpuBrickBufferLayout.CommittedBytes(1000));
            Assert.AreEqual(0L, GpuBrickBufferLayout.CommittedBytes(0));
        }

        [Test]
        public void SlotsForBudgetInvertsCommittedBytes()
        {
            const long budget = 256L * 1024 * 1024;
            int slots = GpuBrickBufferLayout.SlotsForBudget(budget);

            Assert.LessOrEqual(GpuBrickBufferLayout.CommittedBytes(slots), budget,
                "A budget must never be exceeded by the slot count derived from it.");
            Assert.Greater(GpuBrickBufferLayout.CommittedBytes(slots + 1), budget,
                "and it should use what it is given rather than rounding far below.");
        }

        [Test]
        public void ATinyBudgetStillYieldsAUsableMirror()
        {
            Assert.AreEqual(1, GpuBrickBufferLayout.SlotsForBudget(0),
                "A zero-slot mirror could never publish anything, so the floor is one brick.");
            Assert.AreEqual(1, GpuBrickBufferLayout.SlotsForBudget(-1));
            Assert.AreEqual(1, GpuBrickBufferLayout.SlotsForBudget(2112));
        }
    }
}
