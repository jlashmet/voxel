using System.Collections;
using System.Reflection;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatDemoV10Tests
    {
        [UnityTest]
        public IEnumerator GuidedShowcaseProducesFourPlayerEnvironmentalCascade()
        {
            var board = new ChainCombatBoard();
            var reservations = new ChainReactionReservationCoordinator(board);
            var scenario = new ChainCombatDemoScenario(board, reservations);

            Assert.That(scenario.StepIndex, Is.EqualTo(0));
            Assert.That(scenario.IsComplete, Is.False);

            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Airborne));
            Assert.That(board.CurrentCascadeSteps, Is.EqualTo(1));
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(1));
            yield return null;

            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Collision));
            Assert.That(board.CurrentCascadeSteps, Is.EqualTo(2));
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(2));
            Assert.That(board.CurrentHandoffs, Is.EqualTo(1));
            yield return null;

            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.TreeImpact));
            Assert.That(board.CurrentCascadeSteps, Is.EqualTo(3));
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(3));
            Assert.That(board.CurrentHandoffs, Is.EqualTo(2));
            yield return null;

            int treeId = board.PendingReaction.TreeId;
            ChainTreeState tree = board.GetTree(treeId);
            Assert.That(tree, Is.Not.Null);
            Assert.That(tree.Standing, Is.True);

            Assert.That(scenario.TryAdvance(), Is.True, scenario.LastMessage);
            Assert.That(scenario.IsComplete, Is.True);
            Assert.That(board.PendingReaction, Is.Null);
            Assert.That(tree.Standing, Is.False);
            Assert.That(tree.FallDirection, Is.EqualTo(new GridPos(-1, 0)));

            Assert.That(board.LastCascadeSteps, Is.EqualTo(4), "The demo should prove one deliberate causal step per player.");
            Assert.That(board.LastCascadePlayers, Is.EqualTo(4), "Every command group should contribute to the showcase cascade.");
            Assert.That(board.LastHandoffs, Is.EqualTo(3), "Four sequential player contributions should create three handoffs.");
            Assert.That(board.BestCascadeSteps, Is.GreaterThanOrEqualTo(4));
            Assert.That(board.BestCascadePlayers, Is.EqualTo(4));
            Assert.That(board.BestHandoffs, Is.GreaterThanOrEqualTo(3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayableDemoRuntimeStackExecutesGuidedCascadeEndToEnd()
        {
            var root = new GameObject("Combat Demo End-to-End Test Root");
            ChainCombatLabController controller = root.AddComponent<ChainCombatLabController>();
            root.AddComponent<ChainExecutionPlanner>();
            root.AddComponent<ChainPlanApprovalCoordinator>();
            root.AddComponent<ChainCombatActivationOverlay>();
            root.AddComponent<ChainCombatEventMarker>();
            root.AddComponent<ChainCombatMotionPlayback>();
            root.AddComponent<ChainEnemyIntentOverlay>();
            ChainCombatDemoGuide guide = root.AddComponent<ChainCombatDemoGuide>();

            yield return null;

            Assert.That(GameObject.Find("Chain Combat Lab Camera"), Is.Not.Null,
                "The playable demo must boot its actual camera.");
            Assert.That(GameObject.Find("Chain Combat Lab Light"), Is.Not.Null,
                "The playable demo must boot its actual lighting.");
            Assert.That(GameObject.Find("Chain Combat Lab Visuals"), Is.Not.Null,
                "The playable demo must create the live presentation root.");
            Assert.That(GameObject.Find("Chain Unit - Stephen"), Is.Not.Null);
            Assert.That(GameObject.Find("Chain Unit - Ogre"), Is.Not.Null);
            Assert.That(root.GetComponent<ChainExecutionPlanner>(), Is.Not.Null);
            Assert.That(root.GetComponent<ChainPlanApprovalCoordinator>(), Is.Not.Null);
            Assert.That(root.GetComponent<ChainCombatMotionPlayback>(), Is.Not.Null);
            Assert.That(root.GetComponent<ChainEnemyIntentOverlay>(), Is.Not.Null);
            Assert.That(guide, Is.Not.Null);

            ChainCombatBoard board = GetLiveBoard(controller);
            Assert.That(board, Is.Not.Null,
                "The test must operate on the controller-owned board used by the playable demo, not a separate test board.");

            guide.ResetGuidedDemo();
            yield return null;

            guide.AdvanceOneStep();
            yield return null;
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Airborne),
                "The runtime guide should create the same airborne handoff visible to normal play.");
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(1));

            guide.AdvanceOneStep();
            yield return null;
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Collision),
                "Weldon's runtime reaction must turn the airborne event into a live collision.");
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(2));
            Assert.That(board.CurrentHandoffs, Is.EqualTo(1));

            guide.AdvanceOneStep();
            yield return null;
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.TreeImpact),
                "Madeline's runtime reaction must make the environment the next live event.");
            Assert.That(board.CurrentCascadePlayers, Is.EqualTo(3));
            Assert.That(board.CurrentHandoffs, Is.EqualTo(2));

            int treeId = board.PendingReaction.TreeId;
            ChainTreeState tree = board.GetTree(treeId);
            GameObject treeVisual = GameObject.Find($"Chain Tree {treeId}");
            Assert.That(tree, Is.Not.Null);
            Assert.That(tree.Standing, Is.True);
            Assert.That(treeVisual, Is.Not.Null,
                "The impacted authoritative tree must have a corresponding live scene visual.");
            Assert.That(Vector3.Angle(treeVisual.transform.up, Vector3.up), Is.LessThan(20f),
                "The unresolved tree-impact marker may visibly shake the standing tree, but it must remain upright before Grom's finisher.");

            guide.AdvanceOneStep();
            yield return null;

            Assert.That(board.PendingReaction, Is.Null,
                "The guided runtime cascade should resolve cleanly instead of leaving a stuck event.");
            Assert.That(tree.Standing, Is.False,
                "Grom's final runtime action must actually fell the authoritative tree.");
            Assert.That(tree.FallDirection, Is.EqualTo(new GridPos(-1, 0)));
            Assert.That(board.LastCascadeSteps, Is.EqualTo(4));
            Assert.That(board.LastCascadePlayers, Is.EqualTo(4));
            Assert.That(board.LastHandoffs, Is.EqualTo(3));

            // Motion playback intentionally animates the authoritative 90-degree fall rather than snapping it in one frame.
            // Give the presentation enough real time to demonstrate a visibly fallen tree, then inspect the actual scene object.
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(Vector3.Angle(treeVisual.transform.up, Vector3.up), Is.GreaterThan(45f),
                "The controller presentation must visibly animate the fallen-tree state, not only mutate invisible board data.");

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        private static ChainCombatBoard GetLiveBoard(ChainCombatLabController controller)
        {
            FieldInfo boardField = typeof(ChainCombatLabController).GetField(
                "_board", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(boardField, Is.Not.Null, "ChainCombatLabController should still own the authoritative demo board.");

            var board = boardField.GetValue(controller) as ChainCombatBoard;
            Assert.That(board, Is.Not.Null);
            return board;
        }
    }
}
