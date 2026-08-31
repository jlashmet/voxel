using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Api
{
    /// <summary>
    /// Render detail selected by game/composition policy for a semantic far-world structure.
    /// Distance thresholds are intentionally not part of this engine contract.
    /// </summary>
    public enum FarStructureTier : byte
    {
        Culled = 0,
        Mid = 1,
        Far = 2,
        Horizon = 3
    }

    [Flags]
    public enum FarStructureVisualFlags : byte
    {
        None = 0,
        SettlementAnchor = 1 << 0,
        Landmark = 1 << 1,
        HorizonLandmark = 1 << 2,
        NearSurfaceReady = 1 << 3
    }

    /// <summary>
    /// Engine-facing, render-ready description of one far structure. It deliberately contains
    /// no WorldBuilder intent, voxel-storage, region-residency, renderer, or GameObject state.
    /// </summary>
    public readonly struct FarStructureInstance
    {
        public FarStructureInstance(
            ulong stableId,
            float3 position,
            quaternion rotation,
            float3 scale,
            float3 boundsCenter,
            float3 boundsExtents,
            string proxyKey,
            string styleKey,
            FarStructureTier tier,
            FarStructureVisualFlags flags = FarStructureVisualFlags.None)
        {
            StableId = stableId;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            BoundsCenter = boundsCenter;
            BoundsExtents = boundsExtents;
            ProxyKey = proxyKey ?? string.Empty;
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
        public string ProxyKey { get; }
        public string StyleKey { get; }
        public FarStructureTier Tier { get; }
        public FarStructureVisualFlags Flags { get; }
    }

    /// <summary>
    /// Stable rendering boundary for far semantic structures. Game/composition code selects
    /// visibility and tiers; Rendering.Runtime owns proxy-mesh caching, batching, and drawing.
    /// </summary>
    public interface IFarStructureRenderer
    {
        int InstanceCount { get; }

        /// <summary>Unity-compatible enable switch for deterministic capture/showcase control.</summary>
        bool enabled { get; set; }

        void SetInstances(IReadOnlyList<FarStructureInstance> instances);
        void Clear();
        void DrawNow();
    }
}
