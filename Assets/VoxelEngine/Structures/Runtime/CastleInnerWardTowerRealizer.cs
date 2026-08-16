using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Voxel profile for already-planned inner-ward tower centres. Planning owns whether these
    /// towers exist and where they stand; Runtime owns only the smaller secondary-ring geometry.
    /// </summary>
    internal static class CastleInnerWardTowerRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localCentres)
        {
            if (localCentres == null || localCentres.Length == 0)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int radius = math.max(18, plan.TowerRadius * 3 / 4);
            int height = math.max(plan.WallHeight + 30, plan.TowerHeight * 4 / 5);

            for (int i = 0; i < localCentres.Length; i++)
            {
                int2 local = localCentres[i];
                uint variation = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Walls, (uint)(0x2A00 + i));
                bool roof = (variation & 1u) != 0u;

                CastleTowerRealizer.Build(
                    ref brush,
                    in plan,
                    new int3(
                        plan.Centre.x + local.x,
                        baseY,
                        plan.Centre.z + local.y),
                    radius,
                    height,
                    roof);
            }
        }
    }
}
