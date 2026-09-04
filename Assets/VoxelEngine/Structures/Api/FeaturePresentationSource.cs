using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Integer world-voxel bounds for sparse presentation queries. Querying these bounds is a
    /// metadata operation only; it does not imply voxel-region generation or residency.
    /// </summary>
    public readonly struct FeaturePresentationBounds : IEquatable<FeaturePresentationBounds>
    {
        public int3 Min { get; }
        public int3 Max { get; }

        public FeaturePresentationBounds(int3 min, int3 max)
        {
            if (max.x <= min.x || max.y <= min.y || max.z <= min.z)
                throw new ArgumentOutOfRangeException(nameof(max), "Presentation query bounds must have positive extent.");
            Min = min;
            Max = max;
        }

        public bool Intersects(FeaturePresentationBake bake) =>
            bake.BoundsMax.x >= Min.x && bake.BoundsMin.x < Max.x
            && bake.BoundsMax.y >= Min.y && bake.BoundsMin.y < Max.y
            && bake.BoundsMax.z >= Min.z && bake.BoundsMin.z < Max.z;

        public bool Equals(FeaturePresentationBounds other) => Min.Equals(other.Min) && Max.Equals(other.Max);
        public override bool Equals(object obj) => obj is FeaturePresentationBounds other && Equals(other);
        public override int GetHashCode() => (Min.GetHashCode() * 397) ^ Max.GetHashCode();
    }

    /// <summary>
    /// Read-only sparse visibility cache for derived feature-presentation bakes. Implementations
    /// index presentation data only and must not retain voxel bricks, renderer objects, collision,
    /// interiors, AI, or physics state.
    /// </summary>
    public interface IFeaturePresentationSource
    {
        bool TryGet(ulong sourceId, out FeaturePresentationBake bake);

        /// <summary>
        /// Returns intersecting bakes in stable SourceId order, independent of insertion or sector
        /// traversal order.
        /// </summary>
        IReadOnlyList<FeaturePresentationBake> Query(FeaturePresentationBounds bounds);
    }
}
