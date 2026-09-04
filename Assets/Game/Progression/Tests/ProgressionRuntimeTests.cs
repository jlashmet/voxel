using System;
using Game.Progression.Runtime;
using Game.Quests.Api;
using Game.Quests.Runtime;
using NUnit.Framework;

namespace Game.Progression.Tests
{
    public sealed class ProgressionRuntimeTests
    {
        [Test]
        public void OneRuntimeAdvancesQuestAndStandaloneFromSameSemanticObservation()
        {
            var runtime = new ProgressionRuntime();
            runtime.RegisterQuest(new QuestGraphDefinition("quest", "talk", new[]
            {
                new QuestStepDefinition("talk", new[] { new ObjectiveDefinition("talk.done", ProgressionCondition.NpcInteraction("npc"), 1) }, Array.Empty<string>())
            }));
            runtime.RegisterStandaloneObjective(new StandaloneObjectiveDefinition("objective", ProgressionCondition.NpcInteraction("npc"), 1));
            runtime.Start("quest", "start-q");
            runtime.Start("objective", "start-o");

            ProgressionUpdateResult result = runtime.Observe(new ProgressionUpdateSignal("observe-1", ProgressionSignalKind.NpcInteracted, "npc"));

            Assert.That(result.Status, Is.EqualTo(ProgressionApplyStatus.Applied));
            Assert.That(runtime.GetSnapshot("quest").Status, Is.EqualTo(ProgressionNodeStatus.Completed));
            Assert.That(runtime.GetSnapshot("objective").Status, Is.EqualTo(ProgressionNodeStatus.Completed));
        }

        [Test]
        public void GraphTraversalUsesDeclaredOrderAndRejectsMalformedGraphs()
        {
            var runtime = new ProgressionRuntime();
            runtime.RegisterQuest(new QuestGraphDefinition("branch", "root", new[]
            {
                new QuestStepDefinition("root", new[] { new ObjectiveDefinition("root.done", "root", 1) }, new[] { "left", "right" }),
                new QuestStepDefinition("left", new[] { new ObjectiveDefinition("left.done", "left", 1) }, Array.Empty<string>()),
                new QuestStepDefinition("right", new[] { new ObjectiveDefinition("right.done", "right", 1) }, Array.Empty<string>())
            }));
            runtime.Start("branch", "start");
            runtime.Observe(new ProgressionUpdateSignal("root", ProgressionSignalKind.Event, "root"));
            Assert.That(runtime.GetSnapshot("branch").ActiveNodeId, Is.EqualTo("left"));

            Assert.Throws<InvalidOperationException>(() => new QuestGraphRegistry(new[]
            {
                new QuestGraphDefinition("missing", "a", new[] { new QuestStepDefinition("a", Array.Empty<ObjectiveDefinition>(), new[] { "nope" }) })
            }));
            Assert.Throws<InvalidOperationException>(() => new QuestGraphRegistry(new[]
            {
                new QuestGraphDefinition("cycle", "a", new[]
                {
                    new QuestStepDefinition("a", Array.Empty<ObjectiveDefinition>(), new[] { "b" }),
                    new QuestStepDefinition("b", Array.Empty<ObjectiveDefinition>(), new[] { "a" })
                })
            }));
            Assert.Throws<InvalidOperationException>(() => new StandaloneObjectiveRegistry(new[]
            {
                new StandaloneObjectiveDefinition("bad-count", ProgressionCondition.Event("event"), 0)
            }));
            Assert.Throws<InvalidOperationException>(() => new StandaloneObjectiveRegistry(new[]
            {
                new StandaloneObjectiveDefinition("ungated-reward", ProgressionCondition.Always(), 1, "reward")
            }));
        }

