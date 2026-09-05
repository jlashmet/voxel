using System;
using Game.Encounters.Api;
using Game.Outcomes.Api;
using Game.Quests.Api;
using Game.WorldBuilder.Api;

namespace Game.Story.Api
{
    public enum StoryEventKind
    {
        NewGame = 0,
        NpcInteracted = 1,
        CutsceneCompleted = 2,
        QuestCompleted = 3,
        SiteProximityEntered = 4,
        EncounterResolved = 5
    }

    /// <summary>
    /// Semantic gameplay event consumed by story rules. It carries stable authored identities, never
    /// scene objects, positions, or engine references.
    /// </summary>
    public readonly struct StoryEvent
    {
        public StoryEventKind Kind { get; }
        public NpcRef Npc { get; }
        public CutsceneRef Cutscene { get; }
        public QuestRef Quest { get; }
        public SiteRef Site { get; }
        public EncounterId Encounter { get; }
        public EncounterResolutionResult EncounterResult { get; }

        private StoryEvent(
            StoryEventKind kind,
            NpcRef npc,
            CutsceneRef cutscene,
            QuestRef quest,
            SiteRef site,
            EncounterId encounter,
            EncounterResolutionResult encounterResult)
        {
            Kind = kind;
            Npc = npc;
            Cutscene = cutscene;
            Quest = quest;
            Site = site;
            Encounter = encounter;
            EncounterResult = encounterResult;
        }

        public static StoryEvent NewGame() =>
            new StoryEvent(StoryEventKind.NewGame, default, default, default, default, default, default);

        public static StoryEvent NpcInteracted(NpcRef npc) =>
            new StoryEvent(StoryEventKind.NpcInteracted, npc, default, default, default, default, default);

        public static StoryEvent CutsceneCompleted(CutsceneRef cutscene) =>
            new StoryEvent(StoryEventKind.CutsceneCompleted, default, cutscene, default, default, default, default);

        public static StoryEvent QuestCompleted(QuestRef quest) =>
            new StoryEvent(StoryEventKind.QuestCompleted, default, default, quest, default, default, default);

        public static StoryEvent SiteProximityEntered(SiteRef site) =>
            new StoryEvent(StoryEventKind.SiteProximityEntered, default, default, default, site, default, default);

        public static StoryEvent EncounterResolved(
            EncounterId encounter,
            EncounterResolutionResult result)
        {
            if (!encounter.IsValid)
                throw new ArgumentException("Encounter id is required.", nameof(encounter));
            return new StoryEvent(
                StoryEventKind.EncounterResolved,
                default,
                default,
                default,
                default,
                encounter,
                result);
        }
    }

    /// <summary>
    /// Read-only story state queried while an event is being evaluated. Dispatch is event-atomic:
    /// every condition for one incoming event observes this pre-effect state snapshot.
    /// </summary>
    public interface IStoryStateView
    {
        bool IsObjectiveActive(ObjectiveRef objective);
        bool IsQuestActive(QuestRef quest);
        bool IsQuestCompleted(QuestRef quest);
        bool IsCutsceneCompleted(CutsceneRef cutscene);
    }

    /// <summary>
    /// Runtime integration seam. Story decides semantic effects; gameplay systems own how quests,
    /// legacy objectives, and cutscenes are actually started.
    /// </summary>
    public interface IStoryEffectSink
    {
        void StartObjective(ObjectiveRef objective);
        void StartQuest(QuestRef quest);
        void PlayCutscene(CutsceneRef cutscene);
    }

    /// <summary>
    /// Additive campaign-progression effects used only when source content changes persistent player
    /// state. Keeping this separate preserves existing IStoryEffectSink consumers.
    /// </summary>
    public interface IStoryProgressEffectSink : IStoryEffectSink
    {
        void JoinPartyMember(string memberId);
        void GrantSpell(string spellId);
    }

    /// <summary>
    /// Additive terminal-policy seam. Story may publish an authored semantic outcome condition, but
    /// cannot resolve or mutate GameOutcome directly; System 15 remains the terminal authority.
    /// </summary>
    public interface IStoryOutcomeEffectSink : IStoryProgressEffectSink
    {
        void ObserveOutcomeCondition(OutcomeConditionRef condition);
    }
}
