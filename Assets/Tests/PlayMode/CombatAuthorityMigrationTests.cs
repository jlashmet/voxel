using System;
using System.Reflection;
using MountingForce.CombatPrototype;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CombatAuthorityMigrationTests
    {
        [Test]
        public void MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency()
        {
            Assembly authorityAssembly = typeof(ChainCombatBoard).Assembly;
            Assert.That(authorityAssembly.GetName().Name, Is.EqualTo("Game.Combat.Runtime"));

            Type[] authoritativeTypes =
            {
                typeof(CombatBoard),
                typeof(ChainCombatBoard),
                typeof(ChainExecutionPlan),
                typeof(ChainReactionReservationCoordinator),
                typeof(ChainRoundReadinessCoordinator),
                typeof(ChainEnemyTacticalAI)
            };

            for (int i = 0; i < authoritativeTypes.Length; i++)
            {
                Assert.That(authoritativeTypes[i].Assembly, Is.SameAs(authorityAssembly),
                    authoritativeTypes[i].Name + " must be owned by Game.Combat.Runtime rather than the lab assembly.");
            }

            AssemblyName[] references = authorityAssembly.GetReferencedAssemblies();
            for (int i = 0; i < references.Length; i++)
            {
                string dependency = references[i].Name ?? string.Empty;
                Assert.That(dependency, Is.Not.EqualTo("MountingForce.CombatPrototype"),
                    "Production combat cannot depend back on the lab assembly.");
                Assert.That(dependency, Does.Not.Contain("InputSystem"),
                    "Combat simulation must consume semantic Game.Input data, never Unity device APIs.");
                Assert.That(dependency, Does.Not.StartWith("UnityEngine"),
                    "Authoritative combat rules must remain engine independent.");
            }

            var board = new ChainCombatBoard();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen, CombatTeam.Friendly);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre, CombatTeam.Enemy);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon, CombatTeam.Friendly);

            Assert.That(stephen, Is.Not.Null);
            Assert.That(ogre, Is.Not.Null);
            Assert.That(weldon, Is.Not.Null);
            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Airborne));
            Assert.That(ogre.Airborne, Is.True);

            var reservations = new ChainReactionReservationCoordinator(board);
            Assert.That(reservations.TryReserve(weldon.CommandGroup), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryClaim(weldon.Id, ChainReactionAbility.Crosswind), Is.True, reservations.LastMessage);
            Assert.That(board.PendingReaction.ClaimedByUnitId, Is.EqualTo(weldon.Id),
                "Reservation/claim ownership must still be enforced by the migrated production authority.");

            AssertDeterministicPlanReplay();
            AssertDeterministicEnemyPlanning();
        }

        private static void AssertDeterministicPlanReplay()
        {
            var seed = new ChainCombatBoard();
            ChainUnitState stephen = Find(seed, ChainRecruitKind.Stephen, CombatTeam.Friendly);
            ChainUnitState weldon = Find(seed, ChainRecruitKind.Weldon, CombatTeam.Friendly);
            ChainUnitState ogre = Find(seed, ChainRecruitKind.Ogre, CombatTeam.Enemy);

            var plan = new ChainExecutionPlan();
            plan.Add(ChainPlannedAction.Uppercut(stephen.CommandGroup, stephen.Id, ogre.Id));
            plan.Add(ChainPlannedAction.React(
                weldon.CommandGroup,
                weldon.Id,
                ChainReactionAbility.Crosswind,
                ChainReactionKind.Airborne,
                0,
                new GridPos(10, 4)));

            ChainExecutionPreview first = ChainExecutionPlanSimulator.Simulate(new ChainCombatBoard(), plan.Actions);
            ChainExecutionPreview second = ChainExecutionPlanSimulator.Simulate(new ChainCombatBoard(), plan.Actions);

            Assert.That(first.HasFailure, Is.False, first.FailureMessage);
            Assert.That(second.HasFailure, Is.False, second.FailureMessage);
            Assert.That(first.ExecutedActionCount, Is.EqualTo(second.ExecutedActionCount));
            AssertBoardsEquivalent(first.FinalBoard, second.FinalBoard);
        }

        private static void AssertDeterministicEnemyPlanning()
        {
            var firstBoard = new ChainCombatBoard();
            var secondBoard = new ChainCombatBoard();
            var first = new ChainEnemyTacticalAI(firstBoard);
            var second = new ChainEnemyTacticalAI(secondBoard);

            Assert.That(first.PlannedRound, Is.EqualTo(firstBoard.Round));
            Assert.That(second.PlannedRound, Is.EqualTo(secondBoard.Round));
            Assert.That(first.Intents.Count, Is.EqualTo(3));
            Assert.That(second.Intents.Count, Is.EqualTo(first.Intents.Count));

            for (int i = 0; i < first.Intents.Count; i++)
            {
                ChainEnemyIntent a = first.Intents[i];
                ChainEnemyIntent b = second.Intents[i];
                Assert.That(a.EnemyId, Is.EqualTo(b.EnemyId));
                Assert.That(a.Kind, Is.EqualTo(b.Kind));
                Assert.That(a.TargetUnitId, Is.EqualTo(b.TargetUnitId));
                Assert.That(a.Direction, Is.EqualTo(b.Direction));
                Assert.That(a.Description, Is.EqualTo(b.Description));
            }
        }

        private static void AssertBoardsEquivalent(ChainCombatBoard a, ChainCombatBoard b)
        {
            Assert.That(a.Round, Is.EqualTo(b.Round));
            Assert.That(a.CurrentCascadeSteps, Is.EqualTo(b.CurrentCascadeSteps));
            Assert.That(a.CurrentCascadePlayers, Is.EqualTo(b.CurrentCascadePlayers));
            Assert.That(a.CurrentHandoffs, Is.EqualTo(b.CurrentHandoffs));
            Assert.That(a.Units.Count, Is.EqualTo(b.Units.Count));

            for (int i = 0; i < a.Units.Count; i++)
            {
                ChainUnitState left = a.Units[i];
                ChainUnitState right = b.Units[i];
                Assert.That(left.Id, Is.EqualTo(right.Id));
                Assert.That(left.Kind, Is.EqualTo(right.Kind));
                Assert.That(left.Team, Is.EqualTo(right.Team));
                Assert.That(left.CommandGroup, Is.EqualTo(right.CommandGroup));
                Assert.That(left.Position, Is.EqualTo(right.Position));
                Assert.That(left.Hp, Is.EqualTo(right.Hp));
                Assert.That(left.IsAlive, Is.EqualTo(right.IsAlive));
                Assert.That(left.Airborne, Is.EqualTo(right.Airborne));
                Assert.That(left.ActionSpent, Is.EqualTo(right.ActionSpent));
                Assert.That(left.ReactionSpent, Is.EqualTo(right.ReactionSpent));
            }

            Assert.That(a.Trees.Count, Is.EqualTo(b.Trees.Count));
            for (int i = 0; i < a.Trees.Count; i++)
            {
                ChainTreeState left = a.Trees[i];
                ChainTreeState right = b.Trees[i];
                Assert.That(left.Id, Is.EqualTo(right.Id));
                Assert.That(left.Position, Is.EqualTo(right.Position));
                Assert.That(left.Standing, Is.EqualTo(right.Standing));
                Assert.That(left.FallDirection, Is.EqualTo(right.FallDirection));
            }

            if (a.PendingReaction == null || b.PendingReaction == null)
            {
                Assert.That(a.PendingReaction, Is.EqualTo(b.PendingReaction));
                return;
            }

            Assert.That(a.PendingReaction.Kind, Is.EqualTo(b.PendingReaction.Kind));
            Assert.That(a.PendingReaction.PrimaryUnitId, Is.EqualTo(b.PendingReaction.PrimaryUnitId));
            Assert.That(a.PendingReaction.SecondaryUnitId, Is.EqualTo(b.PendingReaction.SecondaryUnitId));
            Assert.That(a.PendingReaction.TreeId, Is.EqualTo(b.PendingReaction.TreeId));
            Assert.That(a.PendingReaction.IsClaimed, Is.EqualTo(b.PendingReaction.IsClaimed));
            Assert.That(a.PendingReaction.ClaimedByUnitId, Is.EqualTo(b.PendingReaction.ClaimedByUnitId));
            Assert.That(a.PendingReaction.ClaimedByCommandGroup, Is.EqualTo(b.PendingReaction.ClaimedByCommandGroup));
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind, CombatTeam team)
        {
            for (int i = 0; i < board.Units.Count; i++)
            {
                ChainUnitState unit = board.Units[i];
                if (unit.Kind == kind && unit.Team == team && unit.IsAlive) return unit;
            }

            return null;
        }
    }
}
