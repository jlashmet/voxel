namespace MountingForce.Story
{
    public interface IStoryActorRuntime
    {
        StoryInt3 Position { get; }
        void PlaceAt(StoryStagePoint destination);
        IStoryOperation MoveTo(StoryStagePoint destination, int durationHintMilliseconds);
        IStoryOperation FaceTowards(StoryInt3 targetPosition);
    }
}
