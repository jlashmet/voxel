using System;

namespace MountingForce.Story
{
    /// <summary>Completion barrier for movement, dialogue, camera transitions, and other story work.</summary>
    public interface IStoryOperation
    {
        bool IsComplete { get; }
    }

    public sealed class CompletedStoryOperation : IStoryOperation
    {
        public static readonly CompletedStoryOperation Instance = new CompletedStoryOperation();
        private CompletedStoryOperation() { }
        public bool IsComplete => true;
    }

    /// <summary>Authoritative gameplay seam. Implementations resolve semantic actor ids to runtime actors.</summary>
    public interface IStoryActorController
    {
        bool Contains(StoryActorId actor);
        void PlaceAt(StoryActorId actor, StoryStagePoint destination);
        IStoryOperation MoveTo(StoryActorId actor, StoryStagePoint destination, int durationHintMilliseconds);
        IStoryOperation FaceActor(StoryActorId actor, StoryActorId target);
        IStoryOperation FacePoint(StoryActorId actor, StoryStagePoint target);
    }

    /// <summary>Client-local presentation seam; camera, dialogue, and audio never own gameplay state.</summary>
    public interface IStoryPresentation
    {
        IStoryOperation SetCamera(StoryCueId cameraCue);
        IStoryOperation ShowDialogue(StoryCueId dialogueCue);
        IStoryOperation PlaySound(StoryCueId soundCue);
    }

    public sealed class StoryExecutionContext
    {
        public IStoryActorController Actors { get; }
        public IStoryPresentation Presentation { get; }
        public StoryStageBinding Stage { get; }

        public StoryExecutionContext(IStoryActorController actors, IStoryPresentation presentation, StoryStageBinding stage)
        {
            Actors = actors ?? throw new ArgumentNullException(nameof(actors));
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
        }
    }
}
