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
            int3 min = CastleSpatialProjection.KeepMinimum(in keepPlan);
            int3 size = CastleSpatialProjection.KeepSize(in keepPlan);
            int baseY = min.y;
            int height = keepPlan.KeepHeight + 30;

            for (int i = 0; i < turrets.Length; i++)
            {
                CastleKeepTurretSpec turret = turrets[i];
                int corner = (int)turret.Corner;
                int2 position = new int2(
                    (corner & 1) == 0 ? min.x : min.x + size.x,
                    (corner & 2) == 0 ? min.z : min.z + size.z);

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
