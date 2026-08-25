using Unity.Mathematics;
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
        private readonly IMaterialAuthoringCatalogue _materials;
        private readonly IMaterialPlacementCatalogue _placement;

        public StructureAuthoringSession(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            IMaterialAuthoringCatalogue materials,
            int writeBudget)
        {
            _brush = new VoxelBrush(reads, mutations, materials, writeBudget);
            _materials = materials;
            _placement = materials as IMaterialPlacementCatalogue;
        }

        public bool BudgetExceeded => _brush.BudgetExceeded;
        public int WriteBudget => _brush.WriteBudget;
        public long TotalVoxelsWritten => _brush.TotalVoxelsWritten;

        public byte Get(int x, int y, int z) => _brush.Get(x, y, z);
        public byte GetCoating(int x, int y, int z) => _brush.GetCoating(x, y, z);
        public bool IsSolid(int x, int y, int z) => _brush.IsSolid(x, y, z);

        public void Set(int x, int y, int z, byte material) =>
            _brush.Set(x, y, z, material);

        public void SetWithPlacementStyle(int x, int y, int z, byte material)
        {
            ushort style = _placement != null
                ? _placement.GetPlacementSurfaceStyle(material)
                : material == VoxelGrid.MaterialEmpty
                    ? SurfaceStyles.MaterialDefault
                    : SurfaceStyles.Planar;

            byte coating = _brush.GetCoating(x, y, z);
            if (coating != Coatings.None && _materials != null
                && !_materials.AllowsCoating(material, coating))
                coating = Coatings.None;

            _brush.SetStyled(x, y, z, material, style, coating);
        }

        public void SetStyled(
            int x,
            int y,
            int z,
            byte material,
            ushort surfaceStyle,
            byte coating = Coatings.None,
            VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
            _brush.SetStyled(x, y, z, material, surfaceStyle, coating, flags);

        public void Coat(int x, int y, int z, byte coating) =>
            _brush.Coat(x, y, z, coating);

        public void FillBulk(int3 min, int3 size, byte material) =>
            _brush.FillBulk(min, size, material);

        public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) =>
            _brush.FillColumnBulk(x, minY, maxYExclusive, z, material);

        public void Box(int3 min, int3 size, byte material) =>
            _brush.Box(min, size, material);

        public void HollowBox(
            int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) =>
            _brush.HollowBox(min, size, thickness, material, floor, ceiling);

        public void Cylinder(
            int cx, int baseY, int cz, int radius, int height, byte material,
            int innerRadius = 0) =>
            _brush.Cylinder(cx, baseY, cz, radius, height, material, innerRadius);

        public void Disc(int cx, int y, int cz, int radius, byte material) =>
            _brush.Disc(cx, y, cz, radius, material);

        public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) =>
            _brush.Cone(cx, baseY, cz, radius, height, material);

        public void HangingCone(
            int cx, int ceilingY, int cz, int radius, int height, byte material) =>
            _brush.HangingCone(cx, ceilingY, cz, radius, height, material);

        public void Gable(int3 min, int3 size, bool alongX, byte material) =>
            _brush.Gable(min, size, alongX, material);

        public void Crenellate(
            int3 start, int3 step, int count, int width, int height,
            int merlon, int gap, byte material) =>
            _brush.Crenellate(start, step, count, width, height, merlon, gap, material);

        public void CrenellateRing(
            int cx, int y, int cz, int radius, int height, byte material) =>
            _brush.CrenellateRing(cx, y, cz, radius, height, material);

        public void Arch(
            int3 min, int width, int height, int depth, int depthAxis, byte material) =>
            _brush.Arch(min, width, height, depth, depthAxis, material);

        public void Stairs(
            int3 min, int width, int steps, int rise, int run, int axis, byte material) =>
            _brush.Stairs(min, width, steps, rise, run, axis, material);

        public void SpiralStair(
            int cx, int baseY, int cz, int radius, int height, byte material) =>
            _brush.SpiralStair(cx, baseY, cz, radius, height, material);

        public void Carve(int3 min, int3 size) => _brush.Carve(min, size);

        public void Weather(
            int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) =>
            _brush.Weather(min, size, coating, seed, chanceOutOf100);
    }
}
