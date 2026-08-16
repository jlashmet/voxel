using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Sequences the seven historical keep slices for a spatially planned castle. Every authored
    /// choice is supplied by planning; this coordinator only preserves streaming cadence while
    /// delegating voxel geometry to dedicated keep components.
    /// </summary>
    internal static class CastlePlannedKeepRealizer
    {
        internal static bool Step(
            ref VoxelBrush brush,
            in CastlePlan castlePlan,
            in CastlePlan keepPlan,
            int2 worldKeepCentre,
            CastleKeepFloorPlan[] floorPlans,
            CastleKeepTurretSpec[] turrets,
            in CastleKeepCirculationPlan circulation,
            CastleKeepWindowSpec[] windows,
            in CastleKeepAnnexPlan annexes,
            ref int stage)
        {
            if (stage < 0)
                throw new ArgumentOutOfRangeException(nameof(stage));
            if (stage >= 7)
                return true;

            int3 min = CastleSpatialProjection.KeepMinimum(in keepPlan);
            int baseY = min.y;
            int3 size = CastleSpatialProjection.KeepSize(in keepPlan);

            switch (stage)
            {
                case 0:
                    CastleKeepShellRealizer.Build(ref brush, min, size, baseY);
                    break;

                case 1:
                    CastlePlannedKeepTurretRealizer.BuildAll(
                        ref brush, in keepPlan, turrets);
                    break;

                case 2:
                    CastleKeepFloorRealizer.BuildPlanned(
                        ref brush,
                        in keepPlan,
                        min,
                        size,
                        baseY,
                        keepPlan.Floors,
                        floorPlans);
                    break;

                case 3:
                    CastleKeepCirculationRealizer.Build(
                        ref brush,
                        in castlePlan,
                        worldKeepCentre,
                        in circulation);
                    break;

                case 4:
                    CastlePlannedKeepWindowRealizer.BuildAll(
                        ref brush, in keepPlan, windows);
                    break;

                case 5:
                    CastlePlannedKeepExteriorRealizer.Build(
                        ref brush, in keepPlan, in annexes);
                    break;

                case 6:
                    CastleKeepAnnexRealizer.BuildPlanned(
                        ref brush, in keepPlan, in annexes);
                    break;
            }

            stage++;
            return stage >= 7;
        }
    }
}
