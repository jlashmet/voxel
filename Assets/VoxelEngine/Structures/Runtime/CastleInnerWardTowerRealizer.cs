using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the planner-owned towers of the secondary defensive ring. Tower count and centres
    /// come from CastleSpatialPlan; this component owns only the smaller inner-ring voxel profile.
    /// </summary>
    internal static class CastleInnerWardTowerRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers)
        {
            if (towers == null || towers.Length == 0)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int radius = CastleInnerWardTowerPlanner.Radius(in plan);
            int height = CastleInnerWardTowerPlanner.Height(in plan);

            for (int i = 0; i < towers.Length; i++)
            {
                int2 local = towers[i].Centre;
                int3 centre = new int3(
                    plan.Centre.x + local.x,
                    baseY,
                    plan.Centre.z + local.y);

                uint variation = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Walls, (uint)(0x2A00 + towers[i].Id));
                bool roof = (variation & 1u) != 0u;
                CastleTowerRealizer.Build(
                    ref brush,
                    in plan,
                    centre,
                    radius,
                    height,
                    roof);
            }
        }
    }
}
