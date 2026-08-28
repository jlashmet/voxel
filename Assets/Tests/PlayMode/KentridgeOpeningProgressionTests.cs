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

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeOpeningProgressionTests
    {
        [Test]
        public void OpeningProgressionKeepsPubIntroThenAuthorsLoganAwonAndMedrareBeats()
        {
            KnownOpeningCampaignContent content = BuildContent();

            Assert.That(KentridgeOpeningProgressionCutscenes.LoganDefinition.Steps[0].Type, Is.EqualTo(CutsceneStepType.Camera));
            Assert.That(
                KentridgeOpeningScript.LineFor(KentridgeOpeningProgressionCutscenes.LoganIntroduction),
                Is.EqualTo("You can call me Logan."));
            Assert.That(
                KentridgeOpeningScript.LineFor(KentridgeOpeningProgressionCutscenes.AwonTournament),
                Does.Contain("tournament for adventurers"));
            Assert.That(
                KentridgeOpeningScript.LineFor(KentridgeOpeningProgressionCutscenes.MedrareRumor),
                Does.Contain("Logan").And.Contain("Kentridge"));

            Assert.That(content.LoganOpeningCutscene.Equals(default(CutsceneRef)), Is.False);
            Assert.That(content.AwonOpeningCutscene.Equals(default(CutsceneRef)), Is.False);
            Assert.That(content.MedrareOpeningCutscene.Equals(default(CutsceneRef)), Is.False);
        }

        [Test]
        public void EnteringAwonAndMedrareDispatchesOnlyTheirOpeningCutscenes()
        {
            KnownOpeningCampaignContent content = BuildContent();
            var state = new OpenStoryState();
            var sink = new RecordingEffectSink();

            int awonMatches = StoryRuleEngine.Dispatch(
                content.Blueprint.StoryRules,
                StoryEvent.SiteEntered(content.AwonSite),
                state,
                sink);

            Assert.That(awonMatches, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { content.AwonOpeningCutscene }, sink.Played);

            sink.Played.Clear();
            int medrareMatches = StoryRuleEngine.Dispatch(
                content.Blueprint.StoryRules,
                StoryEvent.SiteEntered(content.MedrareSite),
                state,
                sink);

            Assert.That(medrareMatches, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { content.MedrareOpeningCutscene }, sink.Played);
        }

        private static KnownOpeningCampaignContent BuildContent()
        {
            var destination = new CutsceneDefinition(
                "test.destination",
                CutsceneStageSetupDefinition.Empty,
                Array.Empty<CutsceneStep>());
            return KnownOpeningCampaignContent.Build(destination);
        }

        private sealed class OpenStoryState : IStoryStateView
        {
            public bool IsObjectiveActive(ObjectiveRef objective) => false;
            public bool IsQuestActive(QuestRef quest) => false;
            public bool IsQuestCompleted(QuestRef quest) => false;
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => false;
        }

        private sealed class RecordingEffectSink : IStoryEffectSink
        {
            public readonly List<CutsceneRef> Played = new List<CutsceneRef>();
            public void StartObjective(ObjectiveRef objective) { }
            public void StartQuest(QuestRef quest) { }
            public void PlayCutscene(CutsceneRef cutscene) => Played.Add(cutscene);
        }
    }
}
