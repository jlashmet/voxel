using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the compatibility keep facade dressing: floor courses, heraldic banners, and
    /// deterministic masonry staining. Spatial exterior planning may bypass this recipe entirely.
    /// </summary>
    internal static class CastleKeepFacadeRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY,
            int floors)
        {
            for (int f = 1; f < floors; f++)
            {
                int courseY = baseY + f * plan.FloorHeight - 3;
                brush.Box(new int3(min.x - 3, courseY, min.z - 3),
                          new int3(size.x + 6, 3, 4), Mat.DarkStone);
                brush.Box(new int3(min.x - 3, courseY, min.z + size.z - 1),
                          new int3(size.x + 6, 3, 4), Mat.DarkStone);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int bannerX = plan.Centre.x + side * 52;
                brush.Box(new int3(bannerX - 7, baseY + plan.FloorHeight * 2 + 8, min.z - 3),
                          new int3(14, 54, 3), Mat.Cloth);
                brush.Box(new int3(bannerX - 10, baseY + plan.FloorHeight * 2 + 59, min.z - 4),
                          new int3(20, 3, 4), Mat.Gold);
            }

            int2[] keepStains = { new(-74, 5), new(-35, 14), new(42, 8), new(76, 20) };
            for (int i = 0; i < keepStains.Length; i++)
            {
                int stainX = plan.Centre.x + keepStains[i].x;
                int stainHeight = 8 + (i * 6 % 15);
                brush.Box(new int3(stainX, baseY + keepStains[i].y, min.z - 2),
                          new int3(9 + (i & 1) * 6, stainHeight, 2), Mat.Moss);
                brush.Box(new int3(stainX + 3, baseY + 2, min.z - 2),
                          new int3(3, keepStains[i].y + 5, 2), Mat.Moss);
            }
        }
    }
}
