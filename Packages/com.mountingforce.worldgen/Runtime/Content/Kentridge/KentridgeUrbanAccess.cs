using System;
using System.Collections.Generic;

namespace MountingForce.WorldGen.Content.Kentridge
{
    public enum KentridgeUrbanReturnSide : byte
    {
        West,
        East,
    }

    /// <summary>
    /// Semantic pedestrian route around one inhabited block face. The route joins three things that
    /// the massing plan previously described separately: the lower facade doors, the block's corner
    /// return, and the protected opening into the upper court.
    /// </summary>
    public readonly struct KentridgeUrbanAccessRoute
    {
        public readonly string Id;
        public readonly DistrictKind District;
        public readonly KentridgeUrbanBand Band;
        public readonly Int2 ElevationSampleDm;
        public readonly int SouthMinXDm;
        public readonly int SouthMaxXDm;
        public readonly int SouthZDm;
        public readonly KentridgeUrbanReturnSide ReturnSide;
        public readonly int ReturnXDm;
        public readonly int ReturnNorthZDm;
        public readonly int ReturnSouthZDm;
        public readonly int CourtCentreXDm;
        public readonly int CourtWidthDm;
        public readonly int DoorLevelBelowShelfDm;
        public readonly int StairLengthDm;
        public readonly int StairSteps;

        public KentridgeUrbanAccessRoute(
            string id,
            DistrictKind district,
            KentridgeUrbanBand band,
            Int2 elevationSampleDm,
            int southMinXDm,
            int southMaxXDm,
            int southZDm,
            KentridgeUrbanReturnSide returnSide,
            int returnXDm,
            int returnNorthZDm,
            int returnSouthZDm,
            int courtCentreXDm,
            int courtWidthDm,
            int doorLevelBelowShelfDm,
            int stairLengthDm,
            int stairSteps)
        {
            Id = id;
            District = district;
            Band = band;
            ElevationSampleDm = elevationSampleDm;
            SouthMinXDm = southMinXDm;
            SouthMaxXDm = southMaxXDm;
            SouthZDm = southZDm;
            ReturnSide = returnSide;
            ReturnXDm = returnXDm;
            ReturnNorthZDm = returnNorthZDm;
            ReturnSouthZDm = returnSouthZDm;
            CourtCentreXDm = courtCentreXDm;
            CourtWidthDm = courtWidthDm;
            DoorLevelBelowShelfDm = doorLevelBelowShelfDm;
            StairLengthDm = stairLengthDm;
            StairSteps = stairSteps;
        }

        public int SouthLengthDm => SouthMaxXDm - SouthMinXDm;
        public int ReturnLengthDm => ReturnSouthZDm - ReturnNorthZDm;
    }

    public sealed class KentridgeUrbanAccessPlan
    {
        public IReadOnlyList<KentridgeUrbanAccessRoute> Routes => _routes;
        private readonly List<KentridgeUrbanAccessRoute> _routes;

        public KentridgeUrbanAccessPlan(List<KentridgeUrbanAccessRoute> routes)
        {
            _routes = routes;
        }
    }

    /// <summary>
    /// Derives walkable block interfaces from Kentridge's urban massing contract. This layer does not
    /// choose stair materials or meshes; it only states that lower facade doors must share a contour
    /// walk and that each deliberate court gap must provide a real climb back to the shelf above.
    /// </summary>
    public static class KentridgeUrbanAccessPlanner
    {
        public const int ContourWalkWidthDm = 10;
        public const int TopLandingDepthDm = 14;

        public static KentridgeUrbanAccessPlan Build(uint seed)
        {
            KentridgeUrbanMassingPlan massing = KentridgeUrbanOrganizer.Build(seed);
            var routes = new List<KentridgeUrbanAccessRoute>(massing.Blocks.Count);
            int foundationDm = KentridgeDefinition.Theme.FoundationHeightDm;

            for (int i = 0; i < massing.Blocks.Count; i++)
            {
                KentridgeUrbanBlock block = massing.Blocks[i];
                if (block.CourtAccessEdge != KentridgeBlockEdge.South)
                    throw new InvalidOperationException(
                        "First Kentridge access pass expects south court access: " + block.Id);

                bool westReturn = (block.FrontageEdges & KentridgeBlockEdge.West) != 0;
                bool eastReturn = (block.FrontageEdges & KentridgeBlockEdge.East) != 0;
                if (westReturn == eastReturn)
                    throw new InvalidOperationException(
                        "Kentridge block must expose exactly one corner return: " + block.Id);

                int dropDm = block.EmbedBelowShelfDm - foundationDm;
                if (dropDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge facade door level must sit below its shelf: " + block.Id);

                int stairLengthDm = dropDm <= 15 ? 36 : 72;
                if (stairLengthDm + TopLandingDepthDm > block.DepthDm)
                    stairLengthDm = block.DepthDm - TopLandingDepthDm;
                if (stairLengthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge block cannot fit its court stair: " + block.Id);

                int stairSteps = Math.Max(5, (dropDm + 1) / 2);
                int courtCentreXDm = (block.MinDm.X + block.MaxDm.X) / 2;
                KentridgeUrbanReturnSide side = westReturn
                    ? KentridgeUrbanReturnSide.West
                    : KentridgeUrbanReturnSide.East;

                routes.Add(new KentridgeUrbanAccessRoute(
                    block.Id + "-access",
                    block.District,
                    block.Band,
                    block.ElevationSampleDm,
                    block.MinDm.X,
                    block.MaxDm.X,
                    block.MinDm.Y,
                    side,
                    westReturn ? block.MinDm.X : block.MaxDm.X,
                    block.MinDm.Y,
                    block.MaxDm.Y,
                    courtCentreXDm,
                    block.AccessWidthDm,
                    dropDm,
                    stairLengthDm,
                    stairSteps));
            }

            Validate(routes);
            return new KentridgeUrbanAccessPlan(routes);
        }

        private static void Validate(List<KentridgeUrbanAccessRoute> routes)
        {
            if (routes.Count != 8)
                throw new InvalidOperationException(
                    "Kentridge urban access must cover all eight authored blocks.");

            for (int i = 0; i < routes.Count; i++)
            {
                KentridgeUrbanAccessRoute route = routes[i];
                if (route.SouthLengthDm <= 0 || route.ReturnLengthDm <= 0)
                    throw new InvalidOperationException(
                        "Kentridge access route has invalid contour extent: " + route.Id);
                if (route.CourtWidthDm < 20)
                    throw new InvalidOperationException(
                        "Kentridge court access is too narrow to read as circulation: " + route.Id);
                if (route.DoorLevelBelowShelfDm <= 0 || route.StairLengthDm <= 0
                    || route.StairSteps <= 0)
                    throw new InvalidOperationException(
                        "Kentridge access route has invalid vertical circulation: " + route.Id);
                if (route.CourtCentreXDm - route.CourtWidthDm / 2 < route.SouthMinXDm
                    || route.CourtCentreXDm + route.CourtWidthDm / 2 > route.SouthMaxXDm)
                    throw new InvalidOperationException(
                        "Kentridge court stair escaped its block frontage: " + route.Id);
            }
        }
    }
}
