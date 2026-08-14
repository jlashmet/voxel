using System.Collections;
using System.Reflection;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatPlanApprovalV9Tests
    {
        private static readonly FieldInfo BoardField = typeof(ChainCombatLabController).GetField(
            "_board", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ReadinessField = typeof(ChainCombatLabController).GetField(
            "_roundReadiness", BindingFlags.Instance | BindingFlags.NonPublic);

        [UnityTest]
        public IEnumerator EditingAnApprovedPlanRevokesEveryReadyApproval()
        {
            var root = new GameObject("Plan Approval Revision Test Root");
            ChainCombatLabController controller = root.AddComponent<ChainCombatLabController>();
            ChainExecutionPlanner planner = root.AddComponent<ChainExecutionPlanner>();
            ChainPlanApprovalCoordinator approvals = root.AddComponent<ChainPlanApprovalCoordinator>();

            yield return null;

            ChainCombatBoard board = GetBoard(controller);
            ChainRoundReadinessCoordinator readiness = GetReadiness(controller);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);

            planner.Plan.Add(ChainPlannedAction.Move(stephen.CommandGroup, stephen.Id, new GridPos(2, 3)));
            approvals.SynchronizeNow();
            int approvedRevision = planner.Plan.Revision;

            SetEveryoneReady(readiness);
            approvals.SynchronizeNow();
            Assert.That(planner.TeamReadyToExecute, Is.True,
                "All living players should be able to approve the exact ghost-plan revision they inspected.");

            planner.Plan.Add(ChainPlannedAction.Move(weldon.CommandGroup, weldon.Id, new GridPos(4, 1)));
            approvals.SynchronizeNow();

            Assert.That(planner.TeamReadyToExecute, Is.False,
                "Changing the shared future after approval must make it impossible to execute using stale Ready flags.");
            for (int group = 1; group <= 4; group++)
                Assert.That(readiness.IsReady(group), Is.False, $"P{group}'s approval should be revoked when the plan changes.");
            Assert.That(approvals.LastInvalidatedRevision, Is.EqualTo(approvedRevision));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CommittingApprovedPlanPreservesReadyForEnemyHandoff()
        {
            var root = new GameObject("Plan Approval Execution Test Root");
            ChainCombatLabController controller = root.AddComponent<ChainCombatLabController>();
            ChainExecutionPlanner planner = root.AddComponent<ChainExecutionPlanner>();
            ChainPlanApprovalCoordinator approvals = root.AddComponent<ChainPlanApprovalCoordinator>();

            yield return null;

            ChainCombatBoard board = GetBoard(controller);
            ChainRoundReadinessCoordinator readiness = GetReadiness(controller);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);

            planner.Plan.Add(ChainPlannedAction.Move(stephen.CommandGroup, stephen.Id, new GridPos(2, 3)));
            approvals.SynchronizeNow();
            int committedRevision = planner.Plan.Revision;
            SetEveryoneReady(readiness);
            approvals.SynchronizeNow();

            Assert.That(ChainExecutionPlanSimulator.ExecuteAll(board, planner.Plan.Actions, out string result), Is.True, result);
            planner.Plan.ResetWithoutHistory();
            approvals.SynchronizeNow();

            Assert.That(approvals.LastCommittedRevision, Is.EqualTo(committedRevision));
            Assert.That(readiness.AllLivingPlayersReady, Is.True,
                "Executing the exact approved plan should preserve Ready so the enemy phase can immediately follow.");
            for (int group = 1; group <= 4; group++)
                Assert.That(readiness.IsReady(group), Is.True);

            Object.Destroy(root);
            yield return null;
        }

        private static ChainCombatBoard GetBoard(ChainCombatLabController controller)
        {
            Assert.That(BoardField, Is.Not.Null);
            var board = BoardField.GetValue(controller) as ChainCombatBoard;
            Assert.That(board, Is.Not.Null);
            return board;
        }

        private static ChainRoundReadinessCoordinator GetReadiness(ChainCombatLabController controller)
        {
            Assert.That(ReadinessField, Is.Not.Null);
            var readiness = ReadinessField.GetValue(controller) as ChainRoundReadinessCoordinator;
            Assert.That(readiness, Is.Not.Null);
            return readiness;
        }

        private static void SetEveryoneReady(ChainRoundReadinessCoordinator readiness)
        {
            for (int group = 1; group <= 4; group++)
                Assert.That(readiness.TrySetReady(group, true), Is.True, readiness.LastMessage);
            Assert.That(readiness.AllLivingPlayersReady, Is.True);
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Kind == kind) return board.Units[i];
            Assert.Fail("Could not find recruit kind " + kind);
            return null;
        }
    }
}
