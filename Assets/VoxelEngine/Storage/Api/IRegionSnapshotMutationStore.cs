using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Authoritative semantic snapshot replacement capability.
    ///
    /// Network code supplies logical snapshot bytes and the advertised semantic hash. Storage owns
    /// encoded-snapshot validation, physical region replacement and post-apply semantic verification.
    /// No Region, BrickRef, pool slot or allocator detail crosses this boundary.
    /// </summary>
    public interface IRegionSnapshotMutationStore
    {
        /// <summary>
        /// Validates and applies one semantic snapshot. When <paramref name="createIfMissing"/> is
        /// false the target region must already be resident; BULK current-state replacement may set
        /// it true while REPAIR keeps it false. Returns true only when the resulting resident region
        /// matches <paramref name="expectedSemanticHash"/> exactly.
        /// </summary>
        bool TryApplySemanticSnapshot(
            int3 regionCoord,
            byte[] snapshotBytes,
            uint expectedSemanticHash,
            bool createIfMissing);
    }
}
