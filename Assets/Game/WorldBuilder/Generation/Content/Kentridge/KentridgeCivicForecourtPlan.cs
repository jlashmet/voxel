using System;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// Formal summit room framed by Kentridge's two stable civic anchors. The Church east wall and
    /// Mayor House west wall define the court width; the CivicCrown skeleton reservation defines its
    /// north/south depth. This keeps the public room semantic even if later building grammar changes
    /// anonymous fabric elsewhere in town.
    /// </summary>
    public readonly struct KentridgeCivicForecourtPlan
    {
        public readonly int MinXDm;
        public readonly int MaxXDm;
        public readonly int MinZDm;
        public readonly int MaxZDm;
        public readonly Int2 CentreDm;

        public KentridgeCivicForecourtPlan(
            int minXDm,
            int maxXDm,
            int minZDm,
            int maxZDm,
            Int2 centreDm)
        {
            MinXDm = minXDm;
            MaxXDm = maxXDm;
            MinZDm = minZDm;
            MaxZDm = maxZDm;
            CentreDm = centreDm;
        }

        public int WidthDm => MaxXDm - MinXDm;
        public int DepthDm => MaxZDm - MinZDm;
    }

    public static class KentridgeCivicForecourtPlanner
    {
        public static KentridgeCivicForecourtPlan Build(uint seed)
        {
            SettlementPlan settlement = KentridgeDefinition.Build(seed);
            KentridgeUrbanSkeletonPlan skeleton = KentridgeUrbanSkeleton.Build(seed);
            KentridgeUrbanNode crown = skeleton.Get(KentridgeUrbanNodeId.CivicCrown);

            BuildingPlot church = FindPlot(settlement, KentridgeRole.Church);
            BuildingPlot mayor = FindPlot(settlement, KentridgeRole.MayorHouse);
            Int3 churchEnvelope = KentridgeDefinition.FootprintDm(church.Archetype);

            int minX = church.PositionDm.X + churchEnvelope.X;
            int maxX = mayor.PositionDm.X;
            int minZ = crown.CentreDm.Y - crown.OpenSpaceHalfExtentsDm.Y;
            int maxZ = crown.CentreDm.Y + crown.OpenSpaceHalfExtentsDm.Y;

            var plan = new KentridgeCivicForecourtPlan(
                minX,
                maxX,
                minZ,
                maxZ,
                crown.CentreDm);
            Validate(plan, crown, church, mayor);
            return plan;
        }

        private static BuildingPlot FindPlot(SettlementPlan settlement, KentridgeRole role)
        {
            for (int i = 0; i < settlement.Plots.Count; i++)
                if (settlement.Plots[i].RoleId == (int)role)
                    return settlement.Plots[i];

            throw new InvalidOperationException(
                "Kentridge civic forecourt is missing stable plot: " + role);
        }

        private static void Validate(
            KentridgeCivicForecourtPlan plan,
            KentridgeUrbanNode crown,
            BuildingPlot church,
            BuildingPlot mayor)
        {
            if (crown.Kind != KentridgeUrbanNodeKind.Forecourt
                || crown.Band != KentridgeUrbanBand.CivicCrown)
                throw new InvalidOperationException(
                    "Kentridge civic forecourt must be derived from the CivicCrown node.");

            if (church.Frontage != FrontageDirection.East
                || mayor.Frontage != FrontageDirection.West)
                throw new InvalidOperationException(
                    "Kentridge civic anchors no longer frame the main ascent.");

            if (plan.WidthDm <= KentridgeTownPlanner.MainRoadWidthDm)
                throw new InvalidOperationException(
                    "Kentridge civic forecourt must leave pedestrian apron beyond the main road.");
            if (plan.DepthDm <= 0)
                throw new InvalidOperationException(
                    "Kentridge civic forecourt has no north/south depth.");

            int nodeMinX = crown.CentreDm.X - crown.OpenSpaceHalfExtentsDm.X;
            int nodeMaxX = crown.CentreDm.X + crown.OpenSpaceHalfExtentsDm.X;
            if (plan.MinXDm < nodeMinX || plan.MaxXDm > nodeMaxX)
                throw new InvalidOperationException(
                    "Stable civic anchor gap escaped the authored CivicCrown reservation.");

            if (plan.MinXDm >= KentridgeTownPlanner.MainSpineXDm
                || plan.MaxXDm <= KentridgeTownPlanner.MainSpineXDm)
                throw new InvalidOperationException(
                    "Kentridge civic forecourt must span the main procession axis.");
        }
    }
}
