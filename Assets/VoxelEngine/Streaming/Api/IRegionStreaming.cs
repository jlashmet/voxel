using Unity.Mathematics;

namespace VoxelEngine.Streaming.Api
{
    /// <summary>
    /// Application-facing orchestration capability for region residency/loading.
    /// Streaming owns residency policy; callers never manipulate Storage representation directly.
    /// </summary>
    public interface IRegionStreaming
    {
        void QueueLoad(in RegionLoadRequest request);
        int PublishLoaded(float mainThreadBudgetMs);

        bool IsResident(int3 regionCoord);
        bool Evict(int3 regionCoord);
    }
}
