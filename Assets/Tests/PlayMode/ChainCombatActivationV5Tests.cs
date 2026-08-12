using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatActivationV5Tests
    {
        [UnityTest]
        public IEnumerator ActiveRecruitGetsMoveAndActionInEitherOrder()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.GetActiveRecruitId(1), Is.EqualTo(0));
            Assert.That(board.TryMove(stephen.Id, new GridPos(2, 3)), Is.True, board.LastMessage);
            Assert.That(board.GetActiveRecruitId(1), Is.EqualTo(stephen.Id));
            Assert.That(stephen.MoveSpent, Is.True);
            Assert.That(stephen.ActionSpent, Is.False);

            Assert.That(board.TryMove(stephen.Id, new GridPos(2, 2)), Is.False, "The active recruit gets only one reposition.");
            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(stephen.ActionSpent, Is.True);

            yield return null;
        }

        [UnityTest]
        public IEnumerator BenchRecruitStillClaimsReactionAfterAnotherRecruitActivated()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState brutus = Find(board, ChainRecruitKind.Brutus);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.GetActiveRecruitId(1), Is.EqualTo(stephen.Id));

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

            Assert.That(board.TryPlaceAmplifier(mira.Id, new GridPos(2, 1)), Is.False,
                "A second recruit in the same player group must not gain a proactive action this round.");
            Assert.That(mira.ActionSpent, Is.False);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(madeline.Id),
                "A rejected action must not steal the player's active-recruit ownership.");

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
            Assert.That(mira.ActionSpent, Is.False);

            Assert.That(board.TryConverge(madeline.Id, ogre.Id, goblinA.Id), Is.True, board.LastMessage);
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(madeline.Id));

            Assert.That(board.EndRound(), Is.True, board.LastMessage);
            Assert.That(board.Round, Is.EqualTo(2));
            Assert.That(board.GetActiveRecruitId(3), Is.EqualTo(0));
            Assert.That(madeline.MoveSpent, Is.False);
            Assert.That(madeline.ActionSpent, Is.False);
            Assert.That(madeline.ReactionSpent, Is.False);
            Assert.That(mira.MoveSpent, Is.False);
            Assert.That(mira.ActionSpent, Is.False);
            Assert.That(mira.ReactionSpent, Is.False);

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
