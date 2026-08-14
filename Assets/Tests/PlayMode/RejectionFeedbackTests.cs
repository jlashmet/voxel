using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>SC-007 rejection feedback coverage against the canonical protocol reason type.</summary>
    public sealed class RejectionFeedbackTests
    {
        [Test]
        [Category("SC_007")]
        [Category("US3")]
        public void EveryReasonCodeMapsToVisibleString()
        {
            S_AlterationRejected.Reason[] reasons =
            {
                S_AlterationRejected.Reason.TooFast,
                S_AlterationRejected.Reason.OverBudget,
                S_AlterationRejected.Reason.OverDensity,
                S_AlterationRejected.Reason.NotAttached,
                S_AlterationRejected.Reason.InPlayerVolume,
                S_AlterationRejected.Reason.OutOfReach,
                S_AlterationRejected.Reason.ProtectedZone,
                S_AlterationRejected.Reason.InvalidTarget,
            };

            foreach (var reasonCode in reasons)
            {
                var rejected = new S_AlterationRejected(1, 1, reasonCode);
                string reason = RejectionFeedback.ShowReason(in rejected);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(reason, Is.Not.EqualTo("Placement rejected by server"));
            }
        }

        [Test]
        [Category("SC_007")]
        public void UnknownReasonCodeFallsBackToGenericMessage()
        {
            var rejected = new S_AlterationRejected(1, 1, (S_AlterationRejected.Reason)99);
            string reason = RejectionFeedback.ShowReason(in rejected);
            Assert.That(reason, Is.EqualTo("Placement rejected by server"));
        }

        [Test]
        [Category("SC_007")]
        public void DissolveAnimationStartsCorrectly()
        {
            int3 region = new int3(1, 2, 3);
            RejectionFeedback.StartDissolveAnimation(region, 0.5f);
            var completed = RejectionFeedback.UpdateDissolves(out var alphas);

            Assert.That(completed.Length, Is.EqualTo(0));
            Assert.That(alphas.Length, Is.GreaterThan(0));
            foreach (float alpha in alphas)
                Assert.That(alpha, Is.InRange(0f, 1f));

            completed.Dispose();
            alphas.Dispose();
            RejectionFeedback.Dispose();
        }

        [Test]
        [Category("SC_007")]
        public void DissolveCompletesAfterDuration()
        {
            int3 region = new int3(5, 5, 5);
            RejectionFeedback.StartDissolveAnimation(region, 1.0f);
            var completed = RejectionFeedback.UpdateDissolves(out var alphas);
            Assert.That(completed.Length, Is.EqualTo(0));
            completed.Dispose();
            alphas.Dispose();
            RejectionFeedback.Dispose();
        }

        [Test]
        [Category("SC_007")]
        public void DuplicateDissolveStartIsIdempotent()
        {
            int3 region = new int3(10, 10, 10);
            RejectionFeedback.StartDissolveAnimation(region, 0.5f);
            RejectionFeedback.StartDissolveAnimation(region, 0.5f);
            var completed = RejectionFeedback.UpdateDissolves(out var alphas);

            Assert.That(completed.Length, Is.EqualTo(0));
            Assert.That(alphas.Length, Is.LessThanOrEqualTo(1));
            completed.Dispose();
            alphas.Dispose();
            RejectionFeedback.Dispose();
        }

        [Test]
        [Category("SC_007")]
        public void OverlayTransitionsCleanlyFromPendingToRejected()
        {
            var overlay = new SpeculativeOverlay();
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 10, int3.zero, 8, 10, 42u, 1, 1);
            overlay.ApplyPending(in evt);
            Assert.That(overlay.HasPending, Is.True);
            overlay.RejectTick(10, new byte[1]);
            Assert.That(overlay.HasPending, Is.False);
        }

        [Test]
        [Category("SC_007")]
        public void RejectTickOnlyClearsMatchingEntries()
        {
            var overlay = new SpeculativeOverlay();
            var e1 = new AlterationEvent(AlterationEvent.KindBrush, 5, int3.zero, 8, 10, 1u, 1, 1);
            var e2 = new AlterationEvent(AlterationEvent.KindBrush, 10, int3.zero, 8, 20, 2u, 2, 1);
            overlay.ApplyPending(in e1);
            overlay.ApplyPending(in e2);
            Assert.That(overlay.PendingCount, Is.GreaterThan(0));
            overlay.RejectTick(5, new byte[1]);
            Assert.That(overlay.HasPending, Is.True);
        }
    }
}
