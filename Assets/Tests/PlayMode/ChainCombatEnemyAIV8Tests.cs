using System.Collections;
using System.Reflection;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatEnemyAIV8Tests
    {
        [UnityTest]
        public IEnumerator CurrentCascadeLabBootsWithEnemyAIStack()
        {
            var root = new GameObject("Cascade Lab V8 Test Root");
            root.AddComponent<ChainCombatLabController>();
            root.AddComponent<ChainCombatSetupActionsPanel>();
            root.AddComponent<ChainCombatActivationOverlay>();
            root.AddComponent<ChainCombatEventMarker>();
            root.AddComponent<ChainCombatMotionPlayback>();
            root.AddComponent<ChainEnemyIntentOverlay>();

            yield return null;

            Assert.That(GameObject.Find("Chain Combat Lab Camera"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Combat Lab Light"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Combat Lab Visuals"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Stephen"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Ogre"), Is.Not.Null);
            Assert.That(root.GetComponent<ChainEnemyIntentOverlay>(), Is.Not.Null);
            Assert.That(root.GetComponent<ChainCombatSetupActionsPanel>(), Is.Not.Null);

            Object.Destroy(root);
            yield return null;
        }

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
            var reservations = new ChainReactionReservationCoordinator(board);
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

            Assert.That(reservations.TryReserve(3), Is.True, reservations.LastMessage,
                "P3 must be able to reserve an enemy-created collision even though P3 is already Ready.");
            Assert.That(reservations.TryClaim(madeline.Id, ChainReactionAbility.Repulse), Is.True, reservations.LastMessage,
                "The playable reservation layer must allow a Ready player to take over the enemy-created collision.");
            Assert.That(board.TryRepulse(madeline.Id, ogre.Id, new GridPos(0, 4)), Is.True, board.LastMessage);
            reservations.Synchronize();

            while (board.PendingReaction != null)
                Assert.That(reservations.TryPass(), Is.True, reservations.LastMessage);

            Assert.That(readiness.TryAdvanceRound(), Is.True, readiness.LastMessage);
            Assert.That(board.Round, Is.EqualTo(2));
            Assert.That(readiness.EnemyPhaseActive, Is.False);

            yield return null;
        }

        [UnityTest]
        public IEnumerator GoblinPrefersShoveIntoTreeAndReadyGromCanTurnItIntoTimber()
        {
            var board = new ChainCombatBoard();
            var reservations = new ChainReactionReservationCoordinator(board);
            ChainUnitState grom = Find(board, ChainRecruitKind.Grom);
            ChainUnitState goblinB = FindByName(board, "Goblin B");
            ChainTreeState tree = FindTree(board, new GridPos(11, 8));

            SetPosition(goblinB, new GridPos(9, 8));
            SetPosition(grom, new GridPos(10, 8));

            var readiness = new ChainRoundReadinessCoordinator(board);
            readiness.EnemyAI.PlanRound();
            ChainEnemyIntent goblinIntent = FindIntent(readiness, goblinB.Id);

            Assert.That(goblinIntent.Kind, Is.EqualTo(ChainEnemyIntentKind.Shove), goblinIntent.Description);
            Assert.That(goblinIntent.TargetUnitId, Is.EqualTo(grom.Id));
            Assert.That(goblinIntent.Direction, Is.EqualTo(new GridPos(1, 0)));

            SetEveryoneReady(readiness);
            Assert.That(readiness.TryAdvanceRound(), Is.True, readiness.LastMessage);

            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.TreeImpact));
            Assert.That(board.PendingReaction.PrimaryUnitId, Is.EqualTo(grom.Id));
            Assert.That(board.PendingReaction.TreeId, Is.EqualTo(tree.Id));
            Assert.That(readiness.EnemyPhaseActive, Is.True);

            Assert.That(reservations.TryReserve(4), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryClaim(grom.Id, ChainReactionAbility.Timber), Is.True, reservations.LastMessage,
                "Ready P4 must be able to reserve and exploit the tree impact the enemy created.");
            Assert.That(board.TryTimber(grom.Id, new GridPos(13, 8)), Is.True, board.LastMessage);
            reservations.Synchronize();
            Assert.That(tree.Standing, Is.False);

            while (board.PendingReaction != null)
                Assert.That(reservations.TryPass(), Is.True, reservations.LastMessage);

            Assert.That(readiness.TryAdvanceRound(), Is.True, readiness.LastMessage);
            Assert.That(board.Round, Is.EqualTo(2));

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

        private static ChainUnitState FindByName(ChainCombatBoard board, string name)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Name == name) return board.Units[i];
            Assert.Fail($"Could not find unit {name}.");
            return null;
        }

        private static ChainTreeState FindTree(ChainCombatBoard board, GridPos position)
        {
            for (int i = 0; i < board.Trees.Count; i++)
                if (board.Trees[i].Position.Equals(position)) return board.Trees[i];
            Assert.Fail($"Could not find tree at {position}.");
            return null;
        }

        private static void SetPosition(ChainUnitState unit, GridPos position)
        {
            PropertyInfo property = typeof(ChainUnitState).GetProperty(
                "Position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            property.SetValue(unit, position);
        }
    }
}
