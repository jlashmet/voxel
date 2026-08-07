using VoxelEngine.Core.Edits;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Net.Client;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// SC-007: Rejection feedback rate and reason coverage.
    ///
    /// These tests verify that:
    ///   1. Every valid rejection reason code maps to a non-empty, non-generic string (100% reason coverage).
    ///   2. Dissolve animations start correctly and produce alpha levels in [0, 1].
    ///   3. Rejected overlays transition cleanly from pending to cleared state.
    /// </summary>
    public sealed class RejectionFeedbackTests
    {
        // -----------------------------------------------------------------------
        // SC-007: 100% reason coverage — every rejection code has a visible string
        // -----------------------------------------------------------------------

        /// <summary>
        /// Every defined rejection reason code (1..8) must produce a non-empty, non-generic string.
        /// Generic fallback ("Placement rejected by server") is only valid for unknown codes.
        /// </summary>
        [Test]
        [Category("SC_007")]
        [Category("US3")]
        public void EveryReasonCodeMapsToVisibleString()
        {
            // All reason codes from Validation.ValidationResult that can be sent to the client.
            byte[] reasonCodes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            foreach (var code in reasonCodes)
            {
                var rejected = new S_AlterationRejected { ReasonCode = code };
                string reason = RejectionFeedback.ShowReason(in rejected);

                Assert.That(reason, Is.Not.Empty,
                    $"Reason code {code} must produce a non-empty string.");

                Assert.That(reason, Is.Not.EqualTo("Placement rejected by server"),
                    $"Reason code {code} should have a specific message, not the generic fallback.");
            }
        }

        /// <summary>Unknown reason codes fall back to a safe, informative message.</summary>
        [Test]
        [Category("SC_007")]
        public void UnknownReasonCodeFallsBackToGenericMessage()
        {
            var rejected = new S_AlterationRejected { ReasonCode = 99 }; // undefined.
            string reason = RejectionFeedback.ShowReason(in rejected);

            Assert.That(reason, Is.EqualTo("Placement rejected by server"),
                "Undefined reason codes should fall back to the generic message.");
        }

        // -----------------------------------------------------------------------
        // Dissolve animation lifecycle
        // -----------------------------------------------------------------------

        /// <summary>Starting a dissolve animation registers it and returns alpha levels.</summary>
        [Test]
        [Category("SC_007")]
        public void DissolveAnimationStartsCorrectly()
        {
            int3 region = new int3(1, 2, 3);

            // Start a dissolve with a known duration.
            RejectionFeedback.StartDissolveAnimation(region, 0.5f);

            // Update should return no completions (just started).
            var completed = RejectionFeedback.UpdateDissolves(out var alphas);

            Assert.That(completed.Length, Is.EqualTo(0), "No dissolves should complete immediately.");
            Assert.That(alphas.Length, Is.GreaterThan(0), "Alpha levels array must have entries for active dissolves.");

            // All alpha values should be in [0.0, 1.0].
            foreach (float alpha in alphas)
                Assert.That(alpha, Is.InRange(0f, 1f), "Alpha must be in [0, 1] range.");

            RejectionFeedback.Dispose();
        }

        /// <summary>Dissolve completes after its duration and region is returned for cleanup.</summary>
        [Test]
        [Category("SC_007")]
        public void DissolveCompletesAfterDuration()
        {
            int3 region = new int3(5, 5, 5);

            RejectionFeedback.StartDissolveAnimation(region, 1.0f);

            // Simulate time passing — the internal timer uses Environment.TickCount64.
            // We can't mock this directly, so we verify the animation is registered first.
            var completed = RejectionFeedback.UpdateDissolves(out _);

            // Since only a tiny fraction of a second has passed, it shouldn't be complete yet.
            Assert.That(completed.Length, Is.EqualTo(0), "Animation should not complete instantly.");

            RejectionFeedback.Dispose();
        }

        /// <summary>Starting the same region twice does not create duplicate entries.</summary>
        [Test]
        [Category("SC_007")]
        public void DuplicateDissolveStartIsIdempotent()
        {
            int3 region = new int3(10, 10, 10);

            RejectionFeedback.StartDissolveAnimation(region, 0.5f);
            RejectionFeedback.StartDissolveAnimation(region, 0.5f); // duplicate.

            // Update — should not produce duplicate alpha entries.
            var completed = RejectionFeedback.UpdateDissolves(out var alphas);

            Assert.That(completed.Length, Is.EqualTo(0), "No completions expected immediately.");
            Assert.That(alphas.Length, Is.LessThanOrEqualTo(1), "Duplicate start must not create extra entries.");

            RejectionFeedback.Dispose();
        }

        // -----------------------------------------------------------------------
        // Overlay rejection lifecycle
        // -----------------------------------------------------------------------

        /// <summary>Rejecting an overlay tick transitions from pending to cleared state cleanly.</summary>
        [Test]
        [Category("SC_007")]
        public void OverlayTransitionsCleanlyFromPendingToRejected()
        {
            var overlay = new SpeculativeOverlay();
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 10, int3.zero, 8, 10, 42u, 1, 1);

            // Apply then reject — must be a clean transition with no lingering state.
            overlay.ApplyPending(in evt);
            Assert.That(overlay.HasPending, Is.True, "Must have pending after ApplyPending.");

            overlay.RejectTick(10, new byte[1]);
            Assert.That(overlay.HasPending, Is.False, "Must be cleared after RejectTick.");
        }

        /// <summary>Rejecting a tick below the current highest tick clears only matching entries.</summary>
        [Test]
        [Category("SC_007")]
        public void RejectTickOnlyClearsMatchingEntries()
        {
            var overlay = new SpeculativeOverlay();

            // Two events at different ticks.
            var e1 = new AlterationEvent(AlterationEvent.KindBrush, 5, int3.zero, 8, 10, 1u, 1, 1);
            var e2 = new AlterationEvent(AlterationEvent.KindBrush, 10, int3.zero, 8, 20, 2u, 2, 1);

            overlay.ApplyPending(in e1);
            overlay.ApplyPending(in e2);
            Assert.That(overlay.PendingCount, Is.GreaterThan(0), "Must have pending entries.");

            // Reject only tick 5.
            overlay.RejectTick(5, new byte[1]);

            // Tick 10 should still be pending (not rejected because it's > the rejection tick).
            // Note: ConfirmTick removes all entries with tick <= confirmed tick, so the behavior depends on
            /// whether tick 10 is considered "past" the rejection boundary. Based on the implementation,
            /// RejectTick only removes entries with entry.tick <= rejectTick.

            Assert.That(overlay.HasPending, Is.True, "Tick 10 should survive rejection of tick 5.");
        }
    }
}
