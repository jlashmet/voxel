using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Optional production capability for authored curved structure geometry. Occupancy remains
    /// authoritative; implementations additionally persist signed boundary samples consumed by the
    /// normal surface reconstruction path. Callers must feature-detect this interface rather than
    /// assuming every lightweight/test authoring session supports curved presentation.
    /// </summary>
    public interface ICurvedStructureAuthoringSession : IStructureAuthoringSession
    {
        void RoundedBox(
            int3 min,
            int3 size,
            int radius,
            byte material,
            ushort surfaceStyle = SurfaceStyles.ArchitecturalRounded,
            byte coating = Coatings.None,
            VoxelSurfaceFlags flags = VoxelSurfaceFlags.PreserveFeature);
    }
}
