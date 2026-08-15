using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Narrow application-facing authoring capability for deterministic structure/lookdev passes.
    /// Concrete brush implementation, storage batching and mutation strategy remain in
    /// Structures.Runtime and are constructed only by Composition.
    /// </summary>
    public interface IStructureAuthoringSession
    {
        bool BudgetExceeded { get; }
        long TotalVoxelsWritten { get; }

        void SetStyled(
            int x,
            int y,
            int z,
            byte material,
            ushort surfaceStyle,
            byte coating = Coatings.None,
            VoxelSurfaceFlags flags = VoxelSurfaceFlags.None);

        void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material);
    }
}
