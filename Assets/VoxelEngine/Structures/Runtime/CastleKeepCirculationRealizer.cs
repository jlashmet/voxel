using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes a preplanned keep entrance and vertical-circulation layout from the actual semantic
    /// world-space keep centre. Compatibility projection offsets never enter this component.
    /// </summary>
    internal static class CastleKeepCirculationRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 worldKeepCentre,
            in CastleKeepCirculationPlan circulation)
        {
            if (!CastleKeepCirculationPlanner.TryValidate(
                    in plan, in circulation, out CastleKeepCirculationPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle keep circulation is not valid for realization: {issue}.");
            }

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int2 entrance = worldKeepCentre + circulation.EntranceCentre;

            brush.Arch(
                new int3(entrance.x - 15, baseY + 1, entrance.y - 1),
                30, 34, 10, 2, Mat.Empty);
            brush.Box(
                new int3(entrance.x - 15, baseY + 2, entrance.y + 9),
                new int3(4, 29, 3), Mat.Wood);
            brush.Box(
                new int3(entrance.x + 11, baseY + 2, entrance.y + 9),
                new int3(4, 29, 3), Mat.Wood);
            brush.Box(
                new int3(entrance.x - 9, baseY + 1, entrance.y + 8),
                new int3(18, 24, plan.KeepHalfZ - 28), Mat.Empty);

            int2 grand = worldKeepCentre + circulation.GrandStairOrigin;
            int grandSteps = plan.FloorHeight / circulation.GrandStairRise;
            int grandDepth = grandSteps * circulation.GrandStairRun;
            brush.Box(
                new int3(grand.x, baseY + 1, grand.y),
                new int3(
                    circulation.GrandStairWidth,
                    plan.FloorHeight + 18,
                    grandDepth),
                Mat.Empty);
            brush.Stairs(
                new int3(grand.x, baseY + 1, grand.y),
                circulation.GrandStairWidth,
                grandSteps,
                circulation.GrandStairRise,
                circulation.GrandStairRun,
                2,
                Mat.Wood);
            brush.Box(
                new int3(grand.x - 3, baseY + 1, grand.y),
                new int3(3, 20, 3), Mat.Wood);
            brush.Box(
                new int3(grand.x + circulation.GrandStairWidth, baseY + 1, grand.y),
                new int3(3, 20, 3), Mat.Wood);

            int2 spiral = worldKeepCentre + circulation.SpiralStairCentre;
            brush.SpiralStair(
                spiral.x,
                baseY + 2,
                spiral.y,
                circulation.SpiralStairRadius,
                circulation.VerticalReach,
                Mat.Stone);
        }
    }
}
