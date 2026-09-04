using System.Collections.Generic;
using Game.Progression.Api;
using Game.Progression.Runtime;
using Game.Quests.Api;
using Game.Quests.Runtime;
using NUnit.Framework;

namespace Game.Quests.Tests
{
    public sealed class QuestRuntimeProgressionCompatibilityTests
    {
        [Test]
        public void LegacyFacadePreservesMultiStepSemanticEventOrder()
        {
            var quest = new QuestDefinition(new QuestRef("quest:legacy"), new[]
            {
                new QuestStepDefinition(
                    new QuestStepRef("talk"),
                    "guide",
                    QuestCompletion.InteractWith("guide")),
                new QuestStepDefinition(
                    new QuestStepRef("well"),
                    "well",
                    QuestCompletion.InteractWithSubject("well"))
            });
            var runtime = new QuestRuntime(new[] { quest });

            AssertKinds(runtime.Start(quest.Ref), QuestEventKind.QuestStarted, QuestEventKind.QuestStepActivated);
            AssertKinds(runtime.Observe(QuestObservation.NpcInteracted("guide")),
                QuestEventKind.QuestStepCompleted,
                QuestEventKind.QuestStepActivated);
            AssertKinds(runtime.Observe(QuestObservation.Interacted("well")),
                QuestEventKind.QuestStepCompleted,
                QuestEventKind.QuestCompleted);

            QuestSnapshot snapshot = runtime.GetSnapshot(quest.Ref);
            Assert.That(snapshot.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(snapshot.Steps[0].Status, Is.EqualTo(QuestStepStatus.Completed));
            Assert.That(snapshot.Steps[1].Status, Is.EqualTo(QuestStepStatus.Completed));
        }

        [Test]
        public void SharedProgressionRuntimeIsTheFacadeSessionAuthority()
        {
            var shared = new ProgressionRuntime();
            var quest = new QuestDefinition(new QuestRef("quest:shared"), new[]
            {
                new QuestStepDefinition(
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

        private static void AssertKinds(IReadOnlyList<QuestEvent> events, params QuestEventKind[] expected)
        {
            Assert.That(events.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
                Assert.That(events[i].Kind, Is.EqualTo(expected[i]), "event " + i);
        }
    }
}
