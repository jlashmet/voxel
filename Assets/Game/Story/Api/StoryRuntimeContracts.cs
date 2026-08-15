using System;
using Game.WorldBuilder.Api;

namespace Game.Story.Api
{
    public enum StoryEventKind
    {
        NewGame = 0,
        NpcInteracted = 1,
        CutsceneCompleted = 2
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

        private StoryEvent(StoryEventKind kind, NpcRef npc, CutsceneRef cutscene)
        {
            Kind = kind;
            Npc = npc;
            Cutscene = cutscene;
        }

        public static StoryEvent NewGame() =>
            new StoryEvent(StoryEventKind.NewGame, default, default);

        public static StoryEvent NpcInteracted(NpcRef npc) =>
            new StoryEvent(StoryEventKind.NpcInteracted, npc, default);

        public static StoryEvent CutsceneCompleted(CutsceneRef cutscene) =>
            new StoryEvent(StoryEventKind.CutsceneCompleted, default, cutscene);
    }

    /// <summary>
    /// Read-only story state queried while an event is being evaluated. Dispatch is event-atomic:
    /// every condition for one incoming event observes this pre-effect state snapshot.
    /// </summary>
    public interface IStoryStateView
    {
        bool IsObjectiveActive(ObjectiveRef objective);
        bool IsCutsceneCompleted(CutsceneRef cutscene);
    }

    /// <summary>
    /// Runtime integration seam. The story engine decides semantic effects; gameplay systems own how
    /// objectives and cutscenes are actually started.
    /// </summary>
    public interface IStoryEffectSink
    {
        void StartObjective(ObjectiveRef objective);
        void PlayCutscene(CutsceneRef cutscene);
    }
}
