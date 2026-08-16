using System.Collections.Generic;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Api
{
    /// <summary>
    /// Stable rendering contract for semantic vegetation. Callers depend only on Rendering.Api;
    /// concrete GPU-instanced implementations stay inside Rendering.Runtime.
    /// </summary>
    public interface IVegetationBatchRenderer
    {
        int InstanceCount { get; }

        /// <summary>Unity-compatible enable switch for deterministic capture/showcase control.</summary>
        bool enabled { get; set; }

        void SetInstances(IReadOnlyList<VegetationInstance> instances);
        void Clear();
        void DrawNow();
    }

    /// <summary>
    /// Stable rendering contract for locally reconstructed ambient-life agents. Authoritative
    /// state remains the semantic cluster list supplied by AmbientLife.Api.
    /// </summary>
    public interface IAmbientLifeBatchRenderer
    {
        int AgentCount { get; }

        /// <summary>Unity-compatible enable switch for deterministic capture/showcase control.</summary>
        bool enabled { get; set; }

        void SetClusters(IReadOnlyList<AmbientLifeCluster> clusters);
        void Clear();
        void DrawNow();
    }
}
