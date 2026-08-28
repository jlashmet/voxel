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

            var planningBoard = new ChainCombatBoard();
            var tacticalAi = new ChainEnemyTacticalAI(planningBoard);
            Assert.That(tacticalAi.PlannedRound, Is.EqualTo(planningBoard.Round));
            Assert.That(tacticalAi.Intents.Count, Is.EqualTo(3),
                "The migrated tactical planner must still deterministically commit one intent for each initial enemy.");
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
