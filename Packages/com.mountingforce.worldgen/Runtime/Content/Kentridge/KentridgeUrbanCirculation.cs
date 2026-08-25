using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeUrbanConnectorKind : byte
    {
        ContourLane,
        StairStreet,
    }

    public readonly struct KentridgeUrbanConnector
    {
        public readonly string Id;
        public readonly KentridgeUrbanConnectorKind Kind;
        public readonly KentridgeUrbanBand Band;
        public readonly Int2 StartDm;
        public readonly Int2 EndDm;
        public readonly int WidthDm;

        public KentridgeUrbanConnector(string id, KentridgeUrbanConnectorKind kind, KentridgeUrbanBand band, Int2 startDm, Int2 endDm, int widthDm)
        {
            Id = id;
            Kind = kind;
            Band = band;
            StartDm = startDm;
            EndDm = endDm;
            WidthDm = widthDm;
        }

        public bool IsHorizontal => StartDm.Y == EndDm.Y;
        public bool IsVertical => StartDm.X == EndDm.X;
        public int LengthDm => IsHorizontal ? System.Math.Abs(EndDm.X - StartDm.X) : System.Math.Abs(EndDm.Y - StartDm.Y);
    }

    public sealed class KentridgeUrbanCirculationPlan
    {
        public IReadOnlyList<KentridgeUrbanConnector> Connectors => _connectors;
        private readonly List<KentridgeUrbanConnector> _connectors;
        public KentridgeUrbanCirculationPlan(List<KentridgeUrbanConnector> connectors) { _connectors = connectors; }
    }

    public static class KentridgeUrbanCirculation
    {
        public const int UpperContourZDm = 340;
        public const int UpperContourWidthDm = 40;

        // Tuck the Market-to-Upper stair into the urban fabric: west of the upper-west block's x=850
        // edge but far enough east to read as a stair street between buildings rather than a detached
        // hillside stair at the capture/world edge.
        public const int WestUpperStairXDm = 810;
        public const int WestUpperStairSouthZDm = KentridgeTownPlanner.MarketStreetZDm;
        public const int WestUpperStairNorthZDm = UpperContourZDm;
        public const int WestUpperStairWidthDm = 22;
        public const int WestUpperContourWidthDm = 22;

        public static KentridgeUrbanCirculationPlan Build(uint seed)
        {
            _ = seed;
            int mainEastEdge = KentridgeTownPlanner.MainSpineXDm + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int mainWestEdge = KentridgeTownPlanner.MainSpineXDm - KentridgeTownPlanner.MainRoadWidthDm / 2;
            int eastLaneWestEdge = KentridgeTownPlanner.EastLaneXDm - KentridgeTownPlanner.ServiceRoadWidthDm / 2;

            var connectors = new List<KentridgeUrbanConnector>(3)
            {
                new KentridgeUrbanConnector("upper-east-contour", KentridgeUrbanConnectorKind.ContourLane, KentridgeUrbanBand.UpperWard, new Int2(mainEastEdge, UpperContourZDm), new Int2(eastLaneWestEdge, UpperContourZDm), UpperContourWidthDm),
                new KentridgeUrbanConnector("west-upper-stair-street", KentridgeUrbanConnectorKind.StairStreet, KentridgeUrbanBand.UpperWard, new Int2(WestUpperStairXDm, WestUpperStairSouthZDm), new Int2(WestUpperStairXDm, WestUpperStairNorthZDm), WestUpperStairWidthDm),
                new KentridgeUrbanConnector("west-upper-contour", KentridgeUrbanConnectorKind.ContourLane, KentridgeUrbanBand.UpperWard, new Int2(WestUpperStairXDm, UpperContourZDm), new Int2(mainWestEdge, UpperContourZDm), WestUpperContourWidthDm),
            };
            return new KentridgeUrbanCirculationPlan(connectors);
        }
    }
}
