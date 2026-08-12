using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeVerticalFrontageStyle : byte
    {
        MarketArcade,
        UrbanUndercroft,
        CivicLoggia,
        NobleTerrace,
    }

    public readonly struct KentridgeVerticalFrontageZone
    {
        public readonly string Id;
        public readonly KentridgeUrbanBand Band;
        public readonly DistrictKind District;
        public readonly Int2 StartDm;
        public readonly Int2 EndDm;
        public readonly Int2 ElevationSampleDm;
        public readonly int GapCentreDm;
        public readonly int GapWidthDm;
        public readonly int HeightDm;
        public readonly int DepthDm;
        public readonly int BayPitchDm;
        public readonly KentridgeVerticalFrontageStyle Style;

        public KentridgeVerticalFrontageZone(
            string id, KentridgeUrbanBand band, DistrictKind district,
            Int2 startDm, Int2 endDm, Int2 elevationSampleDm,
            int gapCentreDm, int gapWidthDm, int heightDm, int depthDm,
            int bayPitchDm, KentridgeVerticalFrontageStyle style)
        {
            Id = id;
            Band = band;
            District = district;
            StartDm = startDm;
            EndDm = endDm;
            ElevationSampleDm = elevationSampleDm;
            GapCentreDm = gapCentreDm;
            GapWidthDm = gapWidthDm;
            HeightDm = heightDm;
            DepthDm = depthDm;
            BayPitchDm = bayPitchDm;
            Style = style;
        }

        public int MinXDm => Math.Min(StartDm.X, EndDm.X);
        public int MaxXDm => Math.Max(StartDm.X, EndDm.X);
        public int LengthDm => MaxXDm - MinXDm;
    }

    public sealed class KentridgeVerticalFrontagePlan
    {
        public IReadOnlyList<KentridgeVerticalFrontageZone> Zones => _zones;
        private readonly List<KentridgeVerticalFrontageZone> _zones;
        public KentridgeVerticalFrontagePlan(List<KentridgeVerticalFrontageZone> zones) { _zones = zones; }
    }

    public static class KentridgeVerticalFrontagePlanner
    {
        public const int TopBelowShelfDm = 3;
        public const int FrontInsetDm = 0;
        // Shared semantic floor thickness. Gallery circulation uses the top of this floor as its
        // target elevation, so it belongs to the frontage contract rather than only the voxel compiler.
        public const int FloorThicknessDm = 5;

        public static KentridgeVerticalFrontagePlan Build(uint seed)
        {
            KentridgeUrbanMassingPlan massing = KentridgeUrbanOrganizer.Build(seed);
            var zones = new List<KentridgeVerticalFrontageZone>(6);

            for (int i = 0; i < massing.Blocks.Count; i++)
            {
                KentridgeUrbanBlock block = massing.Blocks[i];
                if (block.Band == KentridgeUrbanBand.LowerWard) continue;

                string runId = block.Id + "-" + block.CourtAccessEdge.ToString().ToLowerInvariant();
                KentridgeFrontageRun run = FindRun(massing, runId);
                if (!run.IsHorizontal || !run.HasGap)
                    throw new InvalidOperationException(
                        "Kentridge vertical frontage requires a horizontal court-access run: " + block.Id);

                ResolveStyle(block.Band, out KentridgeVerticalFrontageStyle style,
                    out int heightDm, out int depthDm, out int bayPitchDm);

                Int2 lowerStart = new Int2(block.MinDm.X, block.MaxDm.Y);
                Int2 lowerEnd = new Int2(block.MaxDm.X, block.MaxDm.Y);
                zones.Add(new KentridgeVerticalFrontageZone(
                    block.Id + "-vertical-frontage",
                    block.Band, block.District,
                    lowerStart, lowerEnd,
                    block.ElevationSampleDm,
                    run.GapCentreDm, run.GapWidthDm,
                    heightDm, depthDm, bayPitchDm, style));
            }

            Validate(zones);
            return new KentridgeVerticalFrontagePlan(zones);
        }

        private static KentridgeFrontageRun FindRun(KentridgeUrbanMassingPlan massing, string id)
        {
            for (int i = 0; i < massing.FrontageRuns.Count; i++)
                if (massing.FrontageRuns[i].Id == id) return massing.FrontageRuns[i];
            throw new InvalidOperationException("Missing Kentridge court-access frontage run: " + id);
        }

        private static void ResolveStyle(
            KentridgeUrbanBand band,
            out KentridgeVerticalFrontageStyle style,
            out int heightDm,
            out int depthDm,
            out int bayPitchDm)
        {
            switch (band)
            {
                case KentridgeUrbanBand.MarketBelt:
                    style = KentridgeVerticalFrontageStyle.MarketArcade;
                    heightDm = 38; depthDm = 18; bayPitchDm = 34; return;
                case KentridgeUrbanBand.UpperWard:
                    style = KentridgeVerticalFrontageStyle.UrbanUndercroft;
                    heightDm = 40; depthDm = 18; bayPitchDm = 32; return;
                case KentridgeUrbanBand.CivicCrown:
                    style = KentridgeVerticalFrontageStyle.CivicLoggia;
                    heightDm = 44; depthDm = 20; bayPitchDm = 36; return;
                case KentridgeUrbanBand.NobleRidge:
                    style = KentridgeVerticalFrontageStyle.NobleTerrace;
                    heightDm = 40; depthDm = 20; bayPitchDm = 38; return;
                default:
                    throw new InvalidOperationException(
                        "Unsupported Kentridge vertical frontage band: " + band);
            }
        }

        private static void Validate(List<KentridgeVerticalFrontageZone> zones)
        {
            if (zones.Count != 6)
                throw new InvalidOperationException("Kentridge vertical frontage count changed unexpectedly.");

            for (int i = 0; i < zones.Count; i++)
            {
                KentridgeVerticalFrontageZone zone = zones[i];
                if (zone.StartDm.Y != zone.EndDm.Y || zone.LengthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge vertical frontage must be a horizontal block edge: " + zone.Id);
                if (zone.GapWidthDm <= 0 || zone.GapWidthDm >= zone.LengthDm)
                    throw new InvalidOperationException(
                        "Kentridge vertical frontage must preserve its court gateway: " + zone.Id);
                if (zone.HeightDm <= FloorThicknessDm || zone.DepthDm <= 0 || zone.BayPitchDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge vertical frontage dimensions are invalid: " + zone.Id);
                if (zone.Band == KentridgeUrbanBand.LowerWard)
                    throw new InvalidOperationException(
                        "Lower Ward should remain landscape-led rather than fully retained.");
            }
        }
    }
}
