using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Stable per-instance context for reusable structure authoring.
    ///
    /// The context carries only deterministic value data plus caller-owned anchor output. Terrain
    /// sampling stays on the existing pure <see cref="TerrainQuery"/> path, and material ids remain
    /// opaque engine values resolved through a semantic structure palette.
    /// </summary>
    public struct StructureGenerationContext
    {
        public ulong InstanceId;
        public uint WorldSeed;
        public int DefinitionId;
        public ulong InstanceSeed;
        public int3 Origin;
        public byte Orientation;
        public int3 BoundsMin;
        public int3 BoundsMaxExclusive;
        public uint TerrainSeed;
        public StructureMaterialPalette Palette;
        public NativeList<ResolvedAnchor> AnchorOutput;

        public StructureGenerationContext(
            ulong instanceId,
            uint worldSeed,
            int definitionId,
            ulong instanceSeed,
            int3 origin,
            byte orientation,
            int3 boundsMin,
            int3 boundsMaxExclusive,
            uint terrainSeed,
            in StructureMaterialPalette palette,
            NativeList<ResolvedAnchor> anchorOutput)
        {
            InstanceId = instanceId;
            WorldSeed = worldSeed;
            DefinitionId = definitionId;
            InstanceSeed = instanceSeed;
            Origin = origin;
            Orientation = (byte)(orientation & 3);
            BoundsMin = boundsMin;
            BoundsMaxExclusive = boundsMaxExclusive;
            TerrainSeed = terrainSeed;
            Palette = palette;
            AnchorOutput = anchorOutput;
        }

        /// <summary>Samples the authoritative deterministic terrain function at a local X/Z point.</summary>
        public int SampleGround(int localX, int localZ)
        {
            return TerrainQuery.HeightAt(Origin.x + localX, Origin.z + localZ, TerrainSeed);
        }

        /// <summary>Resolves an archetype-neutral material role to the configured voxel material.</summary>
        public byte Material(StructureMaterialRole role)
        {
            return Palette.Resolve(role);
        }

        /// <summary>Derives a stable semantic child seed without consuming mutable RNG state.</summary>
        public ulong ChildSeed(in FixedString64Bytes semanticKey, int ordinal = 0)
        {
            return StructureSeed.Child(InstanceSeed, in semanticKey, ordinal);
        }

        /// <summary>True when a world-space position is inside this instance's declared bounds.</summary>
        public bool ContainsWorld(int3 position)
        {
            return position.x >= BoundsMin.x && position.x < BoundsMaxExclusive.x
                && position.y >= BoundsMin.y && position.y < BoundsMaxExclusive.y
                && position.z >= BoundsMin.z && position.z < BoundsMaxExclusive.z;
        }

        /// <summary>
        /// Appends an already-resolved world-space anchor to caller-owned output. Architectural
        /// naming/orientation semantics are layered on this primitive by WB027.
        /// </summary>
        public bool TryAddResolvedAnchor(in FixedString32Bytes name, int3 worldPosition, Facing facing)
        {
            if (!AnchorOutput.IsCreated)
                return false;

            AnchorOutput.Add(new ResolvedAnchor
            {
                Name = name,
                Position = worldPosition,
                Facing = facing,
            });
            return true;
        }
    }
}
