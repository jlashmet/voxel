using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>Acceptance coverage for server-adjudicated provisional building.</summary>
    public sealed class BuildAdjudicationTests
    {
        [Test]
        [Category("US3")]
        public void PermittedPlacementPersists()
        {
            var overlay = new SpeculativeOverlay();
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 1, int3.zero, 8, 10, 42u, 1, 1);
            overlay.ApplyPending(in evt);
            Assert.That(overlay.HasPending, Is.True);

            var table = new RegionTable(16, Allocator.Temp);
            var pool = new BrickPool(64, Allocator.Temp);
            table.LoadRegion(int3.zero);
            overlay.ConfirmTick(1, ref table, ref pool);

            Assert.That(overlay.HasPending, Is.False);
            Assert.That(table.TryGetRegion(int3.zero, out var confirmedRegion), Is.True);
            Assert.That(confirmedRegion.Dirty, Is.True);

            table.Dispose();
            pool.Dispose();
            overlay.Dispose();
        }

        [Test]
        [Category("US3")]
        public void ForbiddenPlacementDissolvesWithReason()
        {
            var overlay = new SpeculativeOverlay();
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 2, int3.zero, 8, 10, 42u, 1, 1);
            overlay.ApplyPending(in evt);
            overlay.RejectTick(2, new byte[] { (byte)'P' });

            Assert.That(overlay.HasPending, Is.False);

            var rejectedMsg = new S_AlterationRejected(
                2,
                1,
                S_AlterationRejected.Reason.InPlayerVolume);
            string visibleReason = RejectionFeedback.ShowReason(in rejectedMsg);

            Assert.That(visibleReason, Is.Not.Empty);
            Assert.That(visibleReason, Does.Contain("player volume").IgnoreCase);
            overlay.Dispose();
        }

        [Test]
        [Category("US3")]
        public void RejectedPlacementNeverAppearsRemotely()
        {
            var overlayA = new SpeculativeOverlay();
            var overlayB = new SpeculativeOverlay();
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 3, int3.zero, 8, 10, 42u, 1, 1);

            overlayA.ApplyPending(in evt);
            overlayB.ApplyPending(in evt);
            overlayA.RejectTick(3, new byte[1]);

            Assert.That(overlayB.HasPending, Is.True);
            overlayB.RejectTick(3, new byte[1]);
            Assert.That(overlayA.HasPending, Is.False);
            Assert.That(overlayB.HasPending, Is.False);

            overlayA.Dispose();
            overlayB.Dispose();
        }
    }
}
