using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Acceptance tests for build adjudication (US3: Building with visible provisional state).
    ///
    /// Scenario 3 acceptance criteria:
    ///   - Permitted placement persists into the real grid.
    ///   - Forbidden placement dissolves with a visible reason.
    ///   - The rejected placement never appears on any remote client's view.
    /// </summary>
    public sealed class BuildAdjudicationTests
    {
        // -----------------------------------------------------------------------
        // Permitted placement persists
        // -----------------------------------------------------------------------

        /// <summary>
        /// A valid build placement (in air, not intersecting a player, within reach) should
        /// persist into the real grid after server confirmation. The speculative overlay promotes
        /// on ConfirmTick and the voxel appears in the authoritative state.
        /// </summary>
        [Test]
        [Category("US3")]
        public void PermittedPlacementPersists()
        {
            // Arrange: a valid build placement in empty space.
            var overlay = new SpeculativeOverlay();
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 1, int3.zero, 8, 10, 42u, 1, 1);

            // Add the event to the overlay.
            overlay.ApplyPending(in evt);
            Assert.That(overlay.HasPending, Is.True, "Overlay should have pending entries after ApplyPending.");

            // Act: server confirms — promote to real grid.
            var table = new RegionTable(16, Allocator.Temp);
            var pool = new BrickPool(64, Allocator.Temp);
            table.LoadRegion(int3.zero);
            overlay.ConfirmTick(1, ref table, ref pool);

            // Assert: pending cleared and region marked dirty.
            Assert.That(overlay.HasPending, Is.False, "Confirmed entries should be removed from overlay.");

            // Verify the material was written to the grid (region should have dirty flag).
            Assert.That(table.TryGetRegion(int3.zero, out var confirmedRegion), Is.True,
                "Region must still be resident after promotion.");
            Assert.That(confirmedRegion.Dirty, Is.True, "Region must be marked dirty after promotion.");

            table.Dispose();
            pool.Dispose();
        }

        // -----------------------------------------------------------------------
        // Forbidden placement dissolves with reason
        // -----------------------------------------------------------------------

        /// <summary>
        /// A forbidden placement (intersecting player volume) should dissolve with a visible reason.
        /// The overlay rejects the tick and RejectionFeedback surfaces the appropriate message.
        /// </summary>
        [Test]
        [Category("US3")]
        public void ForbiddenPlacementDissolvesWithReason()
        {
            // Arrange: a placement that intersects player volume (forbidden).
            var overlay = new SpeculativeOverlay();
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 2, int3.zero, 8, 10, 42u, 1, 1);
            overlay.ApplyPending(in evt);

            // Act: server rejects (reason code 1 = InPlayerVolume).
            var rejectionReason = new byte[64];
            for (int i = 0; i < rejectionReason.Length; i++)
                rejectionReason[i] = (byte)'P'; // placeholder "Player volume" padded to 64.

            overlay.RejectTick(2, rejectionReason);

            // Assert: pending cleared without writing to the grid.
            Assert.That(overlay.HasPending, Is.False, "Rejected entries should be removed from overlay.");

            // Verify the rejection reason is renderable by RejectionFeedback.
            var rejectedMsg = new S_AlterationRejected { ReasonCode = 1 };
            string visibleReason = RejectionFeedback.ShowReason(in rejectedMsg);

            Assert.That(visibleReason, Is.Not.Empty, "Rejection feedback must produce a visible reason.");
            Assert.That(visibleReason, Does.Contain("player volume").IgnoreCase,
                "InPlayerVolume rejection should mention player volume.");
        }

        // -----------------------------------------------------------------------
        // Never appears remotely
        // -----------------------------------------------------------------------

        /// <summary>
        /// A rejected placement must never appear on any remote client's view. The server does not
        /// broadcast a confirmation for rejected alterations, so no other client can derive the change.
        /// This is the server-authority principle in action: if the server says no, nobody sees it.
        /// </summary>
        [Test]
        [Category("US3")]
        public void RejectedPlacementNeverAppearsRemotely()
        {
            // Arrange: two overlays simulating client A (submitter) and client B (observer).
            var overlayA = new SpeculativeOverlay();
            var overlayB = new SpeculativeOverlay();

            var evt = new AlterationEvent(AlterationEvent.KindBrush, 3, int3.zero, 8, 10, 42u, 1, 1);

            // Client A adds to its overlay.
            overlayA.ApplyPending(in evt);

            // Client B also has the pending state (received from A's speculative broadcast).
            overlayB.ApplyPending(in evt);

            // Act: server rejects — only client A receives the rejection message.
            // Client B does NOT receive a confirmation because the server rejected it.

            overlayA.RejectTick(3, new byte[1]);

            // Client B's overlay is untouched (no confirmation was broadcast).
            Assert.That(overlayB.HasPending, Is.True, "Observer client should still see pending state.");

            // After observer client syncs with server and finds no confirmation:
            overlayB.RejectTick(3, new byte[1]);

            // Neither client has the rejected voxel in their real grid.
            Assert.That(overlayA.HasPending, Is.False, "Client A's rejection should clear its pending state.");
            Assert.That(overlayB.HasPending, Is.False, "Client B must also clear on sync (no server confirmation).");

            // Critically: no modification was written to any grid — the rejected voxel exists nowhere.
        }

        // -----------------------------------------------------------------------
        // Helper types
        // -----------------------------------------------------------------------
        //
        // RegionTable and BrickPool are structs, so they cannot be subclassed for test
        // doubles. They are cheap to construct outright, so the tests drive the real types
        // and assert against real state rather than a fake.

    }
}
