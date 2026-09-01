using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Api
{
    /// <summary>
    /// Render detail selected by game/composition policy for a semantic far-world feature.
    /// Distance thresholds are intentionally not part of this engine contract.
    /// </summary>
    public enum FarFeatureTier : byte
    {
        Culled = 0,
        Mid = 1,
        Far = 2,
        Horizon = 3
    }

    [Flags]
    public enum FarFeatureVisualFlags : byte
    {
        None = 0,
        SettlementAnchor = 1 << 0,
        Landmark = 1 << 1,
        HorizonLandmark = 1 << 2,
        NearSurfaceReady = 1 << 3
    }

    /// <summary>
    /// Engine-facing, render-ready description of one far feature. It deliberately contains
    /// no planning, voxel-storage, region-residency, renderer, or GameObject state.
    /// </summary>
    public readonly struct FarFeatureInstance
    {
        public FarFeatureInstance(
            ulong stableId,
            float3 position,
            quaternion rotation,
            float3 scale,
            float3 boundsCenter,
            float3 boundsExtents,
            string geometryKey,
            string styleKey,
            FarFeatureTier tier,
            FarFeatureVisualFlags flags = FarFeatureVisualFlags.None)
        {
            StableId = stableId;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            BoundsCenter = boundsCenter;
            BoundsExtents = boundsExtents;
            GeometryKey = geometryKey ?? string.Empty;
            StyleKey = styleKey ?? string.Empty;
            Tier = tier;
            Flags = flags;
        }

        public ulong StableId { get; }
        public float3 Position { get; }
        public quaternion Rotation { get; }
        public float3 Scale { get; }
        public float3 BoundsCenter { get; }
        public float3 BoundsExtents { get; }
        public string GeometryKey { get; }
        public string StyleKey { get; }
        public FarFeatureTier Tier { get; }
        public FarFeatureVisualFlags Flags { get; }
    }

    /// <summary>
    /// Stable rendering boundary for semantic far features. Game/composition code selects
    /// visibility and tiers; Rendering.Runtime owns geometry caching, batching, and drawing.
    /// </summary>
    public interface IFarFeatureRenderer
    {
        int InstanceCount { get; }

        /// <summary>Unity-compatible enable switch for deterministic capture/showcase control.</summary>
        bool enabled { get; set; }

        void SetInstances(IReadOnlyList<FarFeatureInstance> instances);
        void Clear();
        void DrawNow();
    }
}
