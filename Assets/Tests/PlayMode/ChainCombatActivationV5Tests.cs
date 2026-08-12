using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatActivationV5Tests
    {
        [UnityTest]
        public IEnumerator ActiveRecruitGetsOneMoveAndOneProactiveAction()
        {
            var board = new ChainCombatBoard();
            ChainUnitState mira = Find(board, ChainRecruitKind.Mira);

            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(0));
            Assert.That(board.TryMove(mira.Id, new GridPos(2, 1)), Is.True, board.LastMessage);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(mira.Id));
            Assert.That(mira.MoveSpent, Is.True);
            Assert.That(mira.ActionSpent, Is.False, "Movement should not consume the active recruit's proactive action.");

            Assert.That(board.TryPlaceAmplifier(mira.Id, new GridPos(3, 1)), Is.True, board.LastMessage);
            Assert.That(mira.ActionSpent, Is.True);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(mira.Id));

            Assert.That(board.TryMove(mira.Id, new GridPos(2, 2)), Is.False,
                "The active recruit gets one reposition, not unlimited movement.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator SecondRecruitCannotTakeProactiveTurnButCanStillClaimReaction()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState brutus = Find(board, ChainRecruitKind.Brutus);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.GetActiveRecruitId(1), Is.EqualTo(stephen.Id));
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Airborne));

            // Brutus is not P1's active recruit, but the whole roster remains live as a reaction toolbox.
            Assert.That(board.TryClaimReaction(brutus.Id, ChainReactionAbility.CatchThrow), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction.ClaimedByUnitId, Is.EqualTo(brutus.Id));
            Assert.That(board.GetActiveRecruitId(1), Is.EqualTo(stephen.Id),
                "Claiming a reaction must not steal/change the player's proactive activation.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator DifferentRecruitInSamePlayerGroupIsRejectedForProactivePlay()
        {
            var board = new ChainCombatBoard();
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState mira = Find(board, ChainRecruitKind.Mira);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainUnitState goblinA = FindByName(board, "Goblin A");

            Assert.That(board.TryConverge(madeline.Id, ogre.Id, goblinA.Id), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction, Is.Null);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(madeline.Id));

            Assert.That(board.TryPlaceAmplifier(mira.Id, new GridPos(2, 1)), Is.False);
            StringAssert.Contains("Madeline", board.LastMessage);
            StringAssert.Contains("reactions", board.LastMessage.ToLowerInvariant());
            Assert.That(mira.ActionSpent, Is.False);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(madeline.Id));

            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedAttemptDoesNotCommitActivationAndNewRoundClearsIt()
        {
            var board = new ChainCombatBoard();
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState mira = Find(board, ChainRecruitKind.Mira);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainUnitState goblinA = FindByName(board, "Goblin A");

            // Mira's own occupied cell is not a legal construct target. A misclick should not lock P3 to Mira.
            Assert.That(board.TryPlaceAmplifier(mira.Id, mira.Position), Is.False);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(0));

            Assert.That(board.TryConverge(madeline.Id, ogre.Id, goblinA.Id), Is.True, board.LastMessage);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(madeline.Id));

            Assert.That(board.EndRound(), Is.True, board.LastMessage);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(0));
            Assert.That(madeline.ActionSpent, Is.False);
            Assert.That(madeline.MoveSpent, Is.False);
            Assert.That(mira.ActionSpent, Is.False);
            Assert.That(mira.MoveSpent, Is.False);

            yield return null;
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Kind == kind) return board.Units[i];
            Assert.Fail($"Could not find unit kind {kind}.");
            return null;
        }

        private static ChainUnitState FindByName(ChainCombatBoard board, string name)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Name == name) return board.Units[i];
            Assert.Fail($"Could not find unit {name}.");
            return null;
        }
    }
}
