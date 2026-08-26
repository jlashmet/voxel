using MountingForce.WorldGen.Content.Kentridge;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Shared authored height profile for the vertical Kentridge pass.
    ///
    /// The town owns its macro silhouette. Natural terrain supplies one stable reference altitude;
    /// the settlement then climbs through designed district shelves and, in the upper town, through
    /// an explicit procession of climb / landing / climb / gate / climb / crown. Every road, block,
    /// plot, access route, and structure reads this profile so those public rooms are actual geometry
    /// rather than labels placed on one uninterrupted slope.
    /// </summary>
    public static class KentridgeVerticalProfile
    {
        public const int LowerSouthOffsetDm = -25;
        public const int LowerMidOffsetDm = 0;
        public const int MarketOffsetDm = KentridgeProcessionalClimb.MarketOffsetDm;

        // Upper-town public-room elevations. UpperShoulder remains as a compatibility alias for
        // callers that predate the semantic UpperLanding node.
        public const int UpperLandingOffsetDm = KentridgeProcessionalClimb.UpperLandingOffsetDm;
        public const int UpperShoulderOffsetDm = UpperLandingOffsetDm;
        public const int CivicGateOffsetDm = KentridgeProcessionalClimb.CivicGateOffsetDm;
        public const int UpperCivicOffsetDm = KentridgeProcessionalClimb.UpperCivicOffsetDm;
        public const int SummitOffsetDm = KentridgeProcessionalClimb.SummitOffsetDm;
        public const int EastRidgeBoostDm = 15;

        // Z breakpoints are part of the authored urban section. The processional surface compiler
        // consumes the same values, preventing the visible road from smoothing straight through the
        // semantic landings defined here.
        public const int MarketRiseSouthZDm = KentridgeProcessionalClimb.MarketRiseSouthZDm;
        public const int UpperLandingSouthZDm = KentridgeProcessionalClimb.UpperLandingSouthZDm;
        public const int UpperLandingNorthZDm = KentridgeProcessionalClimb.UpperLandingNorthZDm;
        public const int CivicGateSouthZDm = KentridgeProcessionalClimb.CivicGateSouthZDm;
        public const int CivicGateNorthZDm = KentridgeProcessionalClimb.CivicGateNorthZDm;
        public const int UpperCivicSouthZDm = KentridgeProcessionalClimb.UpperCivicSouthZDm;
        public const int SummitSouthZDm = KentridgeProcessionalClimb.SummitSouthZDm;

        public static int ReferenceSurfaceY(uint seed, int scale)
        {
            return TerrainQuery.HeightAt(
                KentridgeDefinition.TownCentreDm.X * scale,
                KentridgeDefinition.TownCentreDm.Y * scale,
                seed);
        }

        public static int SurfaceYAtDm(int xDm, int zDm, uint seed, int scale)
        {
            return ReferenceSurfaceY(seed, scale) + SurfaceOffsetDm(xDm, zDm) * scale;
        }

        public static int SurfaceOffsetDm(int xDm, int zDm)
        {
            int offset;

            if (zDm >= 900)
                offset = LowerSouthOffsetDm;
            else if (zDm >= 760)
                offset = LerpDm(LowerSouthOffsetDm, LowerMidOffsetDm, 900 - zDm, 140);
            else if (zDm >= 620)
                offset = LerpDm(LowerMidOffsetDm, MarketOffsetDm, 760 - zDm, 140);
            else if (zDm >= MarketRiseSouthZDm)
                offset = MarketOffsetDm;
            else if (zDm >= UpperLandingSouthZDm)
                offset = LerpDm(
                    MarketOffsetDm,
                    UpperLandingOffsetDm,
                    MarketRiseSouthZDm - zDm,
                    MarketRiseSouthZDm - UpperLandingSouthZDm);
            else if (zDm >= UpperLandingNorthZDm)
                offset = UpperLandingOffsetDm;
            else if (zDm >= CivicGateSouthZDm)
                offset = LerpDm(
                    UpperLandingOffsetDm,
                    CivicGateOffsetDm,
                    UpperLandingNorthZDm - zDm,
                    UpperLandingNorthZDm - CivicGateSouthZDm);
            else if (zDm >= CivicGateNorthZDm)
                offset = CivicGateOffsetDm;
            else if (zDm >= UpperCivicSouthZDm)
                offset = LerpDm(
                    CivicGateOffsetDm,
                    UpperCivicOffsetDm,
                    CivicGateNorthZDm - zDm,
                    CivicGateNorthZDm - UpperCivicSouthZDm);
            else if (zDm >= SummitSouthZDm)
                offset = LerpDm(
                    UpperCivicOffsetDm,
                    SummitOffsetDm,
                    UpperCivicSouthZDm - zDm,
                    UpperCivicSouthZDm - SummitSouthZDm);
            else
                offset = SummitOffsetDm;

            // The noble estate climbs a secondary east-side ridge. Fade the extra rise in over a
            // full block instead of introducing a sudden contour step along the service lane.
            if (xDm >= 1420)
            {
                if (zDm <= 300)
                    offset += EastRidgeBoostDm;
                else if (zDm < 420)
                    offset += LerpDm(0, EastRidgeBoostDm, 420 - zDm, 120);
            }

            return offset;
        }

        /// <summary>
        /// Surface target for a plot is sampled at its public frontage, not its centre. This makes
        /// the prepared yard meet the street at the same authored height and leaves the existing
        /// frontage pass free to paint the connector without hiding a surprise vertical step.
        /// </summary>
        public static int PlotSurfaceY(SettlementPlan plan, BuildingPlot plot, uint seed, int scale)
        {
            Int2 frontage = FrontagePointDm(plan, plot);
            return SurfaceYAtDm(frontage.X, frontage.Y, seed, scale);
        }

        public static Int2 FrontagePointDm(SettlementPlan plan, BuildingPlot plot)
        {
            Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
            int centreX = plot.PositionDm.X + footprint.X / 2;
            int centreZ = plot.PositionDm.Y + footprint.Z / 2;

            switch (plot.Frontage)
            {
                case FrontageDirection.South:
                    return new Int2(centreX, plot.PositionDm.Y);
                case FrontageDirection.North:
                    return new Int2(centreX, plot.PositionDm.Y + footprint.Z);
                case FrontageDirection.West:
                    return new Int2(plot.PositionDm.X, centreZ);
                case FrontageDirection.East:
                    return new Int2(plot.PositionDm.X + footprint.X, centreZ);
                default:
                    return new Int2(centreX, centreZ);
            }
        }

        /// <summary>Lowest natural column under a plot, used only to size hidden terrace support.</summary>
        public static int NaturalLowestUnderPlot(SettlementPlan plan, BuildingPlot plot, uint seed, int scale)
        {
            Int3 footprintDm = SettlementFootprints.For(plan, plot.Archetype);
            int ox = plot.PositionDm.X * scale;
            int oz = plot.PositionDm.Y * scale;
            int width = footprintDm.X * scale;
            int depth = footprintDm.Z * scale;
            int sampleStep = math.max(8, 16 * scale);
            int lowest = int.MaxValue;

            for (int z = 0; z <= depth; z += sampleStep)
            for (int x = 0; x <= width; x += sampleStep)
            {
                int h = TerrainQuery.HeightAt(ox + x, oz + z, seed);
                if (h < lowest) lowest = h;
            }

            return lowest;
        }

        private static int LerpDm(int a, int b, int numerator, int denominator)
        {
            if (numerator <= 0) return a;
            if (numerator >= denominator) return b;
            return a + (b - a) * numerator / denominator;
        }
    }
}
