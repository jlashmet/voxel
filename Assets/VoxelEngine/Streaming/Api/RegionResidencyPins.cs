using System;
using Unity.Mathematics;

namespace VoxelEngine.Streaming.Api
{
    /// <summary>
    /// Ownership-safe residency pin for one logical region. Disposing the lease releases only the
    /// caller's pin; it does not force eviction or override other streaming policy.
    /// </summary>
    public interface IRegionResidencyLease : IDisposable
    {
        int3 RegionCoord { get; }
        bool IsReady { get; }
    }

    /// <summary>
    /// Optional ownership extension to <see cref="IRegionStreaming"/> for systems that require a
    /// region to remain resident while a semantic consumer is active.
    /// </summary>
    public interface IRegionResidencyPins
    {
        IRegionResidencyLease AcquireResidency(in RegionLoadRequest request);
    }
}
