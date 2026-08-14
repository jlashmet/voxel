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

    [Flags]
    public enum KentridgeBlockEdge : byte
    {
        None = 0,
        South = 1 << 0,
        West = 1 << 1,
        North = 1 << 2,
        East = 1 << 3,
    }

    /// <summary>
    /// One coherent piece of anonymous town fabric. A block owns the urban intent at a scale above
    /// individual buildings: which perimeter edges should be occupied, how tall that fabric may be,
    /// where a court is protected, and which frontage deliberately opens into that court.
    ///
    /// The later building grammar is free to realise the perimeter as attached houses, shops,
    /// arcades, stepped wings, alleys, or mixed forms. It must preserve the block's public edges and
    /// interior void rather than treating every generated building as an isolated plot.
    /// </summary>
    public readonly struct KentridgeUrbanBlock
    {
        public readonly string Id;
        public readonly KentridgeUrbanBand Band;
        public readonly DistrictKind District;
        public readonly Int2 MinDm;
        public readonly Int2 MaxDm;
        public readonly KentridgeBlockEdge FrontageEdges;
        public readonly KentridgeBlockEdge CourtAccessEdge;
        public readonly Int2 ElevationSampleDm;
        public readonly int CoveragePercent;
        public readonly int MinStoreys;
        public readonly int MaxStoreys;
        public readonly int TargetDepthDm;
        public readonly int EmbedBelowShelfDm;
        public readonly int InteriorVoidInsetDm;
        public readonly int AccessWidthDm;

        public KentridgeUrbanBlock(
            string id,
            KentridgeUrbanBand band,
            DistrictKind district,
            Int2 minDm,
            Int2 maxDm,
            KentridgeBlockEdge frontageEdges,
            KentridgeBlockEdge courtAccessEdge,
            Int2 elevationSampleDm,
            int coveragePercent,
            int minStoreys,
            int maxStoreys,
            int targetDepthDm,
            int embedBelowShelfDm,
            int interiorVoidInsetDm,
            int accessWidthDm)
        {
            Id = id;
            Band = band;
            District = district;
            MinDm = minDm;
            MaxDm = maxDm;
            FrontageEdges = frontageEdges;
            CourtAccessEdge = courtAccessEdge;
            ElevationSampleDm = elevationSampleDm;
            CoveragePercent = coveragePercent;
            MinStoreys = minStoreys;
            MaxStoreys = maxStoreys;
            TargetDepthDm = targetDepthDm;
            EmbedBelowShelfDm = embedBelowShelfDm;
            InteriorVoidInsetDm = interiorVoidInsetDm;
            AccessWidthDm = accessWidthDm;
        }

        public int WidthDm => MaxDm.X - MinDm.X;
        public int DepthDm => MaxDm.Y - MinDm.Y;
        public Int2 InteriorMinDm =>
            new Int2(MinDm.X + InteriorVoidInsetDm, MinDm.Y + InteriorVoidInsetDm);
        public Int2 InteriorMaxDm =>
            new Int2(MaxDm.X - InteriorVoidInsetDm, MaxDm.Y - InteriorVoidInsetDm);
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
        public readonly int GapCentreDm;
        public readonly int GapWidthDm;

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
            int embedBelowShelfDm,
            int gapCentreDm = 0,
            int gapWidthDm = 0)
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
            GapCentreDm = gapCentreDm;
            GapWidthDm = gapWidthDm;
        }

        public bool IsHorizontal => StartDm.Y == EndDm.Y;
        public bool IsVertical => StartDm.X == EndDm.X;
        public bool HasGap => GapWidthDm > 0;

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
        public IReadOnlyList<KentridgeUrbanBlock> Blocks => _blocks;
        public IReadOnlyList<KentridgeFrontageRun> FrontageRuns => _frontageRuns;
        public IReadOnlyList<KentridgeUrbanThreshold> Thresholds => _thresholds;

        private readonly List<KentridgeUrbanBlock> _blocks;
        private readonly List<KentridgeFrontageRun> _frontageRuns;
        private readonly List<KentridgeUrbanThreshold> _thresholds;

        public KentridgeUrbanMassingPlan(
            List<KentridgeUrbanBlock> blocks,
            List<KentridgeFrontageRun> frontageRuns,
            List<KentridgeUrbanThreshold> thresholds)
        {
            _blocks = blocks;
            _frontageRuns = frontageRuns;
            _thresholds = thresholds;
        }
    }

    /// <summary>
    /// Organises Kentridge at the scale of neighbourhood blocks and continuous frontage.
    ///
    /// Named gameplay plots remain owned by KentridgeTownPlanner. Blocks describe the anonymous
    /// urban fabric between/under those anchors while preserving the main north-south ascent and the
    /// public-space reservations defined by KentridgeUrbanSkeleton. Frontage runs are derived from
    /// block edges so later building grammar receives a coherent perimeter/court contract rather than
    /// a disconnected list of prefab positions.
    /// </summary>
    public static class KentridgeUrbanOrganizer
    {
        public static KentridgeUrbanMassingPlan Build(uint seed)
        {
            // The first macro composition is intentionally stable across seeds. Seed is part of the
            // contract so later settlement variants can alter coverage/rhythm without changing APIs.
            _ = seed;

            var blocks = new List<KentridgeUrbanBlock>(8)
            {
                // A lower neighbourhood gives the climb a real urban base instead of beginning with
                // empty terrain. It stays two storeys and only lightly embeds into the lower shelf.
                new KentridgeUrbanBlock(
                    "lower-west-neighbourhood",
                    KentridgeUrbanBand.LowerWard,
                    DistrictKind.Residential,
                    new Int2(690, 958),
                    new Int2(1080, 1048),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.West,
                    KentridgeBlockEdge.South,
                    new Int2(900, KentridgeTownPlanner.ResidentialStreetZDm),
                    68, 2, 2, 58, 18, 20, 32),

                // Market fabric begins just beyond the south edge of the market-square reservation.
                // The centre opening is a lane into a protected rear court, not a missing building.
                new KentridgeUrbanBlock(
                    "market-lower-block",
                    KentridgeUrbanBand.MarketBelt,
                    DistrictKind.Market,
                    new Int2(690, 676),
                    new Int2(1110, 784),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.West,
                    KentridgeBlockEdge.South,
                    new Int2(900, KentridgeTownPlanner.MarketStreetZDm),
                    85, 2, 2, 58, 56, 24, 32),

                // Upper town turns its frontage around the outer corners. The open middle of the
                // ascent remains untouched while these blocks provide side walls and glimpses into
                // courts rather than a single row of roofs.
                new KentridgeUrbanBlock(
                    "upper-west-block",
                    KentridgeUrbanBand.UpperWard,
                    DistrictKind.Market,
                    new Int2(850, 426),
                    new Int2(1110, 520),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.West,
                    KentridgeBlockEdge.South,
                    new Int2(980, 340),
                    82, 2, 3, 58, 56, 22, 24),

                new KentridgeUrbanBlock(
                    "upper-east-block",
                    KentridgeUrbanBand.UpperWard,
                    DistrictKind.Residential,
                    new Int2(1230, 426),
                    new Int2(1400, 520),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.East,
                    KentridgeBlockEdge.South,
                    new Int2(1320, 340),
                    70, 2, 3, 58, 56, 22, 24),

                // Civic blocks frame the crown from both sides. Their interior courts stop the summit
                // from becoming a solid slab while three-storey perimeter fabric reinforces hierarchy.
                new KentridgeUrbanBlock(
                    "civic-west-block",
                    KentridgeUrbanBand.CivicCrown,
                    DistrictKind.Civic,
                    new Int2(900, 226),
                    new Int2(1110, 326),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.West,
                    KentridgeBlockEdge.South,
                    new Int2(1000, 150),
                    75, 3, 3, 58, 56, 24, 24),

                new KentridgeUrbanBlock(
                    "civic-east-block",
                    KentridgeUrbanBand.CivicCrown,
                    DistrictKind.Civic,
                    new Int2(1240, 218),
                    new Int2(1390, 318),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.East,
                    KentridgeBlockEdge.South,
                    new Int2(1300, 150),
                    75, 3, 3, 58, 56, 24, 20),

                // Radcliffe's ridge remains a strong secondary urban mass one storey below the civic
                // crown. The east return stops the ridge reading as an isolated horizontal shelf.
                new KentridgeUrbanBlock(
                    "noble-ridge-block",
                    KentridgeUrbanBand.NobleRidge,
                    DistrictKind.Noble,
                    new Int2(1490, 382),
                    new Int2(1810, 500),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.East,
                    KentridgeBlockEdge.South,
                    new Int2(1650, 250),
                    72, 2, 2, 58, 56, 26, 32),

                // The working yard finally receives an urban edge opposite the gameplay Warehouse.
                // One- and two-storey workshop/service fabric faces the east service lane, turns the
                // south corner, and preserves a broad service court. Keeping this in LowerWard avoids
                // competing with the market/civic skyline or creating another deep vertical facade.
                new KentridgeUrbanBlock(
                    "working-lane-block",
                    KentridgeUrbanBand.LowerWard,
                    DistrictKind.Working,
                    new Int2(1300, 650),
                    new Int2(1460, 820),
                    KentridgeBlockEdge.South | KentridgeBlockEdge.East,
                    KentridgeBlockEdge.South,
                    new Int2(1440, 700),
                    74, 1, 2, 58, 18, 20, 28),
            };

            var runs = new List<KentridgeFrontageRun>(16);
            for (int i = 0; i < blocks.Count; i++)
                AddFrontageRuns(blocks[i], runs);

            var thresholds = new List<KentridgeUrbanThreshold>(1)
            {
                new KentridgeUrbanThreshold(
                    "civic-gate",
                    new Int2(KentridgeTownPlanner.MainSpineXDm, 260),
                    100,
                    KentridgeUrbanBand.UpperWard,
                    KentridgeUrbanBand.CivicCrown),
            };

            Validate(blocks, runs, thresholds);
            return new KentridgeUrbanMassingPlan(blocks, runs, thresholds);
        }

        private static void AddFrontageRuns(
            KentridgeUrbanBlock block,
            List<KentridgeFrontageRun> runs)
        {
            if ((block.FrontageEdges & KentridgeBlockEdge.South) != 0)
                AddRun(
                    block, KentridgeBlockEdge.South,
                    new Int2(block.MinDm.X, block.MinDm.Y),
                    new Int2(block.MaxDm.X, block.MinDm.Y),
                    FrontageDirection.South,
                    runs);

            if ((block.FrontageEdges & KentridgeBlockEdge.West) != 0)
                AddRun(
                    block, KentridgeBlockEdge.West,
                    new Int2(block.MinDm.X, block.MinDm.Y),
                    new Int2(block.MinDm.X, block.MaxDm.Y),
                    FrontageDirection.West,
                    runs);

            if ((block.FrontageEdges & KentridgeBlockEdge.North) != 0)
                AddRun(
                    block, KentridgeBlockEdge.North,
                    new Int2(block.MinDm.X, block.MaxDm.Y),
                    new Int2(block.MaxDm.X, block.MaxDm.Y),
                    FrontageDirection.North,
                    runs);

            if ((block.FrontageEdges & KentridgeBlockEdge.East) != 0)
                AddRun(
                    block, KentridgeBlockEdge.East,
                    new Int2(block.MaxDm.X, block.MinDm.Y),
                    new Int2(block.MaxDm.X, block.MaxDm.Y),
                    FrontageDirection.East,
                    runs);
        }

        private static void AddRun(
            KentridgeUrbanBlock block,
            KentridgeBlockEdge edge,
            Int2 startDm,
            Int2 endDm,
            FrontageDirection frontage,
            List<KentridgeFrontageRun> runs)
        {
            bool access = block.CourtAccessEdge == edge;
            int gapCentre = 0;
            if (access)
                gapCentre = startDm.Y == endDm.Y
                    ? (startDm.X + endDm.X) / 2
                    : (startDm.Y + endDm.Y) / 2;

            runs.Add(new KentridgeFrontageRun(
                block.Id + "-" + edge.ToString().ToLowerInvariant(),
                block.Band,
                block.District,
                startDm,
                endDm,
                frontage,
                block.ElevationSampleDm,
                block.CoveragePercent,
                block.MinStoreys,
                block.MaxStoreys,
                block.TargetDepthDm,
                block.EmbedBelowShelfDm,
                gapCentre,
                access ? block.AccessWidthDm : 0));
        }

        private static void Validate(
            List<KentridgeUrbanBlock> blocks,
            List<KentridgeFrontageRun> runs,
            List<KentridgeUrbanThreshold> thresholds)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                KentridgeUrbanBlock block = blocks[i];
                if (block.WidthDm <= 0 || block.DepthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge urban block has invalid bounds: " + block.Id);
                if (block.FrontageEdges == KentridgeBlockEdge.None)
                    throw new InvalidOperationException(
                        "Kentridge urban block has no public frontage: " + block.Id);
                if (block.CoveragePercent <= 0 || block.CoveragePercent > 100)
                    throw new InvalidOperationException(
                        "Kentridge urban block coverage is invalid: " + block.Id);
                if (block.MinStoreys <= 0 || block.MaxStoreys < block.MinStoreys)
                    throw new InvalidOperationException(
                        "Kentridge urban block storey range is invalid: " + block.Id);
                if (block.TargetDepthDm <= 0 || block.EmbedBelowShelfDm < 0)
                    throw new InvalidOperationException(
                        "Kentridge urban block dimensions are invalid: " + block.Id);
                if (block.InteriorVoidInsetDm <= 0
                    || block.InteriorVoidInsetDm * 2 >= block.WidthDm
                    || block.InteriorVoidInsetDm * 2 >= block.DepthDm)
                    throw new InvalidOperationException(
                        "Kentridge urban block must preserve a positive interior void: " + block.Id);
                if (block.CourtAccessEdge == KentridgeBlockEdge.None
                    || (block.CourtAccessEdge & block.FrontageEdges) == 0
                    || !IsSingleEdge(block.CourtAccessEdge))
                    throw new InvalidOperationException(
                        "Kentridge urban block court access must select one frontage edge: " + block.Id);
                if (block.AccessWidthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge urban block court access must have positive width: " + block.Id);
            }

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
                if (run.HasGap && (run.GapWidthDm >= run.LengthDm || run.GapWidthDm <= 0))
                    throw new InvalidOperationException(
                        "Kentridge frontage access gap is invalid: " + run.Id);
            }

            for (int i = 0; i < thresholds.Count; i++)
            {
                if (thresholds[i].ClearWidthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge urban threshold must preserve positive clearance: "
                        + thresholds[i].Id);
            }
        }

        private static bool IsSingleEdge(KentridgeBlockEdge edge)
        {
            int value = (int)edge;
            return value != 0 && (value & (value - 1)) == 0;
        }
    }
}
