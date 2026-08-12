using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeUrbanConnectorKind : byte
    {
        ContourLane,
        StairStreet,
    }

    /// <summary>
    /// A city-scale connector that belongs to the urban organisation layer rather than the four
    /// stable gameplay streets. Building/circulation grammar may later realise this as a lane,
    /// arcade, stair street, bridge, or a mixture, while preserving its endpoints and clearance.
    /// </summary>
    public readonly struct KentridgeUrbanConnector
    {
        public readonly string Id;
        public readonly KentridgeUrbanConnectorKind Kind;
        public readonly KentridgeUrbanBand Band;
        public readonly Int2 StartDm;
        public readonly Int2 EndDm;
        public readonly int WidthDm;

        public KentridgeUrbanConnector(
            string id,
            KentridgeUrbanConnectorKind kind,
            KentridgeUrbanBand band,
            Int2 startDm,
            Int2 endDm,
            int widthDm)
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
        public int LengthDm => IsHorizontal
            ? System.Math.Abs(EndDm.X - StartDm.X)
            : System.Math.Abs(EndDm.Y - StartDm.Y);
    }

    public sealed class KentridgeUrbanCirculationPlan
    {
        public IReadOnlyList<KentridgeUrbanConnector> Connectors => _connectors;
        private readonly List<KentridgeUrbanConnector> _connectors;

        public KentridgeUrbanCirculationPlan(List<KentridgeUrbanConnector> connectors)
        {
            _connectors = connectors;
        }
    }

    /// <summary>
    /// Secondary pedestrian/city circulation layered over the stable settlement street topology.
    /// The east contour joins Upper Landing to the ridge. The lower-west stair provides a second
    /// Residential-to-Market ascent. A second west-side dogleg now reuses Market Street, climbs at
    /// its far western end, then returns along the upper contour behind the upper-west block. This
    /// gives Market-to-Upper movement a route that is spatially and topologically distinct from the
    /// heroic central spine without mutating the four stable gameplay streets.
    /// </summary>
    public static class KentridgeUrbanCirculation
    {
        public const int UpperContourZDm = 340;
        public const int UpperContourWidthDm = 40;

        public const int LowerWestStairXDm = 1125;
        public const int LowerWestStairSouthZDm = 900;
        public const int LowerWestStairNorthZDm = 590;
        public const int LowerWestStairWidthDm = 22;

        public const int WestUpperStairXDm = 700;
        public const int WestUpperStairSouthZDm = KentridgeTownPlanner.MarketStreetZDm;
        public const int WestUpperStairNorthZDm = UpperContourZDm;
        public const int WestUpperStairWidthDm = 22;
        public const int WestUpperContourWidthDm = 22;

        public static KentridgeUrbanCirculationPlan Build(uint seed)
        {
            _ = seed;

            int mainEastEdge =
                KentridgeTownPlanner.MainSpineXDm + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int mainWestEdge =
                KentridgeTownPlanner.MainSpineXDm - KentridgeTownPlanner.MainRoadWidthDm / 2;
            int eastLaneWestEdge =
                KentridgeTownPlanner.EastLaneXDm - KentridgeTownPlanner.ServiceRoadWidthDm / 2;

            var connectors = new List<KentridgeUrbanConnector>(4)
            {
                new KentridgeUrbanConnector(
                    "upper-east-contour",
                    KentridgeUrbanConnectorKind.ContourLane,
                    KentridgeUrbanBand.UpperWard,
                    new Int2(mainEastEdge, UpperContourZDm),
                    new Int2(eastLaneWestEdge, UpperContourZDm),
                    UpperContourWidthDm),

                new KentridgeUrbanConnector(
                    "lower-west-stair-street",
                    KentridgeUrbanConnectorKind.StairStreet,
                    KentridgeUrbanBand.LowerWard,
                    new Int2(LowerWestStairXDm, LowerWestStairSouthZDm),
                    new Int2(LowerWestStairXDm, LowerWestStairNorthZDm),
                    LowerWestStairWidthDm),

                new KentridgeUrbanConnector(
                    "west-upper-stair-street",
                    KentridgeUrbanConnectorKind.StairStreet,
                    KentridgeUrbanBand.UpperWard,
                    new Int2(WestUpperStairXDm, WestUpperStairSouthZDm),
                    new Int2(WestUpperStairXDm, WestUpperStairNorthZDm),
                    WestUpperStairWidthDm),

                new KentridgeUrbanConnector(
                    "west-upper-contour",
                    KentridgeUrbanConnectorKind.ContourLane,
                    KentridgeUrbanBand.UpperWard,
                    new Int2(WestUpperStairXDm, UpperContourZDm),
                    new Int2(mainWestEdge, UpperContourZDm),
                    WestUpperContourWidthDm),
            };

            return new KentridgeUrbanCirculationPlan(connectors);
        }
    }
}
