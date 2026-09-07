using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace MountingForce.WorldGen.Voxel
{
    public sealed class TopDownWorldRegionPlan
    {
        public TopDownWorldRegionSpec Spec { get; }
        public Int2 CentreDm { get; }
        public int HalfExtentXDm { get; }
        public int HalfExtentZDm { get; }
        public int ElevationDeltaDm { get; }

        public TopDownWorldRegionPlan(
            TopDownWorldRegionSpec spec,
            Int2 centreDm,
            int halfExtentXDm,
            int halfExtentZDm,
            int elevationDeltaDm)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            CentreDm = centreDm;
            HalfExtentXDm = halfExtentXDm;
            HalfExtentZDm = halfExtentZDm;
            ElevationDeltaDm = elevationDeltaDm;
        }

        public bool Contains(Int2 point, int marginDm = 0)
        {
            int halfX = Math.Max(0, HalfExtentXDm - marginDm);
            int halfZ = Math.Max(0, HalfExtentZDm - marginDm);
            return Math.Abs(point.X - CentreDm.X) <= halfX
                && Math.Abs(point.Y - CentreDm.Y) <= halfZ;
        }
    }

    public sealed class TopDownWorldBuildingBlockoutPlan
    {
        public Int2 CentreDm { get; }
        public int HalfExtentXDm { get; }
        public int HalfExtentZDm { get; }
        public int HeightDm { get; }

        public TopDownWorldBuildingBlockoutPlan(
            Int2 centreDm,
            int halfExtentXDm,
            int halfExtentZDm,
            int heightDm)
        {
            CentreDm = centreDm;
            HalfExtentXDm = halfExtentXDm;
            HalfExtentZDm = halfExtentZDm;
            HeightDm = heightDm;
        }

        public bool Overlaps(TopDownWorldBuildingBlockoutPlan other, int clearanceDm = 0)
        {
            if (other == null) return false;
            return Math.Abs(CentreDm.X - other.CentreDm.X)
                       < HalfExtentXDm + other.HalfExtentXDm + clearanceDm
                   && Math.Abs(CentreDm.Y - other.CentreDm.Y)
                       < HalfExtentZDm + other.HalfExtentZDm + clearanceDm;
        }
    }

    public sealed class TopDownWorldSettlementPlan
    {
        private readonly TopDownWorldBuildingBlockoutPlan[] _buildings;

        public TopDownWorldNodeSpec Node { get; }
        public Int2 CentreDm { get; }
        public TopDownWorldSettlementRealizationKind RealizationKind { get; }
        public IReadOnlyList<TopDownWorldBuildingBlockoutPlan> Buildings => _buildings;
        public Int2 ArrivalDm => CentreDm;
        public Int2 ExitDm => CentreDm;

        public TopDownWorldSettlementPlan(
            TopDownWorldNodeSpec node,
            Int2 centreDm,
            TopDownWorldSettlementRealizationKind realizationKind,
            IReadOnlyList<TopDownWorldBuildingBlockoutPlan> buildings)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            CentreDm = centreDm;
            RealizationKind = realizationKind;
            if (buildings == null) throw new ArgumentNullException(nameof(buildings));
            _buildings = new TopDownWorldBuildingBlockoutPlan[buildings.Count];
            for (var i = 0; i < buildings.Count; i++) _buildings[i] = buildings[i];
        }
    }

    public sealed class TopDownWorldPhysicalRoutePlan
    {
        private readonly Int2[] _tiles;
        private readonly string[] _constraintRelaxations;

        public TopDownWorldRouteSpec Route { get; }
        public IReadOnlyList<Int2> Tiles => _tiles;
        public IReadOnlyList<string> ConstraintRelaxations => _constraintRelaxations;
        public bool GeographyConstrained { get; }
        public int SolveSteps { get; }

        public TopDownWorldPhysicalRoutePlan(
            TopDownWorldRouteSpec route,
            IReadOnlyList<Int2> tiles,
            bool geographyConstrained,
            int solveSteps,
            IReadOnlyList<string> constraintRelaxations = null)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            _tiles = new Int2[tiles.Count];
            for (var i = 0; i < tiles.Count; i++) _tiles[i] = tiles[i];
            _constraintRelaxations = constraintRelaxations == null
                ? Array.Empty<string>()
                : Copy(constraintRelaxations);
            GeographyConstrained = geographyConstrained;
            SolveSteps = solveSteps;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var copy = new string[source.Count];
            for (var i = 0; i < source.Count; i++) copy[i] = source[i] ?? string.Empty;
            return copy;
        }
    }

    public sealed class TopDownWorldPhysicalPlan
    {
        private readonly TopDownWorldVoxelNodePlan[] _nodes;
        private readonly TopDownWorldRegionPlan[] _regions;
        private readonly TopDownWorldSettlementPlan[] _settlements;
        private readonly TopDownWorldPhysicalRoutePlan[] _routes;

        public IReadOnlyList<TopDownWorldVoxelNodePlan> Nodes => _nodes;
        public IReadOnlyList<TopDownWorldRegionPlan> Regions => _regions;
        public IReadOnlyList<TopDownWorldSettlementPlan> Settlements => _settlements;
        public IReadOnlyList<TopDownWorldPhysicalRoutePlan> Routes => _routes;
        public int RouteTileCount { get; }
        public int BuildingCount { get; }
        public int GeographyConstrainedRouteCount { get; }
        public int RouteSolveSteps { get; }
        public int ConstraintRelaxationCount { get; }

        public TopDownWorldPhysicalPlan(
            IReadOnlyList<TopDownWorldVoxelNodePlan> nodes,
            IReadOnlyList<TopDownWorldRegionPlan> regions,
            IReadOnlyList<TopDownWorldSettlementPlan> settlements,
            IReadOnlyList<TopDownWorldPhysicalRoutePlan> routes)
        {
            _nodes = Copy(nodes);
            _regions = Copy(regions);
            _settlements = Copy(settlements);
            _routes = Copy(routes);

            var tiles = 0;
            var buildings = 0;
            var constrained = 0;
            var solveSteps = 0;
            var relaxations = 0;
            for (var i = 0; i < _routes.Length; i++)
            {
                tiles += _routes[i].Tiles.Count;
                solveSteps += _routes[i].SolveSteps;
                relaxations += _routes[i].ConstraintRelaxations.Count;
                if (_routes[i].GeographyConstrained) constrained++;
            }
            for (var i = 0; i < _settlements.Length; i++)
                buildings += _settlements[i].Buildings.Count;
            RouteTileCount = tiles;
            BuildingCount = buildings;
            GeographyConstrainedRouteCount = constrained;
            RouteSolveSteps = solveSteps;
            ConstraintRelaxationCount = relaxations;
        }

        public bool TryGetRegion(string id, out TopDownWorldRegionPlan region)
        {
            for (var i = 0; i < _regions.Length; i++)
            {
                if (!string.Equals(_regions[i].Spec.Id, id, StringComparison.Ordinal)) continue;
                region = _regions[i];
                return true;
            }
            region = null;
            return false;
        }

        public bool TryGetSettlement(string nodeId, out TopDownWorldSettlementPlan settlement)
        {
            for (var i = 0; i < _settlements.Length; i++)
            {
                if (!string.Equals(_settlements[i].Node.Id, nodeId, StringComparison.Ordinal)) continue;
                settlement = _settlements[i];
                return true;
            }
            settlement = null;
            return false;
        }

        public bool TryGetRoute(string fromId, string toId, out TopDownWorldPhysicalRoutePlan route)
        {
            for (var i = 0; i < _routes.Length; i++)
            {
                if (!string.Equals(_routes[i].Route.FromId, fromId, StringComparison.Ordinal)
                    || !string.Equals(_routes[i].Route.ToId, toId, StringComparison.Ordinal)) continue;
                route = _routes[i];
                return true;
            }
            route = null;
            return false;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new T[source.Count];
            for (var i = 0; i < source.Count; i++) copy[i] = source[i];
            return copy;
        }
    }

    /// <summary>
    /// Deterministic macro physical planner. It resolves semantic regions against the authoritative
    /// graph, gives settlement nodes physical envelopes, and refuses to route verified roads
    /// through blocking geography unless an explicit semantic solution is authored.
    /// </summary>
    public static class TopDownWorldPhysicalPlanner
    {
        public const int RouteTileStepDm = 30;
        public const int GenericSettlementStreetHalfWidthDm = 45;
        public const int MaxRoadRiseVoxelsPerTile = 24;
        private const int BuildingHalfXDm = 68;
        private const int BuildingHalfZDm = 52;
        private const int BuildingOffsetDm = 190;
        private const int BuildingClearanceDm = 24;

        public static TopDownWorldPhysicalPlan Plan(
            TopDownWorldLayout layout,
            TopDownWorldPhysicalIntentSpec intent,
            Int2 rootCentreDm,
            int cellSizeDm,
            int voxelsPerDecimetre)
        {
            if (!TryPlan(
                    layout,
                    intent,
                    rootCentreDm,
                    cellSizeDm,
                    voxelsPerDecimetre,
                    out TopDownWorldPhysicalPlan plan,
                    out string error))
                throw new InvalidOperationException("Macro physical planning failed: " + error);
            return plan;
        }

        public static bool TryPlan(
            TopDownWorldLayout layout,
            TopDownWorldPhysicalIntentSpec intent,
            Int2 rootCentreDm,
            int cellSizeDm,
            int voxelsPerDecimetre,
            out TopDownWorldPhysicalPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (layout == null) { error = "layout is null"; return false; }
            if (intent == null) { error = "physical intent is null"; return false; }
            if (cellSizeDm < 1) { error = "cell size must be positive"; return false; }
            if (voxelsPerDecimetre < 1) { error = "voxel scale must be positive"; return false; }

            var nodes = new List<TopDownWorldVoxelNodePlan>(layout.Nodes.Count);
            var physicalById = new Dictionary<string, Int2>(StringComparer.Ordinal);
            var nodeById = new Dictionary<string, TopDownWorldNodeSpec>(StringComparer.Ordinal);
            for (var i = 0; i < layout.Nodes.Count; i++)
            {
                TopDownWorldNodePlacement placement = layout.Nodes[i];
                var centre = new Int2(
                    rootCentreDm.X + placement.Position.X * cellSizeDm,
                    rootCentreDm.Y + placement.Position.Y * cellSizeDm);
                if (!physicalById.TryAdd(placement.Node.Id, centre))
                {
                    error = "duplicate physical node id '" + placement.Node.Id + "'";
                    return false;
                }
                nodeById.Add(placement.Node.Id, placement.Node);
                nodes.Add(new TopDownWorldVoxelNodePlan(placement.Node, centre));
            }

            var regions = new List<TopDownWorldRegionPlan>(intent.Regions.Count);
            var regionById = new Dictionary<string, TopDownWorldRegionPlan>(StringComparer.Ordinal);
            for (var i = 0; i < intent.Regions.Count; i++)
            {
                TopDownWorldRegionSpec spec = intent.Regions[i];
                if (regionById.ContainsKey(spec.Id))
                {
                    error = "duplicate macro region id '" + spec.Id + "'";
                    return false;
                }
                if (!physicalById.TryGetValue(spec.PrimaryNodeId, out Int2 primary))
                {
                    error = "macro region '" + spec.Id + "' references unknown primary node '" + spec.PrimaryNodeId + "'";
                    return false;
                }

                Int2 secondary = primary;
                if (!string.IsNullOrEmpty(spec.SecondaryNodeId)
                    && !physicalById.TryGetValue(spec.SecondaryNodeId, out secondary))
                {
                    error = "macro region '" + spec.Id + "' references unknown secondary node '" + spec.SecondaryNodeId + "'";
                    return false;
                }

                Int2 centre = ResolveRegionCentre(layout.Seed, spec, primary, secondary);
                int extentX = Math.Max(1, spec.HalfExtentXDm + Variation(layout.Seed, spec.Id, 3, spec.VariationDm / 2));
                int extentZ = Math.Max(1, spec.HalfExtentZDm + Variation(layout.Seed, spec.Id, 4, spec.VariationDm / 2));
                int elevation = spec.ElevationDeltaDm + Variation(layout.Seed, spec.Id, 5, spec.VariationDm / 3);
                var resolved = new TopDownWorldRegionPlan(spec, centre, extentX, extentZ, elevation);
                regions.Add(resolved);
                regionById.Add(spec.Id, resolved);
            }

            if (!ValidateRegionRelationships(regions, regionById, out error)) return false;

            var settlementIntentById = new Dictionary<string, TopDownWorldSettlementPhysicalSpec>(StringComparer.Ordinal);
            for (var i = 0; i < intent.Settlements.Count; i++)
            {
                TopDownWorldSettlementPhysicalSpec spec = intent.Settlements[i];
                if (!settlementIntentById.TryAdd(spec.NodeId, spec))
                {
                    error = "duplicate settlement physical intent for '" + spec.NodeId + "'";
                    return false;
                }
                if (!nodeById.TryGetValue(spec.NodeId, out TopDownWorldNodeSpec node)
                    || node.Kind != TopDownWorldNodeKind.Settlement)
                {
                    error = "settlement physical intent references non-settlement node '" + spec.NodeId + "'";
                    return false;
                }
            }

            var settlements = new List<TopDownWorldSettlementPlan>();
            var settlementById = new Dictionary<string, TopDownWorldSettlementPlan>(StringComparer.Ordinal);
            for (var i = 0; i < layout.Nodes.Count; i++)
            {
                TopDownWorldNodeSpec node = layout.Nodes[i].Node;
                if (node.Kind != TopDownWorldNodeKind.Settlement) continue;
                if (!settlementIntentById.TryGetValue(node.Id, out TopDownWorldSettlementPhysicalSpec spec))
                {
                    error = "settlement node '" + node.Id + "' has no physical realization intent";
                    return false;
                }

                Int2 centre = physicalById[node.Id];
                IReadOnlyList<TopDownWorldBuildingBlockoutPlan> buildings =
                    spec.RealizationKind == TopDownWorldSettlementRealizationKind.GenericBlockout
                        ? BuildGenericSettlement(layout.Seed, node, centre, spec.MinimumBuildingCount)
                        : Array.Empty<TopDownWorldBuildingBlockoutPlan>();
                if (!ValidateBuildings(node, centre, buildings, out error)) return false;
                var settlement = new TopDownWorldSettlementPlan(node, centre, spec.RealizationKind, buildings);
                settlements.Add(settlement);
                settlementById.Add(node.Id, settlement);
            }

            var routeConstraintByKey = new Dictionary<string, List<TopDownWorldRouteRegionConstraintSpec>>(StringComparer.Ordinal);
            for (var i = 0; i < intent.RouteConstraints.Count; i++)
            {
                TopDownWorldRouteRegionConstraintSpec constraint = intent.RouteConstraints[i];
                if (!regionById.ContainsKey(constraint.RegionId))
                {
                    error = "route constraint references unknown region '" + constraint.RegionId + "'";
                    return false;
                }
                if (constraint.SolutionKind == TopDownWorldRouteRegionSolutionKind.DesignatedCrossing
                    && !regionById.ContainsKey(constraint.SolutionRegionId))
                {
                    error = "route constraint references unknown crossing/pass region '" + constraint.SolutionRegionId + "'";
                    return false;
                }
                bool routeExists = false;
                for (var r = 0; r < layout.Routes.Count; r++)
                {
                    if (string.Equals(layout.Routes[r].FromId, constraint.FromId, StringComparison.Ordinal)
                        && string.Equals(layout.Routes[r].ToId, constraint.ToId, StringComparison.Ordinal))
                    {
                        routeExists = true;
                        break;
                    }
                }
                if (!routeExists)
                {
                    error = "route constraint references unknown route '" + constraint.RouteKey + "'";
                    return false;
                }
                if (!routeConstraintByKey.TryGetValue(constraint.RouteKey, out List<TopDownWorldRouteRegionConstraintSpec> list))
                {
                    list = new List<TopDownWorldRouteRegionConstraintSpec>();
                    routeConstraintByKey.Add(constraint.RouteKey, list);
                }
                list.Add(constraint);
            }

            var routes = new List<TopDownWorldPhysicalRoutePlan>();
            for (var i = 0; i < layout.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = layout.Routes[i];
                if (!route.IsHard) continue;
                if (!physicalById.TryGetValue(route.FromId, out Int2 from)
                    || !physicalById.TryGetValue(route.ToId, out Int2 to))
                {
                    error = "hard route references an unrealized node: " + route.Key;
                    return false;
                }

                Int2 routeFrom = from;
                Int2 routeTo = to;
                if (settlementById.TryGetValue(route.FromId, out TopDownWorldSettlementPlan fromSettlement))
                    routeFrom = ResolveSettlementRouteGate(fromSettlement, to, route.CorridorWidthDm);
                if (settlementById.TryGetValue(route.ToId, out TopDownWorldSettlementPlan toSettlement))
                    routeTo = ResolveSettlementRouteGate(toSettlement, from, route.CorridorWidthDm);

                routeConstraintByKey.TryGetValue(route.Key, out List<TopDownWorldRouteRegionConstraintSpec> constraints);
                if (!TrySolveRoute(
                        layout.Seed,
                        route,
                        routeFrom,
                        routeTo,
                        regions,
                        regionById,
                        constraints,
                        voxelsPerDecimetre,
                        out TopDownWorldPhysicalRoutePlan solved,
                        out error))
                    return false;

                List<Int2> tiles = AttachSettlementApproaches(from, routeFrom, solved.Tiles, routeTo, to);
                int margin = route.CorridorWidthDm / 2;
                if (!ValidateBlockingRegions(route, tiles, regions, regionById, constraints, margin, out error))
                    return false;
                if (!ValidateSlope(route, tiles, layout.Seed, voxelsPerDecimetre, out error)) return false;

                routes.Add(new TopDownWorldPhysicalRoutePlan(
                    route,
                    tiles,
                    solved.GeographyConstrained,
                    solved.SolveSteps + Math.Max(0, tiles.Count - solved.Tiles.Count),
                    solved.ConstraintRelaxations));
            }

            plan = new TopDownWorldPhysicalPlan(nodes, regions, settlements, routes);
            return true;
        }

        private static Int2 ResolveRegionCentre(
            uint seed,
            TopDownWorldRegionSpec spec,
            Int2 primary,
            Int2 secondary)
        {
            int x = primary.X;
            int z = primary.Y;
            if (spec.Relation == TopDownWorldRegionRelationKind.Between
                || spec.Relation == TopDownWorldRegionRelationKind.Separates)
            {
                x = primary.X + (secondary.X - primary.X) / 2;
                z = primary.Y + (secondary.Y - primary.Y) / 2;
            }
            x += spec.OffsetXDm + Variation(seed, spec.Id, 1, spec.VariationDm);
            z += spec.OffsetZDm + Variation(seed, spec.Id, 2, spec.VariationDm);
            return new Int2(x, z);
        }

        private static bool ValidateRegionRelationships(
            IReadOnlyList<TopDownWorldRegionPlan> regions,
            IReadOnlyDictionary<string, TopDownWorldRegionPlan> byId,
            out string error)
        {
            error = string.Empty;
            for (var i = 0; i < regions.Count; i++)
            {
                TopDownWorldRegionPlan region = regions[i];
                if (region.Spec.Kind != TopDownWorldRegionKind.ValleyPass) continue;

                bool insideBarrier = false;
                foreach (KeyValuePair<string, TopDownWorldRegionPlan> pair in byId)
                {
                    TopDownWorldRegionPlan candidate = pair.Value;
                    if (candidate.Spec.Kind != TopDownWorldRegionKind.MountainRidge) continue;
                    if (candidate.Contains(region.CentreDm))
                    {
                        insideBarrier = true;
                        break;
                    }
                }
                if (!insideBarrier)
                {
                    error = "valley/pass region '" + region.Spec.Id + "' is not contained by a mountain/ridge barrier";
                    return false;
                }
            }
            return true;
        }

        private static IReadOnlyList<TopDownWorldBuildingBlockoutPlan> BuildGenericSettlement(
            uint seed,
            TopDownWorldNodeSpec node,
            Int2 centre,
            int count)
        {
            if (count != 4)
                throw new InvalidOperationException(
                    "The current generic macro blockout template is four plots; requested count=" + count +
                    " for '" + node.Id + "'.");

            var offsets = new[]
            {
                new Int2(-BuildingOffsetDm, -BuildingOffsetDm),
                new Int2(BuildingOffsetDm, -BuildingOffsetDm),
                new Int2(-BuildingOffsetDm, BuildingOffsetDm),
                new Int2(BuildingOffsetDm, BuildingOffsetDm)
            };
            var result = new TopDownWorldBuildingBlockoutPlan[count];
            for (var i = 0; i < count; i++)
            {
                int height = 55 + PositiveVariation(seed, node.Id, 20 + i, 16);
                result[i] = new TopDownWorldBuildingBlockoutPlan(
                    new Int2(centre.X + offsets[i].X, centre.Y + offsets[i].Y),
                    BuildingHalfXDm,
                    BuildingHalfZDm,
                    height);
            }
            return result;
        }

        private static bool ValidateBuildings(
            TopDownWorldNodeSpec node,
            Int2 centre,
            IReadOnlyList<TopDownWorldBuildingBlockoutPlan> buildings,
            out string error)
        {
            error = string.Empty;
            for (var i = 0; i < buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = buildings[i];
                if (Math.Abs(building.CentreDm.X - centre.X) + building.HalfExtentXDm
                        > node.EnvelopeHalfExtentDm
                    || Math.Abs(building.CentreDm.Y - centre.Y) + building.HalfExtentZDm
                        > node.EnvelopeHalfExtentDm)
                {
                    error = "generic settlement building escapes envelope for '" + node.Id + "'";
                    return false;
                }
                for (var j = 0; j < i; j++)
                {
                    if (!building.Overlaps(buildings[j], BuildingClearanceDm)) continue;
                    error = "generic settlement buildings overlap for '" + node.Id + "'";
                    return false;
                }
            }
            return true;
        }

        private static Int2 ResolveSettlementRouteGate(
            TopDownWorldSettlementPlan settlement,
            Int2 otherEndpoint,
            int corridorWidthDm)
        {
            if (settlement == null
                || settlement.RealizationKind != TopDownWorldSettlementRealizationKind.GenericBlockout
                || settlement.Buildings.Count == 0)
                return settlement?.CentreDm ?? otherEndpoint;

            Int2 centre = settlement.CentreDm;
            int deltaX = otherEndpoint.X - centre.X;
            int deltaZ = otherEndpoint.Y - centre.Y;
            bool alongX = Math.Abs(deltaX) >= Math.Abs(deltaZ);
            int direction = alongX ? Math.Sign(deltaX) : Math.Sign(deltaZ);
            if (direction == 0) direction = 1;

            int roadRadius = Math.Max(1, corridorWidthDm / 2);
            int distance = GenericSettlementStreetHalfWidthDm + roadRadius + 1;
            for (var i = 0; i < settlement.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[i];
                int candidate = alongX
                    ? Math.Abs(building.CentreDm.X - centre.X) + building.HalfExtentXDm
                    : Math.Abs(building.CentreDm.Y - centre.Y) + building.HalfExtentZDm;
                distance = Math.Max(distance, candidate + BuildingClearanceDm + roadRadius + 1);
            }

            return alongX
                ? new Int2(centre.X + direction * distance, centre.Y)
                : new Int2(centre.X, centre.Y + direction * distance);
        }

        private static List<Int2> AttachSettlementApproaches(
            Int2 from,
            Int2 routeFrom,
            IReadOnlyList<Int2> solved,
            Int2 routeTo,
            Int2 to)
        {
            var tiles = new List<Int2>();
            AppendPath(tiles, BuildWaypoints(new[] { from, routeFrom }));
            AppendPath(tiles, solved);
            AppendPath(tiles, BuildWaypoints(new[] { routeTo, to }));
            return tiles;
        }

        private static void AppendPath(List<Int2> destination, IReadOnlyList<Int2> source)
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (destination.Count > 0
                    && destination[destination.Count - 1].X == source[i].X
                    && destination[destination.Count - 1].Y == source[i].Y)
                    continue;
                destination.Add(source[i]);
            }
        }

        private static bool TrySolveRoute(
            uint seed,
            TopDownWorldRouteSpec route,
            Int2 from,
            Int2 to,
            IReadOnlyList<TopDownWorldRegionPlan> regions,
            IReadOnlyDictionary<string, TopDownWorldRegionPlan> regionById,
            IReadOnlyList<TopDownWorldRouteRegionConstraintSpec> constraints,
            int voxelsPerDecimetre,
            out TopDownWorldPhysicalRoutePlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            var direct = BuildManhattan(from, to, PreferXFirst(seed, route.Key));
            var blocking = new List<TopDownWorldRegionPlan>();
            var relaxations = new List<string>();
            int margin = route.CorridorWidthDm / 2;
            for (var i = 0; i < regions.Count; i++)
            {
                TopDownWorldRegionPlan region = regions[i];
                if (!region.Spec.BlocksUnsolvedHardRoutes) continue;
                if (Intersects(direct, region, margin)) blocking.Add(region);
            }

            var tiles = direct;
            var geographyConstrained = false;
            var solveSteps = direct.Count;
            for (var i = 0; i < blocking.Count; i++)
            {
                TopDownWorldRegionPlan blocker = blocking[i];
                TopDownWorldRouteRegionConstraintSpec solution = FindConstraint(constraints, blocker.Spec.Id);
                if (solution == null)
                {
                    error = "hard route '" + route.Key + "' is blocked by region '" + blocker.Spec.Id +
                            "' and has no authored crossing/pass/route-around solution";
                    return false;
                }

                geographyConstrained = true;
                switch (solution.SolutionKind)
                {
                    case TopDownWorldRouteRegionSolutionKind.GoAround:
                    {
                        bool fromInside = blocker.Contains(from, 0);
                        bool toInside = blocker.Contains(to, 0);
                        if ((fromInside || toInside)
                            && solution.RelaxationMode == TopDownWorldConstraintRelaxationMode.Strict)
                        {
                            string endpoint = fromInside && toInside ? "both endpoints" : fromInside ? "endpoint A" : "endpoint B";
                            error = "Route '" + route.Key + "' cannot satisfy GoAround('" + blocker.Spec.Id +
                                    "'): the blocking region contains " + endpoint + " and the constraint is Strict.";
                            return false;
                        }

                        bool allowEndpointEscape = solution.RelaxationMode == TopDownWorldConstraintRelaxationMode.EndpointEscape;
                        tiles = BuildAround(
                            from,
                            to,
                            blocker,
                            solution.ClearanceDm + margin,
                            seed,
                            route.Key,
                            allowEndpointEscape);
                        if (allowEndpointEscape && (fromInside || toInside))
                        {
                            string endpoint = fromInside && toInside ? "A+B" : fromInside ? "A" : "B";
                            relaxations.Add(
                                "route=" + route.Key +
                                "; region=" + blocker.Spec.Id +
                                "; constraint=GoAround" +
                                "; exact=endpoint-overlap" +
                                "; relaxation=EndpointEscape(" + endpoint + ")" +
                                "; requestedClearanceDm=" + solution.ClearanceDm +
                                "; result=solved");
                        }
                        break;
                    }
                    case TopDownWorldRouteRegionSolutionKind.PassThrough:
                        tiles = direct;
                        break;
                    case TopDownWorldRouteRegionSolutionKind.DesignatedCrossing:
                        TopDownWorldRegionPlan crossing = regionById[solution.SolutionRegionId];
                        tiles = BuildViaCrossing(from, to, crossing.CentreDm);
                        break;
                    default:
                        error = "unsupported geography solution for route '" + route.Key + "'";
                        return false;
                }
                solveSteps += tiles.Count;
            }

            if (!ValidateBlockingRegions(route, tiles, regions, regionById, constraints, margin, out error))
                return false;
            if (!ValidateSlope(route, tiles, seed, voxelsPerDecimetre, out error)) return false;

            plan = new TopDownWorldPhysicalRoutePlan(route, tiles, geographyConstrained, solveSteps, relaxations);
            return true;
        }

        private static TopDownWorldRouteRegionConstraintSpec FindConstraint(
            IReadOnlyList<TopDownWorldRouteRegionConstraintSpec> constraints,
            string regionId)
        {
            if (constraints == null) return null;
            for (var i = 0; i < constraints.Count; i++)
                if (string.Equals(constraints[i].RegionId, regionId, StringComparison.Ordinal))
                    return constraints[i];
            return null;
        }

        private static bool ValidateBlockingRegions(
            TopDownWorldRouteSpec route,
            IReadOnlyList<Int2> tiles,
            IReadOnlyList<TopDownWorldRegionPlan> regions,
            IReadOnlyDictionary<string, TopDownWorldRegionPlan> regionById,
            IReadOnlyList<TopDownWorldRouteRegionConstraintSpec> constraints,
            int margin,
            out string error)
        {
            error = string.Empty;
            for (var r = 0; r < regions.Count; r++)
            {
                TopDownWorldRegionPlan blocker = regions[r];
                if (!blocker.Spec.BlocksUnsolvedHardRoutes) continue;
                TopDownWorldRouteRegionConstraintSpec solution = FindConstraint(constraints, blocker.Spec.Id);
                bool endpointEscape = solution != null
                    && solution.SolutionKind == TopDownWorldRouteRegionSolutionKind.GoAround
                    && solution.RelaxationMode == TopDownWorldConstraintRelaxationMode.EndpointEscape;
                bool startInside = tiles.Count > 0 && blocker.Contains(tiles[0], -margin);
                bool endInside = tiles.Count > 0 && blocker.Contains(tiles[tiles.Count - 1], -margin);
                int firstOutside = 0;
                while (firstOutside < tiles.Count && blocker.Contains(tiles[firstOutside], -margin)) firstOutside++;
                int lastOutside = tiles.Count - 1;
                while (lastOutside >= 0 && blocker.Contains(tiles[lastOutside], -margin)) lastOutside--;

                if (endpointEscape && startInside && endInside && firstOutside > lastOutside)
                {
                    error = "hard route '" + route.Key + "' cannot use EndpointEscape for region '" +
                            blocker.Spec.Id + "' because the route never leaves the blocking region";
                    return false;
                }

                for (var i = 0; i < tiles.Count; i++)
                {
                    if (!blocker.Contains(tiles[i], -margin)) continue;
                    if (solution == null)
                    {
                        error = "hard route '" + route.Key + "' enters blocking region '" + blocker.Spec.Id +
                                "' without a semantic solution";
                        return false;
                    }
                    if (solution.SolutionKind == TopDownWorldRouteRegionSolutionKind.PassThrough) continue;
                    if (solution.SolutionKind == TopDownWorldRouteRegionSolutionKind.DesignatedCrossing)
                    {
                        TopDownWorldRegionPlan crossing = regionById[solution.SolutionRegionId];
                        if (crossing.Contains(tiles[i], -margin)) continue;
                    }
                    if (endpointEscape
                        && ((startInside && i < firstOutside) || (endInside && i > lastOutside)))
                        continue;
                    error = "hard route '" + route.Key + "' leaves its authored geography solution while inside '" +
                            blocker.Spec.Id + "'";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateSlope(
            TopDownWorldRouteSpec route,
            IReadOnlyList<Int2> tiles,
            uint seed,
            int scale,
            out string error)
        {
            error = string.Empty;
            if (tiles.Count == 0)
            {
                error = "hard route '" + route.Key + "' produced no physical tiles";
                return false;
            }
            int previous = TerrainSampler.HeightAt(tiles[0].X * scale, tiles[0].Y * scale, seed);
            for (var i = 1; i < tiles.Count; i++)
            {
                int current = TerrainSampler.HeightAt(tiles[i].X * scale, tiles[i].Y * scale, seed);
                if (Math.Abs(current - previous) > MaxRoadRiseVoxelsPerTile)
                {
                    error = "hard route '" + route.Key + "' exceeds the walkable rise budget between " +
                            tiles[i - 1] + " and " + tiles[i];
                    return false;
                }
                previous = current;
            }
            return true;
        }

        private static List<Int2> BuildViaCrossing(Int2 from, Int2 to, Int2 crossing)
        {
            var waypoints = new[]
            {
                from,
                new Int2(crossing.X, from.Y),
                new Int2(crossing.X, to.Y),
                to
            };
            return BuildWaypoints(waypoints);
        }

        private static List<Int2> BuildAround(
            Int2 from,
            Int2 to,
            TopDownWorldRegionPlan blocker,
            int clearanceDm,
            uint seed,
            string key,
            bool allowEndpointEscape)
        {
            bool fromInside = blocker.Contains(from, 0);
            bool toInside = blocker.Contains(to, 0);
            int left = blocker.CentreDm.X - blocker.HalfExtentXDm - clearanceDm;
            int right = blocker.CentreDm.X + blocker.HalfExtentXDm + clearanceDm;
            int bottom = blocker.CentreDm.Y - blocker.HalfExtentZDm - clearanceDm;
            int top = blocker.CentreDm.Y + blocker.HalfExtentZDm + clearanceDm;
            var candidates = new[]
            {
                new[] { from, new Int2(from.X, top), new Int2(to.X, top), to },
                new[] { from, new Int2(from.X, bottom), new Int2(to.X, bottom), to },
                new[] { from, new Int2(left, from.Y), new Int2(left, to.Y), to },
                new[] { from, new Int2(right, from.Y), new Int2(right, to.Y), to }
            };

            List<Int2> best = null;
            int bestLength = int.MaxValue;
            int rotation = PositiveVariation(seed, key, 71, candidates.Length);
            for (var pass = 0; pass < candidates.Length; pass++)
            {
                int index = (pass + rotation) % candidates.Length;
                List<Int2> candidate = BuildWaypoints(candidates[index]);
                if (IntersectsOutsideEndpointEscape(
                        candidate,
                        blocker,
                        allowEndpointEscape && fromInside,
                        allowEndpointEscape && toInside))
                    continue;
                int length = PathLength(candidate);
                if (length >= bestLength) continue;
                bestLength = length;
                best = candidate;
            }
            if (best == null)
                throw new InvalidOperationException("No dry detour could be built around region '" + blocker.Spec.Id + "'.");
            return best;
        }

        private static bool IntersectsOutsideEndpointEscape(
            IReadOnlyList<Int2> tiles,
            TopDownWorldRegionPlan blocker,
            bool allowStartEscape,
            bool allowEndEscape)
        {
            int firstOutside = 0;
            while (allowStartEscape
                   && firstOutside < tiles.Count
                   && blocker.Contains(tiles[firstOutside], 0))
                firstOutside++;

            int lastOutside = tiles.Count - 1;
            while (allowEndEscape
                   && lastOutside >= 0
                   && blocker.Contains(tiles[lastOutside], 0))
                lastOutside--;

            if ((allowStartEscape || allowEndEscape) && firstOutside > lastOutside)
                return true;

            for (var i = 0; i < tiles.Count; i++)
            {
                if (!blocker.Contains(tiles[i], 0)) continue;
                if (allowStartEscape && i < firstOutside) continue;
                if (allowEndEscape && i > lastOutside) continue;
                return true;
            }
            return false;
        }

        private static List<Int2> BuildManhattan(Int2 from, Int2 to, bool xFirst)
        {
            Int2 bend = xFirst ? new Int2(to.X, from.Y) : new Int2(from.X, to.Y);
            return BuildWaypoints(new[] { from, bend, to });
        }

        private static List<Int2> BuildWaypoints(IReadOnlyList<Int2> waypoints)
        {
            var tiles = new List<Int2>();
            if (waypoints.Count == 0) return tiles;
            tiles.Add(waypoints[0]);
            for (var i = 1; i < waypoints.Count; i++) AppendAxis(tiles, waypoints[i]);
            return tiles;
        }

        private static void AppendAxis(List<Int2> tiles, Int2 target)
        {
            Int2 current = tiles[tiles.Count - 1];
            if (current.X != target.X && current.Y != target.Y)
                throw new InvalidOperationException("Macro physical corridor segment must be axis-aligned.");
            while (current.X != target.X || current.Y != target.Y)
            {
                int dx = target.X - current.X;
                int dz = target.Y - current.Y;
                int stepX = dx == 0 ? 0 : Math.Sign(dx) * Math.Min(RouteTileStepDm, Math.Abs(dx));
                int stepZ = dz == 0 ? 0 : Math.Sign(dz) * Math.Min(RouteTileStepDm, Math.Abs(dz));
                current = new Int2(current.X + stepX, current.Y + stepZ);
                tiles.Add(current);
            }
        }

        private static bool Intersects(IReadOnlyList<Int2> tiles, TopDownWorldRegionPlan region, int marginDm)
        {
            for (var i = 0; i < tiles.Count; i++)
                if (region.Contains(tiles[i], -marginDm)) return true;
            return false;
        }

        private static int PathLength(IReadOnlyList<Int2> tiles)
        {
            var length = 0;
            for (var i = 1; i < tiles.Count; i++)
                length += Math.Abs(tiles[i].X - tiles[i - 1].X) + Math.Abs(tiles[i].Y - tiles[i - 1].Y);
            return length;
        }

        private static bool PreferXFirst(uint seed, string key) => (StableHash(seed, key, 61) & 1u) == 0u;

        private static int Variation(uint seed, string key, int salt, int range)
        {
            if (range <= 0) return 0;
            uint hash = StableHash(seed, key, salt);
            int width = range * 2 + 1;
            return (int)(hash % (uint)width) - range;
        }

        private static int PositiveVariation(uint seed, string key, int salt, int exclusiveMax)
        {
            if (exclusiveMax <= 1) return 0;
            return (int)(StableHash(seed, key, salt) % (uint)exclusiveMax);
        }

        private static uint StableHash(uint seed, string value, int salt)
        {
            uint hash = 2166136261u ^ seed ^ unchecked((uint)salt * 16777619u);
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return hash;
        }
    }
}