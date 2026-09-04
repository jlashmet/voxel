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
        NearSurfaceReady = 1 << 3,
        Ruined = 1 << 4
    }

    /// <summary>
    /// Producer-agnostic primitive vocabulary for conservative far-feature massing. The values are
    /// presentation data only: no generation/runtime ownership crosses the rendering boundary.
    /// </summary>
    public enum FarFeatureGeometryShape : byte
    {
        Box = 0,
        Cylinder = 1,
        Prism = 2,
        Capsule = 3,
        Ramp = 4,
        RoundedBox = 5,
        Ellipsoid = 6,
        Frustum = 7,
        Annulus = 8,
        ArcWedge = 9,
        TerrainCorridor = 10,
    }

    /// <summary>
    /// One normalized conservative mass in a far-feature geometry resource. X/Z are centered around
    /// the instance origin while Y is measured upward from it, matching the renderer transform.
    /// </summary>
    public readonly struct FarFeatureGeometryPrimitive
    {
        public FarFeatureGeometryPrimitive(
            FarFeatureGeometryShape shape,
            float3 min,
            float3 max,
            byte axis = 1)
        {
            if (math.any(max < min)) throw new ArgumentException("Far geometry primitive bounds must be ordered.");
            Shape = shape;
            Min = min;
            Max = max;
            Axis = axis <= 2 ? axis : (byte)1;
        }

        public FarFeatureGeometryShape Shape { get; }
        public float3 Min { get; }
        public float3 Max { get; }
        public byte Axis { get; }
    }

    /// <summary>
    /// Immutable generic geometry payload derived from a canonical feature bake. Rendering.Runtime
    /// may cache this by GeometryKey; producers never register a renderer recipe or implementation.
    /// </summary>
    public sealed class FarFeatureGeometry
    {
        private readonly FarFeatureGeometryPrimitive[] _primitives;

        public FarFeatureGeometry(FarFeatureGeometryPrimitive[] primitives)
        {
            if (primitives == null) throw new ArgumentNullException(nameof(primitives));
            if (primitives.Length == 0)
                throw new ArgumentException("Far feature geometry requires at least one primitive.", nameof(primitives));
            _primitives = new FarFeatureGeometryPrimitive[primitives.Length];
            Array.Copy(primitives, _primitives, primitives.Length);
        }

        public int PrimitiveCount => _primitives.Length;
        public FarFeatureGeometryPrimitive GetPrimitive(int index) => _primitives[index];
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
            FarFeatureVisualFlags flags = FarFeatureVisualFlags.None,
            FarFeatureGeometry geometry = null)
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
            Geometry = geometry;
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
        public FarFeatureGeometry Geometry { get; }
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
