using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the historical front/rear keep aperture pattern. Placement and room semantics stay
    /// outside this component; it owns only carving/glazing the already-positioned keep shell.
    /// </summary>
    internal static class CastleKeepWindowRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY,
            int floors)
        {
            for (int floor = 0; floor < floors; floor++)
            {
                int y = baseY + floor * plan.FloorHeight + 12;
                int height = floor == 1 ? plan.FloorHeight - 14 : plan.FloorHeight - 18;

                for (int bay = 0; bay < 3; bay++)
                {
                    int x = min.x + size.x / 4 + bay * size.x / 4 - 8;
                    bool mainEntrance = floor == 0 && bay == 1;
                    if (!mainEntrance)
                    {
                        brush.Arch(new int3(x, y, min.z), 16, height, 9, 2, Mat.Empty);
                        brush.Box(new int3(x + 3, y + 4, min.z + 2),
                                  new int3(10, height - 10, 2), Mat.LitWindow);
                        brush.Box(new int3(x + 7, y + 5, min.z + 1),
                                  new int3(2, height - 12, 3), Mat.DarkStone);
                        brush.Box(new int3(x + 3, y + height / 2, min.z + 1),
                                  new int3(10, 2, 3), Mat.DarkStone);
                    }

                    brush.Arch(new int3(x, y, min.z + size.z - 8),
                               16, height, 9, 2, Mat.Empty);
                }
            }
        }
    }
}
