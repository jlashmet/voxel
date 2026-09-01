using System.Collections.Generic;
using Game.Progression.Api;
using NUnit.Framework;

namespace Game.Progression.Tests
{
    public sealed class ProgressionApiContractTests
    {
        [Test]
        public void SnapshotOwnsCoherentCopiesOfQuestAndObjectiveTruth()
        {
            var questObjectives = new List<ObjectiveProgressSnapshot>
            {
                new ObjectiveProgressSnapshot(new ObjectiveId("objective:enter"), ProgressionLifecycleState.Active, 2)
            };
            var quests = new List<QuestProgressSnapshot>
            {
                new QuestProgressSnapshot(new QuestId("quest:ridge"), ProgressionLifecycleState.Active, questObjectives, 3)
            };
            var standalone = new List<ObjectiveProgressSnapshot>
            {
                new ObjectiveProgressSnapshot(new ObjectiveId("objective:camp"), ProgressionLifecycleState.Inactive, 4)
            };

            var snapshot = new ProgressionSnapshot(5, quests, standalone);
            quests.Clear();
            standalone.Clear();
            questObjectives.Clear();

            Assert.That(snapshot.Revision, Is.EqualTo(5));
            Assert.That(snapshot.Quests.Count, Is.EqualTo(1));
            Assert.That(snapshot.Quests[0].Objectives.Count, Is.EqualTo(1));
            Assert.That(snapshot.StandaloneObjectives.Count, Is.EqualTo(1));
        }
    }
}
