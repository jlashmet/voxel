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
                CastleRearOrielRealizer.Build(ref brush, in plan);
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
    }
}
