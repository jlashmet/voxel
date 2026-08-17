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

        public StructureGenerationBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int3 position) =>
            position.x >= Min.x && position.x < MaxExclusive.x &&
            position.y >= Min.y && position.y < MaxExclusive.y &&
            position.z >= Min.z && position.z < MaxExclusive.z;

        public bool ContainsVolume(int3 min, int3 maxExclusive) =>
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
    ///
    /// The context carries identity separately from the random stream seed: identity tells other
    /// systems which structure instance produced an anchor, while the seed controls deterministic
    /// variation. Origin/orientation and bounds are integer world-space values. Terrain access is
    /// pure. Materials are opaque voxel ids until semantic roles are layered on by the shared
    /// structure palette. Anchor output remains caller-owned native memory.
    /// </summary>
    public struct StructureGenerationContext
    {
        private NativeArray<byte> _materials;
        private NativeList<ResolvedAnchor> _anchors;

        public ulong InstanceId { get; }
        public ulong InstanceSeed { get; }
        public int3 Origin { get; }

        /// <summary>Cardinal Y rotation, encoded identically to ShapeProgram: 0..3.</summary>
        public byte Orientation { get; }

        public StructureGenerationBounds Bounds { get; }
        public StructureTerrainAccess Terrain { get; }

        public int MaterialCount => _materials.IsCreated ? _materials.Length : 0;
        public bool HasAnchorOutput => _anchors.IsCreated;
        public int AnchorCount => _anchors.IsCreated ? _anchors.Length : 0;

        public StructureGenerationContext(
            ulong instanceId,
            ulong instanceSeed,
            int3 origin,
            byte orientation,
            in StructureGenerationBounds bounds,
            in StructureTerrainAccess terrain,
            NativeArray<byte> materials,
            NativeList<ResolvedAnchor> anchors)
        {
            InstanceId = instanceId;
            InstanceSeed = instanceSeed;
            Origin = origin;
            Orientation = orientation;
            Bounds = bounds;
            Terrain = terrain;
            _materials = materials;
            _anchors = anchors;
        }

        /// <summary>Returns the opaque material id at a catalogue/palette-local index.</summary>
        public byte MaterialAt(int index) => _materials[index];

        /// <summary>Adds one resolved anchor to the caller-owned output list.</summary>
        public void AddAnchor(in ResolvedAnchor anchor)
        {
            if (!_anchors.IsCreated)
                throw new InvalidOperationException("Structure generation context has no anchor output.");

            _anchors.Add(anchor);
        }

        /// <summary>
        /// Exposes the anchors emitted so far without transferring ownership. The returned array is
        /// invalidated by list growth and must not outlive the caller-owned anchor list.
        /// </summary>
        public NativeArray<ResolvedAnchor> AnchorsAsArray()
        {
            if (!_anchors.IsCreated)
                throw new InvalidOperationException("Structure generation context has no anchor output.");

            return _anchors.AsArray();
        }
    }
}
