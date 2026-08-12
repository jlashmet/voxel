using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatEnemyAIV8Tests
    {
        [UnityTest]
        public IEnumerator EnemyAIPlansFromBoardStateInsteadOfUsingAFixedScript()
        {
            var board = new ChainCombatBoard();
            var readiness = new ChainRoundReadinessCoordinator(board);

            Assert.That(readiness.EnemyIntents.Count, Is.EqualTo(3));
            for (int i = 0; i < readiness.EnemyIntents.Count; i++)
            {
                ChainEnemyIntent intent = readiness.EnemyIntents[i];
                ChainUnitState actor = board.GetUnit(intent.EnemyId);
                Assert.That(actor, Is.Not.Null);
                Assert.That(actor.Team, Is.EqualTo(CombatTeam.Enemy));
                Assert.That(intent.Description, Is.Not.Empty);
            }

            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainEnemyIntent ogreIntent = FindIntent(readiness, ogre.Id);
            Assert.That(ogreIntent.Kind, Is.EqualTo(ChainEnemyIntentKind.Attack),
                "The initial board has Stephen adjacent, so the tactical choice should be the immediately valuable attack.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator CommittedEnemyAttackCanBeDodgedAndDoesNotOmniscientlyRetarget()
        {
            var board = new ChainCombatBoard();
            var readiness = new ChainRoundReadinessCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            ChainEnemyIntent committed = FindIntent(readiness, ogre.Id);
            Assert.That(committed.Kind, Is.EqualTo(ChainEnemyIntentKind.Attack));
            Assert.That(committed.TargetUnitId, Is.EqualTo(stephen.Id));

            int hpBefore = stephen.Hp;
            Assert.That(board.TryMove(stephen.Id, new GridPos(2, 1)), Is.True, board.LastMessage);
            SetEveryoneReady(readiness);
            Assert.That(readiness.TryAdvanceRound(), Is.True, readiness.LastMessage);

            Assert.That(stephen.Hp, Is.EqualTo(hpBefore),
                "Stephen moved after seeing the committed intent. The ogre should miss instead of secretly picking a new target.");
            Assert.That(board.Round, Is.EqualTo(2));

            yield return null;
        }

        [UnityTest]
        public IEnumerator OgreChargeUsesNormalPhysicsAndPausesEnemyPhaseForPlayerReaction()
        {
            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryMove(stephen.Id, new GridPos(5, 4)), Is.True, board.LastMessage);

            var readiness = new ChainRoundReadinessCoordinator(board);
            readiness.EnemyAI.PlanRound();
            ChainEnemyIntent ogreIntent = FindIntent(readiness, ogre.Id);
            Assert.That(ogreIntent.Kind, Is.EqualTo(ChainEnemyIntentKind.Charge), ogreIntent.Description);
            Assert.That(ogreIntent.Direction, Is.EqualTo(new GridPos(1, 0)));

            SetEveryoneReady(readiness);
            Assert.That(readiness.TryAdvanceRound(), Is.True, readiness.LastMessage);

            Assert.That(board.PendingReaction, Is.Not.Null,
                "The enemy phase should pause when its own physical action creates a meaningful event.");
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Collision));
            Assert.That(board.PendingReaction.PrimaryUnitId, Is.EqualTo(ogre.Id));
            Assert.That(board.PendingReaction.SecondaryUnitId, Is.EqualTo(stephen.Id));
            Assert.That(readiness.EnemyPhaseActive, Is.True);
            Assert.That(board.Round, Is.EqualTo(1), "The round cannot finish while the enemy-created event is unresolved.");

            Assert.That(board.TryClaimReaction(madeline.Id, ChainReactionAbility.Repulse), Is.True, board.LastMessage,
                "A player who was already Ready must still be able to take over an enemy-created collision.");
            Assert.That(board.TryRepulse(madeline.Id, ogre.Id, new GridPos(0, 4)), Is.True, board.LastMessage);

            // If that reaction does not create another event, continuing the enemy phase should execute remaining committed intents
            // and advance the round. If it does create one, pass it first; both paths prove the AI and reaction machine interleave.
            while (board.PendingReaction != null)
                Assert.That(board.PassReaction(), Is.True, board.LastMessage);

            Assert.That(readiness.TryAdvanceRound(), Is.True, readiness.LastMessage);
            Assert.That(board.Round, Is.EqualTo(2));
            Assert.That(readiness.EnemyPhaseActive, Is.False);

            yield return null;
        }

        private static void SetEveryoneReady(ChainRoundReadinessCoordinator readiness)
        {
            for (int group = 1; group <= 4; group++)
                Assert.That(readiness.TrySetReady(group, true), Is.True, readiness.LastMessage);
            Assert.That(readiness.AllLivingPlayersReady, Is.True);
        }

        private static ChainEnemyIntent FindIntent(ChainRoundReadinessCoordinator readiness, int enemyId)
        {
            for (int i = 0; i < readiness.EnemyIntents.Count; i++)
                if (readiness.EnemyIntents[i].EnemyId == enemyId) return readiness.EnemyIntents[i];
            Assert.Fail($"No AI intent found for enemy #{enemyId}.");
            return null;
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
