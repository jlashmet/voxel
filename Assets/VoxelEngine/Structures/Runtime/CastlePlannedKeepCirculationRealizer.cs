using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the keep entrance and vertical circulation from a completed semantic plan. The
    /// supplied CastlePlan is the compatibility keep projection; circulation coordinates remain
    /// relative to the actual semantic keep centre.
    /// </summary>
    internal static class CastlePlannedKeepCirculationRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan keepPlan,
            in CastleKeepCirculationPlan circulation)
        {
            if (!CastleKeepCirculationPlanner.TryValidate(
                    in keepPlan, in circulation, out CastleKeepCirculationPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle keep circulation plan is invalid at realization: {issue}.");
            }

            int baseY = keepPlan.Centre.y + keepPlan.PlateauHeight;
            int keepCentreX = keepPlan.Centre.x;
            int keepCentreZ = keepPlan.Centre.z + CastleLayout.LegacyKeepCentreZOffset;

            int entranceX = keepCentreX + circulation.EntranceCentre.x;
            int entranceZ = keepCentreZ + circulation.EntranceCentre.y;
            brush.Arch(new int3(entranceX - 15, baseY + 1, entranceZ - 1),
                       30, 34, 10, 2, Mat.Empty);
            brush.Box(new int3(entranceX - 15, baseY + 2, entranceZ + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX + 11, baseY + 2, entranceZ + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX - 9, baseY + 1, entranceZ + 8),
                      new int3(18, 24, keepPlan.KeepHalfZ - 28), Mat.Empty);

            int grandX = keepCentreX + circulation.GrandStairOrigin.x;
            int grandZ = keepCentreZ + circulation.GrandStairOrigin.y;
            int grandSteps = keepPlan.FloorHeight / circulation.GrandStairRise;
            int grandRunLength = grandSteps * circulation.GrandStairRun;
            brush.Box(new int3(grandX, baseY + 1, grandZ),
                      new int3(circulation.GrandStairWidth,
                               keepPlan.FloorHeight + 18,
                               grandRunLength),
                      Mat.Empty);
            brush.Stairs(new int3(grandX, baseY + 1, grandZ),
                         circulation.GrandStairWidth,
                         grandSteps,
                         circulation.GrandStairRise,
                         circulation.GrandStairRun,
                         2,
                         Mat.Wood);
            brush.Box(new int3(grandX - 3, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);
            brush.Box(new int3(grandX + circulation.GrandStairWidth, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);

            int spiralX = keepCentreX + circulation.SpiralStairCentre.x;
            int spiralZ = keepCentreZ + circulation.SpiralStairCentre.y;
            brush.SpiralStair(
                spiralX,
                baseY + 2,
                spiralZ,
                circulation.SpiralStairRadius,
                circulation.VerticalReach,
                Mat.Stone);
        }
    }
}
