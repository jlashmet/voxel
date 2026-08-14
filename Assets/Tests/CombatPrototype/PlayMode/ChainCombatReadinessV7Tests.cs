using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatReadinessV7Tests
    {
        [UnityTest]
        public IEnumerator ReadyBlocksProactivePlayButNotReactions()
        {
            var board = new ChainCombatBoard();
            var ready = new ChainRoundReadinessCoordinator(board);
            var reservations = new ChainReactionReservationCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState brutus = Find(board, ChainRecruitKind.Brutus);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(ready.TrySetReady(1, true), Is.True, ready.LastMessage);
            Assert.That(ready.CanUseProactive(1), Is.False);

            // Ready is a coordination gate used by the application/server command layer, not a reaction lock.
            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True,
                "The physics board remains coordination-agnostic; the app layer is responsible for gating proactive commands.");
            Assert.That(reservations.TryReserve(1), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryClaim(brutus.Id, ChainReactionAbility.CatchThrow), Is.True, reservations.LastMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemyPhaseRequiresEveryLivingPlayerAndNoPendingEvent()
        {
            var board = new ChainCombatBoard();
            var ready = new ChainRoundReadinessCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(ready.TrySetReady(1, true), Is.True);
            Assert.That(ready.TrySetReady(2, true), Is.True);
            Assert.That(ready.TrySetReady(3, true), Is.True);
            Assert.That(ready.TryAdvanceRound(), Is.False);
            StringAssert.Contains("Every living player", ready.LastMessage);

            Assert.That(ready.TrySetReady(4, true), Is.True);
            Assert.That(ready.AllLivingPlayersReady, Is.True);

            // A newly-created physical decision blocks the enemy phase even though everyone is Ready.
            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(ready.TryAdvanceRound(), Is.False);
            StringAssert.Contains("physical event", ready.LastMessage.ToLowerInvariant());

            Assert.That(board.PassReaction(), Is.True, board.LastMessage);
            while (board.PendingReaction != null)
                Assert.That(board.PassReaction(), Is.True, board.LastMessage);

            Assert.That(ready.TryAdvanceRound(), Is.True, ready.LastMessage);
            Assert.That(board.Round, Is.EqualTo(2));
            Assert.That(ready.IsReady(1), Is.False);
            Assert.That(ready.IsReady(2), Is.False);
            Assert.That(ready.IsReady(3), Is.False);
            Assert.That(ready.IsReady(4), Is.False);

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerMayReadyWithoutActivatingAndMayUnreadyBeforeEnemyPhase()
        {
            var board = new ChainCombatBoard();
            var ready = new ChainRoundReadinessCoordinator(board);

            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(0));
            Assert.That(ready.TrySetReady(3, true), Is.True, ready.LastMessage);
            Assert.That(ready.IsReady(3), Is.True);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(0),
                "Ready is allowed to mean 'I pass my proactive activation this round.'");

            Assert.That(ready.TrySetReady(3, false), Is.True, ready.LastMessage);
            Assert.That(ready.CanUseProactive(3), Is.True);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReadyPlayersCanStillOwnLaterReactionDecisions()
        {
            var board = new ChainCombatBoard();
            var ready = new ChainRoundReadinessCoordinator(board);
            var reservations = new ChainReactionReservationCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(ready.TrySetReady(2, true), Is.True);
            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);

            Assert.That(reservations.TryReserve(2), Is.True, reservations.LastMessage,
                "Ready must not remove a player from reaction coordination.");
            Assert.That(reservations.TryClaim(weldon.Id, ChainReactionAbility.Crosswind), Is.True, reservations.LastMessage);
            Assert.That(ready.IsReady(2), Is.True, "Reacting does not reopen proactive play automatically.");

            yield return null;
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Kind == kind) return board.Units[i];
            Assert.Fail($"Could not find unit kind {kind}.");
            return null;
        }
    }
}
