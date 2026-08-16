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
            if (!CastleKeepCirculationPlanValidator.TryValidate(
                    in plan, in circulation, out CastleKeepCirculationPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle keep circulation is not valid for realization: {issue}.");
            }

            CastleKeepFacadeFrame frame = CastleKeepFacadeFrame.For(circulation.EntranceFace);
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int2 entrance = worldKeepCentre + circulation.EntranceCentre;

            int archDepthAxis = math.abs(frame.Inward.x) == 1 ? 0 : 2;
            int2 archMin = RectMinimum(entrance, in frame, -15, -1, 30, 10);
            brush.Arch(
                new int3(archMin.x, baseY + 1, archMin.y),
                30, 34, 10, archDepthAxis, Mat.Empty);
            BoxInFacade(ref brush, entrance, in frame,
                        -15, 9, 4, 29, 3, baseY + 2, Mat.Wood);
            BoxInFacade(ref brush, entrance, in frame,
                        11, 9, 4, 29, 3, baseY + 2, Mat.Wood);

            int aisleDepth = frame.NormalHalfExtent(in plan) - 28;
            BoxInFacade(ref brush, entrance, in frame,
                        -9, 8, 18, 24, aisleDepth, baseY + 1, Mat.Empty);

            int2 grand = worldKeepCentre + circulation.GrandStairOrigin;
            int grandSteps = plan.FloorHeight / circulation.GrandStairRise;
            int grandDepth = grandSteps * circulation.GrandStairRun;
            BoxInFacade(ref brush, grand, in frame,
                        0, 0, circulation.GrandStairWidth,
                        plan.FloorHeight + 18, grandDepth, baseY + 1, Mat.Empty);
            BuildStairs(ref brush, grand, in frame,
                        circulation.GrandStairWidth, grandSteps,
                        circulation.GrandStairRise, circulation.GrandStairRun,
                        baseY + 1, Mat.Wood);
            BoxInFacade(ref brush, grand, in frame,
                        -3, 0, 3, 20, 3, baseY + 1, Mat.Wood);
            BoxInFacade(ref brush, grand, in frame,
                        circulation.GrandStairWidth, 0,
                        3, 20, 3, baseY + 1, Mat.Wood);

            int2 spiral = worldKeepCentre + circulation.SpiralStairCentre;
            brush.SpiralStair(
                spiral.x,
                baseY + 2,
                spiral.y,
                circulation.SpiralStairRadius,
                circulation.VerticalReach,
                Mat.Stone);
        }

        private static void BoxInFacade(
            ref VoxelBrush brush,
            int2 origin,
            in CastleKeepFacadeFrame frame,
            int tangentStart,
            int inwardStart,
            int tangentSize,
            int height,
            int inwardSize,
            int minY,
            byte material)
        {
            if (tangentSize <= 0 || height <= 0 || inwardSize <= 0) return;

            int2 min = RectMinimum(
                origin, in frame, tangentStart, inwardStart, tangentSize, inwardSize);
            bool tangentRunsX = math.abs(frame.Tangent.x) == 1;
            int sizeX = tangentRunsX ? tangentSize : inwardSize;
            int sizeZ = tangentRunsX ? inwardSize : tangentSize;
            brush.Box(new int3(min.x, minY, min.y), new int3(sizeX, height, sizeZ), material);
        }

        private static void BuildStairs(
            ref VoxelBrush brush,
            int2 origin,
            in CastleKeepFacadeFrame frame,
            int width,
            int steps,
            int rise,
            int run,
            int baseY,
            byte material)
        {
            for (int step = 0; step < steps; step++)
            for (int runOffset = 0; runOffset < run; runOffset++)
            for (int widthOffset = 0; widthOffset < width; widthOffset++)
            for (int y = 0; y < rise; y++)
            {
                int inward = step * run + runOffset;
                int2 point = origin
                           + frame.Tangent * widthOffset
                           + frame.Inward * inward;
                brush.Set(point.x, baseY + step * rise + y, point.y, material);
            }
        }

        private static int2 RectMinimum(
            int2 origin,
            in CastleKeepFacadeFrame frame,
            int tangentStart,
            int inwardStart,
            int tangentSize,
            int inwardSize)
        {
            int tangentEnd = tangentStart + tangentSize - 1;
            int inwardEnd = inwardStart + inwardSize - 1;
            int2 a = Map(origin, in frame, tangentStart, inwardStart);
            int2 b = Map(origin, in frame, tangentEnd, inwardStart);
            int2 c = Map(origin, in frame, tangentStart, inwardEnd);
            int2 d = Map(origin, in frame, tangentEnd, inwardEnd);
            return math.min(math.min(a, b), math.min(c, d));
        }

        private static int2 Map(
            int2 origin,
            in CastleKeepFacadeFrame frame,
            int tangent,
            int inward) =>
            origin + frame.Tangent * tangent + frame.Inward * inward;
    }
}
