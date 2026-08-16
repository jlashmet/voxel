using Unity.Jobs;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Frame-path synchronization acknowledgement. Geometry code must never wait for worker
    /// execution: callers poll IsCompleted and use this only once ready. The defensive check
    /// makes an accidental premature acknowledgement observable and non-blocking.
    /// </summary>
    internal static class GeometryFrameJobCompletionGuard
    {
        internal static bool TryCompleteReady(JobHandle handle, ref ulong violationCount)
        {
            if (!handle.IsCompleted)
            {
                violationCount++;
                return false;
            }

            handle.Complete();
            return true;
        }
    }
}
