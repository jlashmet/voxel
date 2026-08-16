using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the keep exterior for a spatially planned castle. Fixed masonry courses and
    /// weathering remain behavior-compatible presentation details, while architectural annex
    /// choices such as the rear oriel are consumed from the preplanned keep-annex semantics.
    /// </summary>
    internal static class CastlePlannedKeepExteriorRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleKeepAnnexPlan annexes)
        {
            CastleKeepAnnexPlanValidator.RequireValid(in annexes);

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hx = plan.KeepHalfX;
            int hz = plan.KeepHalfZ;
            var min = new int3(plan.Centre.x - hx, baseY, plan.Centre.z - hz + 60);
            var size = new int3(hx * 2, plan.KeepHeight, hz * 2);

            BuildFacade(ref brush, in plan, min, size, baseY);
            if (annexes.HasRearOriel)
                BuildRearOriel(ref brush, in plan, min, size, baseY);
        }

        private static void BuildFacade(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY)
        {
            for (int f = 1; f < plan.Floors; f++)
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

        private static void BuildRearOriel(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 keepMin,
            int3 keepSize,
            int baseY)
        {
            const int width = 44;
            const int depth = 22;
            int minX = plan.Centre.x + 18;
            int wallZ = keepMin.z + keepSize.z;
            int firstFloorY = baseY + plan.FloorHeight * 2;
            for (int x = 3; x < width - 2; x += 12)
            {
                brush.Box(new int3(minX + x, firstFloorY - 13, wallZ + 2),
                          new int3(5, 13, 14), Mat.DarkStone);
            }

            for (int storey = 0; storey < 2; storey++)
            {
                int y = firstFloorY + storey * plan.FloorHeight;
                brush.Box(new int3(minX, y, wallZ - 2), new int3(width, 4, depth), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ + depth - 5),
                          new int3(width, plan.FloorHeight - 7, 4), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);
                brush.Box(new int3(minX + width - 4, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);
                for (int bay = 0; bay < 3; bay++)
                {
                    int bayX = minX + 5 + bay * 13;
                    brush.Box(new int3(bayX, y + 9, wallZ + depth - 4),
                              new int3(9, plan.FloorHeight - 18, 3), Mat.LitWindow);
                }
                brush.Box(new int3(minX + 8, y + 4, wallZ - 8),
                          new int3(width - 16, 25, 12), Mat.Empty);
                brush.Box(new int3(minX + 4, y + 4, wallZ + 4),
                          new int3(width - 8, plan.FloorHeight - 8, depth - 9), Mat.Empty);
            }

            int roofY = firstFloorY + plan.FloorHeight * 2;
            brush.Gable(new int3(minX - 4, roofY, wallZ - 4),
                        new int3(width + 8, 24, depth + 8), true, Mat.Tile);
            brush.Box(new int3(minX - 3, firstFloorY + plan.FloorHeight - 1, wallZ - 1),
                      new int3(width + 6, 3, depth + 1), Mat.DarkStone);
        }
    }
}
