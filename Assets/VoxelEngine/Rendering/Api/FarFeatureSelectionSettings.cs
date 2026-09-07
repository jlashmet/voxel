using System;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Api
{
    /// <summary>Composition-owned presentation policy consumed by GPU selection.</summary>
    public readonly struct FarFeatureSelectionSettings
    {
        public readonly float4 DetailThresholds;
        public readonly float2 HorizonThresholds;
        public readonly float3 DistanceCaps;
        public readonly float FocalPixels;

        public FarFeatureSelectionSettings(float4 detailThresholds, float2 horizonThresholds,
                                            float3 distanceCaps, float verticalFovDegrees, int viewportHeight)
        {
            if (!math.all(math.isfinite(detailThresholds)) || !math.all(math.isfinite(horizonThresholds))
                || !(detailThresholds.x > detailThresholds.y && detailThresholds.y > detailThresholds.z
                && detailThresholds.z > detailThresholds.w && detailThresholds.w > horizonThresholds.x
                && horizonThresholds.x > horizonThresholds.y && horizonThresholds.y > 0))
                throw new ArgumentOutOfRangeException(nameof(detailThresholds));
            if (!math.all(math.isfinite(distanceCaps)) || !(distanceCaps.x > 0
                && distanceCaps.y >= distanceCaps.x && distanceCaps.z >= distanceCaps.y))
                throw new ArgumentOutOfRangeException(nameof(distanceCaps));
            if (!(verticalFovDegrees > 1 && verticalFovDegrees < 179) || viewportHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
            DetailThresholds = detailThresholds;
            HorizonThresholds = horizonThresholds;
            DistanceCaps = distanceCaps;
            FocalPixels = viewportHeight * 0.5f / math.tan(math.radians(verticalFovDegrees) * 0.5f);
        }
    }
}
