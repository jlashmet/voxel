using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Runtime adapter over the existing value-type VoxelBrush. Application code sees only the
    /// Structures.Api authoring capability; this class preserves the current batched brush
    /// implementation and its write-budget accounting without leaking the Runtime type.
    /// </summary>
    public sealed class StructureAuthoringSession : IStructureAuthoringSession
    {
        private VoxelBrush _brush;

        public StructureAuthoringSession(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            int writeBudget)
        {
            _brush = new VoxelBrush(reads, mutations, materials, writeBudget);
        }

        public bool BudgetExceeded => _brush.BudgetExceeded;
        public long TotalVoxelsWritten => _brush.TotalVoxelsWritten;

        public void SetStyled(
            int x,
            int y,
            int z,
            byte material,
            ushort surfaceStyle,
            byte coating = Coatings.None,
            VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
            _brush.SetStyled(x, y, z, material, surfaceStyle, coating, flags);

        public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) =>
            _brush.FillColumnBulk(x, minY, maxYExclusive, z, material);
    }
}
