using System;

namespace MountingForce.WorldGen
{
    /// <summary>
    /// City-independent plot placement primitives for authored or generated street graphs.
    ///
    /// A settlement definition owns topology, role ids, districts, street coordinates and structure
    /// envelopes. These helpers own the geometric bookkeeping for putting a footprint against a
    /// horizontal/vertical street or centring it on a plaza. Keeping this in Core means every city
    /// can share the same deterministic frontage rules without depending on Kentridge content.
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
