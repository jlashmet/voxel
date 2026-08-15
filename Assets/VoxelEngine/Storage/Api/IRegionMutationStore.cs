using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Block-granular authoritative mutation capability.
    ///
    /// Callers choose edit/authoring geometry and deterministic voxel order. Storage owns region
    /// lookup, mixed-block materialisation, allocation/free, rollback, uniform collapse and region
    /// commit. Hot loops mutate the borrowed block view directly rather than dispatching through an
    /// interface per voxel.
    /// </summary>
    public interface IRegionMutationStore
    {
        bool IsRegionResident(int3 regionCoord);

        /// <summary>
        /// Replaces one complete logical 8^3 block with a uniform material. Returns true when
        /// either material state or hard-surface metadata changed.
        /// </summary>
        bool SetWholeBlock(int3 worldBlock, byte material, bool markHardSurface);

        /// <summary>
        /// Begins a material-oriented partial mutation of one logical 8^3 block. The returned view
        /// is borrowed and valid only until <see cref="CompletePartialBlock"/>. Returns false when
        /// the region is unavailable. A created=false view is valid when the requested material is
        /// already the block's uniform material; MetadataChanged may still require completion.
        /// </summary>
        bool TryBeginPartialBlock(
            int3 worldBlock,
            byte targetMaterial,
            bool markHardSurface,
            out VoxelBlockMutation mutation);

        /// <summary>
        /// Begins a complete logical-cell authoring mutation for one block. Unlike gameplay-style
        /// partial mutation, authoring may create the containing region when it is not resident;
        /// this preserves generation/rasterisation semantics where the first authored cell makes
        /// its region exist. Uniform storage is materialised so the caller may author material,
        /// surface semantics and boundary samples directly. Unused materialisation is rolled back
        /// by <see cref="CompletePartialBlock"/>.
        /// </summary>
        bool TryBeginCellBlock(
            int3 worldBlock,
            bool markHardSurface,
            out VoxelBlockMutation mutation);

        /// <summary>
        /// Finalises a borrowed block mutation. Storage rolls back unused materialisation,
        /// collapses a newly uniform block when no authored semantic payload remains, frees physical
        /// storage as needed, and commits semantic metadata. Returns true when authoritative block
        /// state changed.
        /// </summary>
        bool CompletePartialBlock(ref VoxelBlockMutation mutation, bool payloadChanged);
    }
}