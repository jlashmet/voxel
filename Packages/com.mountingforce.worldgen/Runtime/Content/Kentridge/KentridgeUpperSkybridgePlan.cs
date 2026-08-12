using System;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// A court-to-court upper street crossing above the still-lower main ascent. Its elevation is not
    /// arbitrary: both Upper Ward courts own the +75 dm shelf while the road at the chosen crossing
    /// remains at +35 dm, producing a real two-level circulation intersection.
    /// </summary>
    public readonly struct KentridgeUpperSkybridgePlan
    {
        public readonly int WestXDm;
        public readonly int EastXDm;
        public readonly int CentreZDm;
        public readonly int DepthDm;
        public readonly Int2 ShelfSampleDm;
        public readonly int ShelfOffsetDm;
        public readonly int RoadOffsetDm;

        public KentridgeUpperSkybridgePlan(
            int westXDm, int eastXDm, int centreZDm, int depthDm,
            Int2 shelfSampleDm, int shelfOffsetDm, int roadOffsetDm)
        {
            WestXDm = westXDm;
            EastXDm = eastXDm;
            CentreZDm = centreZDm;
            DepthDm = depthDm;
            ShelfSampleDm = shelfSampleDm;
            ShelfOffsetDm = shelfOffsetDm;
            RoadOffsetDm = roadOffsetDm;
        }

        public int LengthDm => EastXDm - WestXDm;
        public int ClearanceDm => ShelfOffsetDm - RoadOffsetDm;
        public int SouthZDm => CentreZDm - DepthDm / 2;
        public int NorthZDm => SouthZDm + DepthDm;
    }

    public static class KentridgeUpperSkybridgePlanner
    {
        public const int CrossingZDm = 460;
        public const int BridgeDepthDm = 18;
        public const int RequiredClearanceDm = 40;

        public static KentridgeUpperSkybridgePlan Build(uint seed)
        {
            KentridgeUrbanMassingPlan massing = KentridgeUrbanOrganizer.Build(seed);
            KentridgeUrbanBlock west = FindBlock(massing, "upper-west-block");
            KentridgeUrbanBlock east = FindBlock(massing, "upper-east-block");

            // Connect protected court interiors, not generated buildings. This keeps the overpass
            // independent of the building-grammar branch and makes the city graph own the crossing.
            int westX = west.InteriorMaxDm.X;
            int eastX = east.InteriorMinDm.X;

            // This is deliberately a content-layer statement. At z=460 the processional section is
            // still on the Market shelf; both Upper Ward courts sit on the authored Upper Landing
            // shelf. The voxel compiler later converts those semantic offsets into absolute Y.
            int shelfOffset = KentridgeProcessionalClimb.UpperLandingOffsetDm;
            int roadOffset = KentridgeProcessionalClimb.MarketOffsetDm;

            var plan = new KentridgeUpperSkybridgePlan(
                westX,
                eastX,
                CrossingZDm,
                BridgeDepthDm,
                west.ElevationSampleDm,
                shelfOffset,
                roadOffset);
            Validate(plan, west, east);
            return plan;
        }

        private static KentridgeUrbanBlock FindBlock(
            KentridgeUrbanMassingPlan massing,
            string id)
        {
            for (int i = 0; i < massing.Blocks.Count; i++)
                if (massing.Blocks[i].Id == id) return massing.Blocks[i];
            throw new InvalidOperationException("Kentridge skybridge block missing: " + id);
        }

        private static void Validate(
            KentridgeUpperSkybridgePlan plan,
            KentridgeUrbanBlock west,
            KentridgeUrbanBlock east)
        {
            if (west.Band != KentridgeUrbanBand.UpperWard
                || east.Band != KentridgeUrbanBand.UpperWard)
                throw new InvalidOperationException(
                    "Kentridge upper skybridge endpoints must remain Upper Ward courts.");

            if (CrossingZDm < KentridgeProcessionalClimb.MarketRiseSouthZDm)
                throw new InvalidOperationException(
                    "Kentridge skybridge crossing moved onto the Market-to-Upper climb; its semantic road elevation is no longer flat Market level.");

            if (plan.LengthDm <= KentridgeTownPlanner.MainRoadWidthDm)
                throw new InvalidOperationException("Kentridge upper skybridge span is too short.");
            if (plan.ClearanceDm < RequiredClearanceDm)
                throw new InvalidOperationException(
                    "Kentridge upper skybridge cannot preserve four-metre road clearance.");

            if (plan.WestXDm != west.InteriorMaxDm.X
                || plan.EastXDm != east.InteriorMinDm.X)
                throw new InvalidOperationException(
                    "Kentridge upper skybridge must terminate on protected court interiors.");

            if (plan.SouthZDm < west.InteriorMinDm.Y
                || plan.NorthZDm > west.InteriorMaxDm.Y
                || plan.SouthZDm < east.InteriorMinDm.Y
                || plan.NorthZDm > east.InteriorMaxDm.Y)
                throw new InvalidOperationException(
                    "Kentridge upper skybridge escaped an Upper Ward court in Z.");
        }
    }
}
