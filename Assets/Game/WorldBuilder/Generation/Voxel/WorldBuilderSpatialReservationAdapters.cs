using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Architecture;
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
                            ordinal: 1,
                            yieldingConsumers: ReservationConsumerKind.Vegetation);
                        if (clearance.Bounds.Intersects(window)) claims.Add(clearance);
                    }
                }
            }

            claims.Sort((a, b) => a.Id.CompareTo(b.Id));
            return claims.ToArray();
        }
    }

    /// <summary>
    /// Adapts the source-backed macro layout envelopes and the already resolved road network. Node
    /// envelope dimensions remain owned by TopDownWorldLayout; this class only publishes them through
    /// the shared spatial-query contract. Region envelopes are parent handoffs rather than blanket
    /// exclusions, while settlement envelopes admit roads only through explicit compatible semantics.
    /// </summary>
    public static class TopDownWorldReservationAdapter
    {
        private const int DefaultMinYDm = -256;
        private const int DefaultMaxYDm = 1024;

        public static SpatialReservationSnapshot Build(
            TopDownWorldVoxelPlan plan,
            WorldRoadNetwork roads,
            int minYDm = DefaultMinYDm,
            int maxYDm = DefaultMaxYDm)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (roads == null) throw new ArgumentNullException(nameof(roads));
            if (maxYDm <= minYDm) throw new ArgumentOutOfRangeException(nameof(maxYDm));

            ReservationBoundsDm window = PlanningWindow(plan, minYDm, maxYDm);
            var claims = new List<SpatialReservation>(plan.Nodes.Count + roads.Routes.Count * 8);
            for (int i = 0; i < plan.Nodes.Count; i++)
                claims.Add(NodeEnvelope(plan.Nodes[i], minYDm, maxYDm));

            SpatialReservation[] roadClaims = WorldRoadReservationAdapter.BuildClaims(roads, window);
            for (int i = 0; i < roadClaims.Length; i++) claims.Add(roadClaims[i]);
            AddArrivalHandoffs(plan, roads, claims, minYDm, maxYDm);
            return SpatialReservationSnapshot.Create(claims, window);
        }

        public static void ValidateRoadHandoffs(
            TopDownWorldVoxelPlan plan,
            WorldRoadNetwork roads,
            int minYDm = DefaultMinYDm,
            int maxYDm = DefaultMaxYDm)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (roads == null) throw new ArgumentNullException(nameof(roads));
            ReservationBoundsDm window = PlanningWindow(plan, minYDm, maxYDm);
            var nodeClaims = new List<SpatialReservation>(plan.Nodes.Count);
            for (int i = 0; i < plan.Nodes.Count; i++)
                nodeClaims.Add(NodeEnvelope(plan.Nodes[i], minYDm, maxYDm));
            SpatialReservationSnapshot nodes = SpatialReservationSnapshot.Create(nodeClaims, window);
            SpatialReservation[] roadClaims = WorldRoadReservationAdapter.BuildClaims(roads, window);
            for (int i = 0; i < roadClaims.Length; i++)
            {
                SpatialReservation road = roadClaims[i];
                if ((road.Semantics & ReservationSemantics.ProtectedCorridor) == 0) continue;
                ReservationQueryResult result = nodes.Query(
                    road,
                    ReservationConsumerKind.Road,
                    ReservationCategory.SettlementEnvelope | ReservationCategory.Geographic);
                if (!result.IsAccepted)
                    throw new InvalidOperationException(
                        "Resolved macro road violates a published node envelope: " + result.Describe());
            }
        }

        private static SpatialReservation NodeEnvelope(
            TopDownWorldVoxelNodePlan node,
            int minYDm,
            int maxYDm)
        {
            int half = node.Node.EnvelopeHalfExtentDm;
            var bounds = new ReservationBoundsDm(
                node.CentreDm.X - half,
                minYDm,
                node.CentreDm.Y - half,
                node.CentreDm.X + half + 1,
                maxYDm,
                node.CentreDm.Y + half + 1);
            string owner = "macro-node:" + node.Node.Id;
            string provenance = node.Node.Source + " | TopDownWorldLayout envelope";
            if (node.Node.Kind == TopDownWorldNodeKind.Settlement)
                return WorldBuilderReservationFactory.SettlementEnvelope(
                    owner, bounds, precedence: 50, provenance: provenance);

            ReservationConsumerKind compatible =
                ReservationConsumerKind.SettlementBuilding
                | ReservationConsumerKind.Road
                | ReservationConsumerKind.StructuralChild
                | ReservationConsumerKind.Vegetation
                | ReservationConsumerKind.Underground
                | ReservationConsumerKind.Landmark;
            return SpatialReservation.Box(
                owner,
                ReservationCategory.Geographic,
                ReservationSemantics.HardOccupancy | ReservationSemantics.CompatibleHandoff,
                bounds,
                precedence: 20,
                compatibleConsumers: compatible,
                provenance: provenance);
        }

        private static ReservationBoundsDm PlanningWindow(
            TopDownWorldVoxelPlan plan,
            int minYDm,
            int maxYDm)
        {
            if (plan.Nodes.Count == 0)
                throw new InvalidOperationException("Macro reservation planning requires at least one node.");
            int minX = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxZ = int.MinValue;
            for (int i = 0; i < plan.Nodes.Count; i++)
            {
                TopDownWorldVoxelNodePlan node = plan.Nodes[i];
                int half = node.Node.EnvelopeHalfExtentDm;
                minX = Math.Min(minX, node.CentreDm.X - half);
                minZ = Math.Min(minZ, node.CentreDm.Y - half);
                maxX = Math.Max(maxX, node.CentreDm.X + half + 1);
                maxZ = Math.Max(maxZ, node.CentreDm.Y + half + 1);
            }
            return new ReservationBoundsDm(minX, minYDm, minZ, maxX, maxYDm, maxZ);
        }

        private static void AddArrivalHandoffs(
            TopDownWorldVoxelPlan plan,
            WorldRoadNetwork roads,
            List<SpatialReservation> claims,
            int minYDm,
            int maxYDm)
        {
            var settlementIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Nodes.Count; i++)
                if (plan.Nodes[i].Node.Kind == TopDownWorldNodeKind.Settlement)
                    settlementIds.Add(plan.Nodes[i].Node.Id);

            for (int routeIndex = 0; routeIndex < roads.Routes.Count; routeIndex++)
            {
                WorldRoadNetworkRoute route = roads.Routes[routeIndex];
                bool fromSettlement = settlementIds.Contains(route.Road.Intent.FromId);
                bool toSettlement = settlementIds.Contains(route.Road.Intent.ToId);
                if (!fromSettlement && !toSettlement) continue;
                IReadOnlyList<ResolvedWorldRoadPoint> points = route.Road.Points;
                if (points.Count < 2) continue;
                int aIndex = fromSettlement ? 0 : points.Count - 2;
                int bIndex = fromSettlement ? 1 : points.Count - 1;
                ResolvedWorldRoadPoint a = points[aIndex];
                ResolvedWorldRoadPoint b = points[bIndex];
                claims.Add(WorldBuilderReservationFactory.PublicAccessCorridor(
                    "macro-arrival:" + route.Id,
                    new Int2(a.Xdm, a.Zdm),
                    new Int2(b.Xdm, b.Zdm),
                    Math.Max(minYDm, Math.Min(a.Ydm, b.Ydm) - 12),
                    Math.Min(maxYDm, Math.Max(a.Ydm, b.Ydm) + 25),
                    Math.Max(1, route.ClearanceRadiusDm),
                    precedence: 80,
                    provenance: route.Road.Intent.Provenance + " | settlement arrival handoff"));
            }
        }
    }

    /// <summary>
    /// Architecture keeps ownership of piece selection, orientation, support and attachment policy.
    /// This adapter publishes only the already-resolved site/child clearance volume through the
    /// common spatial service.
    /// </summary>
    public static class StructureSiteReservationAdapter
    {
        public static SpatialReservation SiteClearance(
            string ownerId,
            in StructureSiteGeometry geometry,
            int minYDm,
            int maxYDm,
            int horizontalClearanceDm = 0,
            ReservationConsumerKind compatibleConsumers = ReservationConsumerKind.Connector,
            string provenance = "StructureSiteGeometry")
        {
            if (maxYDm <= minYDm) throw new ArgumentOutOfRangeException(nameof(maxYDm));
            if (horizontalClearanceDm < 0) throw new ArgumentOutOfRangeException(nameof(horizontalClearanceDm));
            var bounds = new ReservationBoundsDm(
                geometry.FootprintMinDm.X,
                minYDm,
                geometry.FootprintMinDm.Y,
                geometry.FootprintMaxDm.X,
                maxYDm,
                geometry.FootprintMaxDm.Y);
            if (horizontalClearanceDm > 0) bounds = bounds.ExpandHorizontal(horizontalClearanceDm);
            return WorldBuilderReservationFactory.StructuralChildClearance(
                ownerId,
                bounds,
                precedence: 40,
                compatibleConsumers: compatibleConsumers,
                provenance: provenance);
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
            int radiusDm = Math.Max(2, Math.Min(8, candidate.HeightUnits / 8));
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
