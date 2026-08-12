using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatExecutionPlanV9Tests
    {
        [UnityTest]
        public IEnumerator ReorderingSetupAfterChainStarterMakesGhostStopOnOrderingConflict()
        {
            var board = new ChainCombatBoard();
            var plan = new ChainExecutionPlan();
            ChainUnitState mira = Find(board, ChainRecruitKind.Mira);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            int amplifier = plan.Add(ChainPlannedAction.Amplifier(mira.CommandGroup, mira.Id, new GridPos(4, 1)));
            int uppercut = plan.Add(ChainPlannedAction.Uppercut(stephen.CommandGroup, stephen.Id, ogre.Id));

            ChainExecutionPreview good = ChainExecutionPlanSimulator.Simulate(board, plan.Actions);
            Assert.That(good.HasFailure, Is.False, good.FailureMessage);
            Assert.That(good.FinalBoard.PendingReaction, Is.Not.Null);
            Assert.That(good.FinalBoard.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Airborne));

            Assert.That(plan.MoveRootAction(uppercut, 0), Is.True);
            ChainExecutionPreview bad = ChainExecutionPlanSimulator.Simulate(board, plan.Actions);

            Assert.That(bad.HasFailure, Is.True,
                "Uppercut now creates an unresolved event before Mira's setup action can execute.");
            Assert.That(bad.FailedPlanId, Is.EqualTo(amplifier));
            Assert.That(bad.FinalBoard.PendingReaction, Is.Not.Null,
                "The reordered ghost should stop with Stephen's unresolved physical event still on the board.");
            Assert.That(bad.FinalBoard.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Airborne));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraggingRootKeepsItsReactionContinuationAttached()
        {
            var board = new ChainCombatBoard();
            var plan = new ChainExecutionPlan();
            ChainUnitState mira = Find(board, ChainRecruitKind.Mira);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            plan.Add(ChainPlannedAction.Amplifier(mira.CommandGroup, mira.Id, new GridPos(4, 1)));
            int uppercut = plan.Add(ChainPlannedAction.Uppercut(stephen.CommandGroup, stephen.Id, ogre.Id));
            plan.Add(ChainPlannedAction.React(
                weldon.CommandGroup, weldon.Id, ChainReactionAbility.Crosswind,
                ChainReactionKind.Airborne, 0, new GridPos(10, 4)));

            Assert.That(plan.MoveRootAction(uppercut, 0), Is.True);
            Assert.That(plan.Actions[0].Kind, Is.EqualTo(ChainPlannedActionKind.Uppercut));
            Assert.That(plan.Actions[1].Kind, Is.EqualTo(ChainPlannedActionKind.Reaction),
                "The reaction row is causally nested under Uppercut and must move with the root block.");
            Assert.That(plan.Actions[1].ReactionAbility, Is.EqualTo(ChainReactionAbility.Crosswind));
            Assert.That(plan.Actions[2].Kind, Is.EqualTo(ChainPlannedActionKind.PlaceAmplifier));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GhostCanPreviewFourPlayerAuthoredReactionChain()
        {
            var board = new ChainCombatBoard();
            var plan = new ChainExecutionPlan();
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState grom = Find(board, ChainRecruitKind.Grom);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainUnitState goblinA = FindEnemyByName(board, "Goblin A");

            plan.Add(ChainPlannedAction.Uppercut(stephen.CommandGroup, stephen.Id, ogre.Id));
            plan.Add(ChainPlannedAction.React(
                weldon.CommandGroup, weldon.Id, ChainReactionAbility.Crosswind,
                ChainReactionKind.Airborne, 0, new GridPos(10, 4)));
            plan.Add(ChainPlannedAction.React(
                madeline.CommandGroup, madeline.Id, ChainReactionAbility.Repulse,
                ChainReactionKind.Collision, goblinA.Id, new GridPos(12, 4)));
            plan.Add(ChainPlannedAction.React(
                grom.CommandGroup, grom.Id, ChainReactionAbility.Timber,
                ChainReactionKind.TreeImpact, 0, new GridPos(13, 4)));

            ChainExecutionPreview preview = ChainExecutionPlanSimulator.Simulate(board, plan.Actions);

            Assert.That(preview.HasFailure, Is.False, preview.FailureMessage);
            Assert.That(preview.ExecutedActionCount, Is.EqualTo(4));
            Assert.That(preview.Frames.Count, Is.EqualTo(5), "Start frame plus one ghost frame per authored instruction.");
            Assert.That(preview.FinalBoard.PendingReaction, Is.Null);
            Assert.That(preview.FinalBoard.GetTree(1).Standing, Is.False,
                "The ghost should reach Grom's final Timber payoff, not merely preview the first launch.");
            Assert.That(preview.FinalBoard.LastCascadeSteps, Is.EqualTo(4));
            Assert.That(preview.FinalBoard.LastCascadePlayers, Is.EqualTo(4));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompoundDragIsOneUndoableEdit()
        {
            var board = new ChainCombatBoard();
            var plan = new ChainExecutionPlan();
            ChainUnitState mira = Find(board, ChainRecruitKind.Mira);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            int amplifier = plan.Add(ChainPlannedAction.Amplifier(mira.CommandGroup, mira.Id, new GridPos(4, 1)));
            int uppercut = plan.Add(ChainPlannedAction.Uppercut(stephen.CommandGroup, stephen.Id, ogre.Id));

            plan.BeginCompoundEdit();
            Assert.That(plan.MoveRootAction(uppercut, 0), Is.True);
            plan.EndCompoundEdit();
            Assert.That(plan.Actions[0].PlanId, Is.EqualTo(uppercut));

            Assert.That(plan.Undo(), Is.True);
            Assert.That(plan.Actions[0].PlanId, Is.EqualTo(amplifier),
                "A drag should undo as one plan edit, not as every intermediate hover position.");
            Assert.That(plan.Redo(), Is.True);
            Assert.That(plan.Actions[0].PlanId, Is.EqualTo(uppercut));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlannerBootsWithGhostBoardAndVisualLayer()
        {
            var root = new GameObject("Cascade Lab V9 Planner Test Root");
            root.AddComponent<ChainCombatLabController>();
            ChainExecutionPlanner planner = root.AddComponent<ChainExecutionPlanner>();

            yield return null;

            Assert.That(planner.Plan, Is.Not.Null);
            Assert.That(planner.Preview, Is.Not.Null);
            Assert.That(planner.Preview.Frames.Count, Is.EqualTo(1));
            Assert.That(GameObject.Find("Chain Plan Ghost Visuals"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Plan Ghost - Stephen"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Plan Ghost - Ogre"), Is.Not.Null);

            Object.Destroy(root);
            yield return null;
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
            {
                ChainUnitState unit = board.Units[i];
                if (unit.Kind == kind) return unit;
            }
            Assert.Fail("Could not find recruit kind " + kind);
            return null;
        }

        private static ChainUnitState FindEnemyByName(ChainCombatBoard board, string name)
        {
            for (int i = 0; i < board.Units.Count; i++)
            {
                ChainUnitState unit = board.Units[i];
                if (unit.Team == CombatTeam.Enemy && unit.Name == name) return unit;
            }
            Assert.Fail("Could not find enemy " + name);
            return null;
        }
    }
}
