using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Backend adapters that translate already-resolved production geometry into the engine-free
    /// WorldBuilder reservation contract. These adapters never solve roads, choose ecology content,
    /// or become mutable world authority.
    /// </summary>
    public static class WorldRoadReservationAdapter
    {
        private const int RoadBelowSurfaceDm = 12;
        private const int RoadAboveSurfaceDm = 24;
        private const int RoadCorePrecedence = 70;
        private const int RoadClearancePrecedence = 60;

        public static SpatialReservation[] BuildClaims(
            WorldRoadNetwork network,
            ReservationBoundsDm window)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));

            var claims = new List<SpatialReservation>();
            for (int routeIndex = 0; routeIndex < network.Routes.Count; routeIndex++)
            {
                WorldRoadNetworkRoute route = network.Routes[routeIndex];
                IReadOnlyList<ResolvedWorldRoadPoint> points = route.Road.Points;
                for (int segment = 0; segment + 1 < points.Count; segment++)
                {
                    ResolvedWorldRoadPoint a = points[segment];
                    ResolvedWorldRoadPoint b = points[segment + 1];
                    int minY = Math.Min(a.Ydm, b.Ydm) - RoadBelowSurfaceDm;
                    int maxY = Math.Max(a.Ydm, b.Ydm) + RoadAboveSurfaceDm + 1;
                    var start = new Int2(a.Xdm, a.Zdm);
                    var end = new Int2(b.Xdm, b.Zdm);
                    string owner = "world-road:" + route.Id + ":" + segment;
                    string provenance = route.Road.Intent.Provenance + " | WorldRoadNetwork reservation adapter";

                    SpatialReservation core = SpatialReservation.Corridor(
                        owner + ":core",
                        ReservationCategory.Road,
                        ReservationSemantics.ProtectedCorridor | ReservationSemantics.CompatibleHandoff,
                        start,
                        end,
                        minY,
                        maxY,
                        Math.Max(1, route.Road.Intent.Profile.CoreRadiusDm),
                        RoadCorePrecedence,
                        ReservationConsumerKind.Road | ReservationConsumerKind.Connector,
                        provenance,
                        ordinal: 0);
                    if (core.Bounds.Intersects(window)) claims.Add(core);

                    int clearanceRadius = Math.Max(route.ClearanceRadiusDm, route.Road.Intent.Profile.CoreRadiusDm);
                    if (clearanceRadius > route.Road.Intent.Profile.CoreRadiusDm)
                    {
                        SpatialReservation clearance = SpatialReservation.Corridor(
                            owner + ":clearance",
                            ReservationCategory.Road,
                            ReservationSemantics.Clearance | ReservationSemantics.CompatibleHandoff,
                            start,
                            end,
                            minY,
                            maxY,
                            clearanceRadius,
                            RoadClearancePrecedence,
                            ReservationConsumerKind.Road | ReservationConsumerKind.Connector,
                            provenance,
                            ordinal: 1);
                        if (clearance.Bounds.Intersects(window)) claims.Add(clearance);
                    }
                }
            }

            claims.Sort((a, b) => a.Id.CompareTo(b.Id));
            return claims.ToArray();
        }
    }

    /// <summary>
    /// Composes Kentridge's settlement-owned claims with the exact resolved road-network claims.
    /// The pre-road-integration derived route claims are filtered so production consumers never see
    /// two competing road geometries.
    /// </summary>
    public static class KentridgeSpatialReservationAdapter
    {
        public static SpatialReservationSnapshot Build(
            uint seed,
            SettlementPlan plan,
            WorldRoadNetwork roads)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (roads == null) throw new ArgumentNullException(nameof(roads));
            if (plan.Seed != seed)
                throw new ArgumentException("Settlement plan seed does not match the reservation seed.", nameof(plan));

            SpatialReservationSnapshot settlement = KentridgeTownPlanner.BuildReservationSnapshot(seed);
            var claims = new List<SpatialReservation>(settlement.Reservations.Count + roads.Routes.Count * 8);
            for (int i = 0; i < settlement.Reservations.Count; i++)
            {
                SpatialReservation claim = settlement.Reservations[i];
                if ((claim.Category & ReservationCategory.Road) != 0) continue;
                claims.Add(claim);
            }

            SpatialReservation[] roadClaims = WorldRoadReservationAdapter.BuildClaims(roads, settlement.Window);
            for (int i = 0; i < roadClaims.Length; i++) claims.Add(roadClaims[i]);
            return SpatialReservationSnapshot.Create(claims, settlement.Window, settlement.BucketSizeDm);
        }

        public static SpatialReservation VegetationCandidate(
            in VegetationCandidate candidate,
            int baseYDm)
        {
            int radiusDm = Math.Max(2, candidate.TrunkRadiusUnits);
            int heightDm = Math.Max(8, candidate.HeightUnits);
            return SpatialReservation.Box(
                "kentridge-vegetation:" + candidate.Ordinal,
                ReservationCategory.Vegetation,
                ReservationSemantics.SoftYield,
                new ReservationBoundsDm(
                    candidate.X - radiusDm,
                    baseYDm,
                    candidate.Z - radiusDm,
                    candidate.X + radiusDm + 1,
                    baseYDm + heightDm,
                    candidate.Z + radiusDm + 1),
                precedence: 5,
                compatibleConsumers: ReservationConsumerKind.None,
                provenance: "KentridgeVegetationLayoutPlanner");
        }
    }
}
