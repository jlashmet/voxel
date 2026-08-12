using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    /// <summary>
    /// Large-scale vertical neighbourhood bands. These are deliberately above building grammar:
    /// they describe where urban mass belongs on the hill, not how an individual building is built.
    /// </summary>
    public enum KentridgeUrbanBand : byte
    {
        LowerWard,
        MarketBelt,
        UpperWard,
        CivicCrown,
        NobleRidge,
    }

    /// <summary>
    /// One continuous piece of street/shelf frontage that should read as occupied urban mass.
    /// A later building grammar is free to split the run into attached houses, shops, alleys,
    /// courtyards, arcades, or other coherent architecture while preserving the macro intent.
    /// </summary>
    public readonly struct KentridgeFrontageRun
    {
        public readonly string Id;
        public readonly KentridgeUrbanBand Band;
        public readonly DistrictKind District;
        public readonly Int2 StartDm;
        public readonly Int2 EndDm;
        public readonly FrontageDirection Frontage;
        public readonly Int2 ElevationSampleDm;
        public readonly int CoveragePercent;
        public readonly int MinStoreys;
        public readonly int MaxStoreys;
        public readonly int TargetDepthDm;
        public readonly int EmbedBelowShelfDm;

        public KentridgeFrontageRun(
            string id,
            KentridgeUrbanBand band,
            DistrictKind district,
            Int2 startDm,
            Int2 endDm,
            FrontageDirection frontage,
            Int2 elevationSampleDm,
            int coveragePercent,
            int minStoreys,
            int maxStoreys,
            int targetDepthDm,
            int embedBelowShelfDm)
        {
            Id = id;
            Band = band;
            District = district;
            StartDm = startDm;
            EndDm = endDm;
            Frontage = frontage;
            ElevationSampleDm = elevationSampleDm;
            CoveragePercent = coveragePercent;
            MinStoreys = minStoreys;
            MaxStoreys = maxStoreys;
            TargetDepthDm = targetDepthDm;
            EmbedBelowShelfDm = embedBelowShelfDm;
        }

        public bool IsHorizontal => StartDm.Y == EndDm.Y;
        public bool IsVertical => StartDm.X == EndDm.X;

        public int LengthDm =>
            IsHorizontal
                ? Math.Abs(EndDm.X - StartDm.X)
                : Math.Abs(EndDm.Y - StartDm.Y);
    }

    /// <summary>
    /// A major over/under or gateway moment in the settlement circulation graph.
    /// This is a city-organization contract, not a request for a particular bridge model.
    /// </summary>
    public readonly struct KentridgeUrbanThreshold
    {
        public readonly string Id;
        public readonly Int2 CentreDm;
        public readonly int ClearWidthDm;
        public readonly KentridgeUrbanBand LowerBand;
        public readonly KentridgeUrbanBand UpperBand;

        public KentridgeUrbanThreshold(
            string id,
            Int2 centreDm,
            int clearWidthDm,
            KentridgeUrbanBand lowerBand,
            KentridgeUrbanBand upperBand)
        {
            Id = id;
            CentreDm = centreDm;
            ClearWidthDm = clearWidthDm;
            LowerBand = lowerBand;
            UpperBand = upperBand;
        }
    }

    public sealed class KentridgeUrbanMassingPlan
    {
        public IReadOnlyList<KentridgeFrontageRun> FrontageRuns => _frontageRuns;
        public IReadOnlyList<KentridgeUrbanThreshold> Thresholds => _thresholds;

        private readonly List<KentridgeFrontageRun> _frontageRuns;
        private readonly List<KentridgeUrbanThreshold> _thresholds;

        public KentridgeUrbanMassingPlan(
            List<KentridgeFrontageRun> frontageRuns,
            List<KentridgeUrbanThreshold> thresholds)
        {
            _frontageRuns = frontageRuns;
            _thresholds = thresholds;
        }
    }

    /// <summary>
    /// Organises Kentridge at the scale of neighbourhoods and continuous frontage.
    ///
    /// Named gameplay plots remain owned by KentridgeTownPlanner. This plan describes the anonymous
    /// urban fabric between/under those anchors, preserving the main north-south ascent as a strong
    /// visual and navigational void. The building-grammar system should eventually consume this
    /// directly; the current voxel massing catalogue is intentionally only a coarse visual adapter.
    /// </summary>
    public static class KentridgeUrbanOrganizer
    {
        public static KentridgeUrbanMassingPlan Build(uint seed)
        {
            // The first macro composition is intentionally stable across seeds. Seed is part of the
            // contract so later settlement variants can alter coverage/rhythm without changing APIs.
            _ = seed;

            var runs = new List<KentridgeFrontageRun>(6)
            {
                // Dense roofs climb immediately behind/below the three named market shops.
                new KentridgeFrontageRun(
                    "market-lower-cascade",
                    KentridgeUrbanBand.MarketBelt,
                    DistrictKind.Market,
                    new Int2(690, 676),
                    new Int2(1110, 676),
                    FrontageDirection.South,
                    new Int2(900, KentridgeTownPlanner.MarketStreetZDm),
                    85, 2, 3, 58, 56),

                // Upper town is intentionally split around the main spine. The gap is the visual
                // ascent and must remain legible all the way to the civic crown.
                new KentridgeFrontageRun(
                    "upper-west-cascade",
                    KentridgeUrbanBand.UpperWard,
                    DistrictKind.Market,
                    new Int2(850, 426),
                    new Int2(1110, 426),
                    FrontageDirection.South,
                    new Int2(980, 340),
                    82, 2, 3, 58, 56),

                new KentridgeFrontageRun(
                    "upper-east-cascade",
                    KentridgeUrbanBand.UpperWard,
                    DistrictKind.Residential,
                    new Int2(1230, 426),
                    new Int2(1400, 426),
                    FrontageDirection.South,
                    new Int2(1320, 340),
                    70, 2, 3, 58, 56),

                // Civic fabric frames the summit rather than filling its centre.
                new KentridgeFrontageRun(
                    "civic-west-cascade",
                    KentridgeUrbanBand.CivicCrown,
                    DistrictKind.Civic,
                    new Int2(900, 226),
                    new Int2(1110, 226),
                    FrontageDirection.South,
                    new Int2(1000, 150),
                    75, 2, 3, 58, 56),

                new KentridgeFrontageRun(
                    "civic-east-cascade",
                    KentridgeUrbanBand.CivicCrown,
                    DistrictKind.Civic,
                    new Int2(1240, 218),
                    new Int2(1390, 218),
                    FrontageDirection.South,
                    new Int2(1300, 150),
                    75, 2, 3, 58, 56),

                // Radcliffe's ridge is a separate high mass on the east, balancing the civic crown
                // without competing with the church/campanile as the central skyline hierarchy.
                new KentridgeFrontageRun(
                    "noble-south-cascade",
                    KentridgeUrbanBand.NobleRidge,
                    DistrictKind.Noble,
                    new Int2(1490, 382),
                    new Int2(1810, 382),
                    FrontageDirection.South,
                    new Int2(1650, 250),
                    72, 2, 3, 58, 56),
            };

            var thresholds = new List<KentridgeUrbanThreshold>(1)
            {
                new KentridgeUrbanThreshold(
                    "civic-gate",
                    new Int2(KentridgeTownPlanner.MainSpineXDm, 260),
                    100,
                    KentridgeUrbanBand.UpperWard,
                    KentridgeUrbanBand.CivicCrown),
            };

            Validate(runs, thresholds);
            return new KentridgeUrbanMassingPlan(runs, thresholds);
        }

        private static void Validate(
            List<KentridgeFrontageRun> runs,
            List<KentridgeUrbanThreshold> thresholds)
        {
            for (int i = 0; i < runs.Count; i++)
            {
                KentridgeFrontageRun run = runs[i];
                if (!run.IsHorizontal && !run.IsVertical)
                    throw new InvalidOperationException(
                        "Kentridge frontage run must be orthogonal: " + run.Id);
                if (run.LengthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge frontage run has no length: " + run.Id);
                if (run.CoveragePercent <= 0 || run.CoveragePercent > 100)
                    throw new InvalidOperationException(
                        "Kentridge frontage coverage is invalid: " + run.Id);
                if (run.MinStoreys <= 0 || run.MaxStoreys < run.MinStoreys)
                    throw new InvalidOperationException(
                        "Kentridge frontage storey range is invalid: " + run.Id);
                if (run.TargetDepthDm <= 0 || run.EmbedBelowShelfDm < 0)
                    throw new InvalidOperationException(
                        "Kentridge frontage dimensions are invalid: " + run.Id);
            }

            for (int i = 0; i < thresholds.Count; i++)
            {
                if (thresholds[i].ClearWidthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge urban threshold must preserve positive clearance: "
                        + thresholds[i].Id);
            }
        }
    }
}
