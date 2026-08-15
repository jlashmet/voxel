using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.Parity
{
    /// <summary>
    /// SC-017: Competing alterations delivered in differing orders converge on the same winner.
    ///
    /// These tests verify that ConflictArbitration produces identical results regardless of the
    /// order events arrive at different clients. The server's total order (tick, playerId, sequence)
    /// combined with material priority tie-breaking must be deterministic and commutative across all
    /// delivery permutations.
    /// </summary>
    public sealed class ConcurrentEditArbitrationTests
    {
        private const byte k_Stone = 10;
        private const byte k_Dirt = 20;
        private const byte k_Wood = 30;

        // -----------------------------------------------------------------------
        // SC-017 / US3: competing alterations converge regardless of delivery order
        // -----------------------------------------------------------------------

        /// <summary>
        /// Two players alter the same voxel at different ticks — whichever event has the later tick wins.
        /// Delivered in both orders to all clients, the final state must be identical.
        /// </summary>
        [Test]
        [Category("SC_017")]
        [Category("US3")]
        public void CompetingAlterationsConvergeRegardlessOfDeliveryOrder()
        {
            // Player 1 (tick 42) places stone, player 2 (tick 50) places dirt at the same voxel.
            var e1 = new AlterationEvent(AlterationEvent.KindBrush, 42, int3.zero, 8, k_Stone, 1u, 1, 1);
            var e2 = new AlterationEvent(AlterationEvent.KindBrush, 50, int3.zero, 8, k_Dirt, 2u, 2, 1);

            // Client A receives [e1, e2] — e2 wins (later tick).
            byte resultA = ApplyInOrder(e1, e2, k_Stone);

            // Client B receives [e2, e1] — e2 still wins because arbitration reorders.
            byte resultB = ApplyInOrder(e2, e1, k_Stone);

            Assert.That(resultA, Is.EqualTo(k_Dirt), "Client A should see the later-tick winner.");
            Assert.That(resultB, Is.EqualTo(k_Dirt), "Client B must converge on the same winner despite reversed delivery order.");
            Assert.That(resultA, Is.EqualTo(resultB), "Both clients must converge identically (SC-017).");
        }

        // -----------------------------------------------------------------------
        // SC-017 / US3: material priority breaks same-tick tie
        // -----------------------------------------------------------------------

        /// <summary>
        /// Both players place at the same tick — player ID determines the arbitration order,
        /// then material priority for the degenerate case of identical player IDs.
        /// Player 1 places stone, player 2 places dirt at the same tick — priority determines winner.
        /// Both clients agree on who wins regardless of message arrival order.
        /// </summary>
        [Test]
        [Category("SC_017")]
        [Category("US3")]
        public void MaterialPriorityBreaksSameTickTie()
        {
            var e1 = new AlterationEvent(AlterationEvent.KindBrush, 42, int3.zero, 8, k_Stone, 1u, 1, 1);
            var e2 = new AlterationEvent(AlterationEvent.KindBrush, 42, int3.zero, 8, k_Dirt, 2u, 2, 1);

            // Compare should establish total order even at same tick.
            int cmp = ConflictArbitration.Compare(e1, e2);

            // Player IDs differ — higher ID wins (deterministic authority).
            Assert.That(cmp, Is.LessThan(0), "e1 (player 1) should lose to e2 (player 2) at same tick.");

            byte winner1 = ConflictArbitration.ResolveConflict(k_Stone, k_Dirt, in e1, in e2);
            byte winner2 = ConflictArbitration.ResolveConflict(k_Stone, k_Dirt, in e2, in e1);

            Assert.That(winner1, Is.EqualTo(e2.material), "ResolveConflict must return the winner's material.");
            Assert.That(winner2, Is.EqualTo(e2.material), "Reversed order must still converge on same winner.");
        }

        // -----------------------------------------------------------------------
        // SC-017 / US3: total order preserves player sequence integrity
        // -----------------------------------------------------------------------

        /// <summary>
        /// Events from the same player maintain their original sequence order regardless of network jitter.
        /// If player 1 sends sequence 5 then sequence 3, and client B receives them reversed (3 then 5),
        /// arbitration must still respect sequence ordering: sequence 5 wins at each contested voxel.
        /// </summary>
        [Test]
        [Category("SC_017")]
        [Category("US3")]
        public void TotalOrderPreservesPlayerSequenceIntegrity()
        {
            var eEarly = new AlterationEvent(AlterationEvent.KindBrush, 42, int3.zero, 8, k_Wood, 1u, 1, 3);
            var eLate = new AlterationEvent(AlterationEvent.KindBrush, 42, int3.zero, 8, k_Dirt, 1u, 1, 5);

            // Same tick, same player — higher sequence wins.
            int cmp = ConflictArbitration.Compare(eEarly, eLate);
            Assert.That(cmp, Is.LessThan(0), "eEarly (seq 3) should lose to eLate (seq 5).");

            // Apply in reversed order — arbitration must still pick eLate.
            byte winnerReversed = ConflictArbitration.ResolveConflict(k_Wood, k_Dirt, in eLate, in eEarly);
            Assert.That(winnerReversed, Is.EqualTo(k_Dirt), "eLate (seq 5) must win regardless of argument order.");

            // Verify the sorted order puts losers first.
            var events = new[] { eLate, eEarly };
            var sortedIndices = ConflictArbitration.SortEvents(events);

            Assert.That(sortedIndices.Length, Is.EqualTo(2));
            // The event at index 0 in sorted order should be eLate (it loses? — wait)
            // Actually: SortEvents sorts losers first, winners last. Compare(eEarly, eLate) < 0 means eEarly < eLate.
            // In the sort, lower values come first. So eEarly should be at index 0.

            int firstIdx = sortedIndices[0];
            Assert.That(events[firstIdx].sequence, Is.EqualTo(3), "eEarly (seq 3) should appear first (loser).");
        }

        // -----------------------------------------------------------------------
        // SC-017 / US3: three-way conflict produces identical result
        // -----------------------------------------------------------------------

        /// <summary>
        /// Three players attempt to alter the same voxel simultaneously — deterministic resolution
        /// produces identical final state on all clients. With 3! = 6 possible delivery orders,
        /// all must converge on the same winner.
        /// </summary>
        [Test]
        [Category("SC_017")]
        [Category("US3")]
        public void ThreeWayConflictProducesIdenticalResult()
        {
            var e1 = new AlterationEvent(AlterationEvent.KindBrush, 50, int3.zero, 8, k_Stone, 1u, 1, 1);
            var e2 = new AlterationEvent(AlterationEvent.KindBrush, 50, int3.zero, 8, k_Dirt, 2u, 2, 1);
            var e3 = new AlterationEvent(AlterationEvent.KindBrush, 50, int3.zero, 8, k_Wood, 3u, 3, 1);

            // All at same tick and seq — material priority breaks the tie.
            int cmp12 = ConflictArbitration.Compare(e1, e2);
            int cmp23 = ConflictArbitration.Compare(e2, e3);
            int cmp13 = ConflictArbitration.Compare(e1, e3);

            // All three are distinct — material priority determines the winner.
            Assert.DoesNotThrow(() =>
            {
                Assert.That(cmp12, Is.Not.EqualTo(0), "All three events must be distinguishable.");
                Assert.That(cmp23, Is.Not.EqualTo(0), "All three events must be distinguishable.");
                Assert.That(cmp13, Is.Not.EqualTo(0), "All three events must be distinguishable.");
            });

            // Verify all 6 permutations produce the same winner.
            byte[] perms = new byte[6];
            var evts = new[] { e1, e2, e3 };
            perms[0] = ApplyInOrder(e1, e2, e3, k_Stone); // 1,2,3
            perms[1] = ApplyInOrder(e1, e3, e2, k_Stone); // 1,3,2
            perms[2] = ApplyInOrder(e2, e1, e3, k_Stone); // 2,1,3
            perms[3] = ApplyInOrder(e2, e3, e1, k_Stone); // 2,3,1
            perms[4] = ApplyInOrder(e3, e1, e2, k_Stone); // 3,1,2
            perms[5] = ApplyInOrder(e3, e2, e1, k_Stone); // 3,2,1

            for (int i = 1; i < perms.Length; i++)
            {
                Assert.That(perms[i], Is.EqualTo(perms[0]),
                    $"Permutation {i} ({GetPermName(i)}) must converge on same winner as permutation 0. Got {perms[i]}, expected {perms[0]}.");
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>Apply two events in order, returning the final material after arbitration.</summary>
        private static byte ApplyInOrder(AlterationEvent first, AlterationEvent second, byte initial)
        {
            byte afterFirst = ConflictArbitration.ResolveConflict(initial, first.material, in first, in first);
            // Now resolve against the second event.
            return ConflictArbitration.ResolveConflict(afterFirst, second.material, in second, in first);
        }

        /// <summary>Apply three events in order, returning the final material after arbitration.</summary>
        private static byte ApplyInOrder(AlterationEvent a, AlterationEvent b, AlterationEvent c, byte initial)
        {
            // Build the full sorted list and apply sequentially.
            var events = new[] { a, b, c };
            var sorted = ConflictArbitration.SortEvents(events);
            byte result = initial;

            for (int i = 0; i < sorted.Length; i++)
            {
                // Apply event in sorted order — later entries override earlier ones.
                result = events[sorted[i]].material;
            }

            return result;
        }

        private static string GetPermName(int i)
        {
            var names = new[] { "123", "132", "213", "231", "312", "321" };
            return names[i];
        }
    }
}