        [Test]
        public void ReplayAndRestartPreserveOneTimeRewardAndOperationIdempotency()
        {
            var runtime = new ProgressionRuntime();
            runtime.RegisterStandaloneObjective(new StandaloneObjectiveDefinition("objective", ProgressionCondition.Event("collect"), 2, "reward"));
            runtime.Start("objective", "start");
            runtime.Observe(new ProgressionUpdateSignal("collect-1", ProgressionSignalKind.Event, "collect"));
            ProgressionStateSnapshot mid = runtime.CaptureState();

            var restored = new ProgressionRuntime();
            restored.RegisterStandaloneObjective(new StandaloneObjectiveDefinition("objective", ProgressionCondition.Event("collect"), 2, "reward"));
            restored.RestoreState(mid);
            ProgressionUpdateResult completed = restored.Observe(new ProgressionUpdateSignal("collect-2", ProgressionSignalKind.Event, "collect"));
            Assert.That(CountTransitions(completed, ProgressionTransitionKind.RewardEmitted), Is.EqualTo(1));
            Assert.That(restored.GetSnapshot("objective").Status, Is.EqualTo(ProgressionNodeStatus.Completed));

            ProgressionStateSnapshot done = restored.CaptureState();
            var restarted = new ProgressionRuntime();
            restarted.RegisterStandaloneObjective(new StandaloneObjectiveDefinition("objective", ProgressionCondition.Event("collect"), 2, "reward"));
            restarted.RestoreState(done);
            ProgressionUpdateResult replay = restarted.Observe(new ProgressionUpdateSignal("collect-2", ProgressionSignalKind.Event, "collect"));
            Assert.That(replay.Status, Is.EqualTo(ProgressionApplyStatus.Replay));
            Assert.That(CountTransitions(replay, ProgressionTransitionKind.RewardEmitted), Is.EqualTo(0));
        }

        [Test]
        public void QuestRuntimeFacadePreservesPublicSemanticEventsWithoutOwningQuestState()
        {
            var quest = new QuestDefinition(new QuestRef("quest"), new[]
            {
                new Game.Quests.Api.QuestStepDefinition(new QuestStepRef("talk"), "npc", QuestCompletion.InteractWith("npc")),
                new Game.Quests.Api.QuestStepDefinition(new QuestStepRef("door"), "door", QuestCompletion.InteractWithSubject("door"))
            });
            var runtime = new QuestRuntime(new[] { quest });

            var started = runtime.Start(quest.Ref);
            Assert.That(started.Count, Is.EqualTo(2));
            Assert.That(started[0].Kind, Is.EqualTo(QuestEventKind.QuestStarted));
            runtime.Observe(QuestObservation.NpcInteracted("npc"));
            var completed = runtime.Observe(QuestObservation.Interacted("door"));

            Assert.That(runtime.IsCompleted(quest.Ref), Is.True);
            Assert.That(completed[completed.Count - 1].Kind, Is.EqualTo(QuestEventKind.QuestCompleted));
            Assert.That(runtime.GetSnapshot(quest.Ref).Steps[0].Status, Is.EqualTo(QuestStepStatus.Completed));
            Assert.That(runtime.GetSnapshot(quest.Ref).Steps[1].Status, Is.EqualTo(QuestStepStatus.Completed));
        }

        [Test]
        public void RegistriesRejectDuplicateEntryIdsDeterministically()
        {
            Assert.Throws<InvalidOperationException>(() => new QuestGraphRegistry(new[]
            {
                OneStepQuest("duplicate", "a"), OneStepQuest("duplicate", "b")
            }));
            Assert.Throws<InvalidOperationException>(() => new StandaloneObjectiveRegistry(new[]
            {
                new StandaloneObjectiveDefinition("duplicate", "a", 1),
                new StandaloneObjectiveDefinition("duplicate", "b", 1)
            }));
        }

        private static QuestGraphDefinition OneStepQuest(string id, string eventId) =>
            new QuestGraphDefinition(id, "step", new[]
            {
                new QuestStepDefinition("step", new[] { new ObjectiveDefinition("done", eventId, 1) }, Array.Empty<string>())
            });

        private static int CountTransitions(ProgressionUpdateResult result, ProgressionTransitionKind kind)
        {
            int count = 0;
            for (var i = 0; i < result.Transitions.Count; i++) if (result.Transitions[i].Kind == kind) count++;
            return count;
        }
    }
}
