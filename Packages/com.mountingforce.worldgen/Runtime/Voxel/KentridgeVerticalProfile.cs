using MountingForce.WorldGen.Content.Kentridge;
using Unity.Mathematics;
using VoxelEngine.Core.Terrain;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Shared authored height profile for the vertical Kentridge pass.
    ///
    /// The important distinction from ordinary terrain adaptation is that the town now owns its
    /// macro silhouette. Natural terrain supplies one stable reference altitude; the settlement
    /// then climbs through intentionally designed elevation bands as the player travels north:
    /// low residential streets, a raised market terrace, an upper civic shoulder, and a summit
    /// around the church. The east side receives a small additional ridge so Radcliffe's estate
    /// does not sit on exactly the same contour as the civic buildings.
    ///
    /// All offsets are integer decimetres. Every voxel catalogue that needs a Kentridge surface
    /// reads this class, which prevents roads, plots, props, and structures from inventing slightly
    /// different versions of the town's vertical layout.
    /// </summary>
    public static class KentridgeVerticalProfile
    {
        public const int LowerSouthOffsetDm = -25;
        public const int LowerMidOffsetDm = 0;
        public const int MarketOffsetDm = 35;
        public const int UpperShoulderOffsetDm = 85;
        public const int UpperCivicOffsetDm = 130;
        public const int SummitOffsetDm = 145;
        public const int EastRidgeBoostDm = 15;

        public static int ReferenceSurfaceY(uint seed, int scale)
        {
            return TerrainSampler.HeightAt(
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

            // Broad horizontal shelves are joined by authored ascent zones. Keeping a real plateau
            // around each major street lets the town read as stacked districts rather than one
            // continuous procedural hill. The civic climb is deliberately continuous all the way
            // into the church shelf: earlier versions jumped at z=160, which made frontage heights
            // disagree with the piecewise road ramps even though both consumed this same profile.
            if (zDm >= 900)
                offset = LowerSouthOffsetDm;
            else if (zDm >= 760)
                offset = LerpDm(LowerSouthOffsetDm, LowerMidOffsetDm, 900 - zDm, 140);
            else if (zDm >= 620)
                offset = LerpDm(LowerMidOffsetDm, MarketOffsetDm, 760 - zDm, 140);
            else if (zDm >= 440)
                offset = MarketOffsetDm;
            else if (zDm >= 300)
                offset = LerpDm(MarketOffsetDm, UpperShoulderOffsetDm, 440 - zDm, 140);
            else if (zDm >= 150)
                offset = LerpDm(UpperShoulderOffsetDm, SummitOffsetDm, 300 - zDm, 150);
            else
                offset = SummitOffsetDm;

            // The noble estate climbs a secondary east-side ridge. Fade the extra rise in over a
            // full block instead of introducing a sudden 1.5 m contour step at z=360; the mansion
            // still receives the full boost while the service lane remains continuously walkable.
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
        public static int PlotSurfaceY(BuildingPlot plot, uint seed, int scale)
        {
            Int2 frontage = FrontagePointDm(plot);
            return SurfaceYAtDm(frontage.X, frontage.Y, seed, scale);
        }

        public static Int2 FrontagePointDm(BuildingPlot plot)
        {
            Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
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
        public static int NaturalLowestUnderPlot(BuildingPlot plot, uint seed, int scale)
        {
            Int3 footprintDm = KentridgeDefinition.FootprintDm(plot.Archetype);
            int ox = plot.PositionDm.X * scale;
            int oz = plot.PositionDm.Y * scale;
            int width = footprintDm.X * scale;
            int depth = footprintDm.Z * scale;
            int sampleStep = math.max(8, 16 * scale);
            int lowest = int.MaxValue;

            for (int z = 0; z <= depth; z += sampleStep)
            for (int x = 0; x <= width; x += sampleStep)
            {
                int h = TerrainSampler.HeightAt(ox + x, oz + z, seed);
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
