using System;
using System.Collections.Generic;
using Game.Composition.Campaign;
using Game.Cutscenes.Api;
using Game.Cutscenes.Runtime;
using Game.Quests.Api;
using Game.Quests.Runtime;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Runtime
{
    /// <summary>
    /// Minimal persistent campaign progression owned by this runtime. The snapshot intentionally
    /// contains only one-shot cutscene completion and source-backed player progression effects; it is
    /// deterministic and engine-independent so a save layer can serialize it without scene objects.
    /// </summary>
    public sealed class CampaignProgressSnapshot
    {
        private readonly CutsceneRef[] _completedCutscenes;
        private readonly string[] _joinedPartyMembers;
        private readonly string[] _grantedSpells;

        public IReadOnlyList<CutsceneRef> CompletedCutscenes => _completedCutscenes;
        public IReadOnlyList<string> JoinedPartyMembers => _joinedPartyMembers;
        public IReadOnlyList<string> GrantedSpells => _grantedSpells;

        public CampaignProgressSnapshot(
            IEnumerable<CutsceneRef> completedCutscenes,
            IEnumerable<string> joinedPartyMembers,
            IEnumerable<string> grantedSpells)
        {
            if (completedCutscenes == null) throw new ArgumentNullException(nameof(completedCutscenes));
            if (joinedPartyMembers == null) throw new ArgumentNullException(nameof(joinedPartyMembers));
            if (grantedSpells == null) throw new ArgumentNullException(nameof(grantedSpells));

            _completedCutscenes = new List<CutsceneRef>(completedCutscenes).ToArray();
            _joinedPartyMembers = new List<string>(joinedPartyMembers).ToArray();
            _grantedSpells = new List<string>(grantedSpells).ToArray();
        }
    }

    /// <summary>
    /// Post-generation campaign host. WorldBuilder supplies immutable authored/runtime binding data;
    /// Story decides semantic transitions; Cutscenes executes choreography. This composition root owns
    /// the mutable session state that connects those systems without making either runtime depend on
    /// the other.
    /// </summary>
    public sealed class CampaignRuntime : IStoryStateView, IStoryProgressEffectSink
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
        private readonly HashSet<string> _joinedPartyMembers = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _grantedSpells = new HashSet<string>(StringComparer.Ordinal);
        private readonly QuestRuntime _quests;

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
            ICutscenePresentation presentation,
            IReadOnlyList<QuestDefinition> questDefinitions = null)
        {
            _blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            _actorProvider = actorProvider ?? throw new ArgumentNullException(nameof(actorProvider));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _quests = new QuestRuntime(questDefinitions ?? Array.Empty<QuestDefinition>());

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

        public int EnterSite(SiteRef site) => Dispatch(StoryEvent.SiteProximityEntered(site));

        public int InteractWithNpc(NpcRef npc)
        {
            // Story gets first refusal so the same physical interaction can start a quest and then
            // immediately be observed by the authoritative quest state machine.
            int matched = Dispatch(StoryEvent.NpcInteracted(npc));
            ObserveQuest(QuestObservation.NpcInteracted(npc.ToString()));

            // Objective-active conditions for this interaction observe the pre-completion state.
            // Completion occurs only after every matching story rule has been evaluated/applied.
            CompleteInteractionObjectives(npc);
            return matched;
        }

        public IReadOnlyList<QuestEvent> ObserveQuest(QuestObservation observation)
        {
            IReadOnlyList<QuestEvent> events = _quests.Observe(observation);
            DispatchQuestCompletions(events);
            return events;
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

            Dispatch(StoryEvent.CutsceneCompleted(completed));
        }

        public bool IsObjectiveActive(ObjectiveRef objective) =>
            _activeObjectives.Contains(objective);

        public bool IsObjectiveCompleted(ObjectiveRef objective) =>
            _completedObjectives.Contains(objective);

        public bool IsCutsceneCompleted(CutsceneRef cutscene) =>
            _completedCutscenes.Contains(cutscene);

        public bool IsQuestActive(QuestRef quest) => _quests.IsActive(quest);

        public bool IsQuestCompleted(QuestRef quest) => _quests.IsCompleted(quest);

        public bool IsPartyMemberJoined(string memberId) =>
            _joinedPartyMembers.Contains(RequireProgressId(memberId, nameof(memberId)));

        public bool HasSpell(string spellId) =>
            _grantedSpells.Contains(RequireProgressId(spellId, nameof(spellId)));

        public QuestSnapshot GetQuestSnapshot(QuestRef quest) => _quests.GetSnapshot(quest);

        public int CompleteQuest(QuestRef quest)
        {
            IReadOnlyList<QuestEvent> events = _quests.Complete(quest);
            return DispatchQuestCompletions(events);
        }

        public CampaignProgressSnapshot CaptureProgress()
        {
            var completed = new List<CutsceneRef>(_completedCutscenes);
            completed.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));

            var members = new List<string>(_joinedPartyMembers);
            members.Sort(StringComparer.Ordinal);

            var spells = new List<string>(_grantedSpells);
            spells.Sort(StringComparer.Ordinal);

            return new CampaignProgressSnapshot(completed, members, spells);
        }

        public void RestoreProgress(CampaignProgressSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (_activeRunner != null)
                throw new InvalidOperationException("Cannot restore campaign progress while a cutscene is active.");

            var completed = new HashSet<CutsceneRef>();
            for (var i = 0; i < snapshot.CompletedCutscenes.Count; i++)
            {
                CutsceneRef cutscene = snapshot.CompletedCutscenes[i];
                if (!_cutscenes.ContainsKey(cutscene))
                    throw new InvalidOperationException(
                        "Campaign progress references unknown cutscene '" + cutscene + "'.");
                completed.Add(cutscene);
            }

            var members = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < snapshot.JoinedPartyMembers.Count; i++)
                members.Add(RequireProgressId(snapshot.JoinedPartyMembers[i], "joinedPartyMembers"));

            var spells = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < snapshot.GrantedSpells.Count; i++)
                spells.Add(RequireProgressId(snapshot.GrantedSpells[i], "grantedSpells"));

            _completedCutscenes.Clear();
            foreach (CutsceneRef cutscene in completed)
                _completedCutscenes.Add(cutscene);

            _joinedPartyMembers.Clear();
            foreach (string member in members)
                _joinedPartyMembers.Add(member);

            _grantedSpells.Clear();
            foreach (string spell in spells)
                _grantedSpells.Add(spell);
        }

        void IStoryEffectSink.StartObjective(ObjectiveRef objective) => StartObjective(objective);
        void IStoryEffectSink.StartQuest(QuestRef quest) => StartQuest(quest);
        void IStoryEffectSink.PlayCutscene(CutsceneRef cutscene) => PlayCutscene(cutscene);
        void IStoryProgressEffectSink.JoinPartyMember(string memberId) =>
            _joinedPartyMembers.Add(RequireProgressId(memberId, nameof(memberId)));
        void IStoryProgressEffectSink.GrantSpell(string spellId) =>
            _grantedSpells.Add(RequireProgressId(spellId, nameof(spellId)));

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
            if (_quests.IsCompleted(quest)) return;
            _quests.Start(quest);
        }

        private int DispatchQuestCompletions(IReadOnlyList<QuestEvent> events)
        {
            int matched = 0;
            for (var i = 0; i < events.Count; i++)
                if (events[i].Kind == QuestEventKind.QuestCompleted)
                    matched += Dispatch(StoryEvent.QuestCompleted(events[i].Quest));
            return matched;
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

        private static string RequireProgressId(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Campaign progression ids must be non-empty.", paramName);
            return value;
        }
    }
}
