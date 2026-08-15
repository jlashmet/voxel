using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Block-granular authoritative mutation capability.
    ///
    /// Callers choose edit geometry and deterministic voxel order. Storage owns region lookup,
    /// mixed-block materialisation, allocation/free, rollback, uniform collapse and region commit.
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
        /// Begins a partial mutation of one logical 8^3 block. The returned view is borrowed and
        /// valid only until <see cref="CompletePartialBlock"/>. Returns false when the region is
        /// unavailable. A created=false view is valid when the requested material is already the
        /// block's uniform material; MetadataChanged may still require completion/commit.
        /// </summary>
        bool TryBeginPartialBlock(
            int3 worldBlock,
            byte targetMaterial,
            bool markHardSurface,
            out VoxelBlockMutation mutation);

        /// <summary>
        /// Finalises a partial mutation. Storage rolls back unused materialisation, collapses a
        /// newly uniform block, frees physical storage as needed, and commits semantic metadata.
        /// Returns true when authoritative block state changed.
        /// </summary>
        bool CompletePartialBlock(ref VoxelBlockMutation mutation, bool materialChanged);
    }
}
