using Unity.Mathematics;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the structural keep shell only: plinth, exterior masonry, and cleared interior.
    /// Placement and dimensions are supplied by the already-projected CastlePlan; this component
    /// makes no topology, room, circulation, decoration, or randomness decisions.
    /// </summary>
    internal static class CastleKeepShellRealizer
    {
        internal static void Build(ref VoxelBrush brush, int3 min, int3 size, int baseY)
        {
            brush.Box(new int3(min.x - 6, baseY - 26, min.z - 6),
                      new int3(size.x + 12, 30, size.z + 12), Mat.DarkStone);
            brush.HollowBox(min, size, 8, Mat.Stone, false, false);
            brush.FillBulk(new int3(min.x + 8, baseY + 1, min.z + 8),
                           new int3(size.x - 16, size.y - 1, size.z - 16), Mat.Empty);
        }
    }
}
