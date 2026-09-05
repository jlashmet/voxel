using System;
using System.Collections.Generic;
using Game.Encounters.Api;
using Game.Outcomes.Api;
using Game.Quests.Api;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace Game.Story.Tests
{
    public sealed class StoryRuleEngineSystem26Tests
    {
        [Test]
        public void CompletedEncounterDispatchesAuthoredOutcomeConditionExactlyOnce()
        {
            var encounter = new EncounterId("story-test-encounter");
            var condition = new OutcomeConditionRef("story-test-terminal-condition");
            CampaignBlueprint blueprint = BuildBlueprint(encounter, condition);
            var sink = new RecordingSink();

            int matched = StoryRuleEngine.Dispatch(
                blueprint.StoryRules,
                StoryEvent.EncounterResolved(encounter, EncounterResolutionResult.Completed),
                EmptyState.Instance,
                sink);

            Assert.That(matched, Is.EqualTo(1));
            Assert.That(sink.ObservedOutcomeConditions, Has.Count.EqualTo(1));
            Assert.That(sink.ObservedOutcomeConditions[0], Is.EqualTo(condition));
        }

        [Test]
        public void NonMatchingEncounterResultDoesNotDispatchOutcomeCondition()
        {
            var encounter = new EncounterId("story-test-encounter");
            var condition = new OutcomeConditionRef("story-test-terminal-condition");
            CampaignBlueprint blueprint = BuildBlueprint(encounter, condition);
            var sink = new RecordingSink();

            int matched = StoryRuleEngine.Dispatch(
                blueprint.StoryRules,
                StoryEvent.EncounterResolved(encounter, EncounterResolutionResult.Failed),
                EmptyState.Instance,
                sink);

            Assert.That(matched, Is.EqualTo(0));
            Assert.That(sink.ObservedOutcomeConditions, Is.Empty);
        }

        private static CampaignBlueprint BuildBlueprint(
            EncounterId encounter,
            OutcomeConditionRef condition)
        {
            CampaignBuilder campaign = Game.WorldBuilder.Api.Campaign.Create("story-system26-tests");
            campaign.Story.Rule("encounter-to-terminal-condition", rule => rule
                .When(StoryTrigger.EncounterResolved(encounter, EncounterResolutionResult.Completed))
                .Then(StoryEffect.ObserveOutcomeCondition(condition)));
            return campaign.Build();
        }

        private sealed class EmptyState : IStoryStateView
        {
            public static readonly EmptyState Instance = new EmptyState();
            private EmptyState() { }

            public bool IsObjectiveActive(ObjectiveRef objective) => false;
            public bool IsQuestActive(QuestRef quest) => false;
            public bool IsQuestCompleted(QuestRef quest) => false;
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => false;
        }

        private sealed class RecordingSink : IStoryOutcomeEffectSink
        {
            public readonly List<OutcomeConditionRef> ObservedOutcomeConditions =
                new List<OutcomeConditionRef>();

            public void StartObjective(ObjectiveRef objective) =>
                throw new InvalidOperationException("Unexpected objective effect.");

            public void StartQuest(QuestRef quest) =>
                throw new InvalidOperationException("Unexpected quest effect.");

            public void PlayCutscene(CutsceneRef cutscene) =>
                throw new InvalidOperationException("Unexpected cutscene effect.");

            public void JoinPartyMember(string memberId) =>
                throw new InvalidOperationException("Unexpected party effect.");

            public void GrantSpell(string spellId) =>
                throw new InvalidOperationException("Unexpected spell effect.");

            public void ObserveOutcomeCondition(OutcomeConditionRef condition) =>
                ObservedOutcomeConditions.Add(condition);
        }
    }
}
