using System;
using System.Collections.Generic;
using Game.Composition.Campaign.Content;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Quests.Api;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeOpeningBuiltPathRegressionTests
    {
        [Test]
        public void RecoveredMedrareJoinCompilesAndRemainsAwonGated()
        {
            var destination = new CutsceneDefinition(
                "test.destination.built-path",
                CutsceneStageSetupDefinition.Empty,
                Array.Empty<CutsceneStep>());
            KnownOpeningCampaignContent content = KnownOpeningCampaignContent.Build(destination);

            Assert.DoesNotThrow(
                () => BlueprintCompiler.Compile(content.Blueprint),
                "The production campaign graph must bind every actor required by the recovered Medrare dialogue before the built scene starts.");

            CutsceneDefinition join = KentridgeOpeningProgressionCutscenes.MedrareJoinDefinition;
            Assert.That(join.Steps.Count, Is.EqualTo(20));
            for (var i = 3; i < join.Steps.Count; i++)
            {
                Assert.That(join.Steps[i].Type, Is.EqualTo(CutsceneStepType.Dialogue));
                Assert.That(KentridgeOpeningScript.LineFor(join.Steps[i].Cue), Is.Not.Empty);
            }

            var state = new StoryState();
            var effects = new StoryEffects();
            Assert.That(
                StoryRuleEngine.Dispatch(
                    content.Blueprint.StoryRules,
                    StoryEvent.NpcInteracted(content.Medrare),
                    state,
                    effects),
                Is.Zero,
                "Medrare must not fire before Logan and Awon complete.");

            state.Complete(content.IntroCutscene);
            Assert.That(
                StoryRuleEngine.Dispatch(
                    content.Blueprint.StoryRules,
                    StoryEvent.NpcInteracted(content.Awon),
                    state,
                    effects),
                Is.EqualTo(1));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.AwonOpeningCutscene));
            state.Complete(content.AwonOpeningCutscene);

            Assert.That(
                StoryRuleEngine.Dispatch(
                    content.Blueprint.StoryRules,
                    StoryEvent.NpcInteracted(content.Medrare),
                    state,
                    effects),
                Is.EqualTo(1));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.MedrareJoinCutscene));
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

        private sealed class StoryEffects : IStoryProgressEffectSink
        {
            public CutsceneRef LastCutscene { get; private set; }

            public void StartObjective(ObjectiveRef objective) { }
            public void StartQuest(QuestRef quest) { }
            public void PlayCutscene(CutsceneRef cutscene) => LastCutscene = cutscene;
            public void JoinPartyMember(string memberId) { }
            public void GrantSpell(string spellId) { }
        }
    }
}
