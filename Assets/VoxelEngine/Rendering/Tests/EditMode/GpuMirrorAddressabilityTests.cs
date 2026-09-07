using System;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuMirrorAddressabilityTests
    {
        [TestCase(1024L * 1024 * 1024)]
        [TestCase(long.MaxValue)]
        public void LargeBudgetCannotExceedPackedSlotAddressSpace(long bytes)
        {
            Assert.That(GpuBrickBufferLayout.SlotsForBudget(bytes), Is.LessThanOrEqualTo(65536));
        }

        [Test]
        public void MirrorRejectsUnaddressableCapacityBeforeAllocating()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GpuVoxelBrickMirror(65537));
        }

        [Test]
        public void MixedSlotCannotSilentlyWrapToAnotherBrick()
        {
            uint last = GpuSurfaceExtractor.PackBrickCacheEntry(VoxelBrickContent.Mixed, 0, 65535);
            Assert.That(last >> 16, Is.EqualTo(65535));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                GpuSurfaceExtractor.PackBrickCacheEntry(VoxelBrickContent.Mixed, 0, 65536));
        }
    }
}
