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
        public void MedrareBeatsUseRecoveredDialogueAndNarrationVerbatim()
        {
            CutsceneDefinition firstSpell = KentridgeOpeningProgressionCutscenes.MedrareFirstSpellDefinition;
            CutsceneDefinition toChurch = KentridgeOpeningProgressionCutscenes.MedrareToChurchDefinition;

            Assert.That(firstSpell.Steps.Count, Is.EqualTo(KentridgeOpeningScript.MedrareFirstSpellLineCount));
            Assert.That(KentridgeOpeningScript.LineFor(firstSpell.Steps[0].Cue),
                Is.EqualTo("Haugh!  What are you doing here?"));
            Assert.That(KentridgeOpeningScript.LineFor(firstSpell.Steps[19].Cue),
                Is.EqualTo("Ahh.  Don't worry."));
            Assert.That(firstSpell.Steps[20].Actor.Value, Is.Null.Or.Empty);
            Assert.That(KentridgeOpeningScript.LineFor(firstSpell.Steps[20].Cue),
                Is.EqualTo("Weldon makes quick movements with his hands and fire shoots out at the lantern."));
            Assert.That(KentridgeOpeningScript.LineFor(firstSpell.Steps[22].Cue),
                Is.EqualTo("Fire spreads across the floor."));

            Assert.That(toChurch.Steps.Count, Is.EqualTo(KentridgeOpeningScript.MedrareToChurchLineCount));
            Assert.That(KentridgeOpeningScript.LineFor(toChurch.Steps[0].Cue),
                Is.EqualTo("Let's take it easy.  I'll meet you outside the church."));
            Assert.That(KentridgeOpeningScript.LineFor(toChurch.Steps[2].Cue),
                Is.EqualTo("Hurry up though, we need to talk to your father."));
        }

        [Test]
        public void AwonAndMedrareCannotRunOutOfOrderAndRemainOneShot()
        {
            KnownOpeningCampaignContent content = BuildContent();
            var state = new MutableStoryState();
            var sink = new RecordingEffectSink();

            Assert.That(Dispatch(content, StoryEvent.NpcInteracted(content.Awon), state, sink), Is.EqualTo(0),
                "Awon must not run before the recovered pub opening completes.");
            Assert.That(Dispatch(content, StoryEvent.SiteEntered(content.MedrareSite), state, sink), Is.EqualTo(0),
                "Medrare must not run before Awon completes.");

            state.Complete(content.IntroCutscene);
            Assert.That(Dispatch(content, StoryEvent.SiteEntered(content.MedrareSite), state, sink), Is.EqualTo(0),
                "Finishing the pub alone must not unlock Medrare.");

            Assert.That(Dispatch(content, StoryEvent.NpcInteracted(content.Awon), state, sink), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { content.AwonOpeningCutscene }, sink.Played);
            state.Complete(content.AwonOpeningCutscene);

            sink.Played.Clear();
            Assert.That(Dispatch(content, StoryEvent.NpcInteracted(content.Awon), state, sink), Is.EqualTo(0),
                "Awon's source trigger is one-shot.");
            Assert.That(Dispatch(content, StoryEvent.SiteEntered(content.MedrareSite), state, sink), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { content.MedrareFirstSpellCutscene }, sink.Played);
            state.Complete(content.MedrareFirstSpellCutscene);

            sink.Played.Clear();
            Assert.That(Dispatch(content, StoryEvent.CutsceneCompleted(content.MedrareFirstSpellCutscene), state, sink), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { content.MedrareToChurchCutscene }, sink.Played);
            state.Complete(content.MedrareToChurchCutscene);

            sink.Played.Clear();
            Assert.That(Dispatch(content, StoryEvent.SiteEntered(content.MedrareSite), state, sink), Is.EqualTo(0));
            Assert.That(Dispatch(content, StoryEvent.CutsceneCompleted(content.MedrareFirstSpellCutscene), state, sink), Is.EqualTo(0));
            Assert.That(sink.Played, Is.Empty, "Revisits must not replay completed opening beats.");
        }

        private static int Dispatch(
            KnownOpeningCampaignContent content,
            StoryEvent storyEvent,
            MutableStoryState state,
            RecordingEffectSink sink) =>
            StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, storyEvent, state, sink);

        private static KnownOpeningCampaignContent BuildContent()
        {
            var destination = new CutsceneDefinition(
                "test.destination",
                CutsceneStageSetupDefinition.Empty,
                Array.Empty<CutsceneStep>());
            return KnownOpeningCampaignContent.Build(destination);
        }

        private sealed class MutableStoryState : IStoryStateView
        {
            private readonly HashSet<CutsceneRef> _completed = new HashSet<CutsceneRef>();
            public void Complete(CutsceneRef cutscene) => _completed.Add(cutscene);
            public bool IsObjectiveActive(ObjectiveRef objective) => false;
            public bool IsQuestActive(QuestRef quest) => false;
            public bool IsQuestCompleted(QuestRef quest) => false;
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => _completed.Contains(cutscene);
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
