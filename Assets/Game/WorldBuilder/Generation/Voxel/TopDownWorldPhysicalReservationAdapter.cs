using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Publishes an already-resolved macro physical plan through the shared WorldBuilder spatial
    /// reservation substrate. Geography and route solving remain owned by TopDownWorldPhysicalPlanner;
    /// this adapter owns only occupancy/clearance/handoff claims and conflict validation.
    /// </summary>
    public static class TopDownWorldPhysicalReservationAdapter
    {
        private const int MinYDm = -256;
        private const int MaxYDm = 1024;
        private const int GenericBuildingClearanceDm = 24;

        public static SpatialReservationSnapshot Build(TopDownWorldPhysicalPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            ReservationBoundsDm window = PlanningWindow(plan);
            var claims = new List<SpatialReservation>(
                plan.Settlements.Count + plan.BuildingCount * 2 + plan.RouteTileCount * 2);

            for (int i = 0; i < plan.Settlements.Count; i++)
            {
                TopDownWorldSettlementPlan settlement = plan.Settlements[i];
                int half = settlement.Node.EnvelopeHalfExtentDm;
                claims.Add(WorldBuilderReservationFactory.SettlementEnvelope(
                    "macro-physical-settlement:" + settlement.Node.Id,
                    new ReservationBoundsDm(
                        settlement.CentreDm.X - half,
                        MinYDm,
                        settlement.CentreDm.Y - half,
                        settlement.CentreDm.X + half + 1,
                        MaxYDm,
                        settlement.CentreDm.Y + half + 1),
                    provenance: settlement.Node.Source + " | TopDownWorldPhysicalPlan"));

                for (int buildingIndex = 0; buildingIndex < settlement.Buildings.Count; buildingIndex++)
                {
                    TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[buildingIndex];
                    string owner = BuildingOwner(settlement, buildingIndex);
                    claims.Add(BuildingFootprint(owner, building));
                    claims.Add(BuildingClearance(owner, building));
                }
            }

            var settlementIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Settlements.Count; i++)
                settlementIds.Add(plan.Settlements[i].Node.Id);

            for (int routeIndex = 0; routeIndex < plan.Routes.Count; routeIndex++)
            {
                TopDownWorldPhysicalRoutePlan route = plan.Routes[routeIndex];
                for (int segment = 0; segment + 1 < route.Tiles.Count; segment++)
                    claims.Add(RoadClaim(route, segment));

                if (route.Tiles.Count < 2) continue;
                int radiusDm = Math.Max(1, route.Route.CorridorWidthDm / 2);
                if (settlementIds.Contains(route.Route.FromId))
                {
                    claims.Add(WorldBuilderReservationFactory.PublicAccessCorridor(
                        "macro-physical-arrival:" + route.Route.Key + ":from",
                        route.Tiles[0],
                        route.Tiles[1],
                        MinYDm,
                        MaxYDm,
                        radiusDm,
                        provenance: route.Route.PlacementEvidence + " | resolved settlement arrival"));
                }
                if (settlementIds.Contains(route.Route.ToId))
                {
                    int last = route.Tiles.Count - 1;
                    claims.Add(WorldBuilderReservationFactory.PublicAccessCorridor(
                        "macro-physical-arrival:" + route.Route.Key + ":to",
                        route.Tiles[last - 1],
                        route.Tiles[last],
                        MinYDm,
                        MaxYDm,
                        radiusDm,
                        provenance: route.Route.PlacementEvidence + " | resolved settlement arrival"));
                }
            }

            return SpatialReservationSnapshot.Create(claims, window);
        }

        public static SpatialReservationSnapshot Validate(TopDownWorldPhysicalPlan plan)
        {
            SpatialReservationSnapshot snapshot = Build(plan);

            var acceptedBuildingClearances = new PlannerLocalReservationSet(
                snapshot.Window,
                snapshot.BucketSizeDm);
            for (int settlementIndex = 0; settlementIndex < plan.Settlements.Count; settlementIndex++)
            {
                TopDownWorldSettlementPlan settlement = plan.Settlements[settlementIndex];
                for (int buildingIndex = 0; buildingIndex < settlement.Buildings.Count; buildingIndex++)
                {
                    TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[buildingIndex];
                    string owner = BuildingOwner(settlement, buildingIndex);
                    SpatialReservation clearance = BuildingClearance(owner, building);
                    ReservationQueryResult buildingResult = acceptedBuildingClearances.TryAdd(
                        clearance,
                        ReservationConsumerKind.SettlementBuilding,
                        ReservationCategory.Building);
                    RequireAccepted("building clearance", owner, buildingResult);

                    SpatialReservation footprint = BuildingFootprint(owner, building);
                    ReservationQueryResult accessResult = snapshot.Query(
                        footprint,
                        ReservationConsumerKind.SettlementBuilding,
                        ReservationCategory.Road | ReservationCategory.PublicAccess);
                    RequireAccepted("building/access", owner, accessResult);
                }
            }

            for (int routeIndex = 0; routeIndex < plan.Routes.Count; routeIndex++)
            {
                TopDownWorldPhysicalRoutePlan route = plan.Routes[routeIndex];
                for (int segment = 0; segment + 1 < route.Tiles.Count; segment++)
                {
                    SpatialReservation road = RoadClaim(route, segment);
                    ReservationQueryResult roadResult = snapshot.Query(
                        road,
                        ReservationConsumerKind.Road,
                        ReservationCategory.Building | ReservationCategory.SettlementEnvelope);
                    RequireAccepted("road/building", road.OwnerId, roadResult);
                }
            }

            return snapshot;
        }

        private static SpatialReservation BuildingFootprint(
            string owner,
            TopDownWorldBuildingBlockoutPlan building)
        {
            return WorldBuilderReservationFactory.BuildingFootprint(
                owner,
                new Int2(
                    building.CentreDm.X - building.HalfExtentXDm,
                    building.CentreDm.Y - building.HalfExtentZDm),
                new Int3(
                    building.HalfExtentXDm * 2,
                    Math.Max(1, building.HeightDm),
                    building.HalfExtentZDm * 2),
                provenance: "TopDownWorldPhysicalPlan generic blockout");
        }

        private static SpatialReservation BuildingClearance(
            string owner,
            TopDownWorldBuildingBlockoutPlan building)
        {
            return WorldBuilderReservationFactory.BuildingClearance(
                owner,
                new Int2(
                    building.CentreDm.X - building.HalfExtentXDm,
                    building.CentreDm.Y - building.HalfExtentZDm),
                new Int3(
                    building.HalfExtentXDm * 2,
                    Math.Max(1, building.HeightDm),
                    building.HalfExtentZDm * 2),
                GenericBuildingClearanceDm,
                provenance: "TopDownWorldPhysicalPlan generic blockout");
        }

        private static SpatialReservation RoadClaim(
            TopDownWorldPhysicalRoutePlan route,
            int segment)
        {
            return WorldBuilderReservationFactory.RoadCorridor(
                "macro-physical-road:" + route.Route.Key + ":" + segment,
                route.Tiles[segment],
                route.Tiles[segment + 1],
                MinYDm,
                MaxYDm,
                Math.Max(1, route.Route.CorridorWidthDm / 2),
                provenance: route.Route.PlacementEvidence + " | TopDownWorldPhysicalPlan resolved route");
        }

        private static string BuildingOwner(TopDownWorldSettlementPlan settlement, int buildingIndex) =>
            "macro-physical-building:" + settlement.Node.Id + ":" + buildingIndex;

        private static ReservationBoundsDm PlanningWindow(TopDownWorldPhysicalPlan plan)
        {
            bool any = false;
            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;

            for (int i = 0; i < plan.Nodes.Count; i++)
            {
                TopDownWorldVoxelNodePlan node = plan.Nodes[i];
                int half = node.Node.EnvelopeHalfExtentDm;
                Include(ref any, ref minX, ref minZ, ref maxX, ref maxZ,
                    node.CentreDm.X - half,
                    node.CentreDm.Y - half,
                    node.CentreDm.X + half + 1,
                    node.CentreDm.Y + half + 1);
            }
            for (int i = 0; i < plan.Regions.Count; i++)
            {
                TopDownWorldRegionPlan region = plan.Regions[i];
                Include(ref any, ref minX, ref minZ, ref maxX, ref maxZ,
                    region.CentreDm.X - region.HalfExtentXDm,
                    region.CentreDm.Y - region.HalfExtentZDm,
                    region.CentreDm.X + region.HalfExtentXDm + 1,
                    region.CentreDm.Y + region.HalfExtentZDm + 1);
            }
            for (int routeIndex = 0; routeIndex < plan.Routes.Count; routeIndex++)
            {
                TopDownWorldPhysicalRoutePlan route = plan.Routes[routeIndex];
                int radius = Math.Max(1, route.Route.CorridorWidthDm / 2);
                for (int tile = 0; tile < route.Tiles.Count; tile++)
                {
                    Int2 point = route.Tiles[tile];
                    Include(ref any, ref minX, ref minZ, ref maxX, ref maxZ,
                        point.X - radius,
                        point.Y - radius,
                        point.X + radius + 1,
                        point.Y + radius + 1);
                }
            }
            if (!any)
                throw new InvalidOperationException("Macro physical reservation planning requires spatial content.");

            return new ReservationBoundsDm(minX, MinYDm, minZ, maxX, MaxYDm, maxZ);
        }

        private static void Include(
            ref bool any,
            ref int minX,
            ref int minZ,
            ref int maxX,
            ref int maxZ,
            int candidateMinX,
            int candidateMinZ,
            int candidateMaxX,
            int candidateMaxZ)
        {
            any = true;
            minX = Math.Min(minX, candidateMinX);
            minZ = Math.Min(minZ, candidateMinZ);
            maxX = Math.Max(maxX, candidateMaxX);
            maxZ = Math.Max(maxZ, candidateMaxZ);
        }

        private static void RequireAccepted(
            string relationship,
            string owner,
            ReservationQueryResult result)
        {
            if (result.IsAccepted) return;
            throw new InvalidOperationException(
                "Macro physical spatial reservation conflict (" + relationship + ") for '" + owner + "': " +
                result.Describe());
        }
    }
}
