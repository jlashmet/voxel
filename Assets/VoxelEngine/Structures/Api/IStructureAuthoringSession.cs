using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Application-facing authoring capability for deterministic structure/content passes.
    /// Concrete brush implementation, storage batching and mutation strategy remain in
    /// Structures.Runtime and are constructed only by Composition.
    ///
    /// All material parameters are opaque indices. This API provides geometry operations only;
    /// it does not define or interpret game material identity.
    /// </summary>
    public interface IStructureAuthoringSession
    {
        bool BudgetExceeded { get; }
        int WriteBudget { get; }
        long TotalVoxelsWritten { get; }

        byte Get(int x, int y, int z);
        byte GetCoating(int x, int y, int z);
        bool IsSolid(int x, int y, int z);

        void Set(int x, int y, int z, byte material);

        void SetStyled(
            int x,
            int y,
            int z,
            byte material,
            ushort surfaceStyle,
            byte coating = Coatings.None,
            VoxelSurfaceFlags flags = VoxelSurfaceFlags.None);

        void Coat(int x, int y, int z, byte coating);

        void FillBulk(int3 min, int3 size, byte material);
        void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material);
        void Box(int3 min, int3 size, byte material);
        void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling);

        void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material,
                      int innerRadius = 0);
        void Disc(int cx, int y, int cz, int radius, byte material);
        void Cone(int cx, int baseY, int cz, int radius, int height, byte material);
        void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material);
        void Gable(int3 min, int3 size, bool alongX, byte material);
        void Crenellate(int3 start, int3 step, int count, int width, int height,
                        int merlon, int gap, byte material);
        void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material);
        void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material);
        void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material);
        void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material);
        void Carve(int3 min, int3 size);

        void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100);
    }
}
