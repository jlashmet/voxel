using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace MountingForce.WorldGen.Voxel
{
    internal sealed class WorldRoadVoxelTerrain : IWorldRoadTerrain
    {
        private readonly uint _seed;
        private readonly int _scale;

        public WorldRoadVoxelTerrain(uint seed, int scale)
        {
            if (scale < 1) throw new ArgumentOutOfRangeException(nameof(scale));
            _seed = seed;
            _scale = scale;
        }

        public int HeightAtDm(int xdm, int zdm)
        {
            int voxelHeight = TerrainSampler.HeightAt(xdm * _scale, zdm * _scale, _seed);
            return DivideRounded(voxelHeight, _scale);
        }

        public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm)
        {
            // TerrainQuery is currently the canonical production terrain authority available to
            // WorldBuilder and exposes height/slope only; it has no water/reservation/barrier field
            // to translate here. The road API still models those flags explicitly so a world owner
            // that introduces such authority can route through it rather than inventing crossings.
            return WorldRoadTerrainFlags.None;
        }

        private static int DivideRounded(int value, int divisor)
        {
            if (value >= 0) return (value + divisor / 2) / divisor;
            return -((-value + divisor / 2) / divisor);
        }
    }

    /// <summary>
    /// Builds the shared road network used by Kentridge physical circulation and runtime spatial
    /// consumers. The physically realized entrance is prepended as a semantic control point when
    /// architecture moved a doorway along its facade, so gameplay access, vegetation clearance and
    /// voxel surfaces all query the same resolved spine.
    /// </summary>
    public static class KentridgeWorldRoadNetwork
    {
        public static WorldRoadNetwork Build(
            SettlementPlan plan,
            uint seed,
            VoxelWorldGenSettings settings)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var terrain = new WorldRoadVoxelTerrain(seed, settings.VoxelsPerDecimetre);
            var routes = new List<WorldRoadNetworkRoute>(plan.Routes.Count + plan.Streets.Count);

            for (int i = 0; i < plan.Routes.Count; i++)
                routes.Add(BuildRoute(plan, plan.Routes[i], seed, terrain));

            // Compatibility only. Modern Kentridge emits zero legacy streets, but authored legacy
            // plans still lower through the same network instead of the old axis-only road backend.
            for (int i = 0; i < plan.Streets.Count; i++)
                routes.Add(BuildStreet(plan.Streets[i], seed, terrain));

            return new WorldRoadNetwork(routes);
        }

        private static WorldRoadNetworkRoute BuildRoute(
            SettlementPlan plan,
            PlannedRoute route,
            uint seed,
            IWorldRoadTerrain terrain)
        {
            var controls = new List<WorldRoadPlanPoint>(route.Points.Count + 1);
            if (TryEntrance(plan, route.Id, out Int2 entrance))
                controls.Add(new WorldRoadPlanPoint(entrance.X, entrance.Y));
            for (int p = 0; p < route.Points.Count; p++)
            {
                Int2 point = route.Points[p];
                if (controls.Count > 0)
                {
                    WorldRoadPlanPoint previous = controls[controls.Count - 1];
                    if (previous.Xdm == point.X && previous.Zdm == point.Y) continue;
                }
                controls.Add(new WorldRoadPlanPoint(point.X, point.Y));
            }

            WorldRoadSemanticClass semanticClass = route.WidthDm >= 26
                ? WorldRoadSemanticClass.Vehicle
                : WorldRoadSemanticClass.Pedestrian;
            int transition = Math.Max(8, route.WidthDm / 2);
            var profile = new WorldRoadProfile(
                "kentridge-" + (semanticClass == WorldRoadSemanticClass.Vehicle ? "road-" : "footpath-") + route.WidthDm,
                "road-surface",
                route.WidthDm,
                transition,
                maximumGradePermille: 420,
                maximumCutFillDm: 42,
                edgeVariationDm: 2,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: semanticClass == WorldRoadSemanticClass.Vehicle ? 900 : 1000,
                crossingPolicy: WorldRoadCrossingPolicy.AllowPass);
            var intent = new WorldRoadIntent(
                route.Id,
                route.Id + ":site",
                route.Id + ":network",
                StableSeed(seed, route.Id),
                profile,
                "Kentridge SettlementPlan.Routes",
                controls);
            ResolvedWorldRoad resolved = WorldRoadResolver.Resolve(
                intent, terrain, sampleSpacingDm: Math.Max(8, route.WidthDm / 2), searchMarginCells: 0);
            EnsureResolved(resolved);
            return new WorldRoadNetworkRoute(
                resolved,
                semanticClass,
                shoulderWidthDm: semanticClass == WorldRoadSemanticClass.Vehicle ? 6 : 4,
                clearanceWidthDm: 10,
                markingPolicy: WorldRoadMarkingPolicy.None,
                crosswalkPolicy: WorldRoadCrosswalkPolicy.None);
        }

        private static WorldRoadNetworkRoute BuildStreet(
            PlannedStreet street,
            uint seed,
            IWorldRoadTerrain terrain)
        {
            var controls = new WorldRoadPlanPoint[street.Points.Count];
            for (int p = 0; p < street.Points.Count; p++)
                controls[p] = new WorldRoadPlanPoint(street.Points[p].X, street.Points[p].Y);
            var profile = new WorldRoadProfile(
                "kentridge-legacy-" + street.Kind.ToString().ToLowerInvariant(),
                "road-surface",
                street.WidthDm,
                Math.Max(10, street.WidthDm / 2),
                maximumGradePermille: 320,
                maximumCutFillDm: 42,
                edgeVariationDm: 2,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: 900,
                crossingPolicy: WorldRoadCrossingPolicy.AllowPass);
            var intent = new WorldRoadIntent(
                street.Id,
                street.Id + ":from",
                street.Id + ":to",
                StableSeed(seed, street.Id),
                profile,
                "Kentridge SettlementPlan.Streets compatibility adapter",
                controls);
            ResolvedWorldRoad resolved = WorldRoadResolver.Resolve(
                intent, terrain, sampleSpacingDm: Math.Max(10, street.WidthDm / 2), searchMarginCells: 0);
            EnsureResolved(resolved);
            return new WorldRoadNetworkRoute(
                resolved,
                WorldRoadSemanticClass.Vehicle,
                shoulderWidthDm: 6,
                clearanceWidthDm: 10);
        }

        private static bool TryEntrance(SettlementPlan plan, string routeId, out Int2 entrance)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Access.Kind != SiteAccessKind.Route
                    || !string.Equals(plot.Access.TargetId, routeId, StringComparison.Ordinal))
                    continue;
                if (!KentridgeGameplaySiteAccessResolver.TryResolve(plan, plot.RoleId, 1, out KentridgeGameplaySiteAccess access))
                    break;
                entrance = new Int2(access.Entrance.Position.X, access.Entrance.Position.Z);
                return true;
            }
            entrance = default;
            return false;
        }

        private static void EnsureResolved(ResolvedWorldRoad road)
        {
            if (road.IsResolved) return;
            throw new InvalidOperationException(
                "Kentridge road '" + road.Intent.Id + "' could not be resolved: "
                + road.Status + " " + road.FailureReason);
        }

        private static uint StableSeed(uint seed, string id)
        {
            unchecked
            {
                uint hash = seed ^ 2166136261u;
                for (int i = 0; i < id.Length; i++) hash = (hash ^ id[i]) * 16777619u;
                return hash == 0 ? 1u : hash;
            }
        }
    }

    public static class TopDownWorldRoadNetwork
    {
        public static WorldRoadNetwork Build(
            TopDownWorldLayout layout,
            Int2 rootCentreDm,
            int cellSizeDm,
            VoxelWorldGenSettings settings)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (cellSizeDm < 1) throw new ArgumentOutOfRangeException(nameof(cellSizeDm));

            var positions = new Dictionary<string, Int2>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                TopDownWorldNodePlacement node = layout.Nodes[i];
                positions.Add(node.Node.Id, new Int2(
                    rootCentreDm.X + node.Position.X * cellSizeDm,
                    rootCentreDm.Y + node.Position.Y * cellSizeDm));
            }

            var terrain = new WorldRoadVoxelTerrain(layout.Seed, settings.VoxelsPerDecimetre);
            var routes = new List<WorldRoadNetworkRoute>();
            for (int i = 0; i < layout.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = layout.Routes[i];
                if (!route.IsHard) continue;
                if (!positions.TryGetValue(route.FromId, out Int2 from)
                    || !positions.TryGetValue(route.ToId, out Int2 to))
                    throw new InvalidOperationException("Macro route references an unrealized node: " + route.Key);

                WorldRoadProfile profile = route.RoadProfile ?? CompatibilityProfile(route);
                var controls = new[]
                {
                    new WorldRoadPlanPoint(from.X, from.Y),
                    new WorldRoadPlanPoint(to.X, to.Y),
                };
                string id = "macro:" + route.Key;
                var intent = new WorldRoadIntent(
                    id,
                    route.FromId,
                    route.ToId,
                    StableSeed(layout.Seed, id),
                    profile,
                    route.Evidence + " | " + route.PlacementEvidence,
                    controls);
                ResolvedWorldRoad resolved = WorldRoadResolver.Resolve(
                    intent,
                    terrain,
                    sampleSpacingDm: Math.Max(20, profile.CarriagewayWidthDm),
                    searchMarginCells: 8);
                if (!resolved.IsResolved)
                    throw new InvalidOperationException(
                        "Macro road '" + route.Key + "' could not be resolved: "
                        + resolved.Status + " " + resolved.FailureReason);
                routes.Add(new WorldRoadNetworkRoute(
                    resolved,
                    WorldRoadSemanticClass.Vehicle,
                    shoulderWidthDm: Math.Max(5, profile.CarriagewayWidthDm / 6),
                    clearanceWidthDm: Math.Max(10, profile.CarriagewayWidthDm / 3),
                    markingPolicy: WorldRoadMarkingPolicy.None,
                    crosswalkPolicy: WorldRoadCrosswalkPolicy.None));
            }
            return new WorldRoadNetwork(routes);
        }

        private static WorldRoadProfile CompatibilityProfile(TopDownWorldRouteSpec route)
        {
            return new WorldRoadProfile(
                "macro-dirt-" + route.CorridorWidthDm,
                "road-surface",
                route.CorridorWidthDm,
                transitionWidthDm: Math.Max(18, route.CorridorWidthDm / 2),
                maximumGradePermille: 160,
                maximumCutFillDm: 36,
                edgeVariationDm: 4,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: 820,
                crossingPolicy: WorldRoadCrossingPolicy.AllowPass | WorldRoadCrossingPolicy.AllowWaterCrossing);
        }

        private static uint StableSeed(uint seed, string id)
        {
            unchecked
            {
                uint hash = seed ^ 2166136261u;
                for (int i = 0; i < id.Length; i++) hash = (hash ^ id[i]) * 16777619u;
                return hash == 0 ? 1u : hash;
            }
        }
    }

    /// <summary>
    /// Lowers resolved WorldBuilder roads into the engine's generic terrain-corridor primitive.
    /// Each bounded physical piece contains one analytic segment and one explicit placement, so
    /// arbitrary headings and slopes do not require square stamps, band stacks, GameObjects or a
    /// world-sized feature footprint. Terrain grading, shoulder material coverage and persisted
    /// surface detail are all evaluated from the same corridor influence in the rasterizer.
    /// </summary>
    public static class WorldRoadNetworkVoxelCatalogue
    {
        private const int SurfaceThicknessDm = 4;
        private const int ClearAboveDm = 24;
        private const int MaxAltitudeVoxels = TerrainSampler.MaxHeight;

        private readonly struct CorridorPiece
        {
            public readonly WorldRoadNetworkRoute Route;
            public readonly ResolvedWorldRoadPoint A;
            public readonly ResolvedWorldRoadPoint B;
            public readonly int SegmentIndex;
            public readonly int PieceIndex;

            public CorridorPiece(WorldRoadNetworkRoute route,
                ResolvedWorldRoadPoint a,
                ResolvedWorldRoadPoint b,
                int segmentIndex,
                int pieceIndex)
            {
                Route = route;
                A = a;
                B = b;
                SegmentIndex = segmentIndex;
                PieceIndex = pieceIndex;
            }
        }

        public static FeatureCatalogue Build(
            WorldRoadNetwork network,
            VoxelWorldGenSettings settings,
            Allocator allocator,
            int precedence = 20)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (network.Routes.Count == 0)
                return FeatureCatalogueBuilder.Allocate(0, 0, 0, 0, 0, 0, 0, 0, 0, allocator);

            int scale = settings.VoxelsPerDecimetre;
            if (scale < 1) throw new ArgumentOutOfRangeException(nameof(settings));
            List<CorridorPiece> pieces = BuildPieces(network, scale);
            if (pieces.Count > FeatureBudget.MaxDefinitions)
                throw new InvalidOperationException(
                    "Shared road lowering requires " + pieces.Count + " bounded corridor definitions, exceeding "
                    + FeatureBudget.MaxDefinitions + ". Resolve/simplify the road graph instead of widening the global budget.");

            int programLengthPerPiece = ShapeOps.InstructionLength(ShapeOp.EmitTerrainCorridor)
                + ShapeOps.InstructionLength(ShapeOp.End);
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: pieces.Count,
                rules: pieces.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: pieces.Count * programLengthPerPiece,
                materials: 0,
                explicitPlacements: pieces.Count,
                overrides: 0,
                allocator);

            try
            {
                byte roadMaterial = settings.Materials.Resolve(MaterialRole.RoadSurface);
                int programOffset = 0;
                for (int i = 0; i < pieces.Count; i++)
                {
                    CorridorPiece piece = pieces[i];
                    WorldRoadProfile profile = piece.Route.Road.Intent.Profile;
                    int core = profile.CoreRadiusDm * scale;
                    int edgeVariation = profile.EdgeVariationDm * scale;
                    int maximumOuter = (profile.CoreRadiusDm
                        + profile.TransitionWidthDm
                        + profile.EdgeVariationDm) * scale;
                    int maximumCutFill = profile.MaximumCutFillDm * scale;
                    int fillDepth = SurfaceThicknessDm * scale;
                    int clearAbove = ClearAboveDm * scale;

                    int3 worldA = new(piece.A.Xdm * scale, piece.A.Ydm * scale, piece.A.Zdm * scale);
                    int3 worldB = new(piece.B.Xdm * scale, piece.B.Ydm * scale, piece.B.Zdm * scale);
                    int3 origin = new(
                        Math.Min(worldA.x, worldB.x) - maximumOuter,
                        Math.Min(worldA.y, worldB.y) - maximumCutFill - fillDepth,
                        Math.Min(worldA.z, worldB.z) - maximumOuter);
                    int3 maximum = new(
                        Math.Max(worldA.x, worldB.x) + maximumOuter,
                        Math.Max(worldA.y, worldB.y) + maximumCutFill + clearAbove,
                        Math.Max(worldA.z, worldB.z) + maximumOuter);
                    int3 footprint = maximum - origin + 1;
                    if (math.cmax(footprint) > FeatureBudget.MaxFootprintVoxels)
                        throw new InvalidOperationException(
                            "Bounded road piece '" + piece.Route.Id + "' exceeds the "
                            + FeatureBudget.MaxFootprintVoxels + "-voxel feature footprint budget.");

                    int3 localA = worldA - origin;
                    int3 localB = worldB - origin;
                    int[] program = RoadProgram(
                        localA,
                        localB,
                        core,
                        maximumOuter,
                        maximumCutFill,
                        fillDepth,
                        clearAbove,
                        edgeVariation,
                        roadMaterial,
                        piece.Route.Road.Intent.Seed,
                        scale);
                    for (int p = 0; p < program.Length; p++)
                        catalogue.Program[programOffset + p] = program[p];

                    catalogue.Definitions[i] = new FeatureDefinition
                    {
                        Name = new FixedString64Bytes(FeatureName(
                            piece.Route.Id, piece.SegmentIndex, piece.PieceIndex)),
                        Kind = FeatureKind.Landform,
                        BasePlane = BasePlaneRule.FixedAltitude,
                        FixedAltitude = 0,
                        Footprint = footprint,
                        MaxSlope = 32,
                        Precedence = precedence,
                        ParameterOffset = 0,
                        ParameterCount = 0,
                        AnchorOffset = 0,
                        AnchorCount = 0,
                        SlotOffset = 0,
                        SlotCount = 0,
                        ProgramOffset = programOffset,
                        ProgramLength = program.Length,
                        MaterialOffset = 0,
                        MaterialCount = 0,
                        MaxPrimitives = 1,
                    };
                    catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                    {
                        Position = origin,
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                    catalogue.Rules[i] = new PlacementRule
                    {
                        DefinitionId = i,
                        CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                        AttemptsPerCell = 0,
                        AcceptProbability = 0,
                        MinAltitude = 0,
                        MaxAltitude = MaxAltitudeVoxels,
                        MaxSlope = 32,
                        MinSpacing = 0,
                        ClusterMin = 0,
                        ClusterMax = 0,
                        ExclusionMask = 0,
                        ExplicitOffset = i,
                        ExplicitCount = 1,
                    };
                    programOffset += program.Length;
                }

                CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (result != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException("Shared road catalogue failed validation: " + result);
                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static List<CorridorPiece> BuildPieces(WorldRoadNetwork network, int scale)
        {
            var result = new List<CorridorPiece>();
            for (int routeIndex = 0; routeIndex < network.Routes.Count; routeIndex++)
            {
                WorldRoadNetworkRoute route = network.Routes[routeIndex];
                WorldRoadProfile profile = route.Road.Intent.Profile;
                int maximumOuter = (profile.CoreRadiusDm
                    + profile.TransitionWidthDm
                    + profile.EdgeVariationDm) * scale;
                int horizontalBudget = FeatureBudget.MaxFootprintVoxels - maximumOuter * 2 - 1;
                int verticalFootprint = (profile.MaximumCutFillDm * 2
                    + SurfaceThicknessDm + ClearAboveDm) * scale + 1;
                if (horizontalBudget < 1 || verticalFootprint > FeatureBudget.MaxFootprintVoxels)
                    throw new InvalidOperationException(
                        "Road profile '" + profile.Id + "' cannot fit the bounded terrain-corridor budget.");

                for (int segment = 0; segment + 1 < route.Road.Points.Count; segment++)
                {
                    ResolvedWorldRoadPoint a = route.Road.Points[segment];
                    ResolvedWorldRoadPoint b = route.Road.Points[segment + 1];
                    int extentVoxels = Math.Max(
                        Math.Abs(b.Xdm - a.Xdm),
                        Math.Abs(b.Zdm - a.Zdm)) * scale;
                    int count = Math.Max(1, (extentVoxels + horizontalBudget - 1) / horizontalBudget);
                    ResolvedWorldRoadPoint from = a;
                    for (int piece = 0; piece < count; piece++)
                    {
                        ResolvedWorldRoadPoint to = piece + 1 == count
                            ? b
                            : Lerp(a, b, piece + 1, count);
                        if (!from.Equals(to))
                            result.Add(new CorridorPiece(route, from, to, segment, piece));
                        from = to;
                    }
                }
            }
            return result;
        }

        private static ResolvedWorldRoadPoint Lerp(
            ResolvedWorldRoadPoint a,
            ResolvedWorldRoadPoint b,
            int numerator,
            int denominator)
        {
            return new ResolvedWorldRoadPoint(
                a.Xdm + DivideRounded((long)(b.Xdm - a.Xdm) * numerator, denominator),
                a.Ydm + DivideRounded((long)(b.Ydm - a.Ydm) * numerator, denominator),
                a.Zdm + DivideRounded((long)(b.Zdm - a.Zdm) * numerator, denominator));
        }

        private static int[] RoadProgram(
            int3 a,
            int3 b,
            int coreRadius,
            int maximumOuterRadius,
            int maximumCutFill,
            int fillDepth,
            int clearAbove,
            int edgeVariation,
            byte material,
            uint seed,
            int scale)
        {
            var code = new List<int>(20);
            Emit(code, ShapeOp.EmitTerrainCorridor,
                a.x, a.y, a.z,
                b.x, b.y, b.z,
                coreRadius,
                maximumOuterRadius,
                maximumCutFill,
                fillDepth,
                clearAbove,
                edgeVariation,
                material,
                unchecked((int)seed),
                scale);
            Emit(code, ShapeOp.End);
            return code.ToArray();
        }

        private static string FeatureName(string id, int segmentIndex, int pieceIndex)
        {
            string suffix = "-s" + segmentIndex + "p" + pieceIndex;
            string prefix = "world-road-" + id;
            int prefixLimit = Math.Max(
                0,
                FixedString64Bytes.UTF8MaxLengthInBytes - suffix.Length);
            if (prefix.Length > prefixLimit) prefix = prefix.Substring(0, prefixLimit);
            return prefix + suffix;
        }

        private static int DivideRounded(long numerator, long denominator)
        {
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            if (numerator >= 0) return (int)((numerator + denominator / 2) / denominator);
            return (int)(-((-numerator + denominator / 2) / denominator));
        }

        private static void Emit(List<int> code, ShapeOp op, params int[] operands)
        {
            code.Add((int)op);
            code.Add(0);
            code.AddRange(operands);
        }
    }
}
