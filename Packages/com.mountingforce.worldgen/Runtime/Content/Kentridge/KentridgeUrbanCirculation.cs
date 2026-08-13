using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeUrbanConnectorKind : byte
    {
        ContourLane,
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
        public int LengthDm => System.Math.Abs(EndDm.X - StartDm.X);
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
    /// The first connector ties the central upper ascent directly to the east/noble ridge at a
    /// constant contour, eliminating the current "separate island" reading without moving any role.
    /// </summary>
    public static class KentridgeUrbanCirculation
    {
        public const int UpperContourZDm = 300;
        public const int UpperContourWidthDm = 22;

        public static KentridgeUrbanCirculationPlan Build(uint seed)
        {
            _ = seed;

            int mainEastEdge =
                KentridgeTownPlanner.MainSpineXDm + KentridgeTownPlanner.MainRoadWidthDm / 2;
            int eastLaneWestEdge =
                KentridgeTownPlanner.EastLaneXDm - KentridgeTownPlanner.ServiceRoadWidthDm / 2;

            var connectors = new List<KentridgeUrbanConnector>(1)
            {
                new KentridgeUrbanConnector(
                    "upper-east-contour",
                    KentridgeUrbanConnectorKind.ContourLane,
                    KentridgeUrbanBand.UpperWard,
                    new Int2(mainEastEdge, UpperContourZDm),
                    new Int2(eastLaneWestEdge, UpperContourZDm),
                    UpperContourWidthDm),
            };

            return new KentridgeUrbanCirculationPlan(connectors);
        }
    }
}
