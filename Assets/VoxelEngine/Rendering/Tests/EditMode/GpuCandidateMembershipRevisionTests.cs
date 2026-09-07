using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuCandidateMembershipRevisionTests
    {
        [Test] public void SameCountToroidalReplacementInvalidatesCandidateInputs()
        {
            var grid = new SurfaceChunkSlotGrid();
            grid.UpdateWindow(int3.zero, 1);
            Assert.That(grid.TryAcquire(new int3(-1, 0, 0), out _), Is.True);
            ulong populated = grid.MembershipVersion;
            grid.UpdateWindow(new int3(1, 0, 0), 1);
            Assert.That(grid.MembershipVersion, Is.GreaterThan(populated));
            ulong moved = grid.MembershipVersion;
            Assert.That(grid.TryAcquire(new int3(2, 0, 0), out _), Is.True);
            Assert.That(grid.ActiveCount, Is.EqualTo(1));
            Assert.That(grid.ActiveCoordinateAt(0), Is.EqualTo(new int3(2, 0, 0)));
            Assert.That(grid.MembershipVersion, Is.GreaterThan(moved));
            ulong replaced = grid.MembershipVersion;
            Assert.That(grid.TryAcquire(new int3(2, 0, 0), out _), Is.True);
            grid.UpdateWindow(new int3(1, 0, 0), 1);
            Assert.That(grid.MembershipVersion, Is.EqualTo(replaced), "No-op ownership must preserve reusable input.");
            grid.Retire(new int3(2, 0, 0));
            Assert.That(grid.ActiveCount, Is.Zero);
            Assert.That(grid.MembershipVersion, Is.GreaterThan(replaced));
        }

        [Test] public void ResizeInvalidatesEvenWithNoResidentChunks()
        {
            var grid = new SurfaceChunkSlotGrid();
            grid.UpdateWindow(int3.zero, 1);
            ulong before = grid.MembershipVersion;
            grid.UpdateWindow(int3.zero, 2);
            Assert.That(grid.MembershipVersion, Is.GreaterThan(before));
            Assert.That(grid.ActiveCount, Is.Zero);
        }
    }
}
