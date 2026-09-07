using System;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Api
{
    /// <summary>
    /// Resolved cap geometry in the same normalized instance space as the far primitive bounds.
    /// Radii are component-wise extents, not scalar radii: normalization can scale each axis
    /// differently. Composition resolves source direction and cell-envelope conventions before
    /// crossing this boundary; Rendering does not consume source primitives or material IDs.
    /// </summary>
    public readonly struct FarFeatureFrustum
    {
        public FarFeatureFrustum(float3 lowerCenter, float3 upperCenter, float3 lowerRadii, float3 upperRadii)
        {
            if (!math.all(math.isfinite(lowerCenter)) || !math.all(math.isfinite(upperCenter))
                || !math.all(math.isfinite(lowerRadii)) || !math.all(math.isfinite(upperRadii))
                || math.any(lowerRadii < 0f) || math.any(upperRadii < 0f))
                throw new ArgumentException("Far frustum cap geometry must be finite with nonnegative radial extents.");
            LowerCenter = lowerCenter;
            UpperCenter = upperCenter;
            LowerRadii = lowerRadii;
            UpperRadii = upperRadii;
            IsDefined = true;
        }

        public float3 LowerCenter { get; }
        public float3 UpperCenter { get; }
        public float3 LowerRadii { get; }
        public float3 UpperRadii { get; }
        public bool IsDefined { get; }
    }
}
