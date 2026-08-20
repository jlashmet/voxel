using System;
using Game.Quests.Api;
using Game.WorldBuilder.Api;

namespace Game.Story.Api
{
    public enum StoryEventKind
    {
        NewGame = 0,
        NpcInteracted = 1,
        CutsceneCompleted = 2,
        QuestCompleted = 3
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

        private StoryEvent(
            StoryEventKind kind,
            NpcRef npc,
            CutsceneRef cutscene,
            QuestRef quest)
        {
            Kind = kind;
            Npc = npc;
            Cutscene = cutscene;
            Quest = quest;
        }

        public static StoryEvent NewGame() =>
            new StoryEvent(StoryEventKind.NewGame, default, default, default);

        public static StoryEvent NpcInteracted(NpcRef npc) =>
            new StoryEvent(StoryEventKind.NpcInteracted, npc, default, default);

        public static StoryEvent CutsceneCompleted(CutsceneRef cutscene) =>
            new StoryEvent(StoryEventKind.CutsceneCompleted, default, cutscene, default);

        public static StoryEvent QuestCompleted(QuestRef quest) =>
            new StoryEvent(StoryEventKind.QuestCompleted, default, default, quest);
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
}
