using System;
using System.Reflection;
using Game.Progression.Api;
using Game.Progression.Runtime;
using Game.Quests.Api;
using Game.Quests.Runtime;
using NUnit.Framework;

namespace Game.Progression.Tests
{
    public sealed class ProgressionRuntimeTests
    {
        [Test]
        public void OneObservationAdvancesQuestAndStandaloneAndOneSnapshotOwnsBoth()
        {
            var runtime = new ProgressionRuntime();
            runtime.RegisterQuest(TwoStepQuest());
            runtime.RegisterStandaloneObjective(new ObjectiveDefinition(
                "objective:guide",
                ProgressionCondition.NpcInteraction("guide")));
            runtime.Start("quest:opening", "start-quest");
            runtime.Start("objective:guide", "start-objective");

            ProgressionUpdateResult result = runtime.Observe(new ProgressionUpdateSignal(
                "talk-guide",
                ProgressionSignalKind.NpcInteracted,
                "guide"));

            Assert.That(result.Status, Is.EqualTo(ProgressionApplyStatus.Applied));
            Assert.That(result.Transitions.Count, Is.EqualTo(6));
            AssertTransition(result, 0, ProgressionTransitionKind.ObjectiveProgressed, "quest:opening", "talk");
            AssertTransition(result, 1, ProgressionTransitionKind.NodeCompleted, "quest:opening", "talk");
            AssertTransition(result, 2, ProgressionTransitionKind.NodeActivated, "quest:opening", "well");
            AssertTransition(result, 3, ProgressionTransitionKind.ObjectiveProgressed, "objective:guide", "objective:guide");
            AssertTransition(result, 4, ProgressionTransitionKind.NodeCompleted, "objective:guide", "objective:guide");
            AssertTransition(result, 5, ProgressionTransitionKind.EntryCompleted, "objective:guide", string.Empty);

            ProgressionSnapshot snapshot = runtime.Snapshot();
            Assert.That(snapshot.Quests.Count, Is.EqualTo(1));
            Assert.That(snapshot.StandaloneObjectives.Count, Is.EqualTo(1));
            Assert.That(snapshot.Quests[0].Id, Is.EqualTo(new QuestId("quest:opening")));
            Assert.That(snapshot.Quests[0].State, Is.EqualTo(ProgressionLifecycleState.Active));
            Assert.That(snapshot.Quests[0].ActiveStepId, Is.EqualTo("well"));
            Assert.That(snapshot.Quests[0].Steps[0].State, Is.EqualTo(ProgressionLifecycleState.Completed));
            Assert.That(snapshot.Quests[0].Steps[1].State, Is.EqualTo(ProgressionLifecycleState.Active));
            Assert.That(snapshot.StandaloneObjectives[0].State, Is.EqualTo(ProgressionLifecycleState.Completed));
            Assert.That(snapshot.StandaloneObjectives[0].CurrentCount, Is.EqualTo(1));
        }

