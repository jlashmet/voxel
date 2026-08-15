namespace VoxelEngine.Streaming.Api
{
    /// <summary>Application-facing orchestration capability for asynchronous region loading.</summary>
    public interface IRegionStreaming
    {
        void QueueLoad(in RegionLoadRequest request);
        int PublishLoaded(float mainThreadBudgetMs);
    }
}
