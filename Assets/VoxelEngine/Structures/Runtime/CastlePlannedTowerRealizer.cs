using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes outer towers exactly as planned. Height/roof/slit variation belongs to planning;
    /// this component performs no seeded choices and only translates local tower specs into voxels.
    /// </summary>
    internal static class CastlePlannedTowerRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers)
        {
            if (towers == null) throw new ArgumentNullException(nameof(towers));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            for (int i = 0; i < towers.Length; i++)
            {
                CastleTowerPlacementSpec tower = towers[i];
                int worldX = plan.Centre.x + tower.Centre.x;
                int worldZ = plan.Centre.z + tower.Centre.y;
                int height = plan.TowerHeight + math.max(0, tower.HeightVariation);

                CastleTowerRealizer.BuildPlanned(
                    ref brush,
                    in plan,
                    new int3(worldX, baseY, worldZ),
                    plan.TowerRadius,
                    height,
                    tower.HasRoof,
                    tower.Slits);
            }
        }
    }
}
