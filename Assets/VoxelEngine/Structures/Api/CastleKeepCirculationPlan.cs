using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public enum CastleKeepCirculationPlanIssue : byte
    {
        None = 0,
        InvalidEntrance,
        InvalidGrandStair,
        InvalidSpiralStair,
        InvalidVerticalReach,
    }

    /// <summary>
    /// Pure keep-local circulation geometry. Coordinates are relative to the actual semantic keep
    /// centre, never the historical Runtime compatibility anchor. Runtime may realize these
    /// anchors, but it must not choose them.
    /// </summary>
    public readonly struct CastleKeepCirculationPlan
    {
        public readonly CastleKeepFace EntranceFace;
        public readonly int2 EntranceCentre;
        public readonly int2 GrandStairOrigin;
        public readonly int GrandStairWidth;
        public readonly int GrandStairRise;
        public readonly int GrandStairRun;
        public readonly int2 SpiralStairCentre;
        public readonly int SpiralStairRadius;
        public readonly int VerticalReach;

        /// <summary>Compatibility constructor for the historical south-facing keep recipe.</summary>
        public CastleKeepCirculationPlan(
            int2 entranceCentre,
            int2 grandStairOrigin,
            int grandStairWidth,
            int grandStairRise,
            int grandStairRun,
            int2 spiralStairCentre,
            int spiralStairRadius,
            int verticalReach)
            : this(
                CastleKeepFace.South,
                entranceCentre,
                grandStairOrigin,
                grandStairWidth,
                grandStairRise,
                grandStairRun,
                spiralStairCentre,
                spiralStairRadius,
                verticalReach)
        {
        }

        public CastleKeepCirculationPlan(
            CastleKeepFace entranceFace,
            int2 entranceCentre,
            int2 grandStairOrigin,
            int grandStairWidth,
            int grandStairRise,
            int grandStairRun,
            int2 spiralStairCentre,
            int spiralStairRadius,
            int verticalReach)
        {
            EntranceFace = entranceFace;
            EntranceCentre = entranceCentre;
            GrandStairOrigin = grandStairOrigin;
            GrandStairWidth = grandStairWidth;
            GrandStairRise = grandStairRise;
            GrandStairRun = grandStairRun;
            SpiralStairCentre = spiralStairCentre;
            SpiralStairRadius = spiralStairRadius;
            VerticalReach = verticalReach;
        }
    }

    /// <summary>
    /// Pure validation for an already planned keep circulation layout. Keeping this separate from
    /// CastleKeepCirculationPlanner lets Runtime verify its input without depending on a planner.
    /// </summary>
    public static class CastleKeepCirculationPlanValidator
    {
        private const int InnerShellInset = 8;

        public static bool TryValidate(
            in CastlePlan plan,
            in CastleKeepCirculationPlan circulation,
            out CastleKeepCirculationPlanIssue issue)
        {
            CastleKeepFacadeFrame frame = CastleKeepFacadeFrame.For(circulation.EntranceFace);
            int normalHalf = frame.NormalHalfExtent(in plan);
            int tangentHalf = frame.TangentHalfExtent(in plan);
            int entranceNormal = math.dot(circulation.EntranceCentre, frame.Outward);
            int entranceTangent = math.dot(circulation.EntranceCentre, frame.Tangent);
            if (entranceNormal != normalHalf ||
                math.abs(entranceTangent) > tangentHalf - InnerShellInset)
            {
                issue = CastleKeepCirculationPlanIssue.InvalidEntrance;
                return false;
            }

            if (circulation.GrandStairWidth <= 0 ||
                circulation.GrandStairRise <= 0 ||
                circulation.GrandStairRun <= 0 ||
                plan.FloorHeight % circulation.GrandStairRise != 0)
            {
                issue = CastleKeepCirculationPlanIssue.InvalidGrandStair;
                return false;
            }

            int grandSteps = plan.FloorHeight / circulation.GrandStairRise;
            int grandDepth = grandSteps * circulation.GrandStairRun;
            int2 grandTangentEnd = circulation.GrandStairOrigin
                                 + frame.Tangent * circulation.GrandStairWidth;
            int2 grandInwardEnd = circulation.GrandStairOrigin
                                + frame.Inward * grandDepth;
            int2 grandFarCorner = grandTangentEnd + frame.Inward * grandDepth;
            if (!InsideInnerShell(in plan, circulation.GrandStairOrigin) ||
                !InsideInnerShell(in plan, grandTangentEnd) ||
                !InsideInnerShell(in plan, grandInwardEnd) ||
                !InsideInnerShell(in plan, grandFarCorner))
            {
                issue = CastleKeepCirculationPlanIssue.InvalidGrandStair;
                return false;
            }

            int radius = circulation.SpiralStairRadius;
            int innerMinX = -plan.KeepHalfX + InnerShellInset;
            int innerMaxX = plan.KeepHalfX - InnerShellInset;
            int innerMinZ = -plan.KeepHalfZ + InnerShellInset;
            int innerMaxZ = plan.KeepHalfZ - InnerShellInset;
            if (radius <= 0 ||
                circulation.SpiralStairCentre.x - radius < innerMinX ||
                circulation.SpiralStairCentre.x + radius > innerMaxX ||
                circulation.SpiralStairCentre.y - radius < innerMinZ ||
                circulation.SpiralStairCentre.y + radius > innerMaxZ)
            {
                issue = CastleKeepCirculationPlanIssue.InvalidSpiralStair;
                return false;
            }

            if (circulation.VerticalReach != plan.Floors * plan.FloorHeight ||
                circulation.VerticalReach <= 0)
            {
                issue = CastleKeepCirculationPlanIssue.InvalidVerticalReach;
                return false;
            }

            issue = CastleKeepCirculationPlanIssue.None;
            return true;
        }

        private static bool InsideInnerShell(in CastlePlan plan, int2 point) =>
            point.x >= -plan.KeepHalfX + InnerShellInset &&
            point.x <= plan.KeepHalfX - InnerShellInset &&
            point.y >= -plan.KeepHalfZ + InnerShellInset &&
            point.y <= plan.KeepHalfZ - InnerShellInset;
    }

    /// <summary>
    /// Plans keep circulation without voxel/storage dependencies. The compatibility overload
    /// preserves the historical south-facing recipe; the facade-aware overload expresses that same
    /// recipe in a cardinal keep basis so planning can later align the entrance with the approach.
    /// </summary>
    public static class CastleKeepCirculationPlanner
    {
        private const int GrandStairTangent = -68;
        private const int GrandStairFrontInset = 28;
        private const int GrandStairWidth = 18;
        private const int GrandStairRise = 2;
        private const int GrandStairRun = 3;
        private const int SpiralStairInset = 34;
        private const int SpiralStairRadius = 22;

        public static CastleKeepCirculationPlan Create(in CastlePlan plan) =>
            Create(in plan, CastleKeepFace.South);

        public static CastleKeepCirculationPlan Create(
            in CastlePlan plan,
            CastleKeepFace entranceFace)
        {
            if (plan.KeepHalfX <= 0 || plan.KeepHalfZ <= 0 ||
                plan.FloorHeight <= 0 || plan.Floors <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(plan), "Castle keep dimensions must be positive before circulation planning.");
            }

            CastleKeepFacadeFrame frame = CastleKeepFacadeFrame.For(entranceFace);
            int tangentHalf = frame.TangentHalfExtent(in plan);
            var circulation = new CastleKeepCirculationPlan(
                entranceFace,
                frame.PointFromFacade(in plan, 0, 0),
                frame.PointFromFacade(
                    in plan, GrandStairTangent, GrandStairFrontInset),
                GrandStairWidth,
                GrandStairRise,
                GrandStairRun,
                frame.PointFromFacade(
                    in plan,
                    -tangentHalf + SpiralStairInset,
                    SpiralStairInset),
                SpiralStairRadius,
                plan.Floors * plan.FloorHeight);

            if (!CastleKeepCirculationPlanValidator.TryValidate(
                    in plan, in circulation, out CastleKeepCirculationPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Planned keep circulation is invalid for the supplied keep dimensions: {issue}.");
            }

            return circulation;
        }

        /// <summary>
        /// Compatibility validation entry point. New validation-only callers should use
        /// CastleKeepCirculationPlanValidator directly.
        /// </summary>
        public static bool TryValidate(
            in CastlePlan plan,
            in CastleKeepCirculationPlan circulation,
            out CastleKeepCirculationPlanIssue issue) =>
            CastleKeepCirculationPlanValidator.TryValidate(in plan, in circulation, out issue);
    }
}
