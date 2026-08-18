using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Integer world-space bounds reserved for one structure instance. Max is exclusive so the
    /// same bounds can be passed directly to bounded voxel loops without off-by-one conversions.
    /// </summary>
    public readonly struct StructureGenerationBounds
    {
        public int3 Min { get; }
        public int3 MaxExclusive { get; }
        public int3 Size => MaxExclusive - Min;

        public bool IsWellFormed =>
            MaxExclusive.x > Min.x && MaxExclusive.y > Min.y && MaxExclusive.z > Min.z;

        public StructureGenerationBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        /// <summary>
        /// Creates half-open bounds from an origin and positive size while proving that every
        /// exclusive maximum is representable as an <see cref="int"/>. This is the validation path
        /// for authored/generated dimensions near world-coordinate limits.
        /// </summary>
        public static bool TryCreate(int3 min, int3 size, out StructureGenerationBounds bounds)
        {
            bounds = default;
            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                return false;

            long maxX = (long)min.x + size.x;
            long maxY = (long)min.y + size.y;
            long maxZ = (long)min.z + size.z;
            if (maxX > int.MaxValue || maxY > int.MaxValue || maxZ > int.MaxValue ||
                maxX < int.MinValue || maxY < int.MinValue || maxZ < int.MinValue)
                return false;

            bounds = new StructureGenerationBounds(
                min,
                new int3((int)maxX, (int)maxY, (int)maxZ));
            return true;
        }

        public bool Contains(int3 position) =>
            IsWellFormed &&
            position.x >= Min.x && position.x < MaxExclusive.x &&
            position.y >= Min.y && position.y < MaxExclusive.y &&
            position.z >= Min.z && position.z < MaxExclusive.z;

        public bool ContainsVolume(int3 min, int3 maxExclusive) =>
            IsWellFormed &&
            maxExclusive.x > min.x && maxExclusive.y > min.y && maxExclusive.z > min.z &&
            min.x >= Min.x && min.y >= Min.y && min.z >= Min.z &&
            maxExclusive.x <= MaxExclusive.x &&
            maxExclusive.y <= MaxExclusive.y &&
            maxExclusive.z <= MaxExclusive.z;
    }

    /// <summary>
    /// Pure terrain access available to structure authoring. It deliberately exposes only the
    /// deterministic terrain query, never generated voxel state, so structure output remains
    /// independent of region generation order.
    /// </summary>
    public readonly struct StructureTerrainAccess
    {
        public uint Seed { get; }

        public StructureTerrainAccess(uint seed)
        {
            Seed = seed;
        }

        public int HeightAt(int worldX, int worldZ) =>
            TerrainQuery.HeightAt(worldX, worldZ, Seed);

        public int SlopeAt(int worldX, int worldZ) =>
            TerrainQuery.SlopeAt(worldX, worldZ, Seed);
    }

    /// <summary>
    /// Stable deterministic inputs and outputs shared by reusable structure authoring components.
    /// This is a value-level view over the existing feature-generation contracts, not a parallel
    /// generation system: geometry still terminates in shape programs/primitives/authoring sessions.
    /// </summary>
    public struct StructureGenerationContext
    {
        private NativeList<ResolvedAnchor> _anchors;

        public ulong InstanceId { get; private set; }
        public uint WorldSeed { get; private set; }
        public int DefinitionId { get; private set; }
        public ulong InstanceSeed { get; private set; }
        public int3 Origin { get; private set; }

        /// <summary>Cardinal Y rotation, encoded identically to ShapeProgram: 0..3.</summary>
        public byte Orientation { get; private set; }

        public StructureGenerationBounds Bounds { get; private set; }
        public StructureTerrainAccess Terrain { get; private set; }
        public StructureMaterialPalette Palette { get; private set; }

        public bool HasAnchorOutput => _anchors.IsCreated;
        public int AnchorCount => _anchors.IsCreated ? _anchors.Length : 0;

        public StructureGenerationContext(
            ulong instanceId,
            uint worldSeed,
            int definitionId,
            ulong instanceSeed,
            int3 origin,
            byte orientation,
            in StructureGenerationBounds bounds,
            in StructureTerrainAccess terrain,
            in StructureMaterialPalette palette,
            NativeList<ResolvedAnchor> anchors)
        {
            InstanceId = instanceId;
            WorldSeed = worldSeed;
            DefinitionId = definitionId;
            InstanceSeed = instanceSeed;
            Origin = origin;
            Orientation = (byte)(orientation & 3);
            Bounds = bounds;
            Terrain = terrain;
            Palette = palette;
            _anchors = anchors;
        }

        /// <summary>
        /// Creates a context using the same stable feature identity rule as FeatureGeneration.
        /// Odd cardinal orientations swap X/Z footprint extents in world-space bounds.
        /// Invalid/overflowing bounds are rejected instead of producing a default context.
        /// </summary>
        public static StructureGenerationContext ForFeature(
            uint worldSeed,
            uint terrainSeed,
            int definitionId,
            int3 origin,
            byte orientation,
            int3 footprint,
            in StructureMaterialPalette palette,
            NativeList<ResolvedAnchor> anchors)
        {
            byte cardinal = (byte)(orientation & 3);
            int3 orientedFootprint = (cardinal & 1) == 0
                ? footprint
                : new int3(footprint.z, footprint.y, footprint.x);
            ulong identity = FeatureHash.Cell(worldSeed, definitionId, origin);

            if (!StructureGenerationBounds.TryCreate(origin, orientedFootprint, out var bounds))
                throw new ArgumentOutOfRangeException(
                    nameof(footprint),
                    "Structure footprint must be positive and remain representable in world-space integer bounds.");

            var terrain = new StructureTerrainAccess(terrainSeed);
            return new StructureGenerationContext(
                identity,
                worldSeed,
                definitionId,
                identity,
                origin,
                cardinal,
                in bounds,
                in terrain,
                in palette,
                anchors);
        }

        public byte Material(StructureMaterialRole role) => Palette.Resolve(role);

        /// <summary>
        /// Derives a stable semantic child stream without consuming mutable RNG state. Optional
        /// sibling components therefore cannot perturb an existing component's random choices.
        /// </summary>
        public ulong ChildSeed(in FixedString64Bytes semanticKey, int ordinal = 0) =>
            StructureSeed.Child(InstanceSeed, in semanticKey, ordinal);

        /// <summary>Adds one already-resolved world-space anchor to caller-owned native output.</summary>
        public bool TryAddResolvedAnchor(in FixedString32Bytes name, int3 worldPosition, Facing facing)
        {
            if (!_anchors.IsCreated)
                return false;

            _anchors.Add(new ResolvedAnchor
            {
                Name = name,
                Position = worldPosition,
                Facing = facing,
            });
            return true;
        }

        public NativeArray<ResolvedAnchor> AnchorsAsArray() =>
            _anchors.IsCreated ? _anchors.AsArray() : default;
    }
}
