using System;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Cutscenes.Api;
using Game.Progression.Api;
using Game.Progression.Runtime;
using Game.Quests.Runtime;
using NUnit.Framework;

namespace Game.Composition.Campaign.Tests
{
    public sealed class KnownOpeningProgressionIntegrationTests
    {
        [Test]
        public void KnownOpeningTravelObjectiveAndWellQuestShareOneProgressionSnapshot()
        {
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(
                new CutsceneDefinition(
                    "test-destination-cutscene",
                    CutsceneStageSetupDefinition.Empty,
                    Array.Empty<CutsceneStep>()));

            var progression = new ProgressionRuntime();
            var quests = new QuestRuntime(
                KentridgeWellQuestDefinition.CreateDefinitions(),
                progression);
            progression.RegisterStandaloneObjective(
                new ObjectiveDefinition(
                    content.TravelObjective.ToString(),
                    ProgressionCondition.NpcInteraction(content.DestinationNpc.ToString())));

            quests.Start(content.WellQuest);
            progression.Start(content.TravelObjective.ToString());

            ProgressionSnapshot snapshot = progression.Snapshot();
            Assert.That(snapshot.Quests.Count, Is.EqualTo(1));
            Assert.That(snapshot.StandaloneObjectives.Count, Is.EqualTo(1));

            QuestProgressSnapshot wellQuest = snapshot.Quests[0];
            Assert.That(wellQuest.Id.ToString(), Is.EqualTo(content.WellQuest.ToString()));
            Assert.That(wellQuest.State, Is.EqualTo(ProgressionLifecycleState.Active));
            Assert.That(wellQuest.ActiveStepId, Is.EqualTo("rescue-boy-at-well"));
            Assert.That(wellQuest.Steps.Count, Is.EqualTo(2),
                "The authored Kentridge well quest must remain a real multi-step quest in unified progression.");

            ObjectiveProgressSnapshot travel = snapshot.StandaloneObjectives[0];
            Assert.That(travel.Id.ToString(), Is.EqualTo(content.TravelObjective.ToString()));
            Assert.That(travel.State, Is.EqualTo(ProgressionLifecycleState.Active));
        }
    }
}