        [Test]
        public void FactDrivenCountsRequireFactsAndExposeNoCompletionBypass()
        {
            var runtime = new ProgressionRuntime();
            runtime.RegisterStandaloneObjective(new ObjectiveDefinition(
                "objective:twice",
                ProgressionCondition.Interaction("lever"),
                2));
            runtime.Start("objective:twice", "start");

            ProgressionUpdateResult first = runtime.Observe(new ProgressionUpdateSignal(
                "lever-1",
                ProgressionSignalKind.Interacted,
                "lever"));
            Assert.That(first.Transitions.Count, Is.EqualTo(1));
            Assert.That(runtime.GetSnapshot("objective:twice").Status, Is.EqualTo(ProgressionLifecycleState.Active));
            Assert.That(runtime.GetSnapshot("objective:twice").ObjectiveCounts["objective:twice"], Is.EqualTo(1));

            runtime.Observe(new ProgressionUpdateSignal(
                "wrong-kind",
                ProgressionSignalKind.NpcInteracted,
                "lever"));
            Assert.That(runtime.GetSnapshot("objective:twice").ObjectiveCounts["objective:twice"], Is.EqualTo(1));

            ProgressionUpdateResult second = runtime.Observe(new ProgressionUpdateSignal(
                "lever-2",
                ProgressionSignalKind.Interacted,
                "lever"));
            Assert.That(second.Transitions[second.Transitions.Count - 1].Kind, Is.EqualTo(ProgressionTransitionKind.EntryCompleted));
            Assert.That(runtime.GetSnapshot("objective:twice").Status, Is.EqualTo(ProgressionLifecycleState.Completed));

            Assert.That(typeof(IProgressionRuntime).GetMethod("ForceComplete"), Is.Null);
            Assert.That(typeof(IProgressionRuntime).GetMethod("Complete"), Is.Null);
            Assert.That(typeof(QuestRuntime).GetMethod("Complete", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        }

        [Test]
        public void SnapshotRestoreRoundTripsAndOperationReplayIsIgnored()
        {
            ProgressionRuntime source = BuildMixedRuntime();
            source.Start("quest:opening", "start-quest");
            source.Start("objective:guide", "start-objective");
            source.Observe(new ProgressionUpdateSignal(
                "talk-guide",
                ProgressionSignalKind.NpcInteracted,
                "guide"));
            ProgressionSnapshot saved = source.Snapshot();

            ProgressionRuntime restored = BuildMixedRuntime();
            restored.RestoreState(saved);
            ProgressionSnapshot roundTrip = restored.Snapshot();

            Assert.That(roundTrip.Revision, Is.EqualTo(saved.Revision));
            Assert.That(roundTrip.CompatibilitySequence, Is.EqualTo(saved.CompatibilitySequence));
            Assert.That(roundTrip.AppliedOperationIds, Is.EqualTo(saved.AppliedOperationIds));
            Assert.That(roundTrip.Quests[0].State, Is.EqualTo(saved.Quests[0].State));
            Assert.That(roundTrip.Quests[0].ActiveStepId, Is.EqualTo(saved.Quests[0].ActiveStepId));
            Assert.That(roundTrip.Quests[0].Steps[0].Objectives[0].CurrentCount,
                Is.EqualTo(saved.Quests[0].Steps[0].Objectives[0].CurrentCount));
            Assert.That(roundTrip.StandaloneObjectives[0].State,
                Is.EqualTo(saved.StandaloneObjectives[0].State));

            ProgressionUpdateResult replay = restored.Observe(new ProgressionUpdateSignal(
                "talk-guide",
                ProgressionSignalKind.NpcInteracted,
                "guide"));
            Assert.That(replay.Status, Is.EqualTo(ProgressionApplyStatus.Replay));
            Assert.That(replay.Transitions, Is.Empty);
            Assert.That(restored.Snapshot().Revision, Is.EqualTo(saved.Revision));

            ProgressionUpdateResult next = restored.Observe(new ProgressionUpdateSignal(
                "use-well",
                ProgressionSignalKind.Interacted,
                "well"));
            Assert.That(next.Status, Is.EqualTo(ProgressionApplyStatus.Applied));
            Assert.That(restored.GetSnapshot("quest:opening").Status, Is.EqualTo(ProgressionLifecycleState.Completed));
        }

        [Test]
        public void LegacyQuestFacadePreservesMultiStepSemanticEventOrder()
        {
            var quest = new QuestDefinition(new QuestRef("quest:legacy"), new[]
            {
                new Game.Quests.Api.QuestStepDefinition(
                    new QuestStepRef("talk"),
                    "guide",
                    QuestCompletion.InteractWith("guide")),
                new Game.Quests.Api.QuestStepDefinition(
                    new QuestStepRef("well"),
                    "well",
                    QuestCompletion.InteractWithSubject("well"))
            });
            var runtime = new QuestRuntime(new[] { quest });

            var started = runtime.Start(quest.Ref);
            AssertKinds(started, QuestEventKind.QuestStarted, QuestEventKind.QuestStepActivated);

            var first = runtime.Observe(QuestObservation.NpcInteracted("guide"));
            AssertKinds(first, QuestEventKind.QuestStepCompleted, QuestEventKind.QuestStepActivated);

            var second = runtime.Observe(QuestObservation.Interacted("well"));
            AssertKinds(second, QuestEventKind.QuestStepCompleted, QuestEventKind.QuestCompleted);

            QuestSnapshot snapshot = runtime.GetSnapshot(quest.Ref);
            Assert.That(snapshot.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(snapshot.Steps[0].Status, Is.EqualTo(QuestStepStatus.Completed));
            Assert.That(snapshot.Steps[1].Status, Is.EqualTo(QuestStepStatus.Completed));
        }

        [Test]
        public void SharedQuestFacadeAndDirectRuntimeUseOneSessionState()
        {
            var shared = new ProgressionRuntime();
            var quest = new QuestDefinition(new QuestRef("quest:shared"), new[]
            {
                new Game.Quests.Api.QuestStepDefinition(
                    new QuestStepRef("talk"),
                    "guide",
                    QuestCompletion.InteractWith("guide"))
            });
            var facade = new QuestRuntime(new[] { quest }, shared);

            facade.Start(quest.Ref);
            shared.Observe(new ProgressionUpdateSignal(
                "shared-observation",
                ProgressionSignalKind.NpcInteracted,
                "guide"));

            Assert.That(facade.IsCompleted(quest.Ref), Is.True);
            Assert.That(ReferenceEquals(facade.Progression, shared), Is.True);
            Assert.That(shared.Snapshot().Quests.Count, Is.EqualTo(1));
        }

        [Test]
        public void ValidationRejectsMalformedGraphsCyclesDuplicatesAndInvalidCounts()
        {
            var missing = new ProgressionRuntime();
            Assert.Throws<InvalidOperationException>(() => missing.RegisterQuest(
                new QuestGraphDefinition("quest:missing", "a", new[]
                {
                    new Game.Progression.Api.QuestStepDefinition(
                        "a",
                        Array.Empty<ObjectiveDefinition>(),
                        "nope")
                })));

            var cycle = new ProgressionRuntime();
            Assert.Throws<InvalidOperationException>(() => cycle.RegisterQuest(
                new QuestGraphDefinition("quest:cycle", "a", new[]
                {
                    new Game.Progression.Api.QuestStepDefinition("a", Array.Empty<ObjectiveDefinition>(), "b"),
                    new Game.Progression.Api.QuestStepDefinition("b", Array.Empty<ObjectiveDefinition>(), "a")
                })));

            var duplicate = new ProgressionRuntime();
            Assert.Throws<InvalidOperationException>(() => duplicate.RegisterQuest(
                new QuestGraphDefinition("quest:duplicate-objective", "a", new[]
                {
                    new Game.Progression.Api.QuestStepDefinition("a", new[]
                    {
                        new ObjectiveDefinition("same", ProgressionCondition.Interaction("a"))
                    }, "b"),
                    new Game.Progression.Api.QuestStepDefinition("b", new[]
                    {
                        new ObjectiveDefinition("same", ProgressionCondition.Interaction("b"))
                    })
                })));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ObjectiveDefinition("bad", ProgressionCondition.Interaction("x"), 0));

            var ids = new ProgressionRuntime();
            ids.RegisterStandaloneObjective(new ObjectiveDefinition(
                "duplicate-entry",
                ProgressionCondition.Interaction("x")));
            Assert.Throws<InvalidOperationException>(() => ids.RegisterQuest(
                new QuestGraphDefinition("duplicate-entry", "a", new[]
                {
                    new Game.Progression.Api.QuestStepDefinition("a", Array.Empty<ObjectiveDefinition>())
                })));
        }

        [Test]
        public void EmptyStepsAdvanceDeterministicallyWithoutGameplaySideEffects()
        {
            var runtime = new ProgressionRuntime();
            runtime.RegisterQuest(new QuestGraphDefinition("quest:empty", "first", new[]
            {
                new Game.Progression.Api.QuestStepDefinition("first", Array.Empty<ObjectiveDefinition>(), "second"),
                new Game.Progression.Api.QuestStepDefinition("second", Array.Empty<ObjectiveDefinition>())
            }));

            ProgressionUpdateResult started = runtime.Start("quest:empty", "start-empty");

            Assert.That(started.Transitions.Count, Is.EqualTo(6));
            AssertTransition(started, 0, ProgressionTransitionKind.EntryStarted, "quest:empty", string.Empty);
            AssertTransition(started, 1, ProgressionTransitionKind.NodeActivated, "quest:empty", "first");
            AssertTransition(started, 2, ProgressionTransitionKind.NodeCompleted, "quest:empty", "first");
            AssertTransition(started, 3, ProgressionTransitionKind.NodeActivated, "quest:empty", "second");
            AssertTransition(started, 4, ProgressionTransitionKind.NodeCompleted, "quest:empty", "second");
            AssertTransition(started, 5, ProgressionTransitionKind.EntryCompleted, "quest:empty", string.Empty);
            Assert.That(runtime.GetSnapshot("quest:empty").Status, Is.EqualTo(ProgressionLifecycleState.Completed));
        }

        private static ProgressionRuntime BuildMixedRuntime()
        {
            var runtime = new ProgressionRuntime();
            runtime.RegisterQuest(TwoStepQuest());
            runtime.RegisterStandaloneObjective(new ObjectiveDefinition(
                "objective:guide",
                ProgressionCondition.NpcInteraction("guide")));
            return runtime;
        }

        private static QuestGraphDefinition TwoStepQuest() =>
            new QuestGraphDefinition("quest:opening", "talk", new[]
            {
                new Game.Progression.Api.QuestStepDefinition(
                    "talk",
                    new[]
                    {
                        new ObjectiveDefinition(
                            "quest:opening:talk",
                            ProgressionCondition.NpcInteraction("guide"))
                    },
                    "well"),
                new Game.Progression.Api.QuestStepDefinition(
                    "well",
                    new[]
                    {
                        new ObjectiveDefinition(
                            "quest:opening:well",
                            ProgressionCondition.Interaction("well"))
                    })
            });

        private static void AssertTransition(
            ProgressionUpdateResult result,
            int index,
            ProgressionTransitionKind kind,
            string entryId,
            string nodeId)
        {
            Assert.That(result.Transitions[index].Kind, Is.EqualTo(kind), "transition " + index);
            Assert.That(result.Transitions[index].EntryId, Is.EqualTo(entryId), "entry " + index);
            Assert.That(result.Transitions[index].NodeId, Is.EqualTo(nodeId), "node " + index);
        }

        private static void AssertKinds(
            System.Collections.Generic.IReadOnlyList<QuestEvent> events,
            params QuestEventKind[] expected)
        {
            Assert.That(events.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
                Assert.That(events[i].Kind, Is.EqualTo(expected[i]), "event " + i);
        }
    }
}
