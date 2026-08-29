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

        public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm) => WorldRoadTerrainFlags.None;

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
    /// Shared bounded voxel lowering for all resolved WorldBuilder roads. Overlapping route-local
    /// stamps are emitted at no more than half carriageway width, which keeps bends/intersections
    /// continuous without arbitrary-angle transforms or per-segment GameObjects. The outer graded
    /// footprint is natural ground material, the inner core is road material, and every placement
    /// height comes from the same WorldRoadInfluence queried by spatial consumers.
    /// </summary>
    public static class WorldRoadNetworkVoxelCatalogue
    {
        private const int SurfaceThicknessDm = 4;
        private const int ClearAboveDm = 24;
        private const int MaxAltitudeVoxels = 4096;

        private readonly struct Stamp
        {
            public readonly int Xdm;
            public readonly int Zdm;
            public readonly int Ydm;
            public Stamp(int xdm, int zdm, int ydm) { Xdm = xdm; Zdm = zdm; Ydm = ydm; }
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
            var stamps = new List<Stamp>[network.Routes.Count];
            var programs = new int[network.Routes.Count][];
            int placements = 0;
            int programLength = 0;
            for (int i = 0; i < network.Routes.Count; i++)
            {
                WorldRoadNetworkRoute route = network.Routes[i];
                stamps[i] = Rasterize(route);
                programs[i] = RoadProgram(route, settings);
                placements += stamps[i].Count;
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: network.Routes.Count,
                rules: network.Routes.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: placements,
                overrides: 0,
                allocator);

            try
            {
                int placementOffset = 0;
                int programOffset = 0;
                for (int i = 0; i < network.Routes.Count; i++)
                {
                    WorldRoadNetworkRoute route = network.Routes[i];
                    int[] program = programs[i];
                    for (int p = 0; p < program.Length; p++) catalogue.Program[programOffset + p] = program[p];

                    int gradeWidthDm = Math.Max(2, route.GradeRadiusDm * 2);
                    int gradeWidth = gradeWidthDm * scale;
                    int fill = SurfaceThicknessDm * scale;
                    int clear = ClearAboveDm * scale;
                    catalogue.Definitions[i] = new FeatureDefinition
                    {
                        Name = new FixedString64Bytes(FeatureName(route.Id)),
                        Kind = FeatureKind.Landform,
                        BasePlane = BasePlaneRule.FixedAltitude,
                        FixedAltitude = 0,
                        Footprint = new int3(gradeWidth, fill + clear, gradeWidth),
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
                        MaxPrimitives = route.MarkingPolicy == WorldRoadMarkingPolicy.None ? 3 : 4,
                    };

                    int firstPlacement = placementOffset;
                    List<Stamp> routeStamps = stamps[i];
                    for (int p = 0; p < routeStamps.Count; p++)
                    {
                        Stamp stamp = routeStamps[p];
                        catalogue.ExplicitPlacements[placementOffset++] = new ExplicitPlacement
                        {
                            Position = new int3(
                                stamp.Xdm * scale - gradeWidth / 2,
                                stamp.Ydm * scale - fill,
                                stamp.Zdm * scale - gradeWidth / 2),
                            Orientation = 0,
                            OverrideOffset = 0,
                            OverrideCount = 0,
                        };
                    }
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
                        ExplicitOffset = firstPlacement,
                        ExplicitCount = placementOffset - firstPlacement,
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

        private static List<Stamp> Rasterize(WorldRoadNetworkRoute route)
        {
            var result = new List<Stamp>(64);
            var seen = new HashSet<long>();
            var influence = new WorldRoadInfluence(route.Road);
            int spacing = Math.Max(8, route.CarriagewayWidthDm / 2);
            for (int segment = 0; segment + 1 < route.Road.Points.Count; segment++)
            {
                ResolvedWorldRoadPoint a = route.Road.Points[segment];
                ResolvedWorldRoadPoint b = route.Road.Points[segment + 1];
                int dx = b.Xdm - a.Xdm;
                int dz = b.Zdm - a.Zdm;
                int extent = Math.Max(Math.Abs(dx), Math.Abs(dz));
                int steps = Math.Max(1, (extent + spacing - 1) / spacing);
                int start = segment == 0 ? 0 : 1;
                for (int step = start; step <= steps; step++)
                {
                    int x = a.Xdm + dx * step / steps;
                    int z = a.Zdm + dz * step / steps;
                    long key = ((long)x << 32) ^ (uint)z;
                    if (!seen.Add(key)) continue;
                    if (!influence.TrySample(x, z, out WorldRoadInfluenceSample sample))
                        throw new InvalidOperationException("Resolved road influence did not cover its own centreline.");
                    result.Add(new Stamp(x, z, sample.TargetHeightDm));
                }
            }
            return result;
        }

        private static int[] RoadProgram(WorldRoadNetworkRoute route, VoxelWorldGenSettings settings)
        {
            int scale = settings.VoxelsPerDecimetre;
            int gradeWidth = Math.Max(2, route.GradeRadiusDm * 2) * scale;
            int coreWidth = route.CarriagewayWidthDm * scale;
            int fill = SurfaceThicknessDm * scale;
            int clear = ClearAboveDm * scale;
            int coreInset = (gradeWidth - coreWidth) / 2;
            byte ground = settings.Materials.Resolve(MaterialRole.Moss);
            byte road = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte marking = settings.Materials.Resolve(MaterialRole.FoundationStone);
            var code = new List<int>(48);
            EmitBox(code, 0, fill, 0, gradeWidth, clear, gradeWidth, 0, PrimitiveMode.Carve);
            EmitBox(code, 0, 0, 0, gradeWidth, fill, gradeWidth, ground, PrimitiveMode.Fill);
            EmitBox(code, coreInset, 0, coreInset, coreWidth, fill, coreWidth, road, PrimitiveMode.Fill);
            if (route.MarkingPolicy == WorldRoadMarkingPolicy.CentreMarkers)
            {
                int markerSize = Math.Max(1, Math.Min(2 * scale, coreWidth));
                int markerInset = (gradeWidth - markerSize) / 2;
                EmitBox(code, markerInset, 0, markerInset, markerSize, fill, markerSize, marking, PrimitiveMode.Fill);
            }
            Emit(code, ShapeOp.End);
            return code.ToArray();
        }

        private static string FeatureName(string id)
        {
            string value = "world-road-" + id;
            return value.Length <= 63 ? value : value.Substring(0, 63);
        }

        private static void EmitBox(
            List<int> code,
            int x, int y, int z,
            int sx, int sy, int sz,
            byte material,
            PrimitiveMode mode)
            => Emit(code, ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);

        private static void Emit(List<int> code, ShapeOp op, params int[] operands)
        {
            code.Add((int)op);
            code.Add(0);
            code.AddRange(operands);
        }
    }
}
