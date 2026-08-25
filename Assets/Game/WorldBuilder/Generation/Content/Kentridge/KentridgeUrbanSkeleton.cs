using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeUrbanNodeId : byte
    {
        SouthApproach = 0,
        ResidentialJunction = 1,
        MarketSquare = 2,
        UpperLanding = 3,
        CivicGate = 4,
        CivicCrown = 5,
        EastMarketJunction = 6,
        EastRidgeLanding = 7,
        WorkingYard = 8,
        // Value 9 is retained for data compatibility, but the duplicate lower-west stair landing is
        // no longer instantiated in the urban skeleton. The primary spine owns that lower climb.
        WestMarketLanding = 9,
        WestMarketJunction = 10,
        WestUpperLanding = 11,
        EastResidentialJunction = 12,
    }

    public enum KentridgeUrbanNodeKind : byte
    {
        Arrival,
        Junction,
        Plaza,
        Landing,
        Gate,
        Forecourt,
        Yard,
    }

    public enum KentridgeUrbanLinkKind : byte
    {
        PrimaryStreet,
        SecondaryContour,
        SecondaryStair,
    }

    public readonly struct KentridgeUrbanNode
    {
        public readonly KentridgeUrbanNodeId Id;
        public readonly KentridgeUrbanNodeKind Kind;
        public readonly KentridgeUrbanBand Band;
        public readonly DistrictKind District;
        public readonly Int2 CentreDm;
        public readonly Int2 OpenSpaceHalfExtentsDm;
        public readonly byte Importance;

        public KentridgeUrbanNode(KentridgeUrbanNodeId id, KentridgeUrbanNodeKind kind, KentridgeUrbanBand band, DistrictKind district, Int2 centreDm, Int2 openSpaceHalfExtentsDm, byte importance)
        {
            Id = id;
            Kind = kind;
            Band = band;
            District = district;
            CentreDm = centreDm;
            OpenSpaceHalfExtentsDm = openSpaceHalfExtentsDm;
            Importance = importance;
        }
    }

    public readonly struct KentridgeUrbanLink
    {
        public readonly string Id;
        public readonly KentridgeUrbanNodeId From;
        public readonly KentridgeUrbanNodeId To;
        public readonly KentridgeUrbanLinkKind Kind;

        public KentridgeUrbanLink(string id, KentridgeUrbanNodeId from, KentridgeUrbanNodeId to, KentridgeUrbanLinkKind kind)
        {
            Id = id;
            From = from;
            To = to;
            Kind = kind;
        }
    }

    public sealed class KentridgeUrbanSkeletonPlan
    {
        public IReadOnlyList<KentridgeUrbanNode> Nodes => _nodes;
        public IReadOnlyList<KentridgeUrbanLink> Links => _links;
        private readonly List<KentridgeUrbanNode> _nodes;
        private readonly List<KentridgeUrbanLink> _links;

        public KentridgeUrbanSkeletonPlan(List<KentridgeUrbanNode> nodes, List<KentridgeUrbanLink> links)
        {
            _nodes = nodes;
            _links = links;
        }

        public KentridgeUrbanNode Get(KentridgeUrbanNodeId id)
        {
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].Id == id) return _nodes[i];
            throw new InvalidOperationException("Kentridge urban node is missing: " + id);
        }
    }

    public static class KentridgeUrbanSkeleton
    {
        public static KentridgeUrbanSkeletonPlan Build(uint seed)
        {
            _ = seed;
            var nodes = new List<KentridgeUrbanNode>(12)
            {
                new KentridgeUrbanNode(KentridgeUrbanNodeId.SouthApproach, KentridgeUrbanNodeKind.Arrival, KentridgeUrbanBand.LowerWard, DistrictKind.Residential, new Int2(KentridgeTownPlanner.MainSpineXDm, 1030), new Int2(55, 35), 1),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.ResidentialJunction, KentridgeUrbanNodeKind.Junction, KentridgeUrbanBand.LowerWard, DistrictKind.Residential, new Int2(KentridgeTownPlanner.MainSpineXDm, KentridgeTownPlanner.ResidentialStreetZDm), new Int2(45, 32), 2),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.MarketSquare, KentridgeUrbanNodeKind.Plaza, KentridgeUrbanBand.MarketBelt, DistrictKind.Market, KentridgeDefinition.TownCentreDm, new Int2(110, 70), 4),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.UpperLanding, KentridgeUrbanNodeKind.Landing, KentridgeUrbanBand.UpperWard, DistrictKind.Market, new Int2(KentridgeTownPlanner.MainSpineXDm, 340), new Int2(48, 34), 3),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.CivicGate, KentridgeUrbanNodeKind.Gate, KentridgeUrbanBand.UpperWard, DistrictKind.Civic, new Int2(KentridgeTownPlanner.MainSpineXDm, 260), new Int2(50, 24), 3),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.CivicCrown, KentridgeUrbanNodeKind.Forecourt, KentridgeUrbanBand.CivicCrown, DistrictKind.Civic, new Int2(KentridgeTownPlanner.MainSpineXDm, 150), new Int2(78, 44), 4),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.EastMarketJunction, KentridgeUrbanNodeKind.Junction, KentridgeUrbanBand.MarketBelt, DistrictKind.Working, new Int2(KentridgeTownPlanner.EastLaneXDm, KentridgeTownPlanner.MarketStreetZDm), new Int2(42, 30), 2),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.EastRidgeLanding, KentridgeUrbanNodeKind.Landing, KentridgeUrbanBand.NobleRidge, DistrictKind.Noble, new Int2(KentridgeTownPlanner.EastLaneXDm, KentridgeUrbanCirculation.UpperContourZDm), new Int2(48, 34), 3),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.WorkingYard, KentridgeUrbanNodeKind.Yard, KentridgeUrbanBand.MarketBelt, DistrictKind.Working, new Int2(KentridgeTownPlanner.EastLaneXDm, 700), new Int2(72, 52), 2),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.WestMarketJunction, KentridgeUrbanNodeKind.Junction, KentridgeUrbanBand.MarketBelt, DistrictKind.Market, new Int2(KentridgeUrbanCirculation.WestUpperStairXDm, KentridgeTownPlanner.MarketStreetZDm), new Int2(22, 18), 2),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.WestUpperLanding, KentridgeUrbanNodeKind.Landing, KentridgeUrbanBand.UpperWard, DistrictKind.Market, new Int2(KentridgeUrbanCirculation.WestUpperStairXDm, KentridgeUrbanCirculation.UpperContourZDm), new Int2(24, 18), 2),
                new KentridgeUrbanNode(KentridgeUrbanNodeId.EastResidentialJunction, KentridgeUrbanNodeKind.Junction, KentridgeUrbanBand.LowerWard, DistrictKind.Residential, new Int2(KentridgeTownPlanner.EastLaneXDm, KentridgeTownPlanner.ResidentialStreetZDm), new Int2(38, 28), 2),
            };

            var links = new List<KentridgeUrbanLink>(14)
            {
                new KentridgeUrbanLink("approach-to-residential", KentridgeUrbanNodeId.SouthApproach, KentridgeUrbanNodeId.ResidentialJunction, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("residential-to-market", KentridgeUrbanNodeId.ResidentialJunction, KentridgeUrbanNodeId.MarketSquare, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("market-to-upper", KentridgeUrbanNodeId.MarketSquare, KentridgeUrbanNodeId.UpperLanding, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("upper-to-civic-gate", KentridgeUrbanNodeId.UpperLanding, KentridgeUrbanNodeId.CivicGate, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("civic-gate-to-crown", KentridgeUrbanNodeId.CivicGate, KentridgeUrbanNodeId.CivicCrown, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("market-to-east-market", KentridgeUrbanNodeId.MarketSquare, KentridgeUrbanNodeId.EastMarketJunction, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("east-market-to-working", KentridgeUrbanNodeId.EastMarketJunction, KentridgeUrbanNodeId.WorkingYard, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("east-market-to-ridge", KentridgeUrbanNodeId.EastMarketJunction, KentridgeUrbanNodeId.EastRidgeLanding, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("upper-to-east-ridge", KentridgeUrbanNodeId.UpperLanding, KentridgeUrbanNodeId.EastRidgeLanding, KentridgeUrbanLinkKind.SecondaryContour),
                new KentridgeUrbanLink("market-to-west-market-junction", KentridgeUrbanNodeId.MarketSquare, KentridgeUrbanNodeId.WestMarketJunction, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("west-market-junction-to-upper-stair", KentridgeUrbanNodeId.WestMarketJunction, KentridgeUrbanNodeId.WestUpperLanding, KentridgeUrbanLinkKind.SecondaryStair),
                new KentridgeUrbanLink("west-upper-to-upper-landing", KentridgeUrbanNodeId.WestUpperLanding, KentridgeUrbanNodeId.UpperLanding, KentridgeUrbanLinkKind.SecondaryContour),
                new KentridgeUrbanLink("residential-to-east-residential", KentridgeUrbanNodeId.ResidentialJunction, KentridgeUrbanNodeId.EastResidentialJunction, KentridgeUrbanLinkKind.PrimaryStreet),
                new KentridgeUrbanLink("east-residential-to-working", KentridgeUrbanNodeId.EastResidentialJunction, KentridgeUrbanNodeId.WorkingYard, KentridgeUrbanLinkKind.PrimaryStreet),
            };

            Validate(nodes, links);
            return new KentridgeUrbanSkeletonPlan(nodes, links);
        }

        private static void Validate(List<KentridgeUrbanNode> nodes, List<KentridgeUrbanLink> links)
        {
            var ids = new HashSet<KentridgeUrbanNodeId>();
            for (int i = 0; i < nodes.Count; i++)
            {
                KentridgeUrbanNode node = nodes[i];
                if (!ids.Add(node.Id)) throw new InvalidOperationException("Duplicate Kentridge urban node: " + node.Id);
                if (node.OpenSpaceHalfExtentsDm.X <= 0 || node.OpenSpaceHalfExtentsDm.Y <= 0) throw new InvalidOperationException("Kentridge urban node has an invalid open-space reservation: " + node.Id);
                if (node.Importance == 0) throw new InvalidOperationException("Kentridge urban node must have nonzero importance: " + node.Id);
            }
            for (int i = 0; i < links.Count; i++)
            {
                KentridgeUrbanLink link = links[i];
                if (!ids.Contains(link.From) || !ids.Contains(link.To)) throw new InvalidOperationException("Kentridge urban link references a missing node: " + link.Id);
                if (link.From == link.To) throw new InvalidOperationException("Kentridge urban link cannot be a self-loop: " + link.Id);
            }
        }
    }
}
