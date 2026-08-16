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
        public readonly int2 EntranceCentre;
        public readonly int2 GrandStairOrigin;
        public readonly int GrandStairWidth;
        public readonly int GrandStairRise;
        public readonly int GrandStairRun;
        public readonly int2 SpiralStairCentre;
        public readonly int SpiralStairRadius;
        public readonly int VerticalReach;

        public CastleKeepCirculationPlan(
            int2 entranceCentre,
            int2 grandStairOrigin,
            int grandStairWidth,
            int grandStairRise,
            int grandStairRun,
            int2 spiralStairCentre,
            int spiralStairRadius,
            int verticalReach)
        {
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
    /// Plans the current keep circulation recipe without voxel/storage dependencies. The values
    /// deliberately preserve the authored entrance and stair locations while making them explicit
    /// planning data that later layouts can vary independently of Runtime geometry code.
    /// </summary>
    public static class CastleKeepCirculationPlanner
    {
        private const int InnerShellInset = 8;
        private const int GrandStairX = -68;
        private const int GrandStairZInset = 28;
        private const int GrandStairWidth = 18;
        private const int GrandStairRise = 2;
        private const int GrandStairRun = 3;
        private const int SpiralStairInset = 34;
        private const int SpiralStairRadius = 22;

        public static CastleKeepCirculationPlan Create(in CastlePlan plan)
        {
            if (plan.KeepHalfX <= 0 || plan.KeepHalfZ <= 0 ||
                plan.FloorHeight <= 0 || plan.Floors <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(plan), "Castle keep dimensions must be positive before circulation planning.");
            }

            var circulation = new CastleKeepCirculationPlan(
                new int2(0, -plan.KeepHalfZ),
                new int2(GrandStairX, -plan.KeepHalfZ + GrandStairZInset),
                GrandStairWidth,
                GrandStairRise,
                GrandStairRun,
                new int2(-plan.KeepHalfX + SpiralStairInset,
                         -plan.KeepHalfZ + SpiralStairInset),
                SpiralStairRadius,
                plan.Floors * plan.FloorHeight);

            if (!TryValidate(in plan, in circulation, out CastleKeepCirculationPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Planned keep circulation is invalid for the supplied keep dimensions: {issue}.");
            }

            return circulation;
        }

        public static bool TryValidate(
            in CastlePlan plan,
            in CastleKeepCirculationPlan circulation,
            out CastleKeepCirculationPlanIssue issue)
        {
            if (circulation.EntranceCentre.y != -plan.KeepHalfZ ||
                math.abs(circulation.EntranceCentre.x) > plan.KeepHalfX - InnerShellInset)
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
            int grandMinX = circulation.GrandStairOrigin.x;
            int grandMaxX = grandMinX + circulation.GrandStairWidth;
            int grandMinZ = circulation.GrandStairOrigin.y;
            int grandMaxZ = grandMinZ + grandSteps * circulation.GrandStairRun;
            int innerMinX = -plan.KeepHalfX + InnerShellInset;
            int innerMaxX = plan.KeepHalfX - InnerShellInset;
            int innerMinZ = -plan.KeepHalfZ + InnerShellInset;
            int innerMaxZ = plan.KeepHalfZ - InnerShellInset;
            if (grandMinX < innerMinX || grandMaxX > innerMaxX ||
                grandMinZ < innerMinZ || grandMaxZ > innerMaxZ)
            {
                issue = CastleKeepCirculationPlanIssue.InvalidGrandStair;
                return false;
            }

            int radius = circulation.SpiralStairRadius;
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
    }
}
