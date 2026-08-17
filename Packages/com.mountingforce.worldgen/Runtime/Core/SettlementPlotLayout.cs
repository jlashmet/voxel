using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen
{
    /// <summary>
    /// One deterministic anonymous-building slot along a one-dimensional street frontage.
    /// The settlement owns what the frontage means in world space; Core only owns subdivision.
    /// </summary>
    public readonly struct SettlementFrontageSite
    {
        public readonly int CentreAlongDm;
        public readonly int SegmentIndex;
        public readonly int SiteIndex;

        public SettlementFrontageSite(int centreAlongDm, int segmentIndex, int siteIndex)
        {
            CentreAlongDm = centreAlongDm;
            SegmentIndex = segmentIndex;
            SiteIndex = siteIndex;
        }
    }

    /// <summary>
    /// City-independent plot placement primitives for authored or generated street graphs.
    ///
    /// A settlement definition owns topology, role ids, districts, street coordinates and structure
    /// envelopes. These helpers own the geometric bookkeeping for putting a footprint against a
    /// horizontal/vertical street, centring it on a plaza, or subdividing a frontage run into
    /// anonymous-building sites. Keeping this in Core means every city can share the same
    /// deterministic frontage rules without depending on Kentridge content.
    /// </summary>
    public static class SettlementPlotLayout
    {
        public static BuildingPlot AlongHorizontalStreet(
            uint seed,
            uint salt,
            int roleId,
            StructureArchetype archetype,
            DistrictKind district,
            string streetId,
            int frontageXDm,
            int streetZDm,
            FrontageDirection frontage,
            int roadWidthDm,
            int setbackDm,
            int jitterDm,
            Int3 footprintDm)
        {
            ValidateFootprint(footprintDm);
            int along = frontageXDm + StableSignedJitter(seed, salt, jitterDm);
            int x = along - footprintDm.X / 2;
            int z;

            switch (frontage)
            {
                case FrontageDirection.South:
                    z = streetZDm + roadWidthDm / 2 + setbackDm;
                    break;
                case FrontageDirection.North:
                    z = streetZDm - roadWidthDm / 2 - setbackDm - footprintDm.Z;
                    break;
                default:
                    throw new ArgumentException(
                        "Horizontal street plots must face north or south.", nameof(frontage));
            }

            var access = new PlannedSiteAccess(
                SiteAccessKind.Street,
                streetId,
                new Int2(along, streetZDm));
            return new BuildingPlot(
                roleId,
                archetype,
                district,
                new Int2(x, z),
                frontage,
                access);
        }

        public static BuildingPlot AlongVerticalStreet(
            uint seed,
            uint salt,
            int roleId,
            StructureArchetype archetype,
            DistrictKind district,
            string streetId,
            int streetXDm,
            int frontageZDm,
            FrontageDirection frontage,
            int roadWidthDm,
            int setbackDm,
            int jitterDm,
            Int3 footprintDm)
        {
            ValidateFootprint(footprintDm);
            int along = frontageZDm + StableSignedJitter(seed, salt, jitterDm);
            int z = along - footprintDm.Z / 2;
            int x;

            switch (frontage)
            {
                case FrontageDirection.West:
                    x = streetXDm + roadWidthDm / 2 + setbackDm;
                    break;
                case FrontageDirection.East:
                    x = streetXDm - roadWidthDm / 2 - setbackDm - footprintDm.X;
                    break;
                default:
                    throw new ArgumentException(
                        "Vertical street plots must face east or west.", nameof(frontage));
            }

            var access = new PlannedSiteAccess(
                SiteAccessKind.Street,
                streetId,
                new Int2(streetXDm, along));
            return new BuildingPlot(
                roleId,
                archetype,
                district,
                new Int2(x, z),
                frontage,
                access);
        }

        public static BuildingPlot CentreOnPlaza(
            int roleId,
            StructureArchetype archetype,
            DistrictKind district,
            string plazaId,
            Int2 centreDm,
            Int3 footprintDm,
            FrontageDirection frontage = FrontageDirection.South)
        {
            ValidateFootprint(footprintDm);
            var access = new PlannedSiteAccess(
                SiteAccessKind.Plaza,
                plazaId,
                centreDm);
            return new BuildingPlot(
                roleId,
                archetype,
                district,
                new Int2(
                    centreDm.X - footprintDm.X / 2,
                    centreDm.Y - footprintDm.Z / 2),
                frontage,
                access);
        }

        /// <summary>
        /// Splits a one-dimensional frontage into deterministic anonymous-building sites. A gap can
        /// represent a plaza entrance, stair, lane, view corridor, or any other city-owned opening.
        /// Coverage and module pitch are explicit policy inputs so this function contains no city
        /// identity or density assumptions.
        /// </summary>
        public static SettlementFrontageSite[] PackFrontage(
            int startDm,
            int endDm,
            int coveragePercent,
            int modulePitchDm,
            bool hasGap = false,
            int gapCentreDm = 0,
            int gapWidthDm = 0)
        {
            if (coveragePercent < 0 || coveragePercent > 100)
                throw new ArgumentOutOfRangeException(nameof(coveragePercent));
            if (modulePitchDm <= 0)
                throw new ArgumentOutOfRangeException(nameof(modulePitchDm));
            if (gapWidthDm < 0)
                throw new ArgumentOutOfRangeException(nameof(gapWidthDm));

            int start = Math.Min(startDm, endDm);
            int end = Math.Max(startDm, endDm);
            if (end <= start || coveragePercent == 0)
                return Array.Empty<SettlementFrontageSite>();

            var segments = new List<(int Start, int End)>(2);
            if (!hasGap || gapWidthDm == 0)
            {
                segments.Add((start, end));
            }
            else
            {
                int gapStart = Math.Max(start, gapCentreDm - gapWidthDm / 2);
                int gapEnd = Math.Min(end, gapStart + gapWidthDm);
                if (gapStart > start) segments.Add((start, gapStart));
                if (gapEnd < end) segments.Add((gapEnd, end));
            }

            var sites = new List<SettlementFrontageSite>();
            int siteIndex = 0;
            for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
            {
                (int segmentStart, int segmentEnd) = segments[segmentIndex];
                int lengthDm = segmentEnd - segmentStart;
                if (lengthDm <= 0) continue;

                int targetOccupiedDm = lengthDm * coveragePercent / 100;
                if (targetOccupiedDm <= 0) continue;
                int count = Math.Max(1,
                    (targetOccupiedDm + modulePitchDm - 1) / modulePitchDm);

                for (int i = 0; i < count; i++)
                {
                    int centreAlongDm = segmentStart
                        + lengthDm * (2 * i + 1) / (2 * count);
                    sites.Add(new SettlementFrontageSite(
                        centreAlongDm,
                        segmentIndex,
                        siteIndex++));
                }
            }

            return sites.ToArray();
        }

        /// <summary>
        /// Stable symmetric jitter for small frontage variation. The result is always in
        /// [-magnitude, +magnitude] and is independent of call order.
        /// </summary>
        public static int StableSignedJitter(uint seed, uint salt, int magnitude)
        {
            if (magnitude <= 0) return 0;

            uint x = seed ^ (salt * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;

            int span = checked(magnitude * 2 + 1);
            return (int)(x % (uint)span) - magnitude;
        }

        private static void ValidateFootprint(Int3 footprintDm)
        {
            if (footprintDm.X <= 0 || footprintDm.Z <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(footprintDm),
                    "Settlement plot footprints must have positive horizontal dimensions.");
        }
    }
}
