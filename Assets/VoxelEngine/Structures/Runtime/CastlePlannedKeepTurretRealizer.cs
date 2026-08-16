using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the four keep corner turrets from frozen authored variation. Coordinates and
    /// dimensions preserve the current compatibility recipe; Runtime only consumes roof choices.
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

            int baseY = keepPlan.Centre.y + keepPlan.PlateauHeight;
            int minX = keepPlan.Centre.x - keepPlan.KeepHalfX;
            int minZ = keepPlan.Centre.z - keepPlan.KeepHalfZ + 60;
            int width = keepPlan.KeepHalfX * 2;
            int depth = keepPlan.KeepHalfZ * 2;

            for (int i = 0; i < turrets.Length; i++)
            {
                CastleKeepTurretSpec turret = turrets[i];
                int2 position = turret.Corner switch
                {
                    CastleKeepTurretCorner.MinXMinZ => new int2(minX, minZ),
                    CastleKeepTurretCorner.MaxXMinZ => new int2(minX + width, minZ),
                    CastleKeepTurretCorner.MinXMaxZ => new int2(minX, minZ + depth),
                    CastleKeepTurretCorner.MaxXMaxZ => new int2(minX + width, minZ + depth),
                    _ => throw new InvalidOperationException(
                        $"Spatial keep contains invalid turret corner {turret.Corner}."),
                };

                CastleTowerRealizer.Build(
                    ref brush,
                    in keepPlan,
                    new int3(position.x, baseY, position.y),
                    26,
                    keepPlan.KeepHeight + 30,
                    turret.HasRoof);
            }
        }
    }
}
