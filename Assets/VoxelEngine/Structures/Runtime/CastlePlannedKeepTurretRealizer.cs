using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the four keep corner turrets from frozen authored variation. Coordinates and
    /// dimensions preserve the current compatibility recipe; Runtime consumes roof and slit choices
    /// without creating authored randomness while mutating voxels.
    /// </summary>
    internal static class CastlePlannedKeepTurretRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan keepPlan,
            CastleKeepTurretSpec[] turrets)
        {
            if (turrets == null || turrets.Length != 4)
                throw new InvalidOperationException("Spatial keep requires four planned corner turrets.");

            int3 min = CastleSpatialProjection.KeepMinimum(in keepPlan);
            int3 size = CastleSpatialProjection.KeepSize(in keepPlan);
            int baseY = min.y;
            int height = keepPlan.KeepHeight + 30;

            for (int i = 0; i < turrets.Length; i++)
            {
                CastleKeepTurretSpec turret = turrets[i];
                int2 position = turret.Corner switch
                {
                    CastleKeepTurretCorner.MinXMinZ => new int2(min.x, min.z),
                    CastleKeepTurretCorner.MaxXMinZ => new int2(min.x + size.x, min.z),
                    CastleKeepTurretCorner.MinXMaxZ => new int2(min.x, min.z + size.z),
                    CastleKeepTurretCorner.MaxXMaxZ => new int2(min.x + size.x, min.z + size.z),
                    _ => throw new InvalidOperationException(
                        $"Spatial keep contains invalid turret corner {turret.Corner}."),
                };

                CastleTowerRealizer.BuildPlanned(
                    ref brush,
                    in keepPlan,
                    new int3(position.x, baseY, position.y),
                    26,
                    height,
                    turret.HasRoof,
                    turret.Slits);
            }
        }
    }
}
