using System;
using System.Collections.Generic;
using Game.Composition.Campaign.Content;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Quests.Api;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeOpeningProgressionTests
    {
        [Test]
        public void OpeningProgressionRejectsOutOfOrderSitesAndPreservesSourceBeats()
        {
            var destination = new CutsceneDefinition(
                "test.destination",
                CutsceneStageSetupDefinition.Empty,
                Array.Empty<CutsceneStep>());
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(destination);
            var state = new StoryState();
            var effects = new StoryEffects();

            Assert.That(
                StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.SiteEntered(content.AwonSite), state, effects),
                Is.Zero,
                "Awon must not start before the post-pub Logan continuation completes.");
            Assert.That(
                StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.SiteEntered(content.MedrareSite), state, effects),
                Is.Zero,
                "Medrare must not start before Awon's lesson completes.");

            state.Complete(content.IntroCutscene);
            Assert.That(
                StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.CutsceneCompleted(content.IntroCutscene), state, effects),
                Is.EqualTo(1));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.LoganToChurchCutscene));

            state.Complete(content.LoganToChurchCutscene);
            Assert.That(
                StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.SiteEntered(content.AwonSite), state, effects),
                Is.EqualTo(1));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.AwonOpeningCutscene));
            Assert.That(
                StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.SiteEntered(content.MedrareSite), state, effects),
                Is.Zero,
                "Medrare still waits for the Awon cutscene itself to complete.");

            state.Complete(content.AwonOpeningCutscene);
            Assert.That(
                StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.SiteEntered(content.MedrareSite), state, effects),
                Is.EqualTo(1));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.MedrareFirstSpellCutscene));

            Assert.That(KentridgeOpeningProgressionCutscenes.LoganToChurchDefinition.Steps.Count, Is.EqualTo(3));
            Assert.That(
                KentridgeOpeningScript.LineFor(KentridgeOpeningProgressionCutscenes.LoganToChurchDefinition.Steps[0].Cue),
                Is.EqualTo("Let's take it easy.  I'll meet you outside the church."));
            Assert.That(
                KentridgeOpeningScript.LineFor(KentridgeOpeningProgressionCutscenes.LoganToChurchDefinition.Steps[2].Cue),
                Is.EqualTo("Hurry up though, we need to talk to your father."));

            CutsceneDefinition awon = KentridgeOpeningProgressionCutscenes.AwonDefinition;
            Assert.That(awon.Steps.Count, Is.EqualTo(KentridgeOpeningScript.AwonOpeningBeatCount));
            Assert.That(KentridgeOpeningScript.LineFor(awon.Steps[0].Cue), Is.EqualTo("Knighting lesson."));
            Assert.That(KentridgeOpeningScript.LineFor(awon.Steps[1].Cue), Is.EqualTo("Beginner sword demonstration."));
            Assert.That(KentridgeOpeningScript.LineFor(awon.Steps[2].Cue), Is.EqualTo("Medium sword demonstration."));
            Assert.That(KentridgeOpeningScript.LineFor(awon.Steps[3].Cue), Is.EqualTo("Advanced sword demonstration."));
            Assert.That(KentridgeOpeningScript.LineFor(awon.Steps[4].Cue), Is.EqualTo("Awon joins the party."));

            CutsceneDefinition medrare = KentridgeOpeningProgressionCutscenes.MedrareFirstSpellDefinition;
            Assert.That(medrare.Steps[0].Type, Is.EqualTo(CutsceneStepType.Wait));
            Assert.That(medrare.Steps[0].DurationMilliseconds, Is.EqualTo(1500));
            Assert.That(medrare.Steps[1].Type, Is.EqualTo(CutsceneStepType.MoveActor));
            Assert.That(medrare.Steps[1].DurationMilliseconds, Is.EqualTo(2000));
            Assert.That(
                KentridgeOpeningScript.LineFor(medrare.Steps[2].Cue),
                Is.EqualTo("Haugh!  What are you doing here?"));
            Assert.That(
                KentridgeOpeningScript.LineFor(medrare.Steps[24].Cue),
                Is.EqualTo("Fire spreads across the floor."));
        }

        private sealed class StoryState : IStoryStateView
        {
            private readonly HashSet<CutsceneRef> _completed = new HashSet<CutsceneRef>();

            public void Complete(CutsceneRef cutscene) => _completed.Add(cutscene);
            public bool IsObjectiveActive(ObjectiveRef objective) => false;
            public bool IsQuestActive(QuestRef quest) => false;
            public bool IsQuestCompleted(QuestRef quest) => false;
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => _completed.Contains(cutscene);
        }

        private sealed class StoryEffects : IStoryEffectSink
        {
            public CutsceneRef LastCutscene { get; private set; }
            public void StartObjective(ObjectiveRef objective) { }
            public void StartQuest(QuestRef quest) { }
            public void PlayCutscene(CutsceneRef cutscene) => LastCutscene = cutscene;
        }
    }
}
