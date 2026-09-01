using System.Collections.Generic;
using System.Linq;
using Game.Cutscenes.Api;
using Game.Quests.Api;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StoryRuleEngineTests
    {
        private sealed class State : IStoryStateView
        {
            public readonly HashSet<ObjectiveRef> ActiveObjectives = new HashSet<ObjectiveRef>();
            public readonly HashSet<CutsceneRef> CompletedCutscenes = new HashSet<CutsceneRef>();
            public readonly HashSet<QuestRef> ActiveQuests = new HashSet<QuestRef>();
            public readonly HashSet<QuestRef> CompletedQuests = new HashSet<QuestRef>();

            public bool IsObjectiveActive(ObjectiveRef objective) => ActiveObjectives.Contains(objective);
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => CompletedCutscenes.Contains(cutscene);
            public bool IsQuestActive(QuestRef quest) => ActiveQuests.Contains(quest);
            public bool IsQuestCompleted(QuestRef quest) => CompletedQuests.Contains(quest);
        }

        private sealed class RecordingSink : IStoryEffectSink
        {
            private readonly State _state;
            public readonly List<string> Effects = new List<string>();

            public RecordingSink(State state) => _state = state;

            public void StartObjective(ObjectiveRef objective)
            {
                Effects.Add("start-objective:" + objective.Id);
                _state.ActiveObjectives.Add(objective);
            }

            public void StartQuest(QuestRef quest)
            {
                Effects.Add("start-quest:" + quest.Id);
                _state.ActiveQuests.Add(quest);
            }

            public void PlayCutscene(CutsceneRef cutscene)
            {
                Effects.Add("play-cutscene:" + cutscene.Id);
            }
        }

        [Test]
        public void OpeningFlowDispatchesThroughSeparateSemanticEvents()
        {
            var game = Campaign.Create("story-runtime-opening");
            SiteRef pub = game.World.RequireSite("pub", site => site.Archetype(SiteArchetype.Pub));
            SiteRef destination = game.World.RequireSite("destination", site => site.Archetype(SiteArchetype.Ruin));
            NpcRef destinationNpc = game.World.RequireNpc("destination-npc", npc => npc
                .PlaceAt(destination)
                .RequireConversation());

            ObjectiveRef travel = game.Story.Objective("travel", objective => objective
                .Target(destination)
                .CompleteWhen(ObjectiveCompletion.InteractWith(destinationNpc)));

            CutsceneRef intro = game.Story.Cutscene(EmptyCutscene("intro"), scene => scene.At(pub));
            CutsceneRef destinationScene = game.Story.Cutscene(
                EmptyCutscene("destination-scene"),
                scene => scene.At(destination));

            game.Story.Rule("start-intro", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.PlayCutscene(intro)));

            game.Story.Rule("travel-after-intro", rule => rule
                .When(StoryTrigger.CutsceneCompleted(intro))
                .Then(StoryEffect.StartObjective(travel)));

            game.Story.Rule("destination-dialogue", rule => rule
                .When(StoryTrigger.InteractWith(destinationNpc))
                .If(StoryCondition.ObjectiveActive(travel))
                .If(StoryCondition.CutsceneNotCompleted(destinationScene))
                .Then(StoryEffect.PlayCutscene(destinationScene)));

            CampaignBlueprint blueprint = game.Build();
            var state = new State();
            var sink = new RecordingSink(state);

            Assert.That(
                StoryRuleEngine.Dispatch(blueprint.StoryRules, StoryEvent.NewGame(), state, sink),
                Is.EqualTo(1));
            Assert.That(sink.Effects, Is.EqualTo(new[] { "play-cutscene:intro" }));

            state.CompletedCutscenes.Add(intro);
            Assert.That(
                StoryRuleEngine.Dispatch(
                    blueprint.StoryRules,
                    StoryEvent.CutsceneCompleted(intro),
                    state,
                    sink),
                Is.EqualTo(1));
            Assert.That(sink.Effects.Last(), Is.EqualTo("start-objective:travel"));
            Assert.That(state.ActiveObjectives.Contains(travel), Is.True);

            Assert.That(
                StoryRuleEngine.Dispatch(
                    blueprint.StoryRules,
                    StoryEvent.NpcInteracted(destinationNpc),
                    state,
                    sink),
                Is.EqualTo(1));
            Assert.That(sink.Effects.Last(), Is.EqualTo("play-cutscene:destination-scene"));

            state.CompletedCutscenes.Add(destinationScene);
            int countBefore = sink.Effects.Count;
            Assert.That(
                StoryRuleEngine.Dispatch(
                    blueprint.StoryRules,
                    StoryEvent.NpcInteracted(destinationNpc),
                    state,
                    sink),
                Is.EqualTo(0));
            Assert.That(sink.Effects.Count, Is.EqualTo(countBefore));
        }

        [Test]
        public void OneRulesEffectsCannotEnableAnotherRuleDuringSameEvent()
        {
            var game = Campaign.Create("atomic-story-event");
            SiteRef site = game.World.RequireSite("site", value => value.Archetype(SiteArchetype.Pub));
            NpcRef npc = game.World.RequireNpc("npc", value => value.PlaceAt(site));
            ObjectiveRef objective = game.Story.Objective("objective", value => value
                .Target(site)
                .CompleteWhen(ObjectiveCompletion.InteractWith(npc)));
            CutsceneRef scene = game.Story.Cutscene(EmptyCutscene("scene"), value => value.At(site));

            game.Story.Rule("activate", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.StartObjective(objective)));

            game.Story.Rule("play-if-active", rule => rule
                .When(StoryTrigger.NewGame())
                .If(StoryCondition.ObjectiveActive(objective))
                .Then(StoryEffect.PlayCutscene(scene)));

            CampaignBlueprint blueprint = game.Build();
            var state = new State();
            var sink = new RecordingSink(state);

            int matched = StoryRuleEngine.Dispatch(
                blueprint.StoryRules,
                StoryEvent.NewGame(),
                state,
                sink);

            Assert.That(matched, Is.EqualTo(1));
            Assert.That(sink.Effects, Is.EqualTo(new[] { "start-objective:objective" }));
            Assert.That(state.ActiveObjectives.Contains(objective), Is.True,
                "The sink may mutate gameplay state after evaluation, but that mutation must not affect rule matching for the event already being dispatched.");
        }

        [Test]
        public void MatchingRulesAndEffectsExecuteInAuthoredOrder()
        {
            var game = Campaign.Create("story-order");
            SiteRef site = game.World.RequireSite("site", value => value.Archetype(SiteArchetype.Pub));
            NpcRef npc = game.World.RequireNpc("npc", value => value.PlaceAt(site));
            ObjectiveRef firstObjective = game.Story.Objective("first", value => value
                .Target(site)
                .CompleteWhen(ObjectiveCompletion.InteractWith(npc)));
            ObjectiveRef secondObjective = game.Story.Objective("second", value => value
                .Target(site)
                .CompleteWhen(ObjectiveCompletion.InteractWith(npc)));
            CutsceneRef scene = game.Story.Cutscene(EmptyCutscene("scene"), value => value.At(site));

            game.Story.Rule("first-rule", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.StartObjective(firstObjective))
                .Then(StoryEffect.PlayCutscene(scene)));
            game.Story.Rule("second-rule", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.StartObjective(secondObjective)));

            CampaignBlueprint blueprint = game.Build();
            var state = new State();
            var sink = new RecordingSink(state);

            Assert.That(
                StoryRuleEngine.Dispatch(blueprint.StoryRules, StoryEvent.NewGame(), state, sink),
                Is.EqualTo(2));
            Assert.That(sink.Effects, Is.EqualTo(new[]
            {
                "start-objective:first",
                "play-cutscene:scene",
                "start-objective:second"
            }));
        }

        private static CutsceneDefinition EmptyCutscene(string id) =>
            new CutsceneDefinition(
                id,
                CutsceneStageSetupDefinition.Empty,
                new CutsceneStep[0]);
    }
}
