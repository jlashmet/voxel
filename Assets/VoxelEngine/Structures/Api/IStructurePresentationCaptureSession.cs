using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Nonresident semantic structure-authoring sink that derives bounded presentation data from
    /// the same authoring calls used by detailed generation. Implementations must not allocate or
    /// rasterise voxel regions; the returned bake is disposable derived data, never world truth.
    /// </summary>
    public interface IStructurePresentationCaptureSession : IStructureAuthoringSession
    {
        FeaturePresentationBake Bake(
            ulong sourceId,
            ulong revisionSeed,
            FeatureKind kind,
            int3 position,
            byte orientation = 0);
    }
}
