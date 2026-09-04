using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Derived presentation snapshot for one canonical generated feature instance.
    ///
    /// This is not world truth: the feature catalogue, placement and world seed remain authoritative.
    /// The snapshot exists so distant presentation can reuse the same ordered primitive stream without
    /// requiring any voxel region to be generated or resident. Primitive material/style/coating fields
    /// remain intact, so downstream presentation does not need an object-type recipe registry.
    /// </summary>
    public sealed class FeaturePresentationBake
    {
        private readonly Primitive[] _primitives;

        public ulong SourceId { get; }
        public ulong Revision { get; }
        public FeatureKind Kind { get; }
        public int3 Position { get; }
        public byte Orientation { get; }
        public int3 BoundsMin { get; }
        public int3 BoundsMax { get; }

        public int PrimitiveCount => _primitives.Length;

        public FeaturePresentationBake(
            ulong sourceId,
            ulong revision,
            FeatureKind kind,
            int3 position,
            byte orientation,
            int3 boundsMin,
            int3 boundsMax,
            Primitive[] primitives)
        {
            if (primitives == null) throw new ArgumentNullException(nameof(primitives));
            if (primitives.Length == 0)
                throw new ArgumentException("A presentation bake requires at least one primitive.", nameof(primitives));
            if (boundsMax.x < boundsMin.x || boundsMax.y < boundsMin.y || boundsMax.z < boundsMin.z)
                throw new ArgumentException("Presentation bake bounds must be ordered.");

            SourceId = sourceId;
            Revision = revision;
            Kind = kind;
            Position = position;
            Orientation = orientation;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            _primitives = new Primitive[primitives.Length];
            Array.Copy(primitives, _primitives, primitives.Length);
        }

        public Primitive GetPrimitive(int index) => _primitives[index];
    }

    /// <summary>
    /// Converts canonical feature-generation inputs into derived presentation data. Producers do not
    /// implement or register anything far-specific: every normal feature that already has a catalogue
    /// definition and placement is eligible for the same baker.
    /// </summary>
    public interface IFeaturePresentationBaker
    {
        bool TryBake(
            in FeatureCatalogue catalogue,
            uint worldSeed,
            int definitionId,
            in ExplicitPlacement placement,
            out FeaturePresentationBake bake);
    }
}
