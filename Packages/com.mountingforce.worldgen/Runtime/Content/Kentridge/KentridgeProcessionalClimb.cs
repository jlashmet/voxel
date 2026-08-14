using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeProcessionalSegmentKind : byte
    {
        Rise,
        Landing,
    }

    /// <summary>One northbound piece of the Market-to-Civic public procession.</summary>
    public readonly struct KentridgeProcessionalSegment
    {
        public readonly string Id;
        public readonly KentridgeProcessionalSegmentKind Kind;
        public readonly int SouthZDm;
        public readonly int NorthZDm;
        public readonly int SouthOffsetDm;
        public readonly int NorthOffsetDm;
        public readonly int WidthDm;

        public KentridgeProcessionalSegment(
            string id,
            KentridgeProcessionalSegmentKind kind,
            int southZDm,
            int northZDm,
            int southOffsetDm,
            int northOffsetDm,
            int widthDm)
        {
            Id = id;
            Kind = kind;
            SouthZDm = southZDm;
            NorthZDm = northZDm;
            SouthOffsetDm = southOffsetDm;
            NorthOffsetDm = northOffsetDm;
            WidthDm = widthDm;
        }

        public int LengthDm => SouthZDm - NorthZDm;
        public int RiseDm => NorthOffsetDm - SouthOffsetDm;
    }

    public sealed class KentridgeProcessionalClimbPlan
    {
        public IReadOnlyList<KentridgeProcessionalSegment> Segments => _segments;
        private readonly List<KentridgeProcessionalSegment> _segments;

        public KentridgeProcessionalClimbPlan(List<KentridgeProcessionalSegment> segments)
        {
            _segments = segments;
        }
    }

    /// <summary>
    /// Turns the central skeleton into a legible sequence of urban rooms. Market Square is already a
    /// full plaza; north of it the route alternates climb and pause before terminating at Civic Crown.
    /// Landing widths come directly from the skeleton's open-space reservations, while rise widths
    /// retain the stable main-street width.
    /// </summary>
    public static class KentridgeProcessionalClimb
    {
        // This semantic section owns the upper-town breakpoints. The voxel height profile aliases
        // them so city planning stays above rendering rather than depending on it.
        public const int MarketOffsetDm = 35;
        public const int UpperLandingOffsetDm = 75;
        public const int CivicGateOffsetDm = 100;
        public const int UpperCivicOffsetDm = 130;
        public const int SummitOffsetDm = 145;

        public const int MarketRiseSouthZDm = 440;
        public const int UpperLandingSouthZDm = 375;
        public const int UpperLandingNorthZDm = 320;
        public const int CivicGateSouthZDm = 280;
        public const int CivicGateNorthZDm = 240;
        public const int UpperCivicSouthZDm = 190;
        public const int SummitSouthZDm = 150;

        public static KentridgeProcessionalClimbPlan Build(uint seed)
        {
            KentridgeUrbanSkeletonPlan skeleton = KentridgeUrbanSkeleton.Build(seed);
            KentridgeUrbanNode upper = skeleton.Get(KentridgeUrbanNodeId.UpperLanding);
            KentridgeUrbanNode gate = skeleton.Get(KentridgeUrbanNodeId.CivicGate);
            KentridgeUrbanNode crown = skeleton.Get(KentridgeUrbanNodeId.CivicCrown);

            var segments = new List<KentridgeProcessionalSegment>(6)
            {
                new KentridgeProcessionalSegment(
                    "market-to-upper-rise",
                    KentridgeProcessionalSegmentKind.Rise,
                    MarketRiseSouthZDm,
                    UpperLandingSouthZDm,
                    MarketOffsetDm,
                    UpperLandingOffsetDm,
                    KentridgeTownPlanner.MainRoadWidthDm),

                new KentridgeProcessionalSegment(
                    "upper-landing",
                    KentridgeProcessionalSegmentKind.Landing,
                    UpperLandingSouthZDm,
                    UpperLandingNorthZDm,
                    UpperLandingOffsetDm,
                    UpperLandingOffsetDm,
                    upper.OpenSpaceHalfExtentsDm.X * 2),

                new KentridgeProcessionalSegment(
                    "upper-to-civic-gate-rise",
                    KentridgeProcessionalSegmentKind.Rise,
                    UpperLandingNorthZDm,
                    CivicGateSouthZDm,
                    UpperLandingOffsetDm,
                    CivicGateOffsetDm,
                    KentridgeTownPlanner.MainRoadWidthDm),

                new KentridgeProcessionalSegment(
                    "civic-gate",
                    KentridgeProcessionalSegmentKind.Landing,
                    CivicGateSouthZDm,
                    CivicGateNorthZDm,
                    CivicGateOffsetDm,
                    CivicGateOffsetDm,
                    gate.OpenSpaceHalfExtentsDm.X * 2),

                new KentridgeProcessionalSegment(
                    "gate-to-upper-civic-rise",
                    KentridgeProcessionalSegmentKind.Rise,
                    CivicGateNorthZDm,
                    UpperCivicSouthZDm,
                    CivicGateOffsetDm,
                    UpperCivicOffsetDm,
                    KentridgeTownPlanner.MainRoadWidthDm),

                new KentridgeProcessionalSegment(
                    "final-crown-rise",
                    KentridgeProcessionalSegmentKind.Rise,
                    UpperCivicSouthZDm,
                    SummitSouthZDm,
                    UpperCivicOffsetDm,
                    SummitOffsetDm,
                    KentridgeTownPlanner.MainRoadWidthDm),
            };

            Validate(segments, upper, gate, crown);
            return new KentridgeProcessionalClimbPlan(segments);
        }

        private static void Validate(
            List<KentridgeProcessionalSegment> segments,
            KentridgeUrbanNode upper,
            KentridgeUrbanNode gate,
            KentridgeUrbanNode crown)
        {
            if (segments.Count != 6)
                throw new InvalidOperationException("Kentridge processional climb must have six pieces.");

            for (int i = 0; i < segments.Count; i++)
            {
                KentridgeProcessionalSegment segment = segments[i];
                if (segment.LengthDm <= 0 || segment.WidthDm <= 0 || segment.RiseDm < 0)
                    throw new InvalidOperationException(
                        "Invalid Kentridge processional segment: " + segment.Id);
                if (segment.Kind == KentridgeProcessionalSegmentKind.Landing
                    && segment.RiseDm != 0)
                    throw new InvalidOperationException(
                        "Kentridge landing must be flat: " + segment.Id);
                if (i > 0)
                {
                    KentridgeProcessionalSegment previous = segments[i - 1];
                    if (previous.NorthZDm != segment.SouthZDm
                        || previous.NorthOffsetDm != segment.SouthOffsetDm)
                        throw new InvalidOperationException(
                            "Kentridge processional climb has a discontinuity before: " + segment.Id);
                }
            }

            KentridgeProcessionalSegment upperLanding = segments[1];
            KentridgeProcessionalSegment civicGate = segments[3];
            if (upper.CentreDm.Y < upperLanding.NorthZDm
                || upper.CentreDm.Y > upperLanding.SouthZDm)
                throw new InvalidOperationException("Upper Landing node escaped its flat public room.");
            if (gate.CentreDm.Y < civicGate.NorthZDm
                || gate.CentreDm.Y > civicGate.SouthZDm)
                throw new InvalidOperationException("Civic Gate node escaped its flat public room.");
            if (crown.CentreDm.Y != SummitSouthZDm)
                throw new InvalidOperationException(
                    "Civic Crown should remain the terminus of the processional climb.");
        }
    }
}
