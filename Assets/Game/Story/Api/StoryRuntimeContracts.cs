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
        QuestCompleted = 3,
        SiteEntered = 4
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

        private StoryEvent(
            StoryEventKind kind,
            NpcRef npc,
            CutsceneRef cutscene,
            QuestRef quest,
            SiteRef site)
        {
            Kind = kind;
            Npc = npc;
            Cutscene = cutscene;
            Quest = quest;
            Site = site;
        }

        public static StoryEvent NewGame() =>
            new StoryEvent(StoryEventKind.NewGame, default, default, default, default);

        public static StoryEvent NpcInteracted(NpcRef npc) =>
            new StoryEvent(StoryEventKind.NpcInteracted, npc, default, default, default);

        public static StoryEvent CutsceneCompleted(CutsceneRef cutscene) =>
            new StoryEvent(StoryEventKind.CutsceneCompleted, default, cutscene, default, default);

        public static StoryEvent QuestCompleted(QuestRef quest) =>
            new StoryEvent(StoryEventKind.QuestCompleted, default, default, quest, default);

        public static StoryEvent SiteEntered(SiteRef site) =>
            new StoryEvent(StoryEventKind.SiteEntered, default, default, default, site);
    }

    public interface IStoryStateView
    {
        bool IsObjectiveActive(ObjectiveRef objective);
        bool IsQuestActive(QuestRef quest);
        bool IsQuestCompleted(QuestRef quest);
        bool IsCutsceneCompleted(CutsceneRef cutscene);
    }

    public interface IStoryEffectSink
    {
        void StartObjective(ObjectiveRef objective);
        void StartQuest(QuestRef quest);
        void PlayCutscene(CutsceneRef cutscene);
    }
}
