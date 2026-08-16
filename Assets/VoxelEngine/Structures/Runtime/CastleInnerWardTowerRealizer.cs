using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Voxel profile for already-planned inner-ward towers. Planning owns whether these towers
    /// exist, where they stand, and their roof/slit variation; Runtime owns only the smaller
    /// secondary-ring geometry profile.
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
            int radius = math.max(18, plan.TowerRadius * 3 / 4);
            int height = math.max(plan.WallHeight + 30, plan.TowerHeight * 4 / 5);

            for (int i = 0; i < towers.Length; i++)
            {
                CastleTowerPlacementSpec tower = towers[i];
                int realizedHeight = height + math.max(0, tower.HeightVariation);
                CastleTowerRealizer.BuildPlanned(
                    ref brush,
                    in plan,
                    new int3(
                        plan.Centre.x + tower.Centre.x,
                        baseY,
                        plan.Centre.z + tower.Centre.y),
                    radius,
                    realizedHeight,
                    tower.HasRoof,
                    tower.Slits);
            }
        }
    }
}
