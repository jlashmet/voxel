using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Pure planning policy for the secondary defensive ring. The topology's DesiredTowerCount
    /// continues to describe the outer perimeter; a nested ward receives one smaller tower at
    /// each of its planned corners without changing that outer semantic contract.
    /// </summary>
    public static class CastleInnerWardTowerPlanner
    {
        public static CastleTowerPlacementSpec[] Create(int2[] innerWardVertices)
        {
            if (innerWardVertices == null || innerWardVertices.Length == 0)
                return Array.Empty<CastleTowerPlacementSpec>();

            var towers = new CastleTowerPlacementSpec[innerWardVertices.Length];
            for (int i = 0; i < towers.Length; i++)
            {
                towers[i] = new CastleTowerPlacementSpec
                {
                    Id = i,
                    Centre = innerWardVertices[i],
                    Role = CastleTowerPlacementRole.Corner,
                };
            }
            return towers;
        }

        public static int Radius(in CastlePlan plan) =>
            math.max(18, plan.TowerRadius * 3 / 4);

        public static int Height(in CastlePlan plan) =>
            math.max(plan.WallHeight + 30, plan.TowerHeight * 4 / 5);
    }
}
