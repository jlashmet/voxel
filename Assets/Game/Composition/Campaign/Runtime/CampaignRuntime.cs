using System;
using System.Collections.Generic;
using Game.Composition.Campaign;
using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using Game.Quests.Api;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Runtime
{
    /// <summary>
    /// Post-generation campaign host. WorldBuilder supplies immutable authored/runtime binding data;
    /// Story decides semantic transitions; Cutscenes executes choreography. This composition root owns
    /// the mutable session state that connects those systems without making either runtime depend on
    /// the other.
    /// </summary>
    public sealed class CampaignRuntime : IStoryStateView, IStoryEffectSink
    {
        private readonly CampaignBlueprint _blueprint;
        private readonly IWorldBoundCutsceneActorProvider _actorProvider;
        private readonly ICutscenePresentation _presentation;
        private readonly Dictionary<CutsceneRef, CutsceneSpec> _cutscenes =
            new Dictionary<CutsceneRef, CutsceneSpec>();
        private readonly Dictionary<CutsceneRef, CutsceneStageBinding> _stages =
            new Dictionary<CutsceneRef, CutsceneStageBinding>();
        private readonly HashSet<ObjectiveRef> _knownObjectives = new HashSet<ObjectiveRef>();
        private readonly HashSet<ObjectiveRef> _activeObjectives = new HashSet<ObjectiveRef>();
        private readonly HashSet<ObjectiveRef> _completedObjectives = new HashSet<ObjectiveRef>();
        private readonly HashSet<CutsceneRef> _completedCutscenes = new HashSet<CutsceneRef>();

        // Quests are tracked the way objectives are, by reference, because a CampaignBlueprint does
        // not carry QuestDefinitions yet. Story can therefore start a quest and ask whether one is
        // active or complete, which is the whole seam it needs; the richer step/completion machine
        // in Game.Quests.Runtime stays unwired until blueprints can supply the definitions it takes
        // in its constructor. Handing it an empty definition list here would make every StartQuest
        // throw on an unknown quest.
        private readonly HashSet<QuestRef> _activeQuests = new HashSet<QuestRef>();
        private readonly HashSet<QuestRef> _completedQuests = new HashSet<QuestRef>();

        private CutsceneRunner _activeRunner;
        private CutsceneRef _activeCutscene;

        public bool HasActiveCutscene => _activeRunner != null;

        public CutsceneRef ActiveCutscene
        {
            get
            {
                if (_activeRunner == null)
                    throw new InvalidOperationException("No campaign cutscene is currently active.");
                return _activeCutscene;
            }
        }

        public CampaignRuntime(
            CampaignBlueprint blueprint,
            IReadOnlyList<CutsceneStageRealization> stages,
            IWorldBoundCutsceneActorProvider actorProvider,
            ICutscenePresentation presentation)
        {
            _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            _actorProvider = actorProvider ?? throw new ArgumentNullException(nameof(actorProvider));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i]
                    ?? throw new InvalidOperationException(
                        "Campaign blueprint contains a null cutscene at index " + i + ".");
                if (_cutscenes.ContainsKey(cutscene.Ref))
                    throw new InvalidOperationException(
                        "Campaign blueprint contains duplicate cutscene ref '" + cutscene.Ref + "'.");
                _cutscenes.Add(cutscene.Ref, cutscene);
            }

            for (var i = 0; i < blueprint.Objectives.Count; i++)
            {
                ObjectiveSpec objective = blueprint.Objectives[i]
                    ?? throw new InvalidOperationException(
                        "Campaign blueprint contains a null objective at index " + i + ".");
                if (!_knownObjectives.Add(objective.Ref))
                    throw new InvalidOperationException(
                        "Campaign blueprint contains duplicate objective ref '" + objective.Ref + "'.");
            }

            for (var i = 0; i < stages.Count; i++)
            {
                CutsceneStageRealization stage = stages[i]
                    ?? throw new InvalidOperationException(
                        "Cutscene stage realization collection contains null at index " + i + ".");

                CutsceneSpec cutscene;
                if (!_cutscenes.TryGetValue(stage.Cutscene, out cutscene))
                    throw new InvalidOperationException(
                        "Stage realization references unknown cutscene '" + stage.Cutscene + "'.");
                if (!cutscene.Site.Equals(stage.Site))
                    throw new InvalidOperationException(
                        "Stage realization for cutscene '" + stage.Cutscene +
                        "' belongs to site '" + stage.Site + "' instead of authored site '" +
                        cutscene.Site + "'.");
                if (_stages.ContainsKey(stage.Cutscene))
                    throw new InvalidOperationException(
                        "Cutscene '" + stage.Cutscene + "' has more than one realized stage binding.");

                _stages.Add(stage.Cutscene, stage.Binding);
            }
        }

        public int StartNewGame() => Dispatch(StoryEvent.NewGame());

        public int InteractWithNpc(NpcRef npc)
        {
            int matched = Dispatch(StoryEvent.NpcInteracted(npc));

            // Objective-active conditions for this interaction observe the pre-completion state.
            // Completion occurs only after every matching story rule has been evaluated/applied.
            CompleteInteractionObjectives(npc);
            return matched;
        }

        public void Tick(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (_activeRunner == null) return;

            _activeRunner.Tick(elapsedMilliseconds);
            if (!_activeRunner.IsComplete) return;

            CutsceneRef completed = _activeCutscene;
            _activeRunner = null;
            _activeCutscene = default(CutsceneRef);
            _completedCutscenes.Add(completed);

            // Clear the active slot before dispatch so a completion rule may immediately start the
            // next authored cutscene without violating the one-active-cutscene invariant.
            Dispatch(StoryEvent.CutsceneCompleted(completed));
        }

        public bool IsObjectiveActive(ObjectiveRef objective) =>
            _activeObjectives.Contains(objective);

        public bool IsObjectiveCompleted(ObjectiveRef objective) =>
            _completedObjectives.Contains(objective);

        public bool IsCutsceneCompleted(CutsceneRef cutscene) =>
            _completedCutscenes.Contains(cutscene);

        public bool IsQuestActive(QuestRef quest) => _activeQuests.Contains(quest);

        public bool IsQuestCompleted(QuestRef quest) => _completedQuests.Contains(quest);

        /// <summary>
        /// Records a quest completion and lets story rules react to it.
        ///
        /// Gameplay owns when a quest is finished — unlike an objective, nothing in the blueprint
        /// describes how a quest completes — so this is the entry point that turns that decision
        /// into the <see cref="StoryEvent.QuestCompleted"/> the rules can trigger on.
        /// </summary>
        public int CompleteQuest(QuestRef quest)
        {
            if (!_activeQuests.Remove(quest))
                throw new InvalidOperationException(
                    "Cannot complete quest '" + quest + "' because it is not active.");

            _completedQuests.Add(quest);
            return Dispatch(StoryEvent.QuestCompleted(quest));
        }

        void IStoryEffectSink.StartObjective(ObjectiveRef objective) => StartObjective(objective);
        void IStoryEffectSink.StartQuest(QuestRef quest) => StartQuest(quest);
        void IStoryEffectSink.PlayCutscene(CutsceneRef cutscene) => PlayCutscene(cutscene);

        private int Dispatch(StoryEvent storyEvent) =>
            StoryRuleEngine.Dispatch(
                _blueprint.StoryRules,
                storyEvent,
                this,
                this);

        private void StartObjective(ObjectiveRef objective)
        {
            if (!_knownObjectives.Contains(objective))
                throw new InvalidOperationException(
                    "Story attempted to start unknown objective '" + objective + "'.");
            if (_completedObjectives.Contains(objective))
                throw new InvalidOperationException(
                    "Story attempted to restart completed objective '" + objective + "'.");

            _activeObjectives.Add(objective);
        }

        private void StartQuest(QuestRef quest)
        {
            if (_completedQuests.Contains(quest))
                throw new InvalidOperationException(
                    "Story attempted to restart completed quest '" + quest + "'.");

            // Deliberately no known-quest check: objectives are validated against the blueprint's
            // Objectives list, and there is no equivalent list for quests to validate against.
            _activeQuests.Add(quest);
        }

        private void PlayCutscene(CutsceneRef cutscene)
        {
            if (_activeRunner != null)
                throw new InvalidOperationException(
                    "Cannot start cutscene '" + cutscene + "' while cutscene '" +
                    _activeCutscene + "' is still active.");

            CutsceneSpec spec;
            if (!_cutscenes.TryGetValue(cutscene, out spec))
                throw new InvalidOperationException(
                    "Story attempted to play unknown cutscene '" + cutscene + "'.");

            CutsceneStageBinding stage = ResolveStage(spec);
            var actors = new WorldBoundCutsceneActorController(spec, _actorProvider);
            CutsceneRunner runner = CutscenePlayback.Start(
                spec.Definition,
                actors,
                _presentation,
                stage);

            _activeCutscene = cutscene;
            _activeRunner = runner;
        }

        private CutsceneStageBinding ResolveStage(CutsceneSpec cutscene)
        {
            CutsceneStageBinding stage;
            if (_stages.TryGetValue(cutscene.Ref, out stage))
                return stage;

            if (cutscene.Definition.RequiredStagePoints.Count == 0)
                return new CutsceneStageBinding();

            throw new InvalidOperationException(
                "Cutscene '" + cutscene.Ref + "' requires " +
                cutscene.Definition.RequiredStagePoints.Count +
                " realized stage point(s), but no stage realization was supplied.");
        }

        private void CompleteInteractionObjectives(NpcRef npc)
        {
            for (var i = 0; i < _blueprint.Objectives.Count; i++)
            {
                ObjectiveSpec objective = _blueprint.Objectives[i];
                if (!_activeObjectives.Contains(objective.Ref)) continue;

                InteractWithNpcTriggerSpec interaction =
                    objective.Completion as InteractWithNpcTriggerSpec;
                if (interaction == null || !interaction.Npc.Equals(npc)) continue;

                _activeObjectives.Remove(objective.Ref);
                _completedObjectives.Add(objective.Ref);
            }
        }
    }
}
