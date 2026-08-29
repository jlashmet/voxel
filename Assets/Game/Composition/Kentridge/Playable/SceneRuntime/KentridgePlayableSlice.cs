using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    public sealed class TopDownWorldVoxelRoutePlan
    {
        private readonly Int2[] _tiles;

        public TopDownWorldRouteSpec Route { get; }
        public IReadOnlyList<Int2> Tiles => _tiles;

        public TopDownWorldVoxelRoutePlan(TopDownWorldRouteSpec route, IReadOnlyList<Int2> tiles)
        {
            Route = route ?? throw new ArgumentNullException(nameof(route));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            _tiles = new Int2[tiles.Count];
            for (var i = 0; i < tiles.Count; i++) _tiles[i] = tiles[i];
        }
    }

    public sealed class TopDownWorldVoxelNodePlan
    {
        public TopDownWorldNodeSpec Node { get; }
        public Int2 CentreDm { get; }

        public TopDownWorldVoxelNodePlan(TopDownWorldNodeSpec node, Int2 centreDm)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            CentreDm = centreDm;
        }
    }

    public sealed class TopDownWorldVoxelPlan
    {
        private readonly TopDownWorldVoxelNodePlan[] _nodes;
        private readonly TopDownWorldVoxelRoutePlan[] _routes;

        public IReadOnlyList<TopDownWorldVoxelNodePlan> Nodes => _nodes;
        public IReadOnlyList<TopDownWorldVoxelRoutePlan> Routes => _routes;
        public int RouteTileCount { get; }

        public TopDownWorldVoxelPlan(
            IReadOnlyList<TopDownWorldVoxelNodePlan> nodes,
            IReadOnlyList<TopDownWorldVoxelRoutePlan> routes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            _nodes = new TopDownWorldVoxelNodePlan[nodes.Count];
            _routes = new TopDownWorldVoxelRoutePlan[routes.Count];
            for (var i = 0; i < nodes.Count; i++) _nodes[i] = nodes[i];
            var tiles = 0;
            for (var i = 0; i < routes.Count; i++)
            {
                _routes[i] = routes[i];
                tiles += routes[i].Tiles.Count;
            }
            RouteTileCount = tiles;
        }

        public bool TryGetNodeCentre(string nodeId, out Int2 centreDm)
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                if (!string.Equals(_nodes[i].Node.Id, nodeId, StringComparison.Ordinal))
                    continue;
                centreDm = _nodes[i].CentreDm;
                return true;
            }
            centreDm = default;
            return false;
        }
    }

    /// <summary>
    /// Voxel realization of a production top-down WorldBuilder layout. The semantic graph owns
    /// destinations, envelopes and hard connections; this backend turns them into low-precedence
    /// grounded destination pads plus overlapping surface-painted travel tiles. Later town/landmark
    /// catalogues intentionally override these neutral layout markers.
    /// </summary>
    public static class TopDownWorldVoxelCatalogue
    {
        public const int RouteTileStepDm = 30;
        private const int VerticalSearchVoxels = 1024;
        private const int MaxAltitudeVoxels = 4096;

        public static TopDownWorldVoxelPlan Plan(
            TopDownWorldLayout layout,
            Int2 rootCentreDm,
            int cellSizeDm)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (cellSizeDm < 1) throw new ArgumentOutOfRangeException(nameof(cellSizeDm));

            var nodes = new List<TopDownWorldVoxelNodePlan>(layout.Nodes.Count);
            var physicalById = new Dictionary<string, Int2>(StringComparer.Ordinal);
            for (var i = 0; i < layout.Nodes.Count; i++)
            {
                TopDownWorldNodePlacement placement = layout.Nodes[i];
                var centre = new Int2(
                    rootCentreDm.X + placement.Position.X * cellSizeDm,
                    rootCentreDm.Y + placement.Position.Y * cellSizeDm);
                nodes.Add(new TopDownWorldVoxelNodePlan(placement.Node, centre));
                physicalById.Add(placement.Node.Id, centre);
            }

            var routes = new List<TopDownWorldVoxelRoutePlan>(layout.Routes.Count);
            for (var i = 0; i < layout.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = layout.Routes[i];
                if (!physicalById.TryGetValue(route.FromId, out Int2 from)
                    || !physicalById.TryGetValue(route.ToId, out Int2 to))
                    throw new InvalidOperationException("Macro route references an unrealized node: " + route.Key);

                routes.Add(new TopDownWorldVoxelRoutePlan(route, BuildRouteTiles(from, to)));
            }

            return new TopDownWorldVoxelPlan(nodes, routes);
        }

        public static FeatureCatalogue Build(
            TopDownWorldLayout layout,
            Int2 rootCentreDm,
            int cellSizeDm,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            TopDownWorldVoxelPlan plan = Plan(layout, rootCentreDm, cellSizeDm);
            int scale = settings.VoxelsPerDecimetre;
            int routeDefinitionCount = layout.Routes.Count;
            int nodeDefinitionCount = layout.Nodes.Count;
            int definitionCount = routeDefinitionCount + nodeDefinitionCount;
            int programLength = 0;
            var routePrograms = new int[routeDefinitionCount][];
            var nodePrograms = new int[nodeDefinitionCount][];

            byte road = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte marker = settings.Materials.Resolve(MaterialRole.FoundationStone);
            for (var i = 0; i < routeDefinitionCount; i++)
            {
                int width = layout.Routes[i].CorridorWidthDm * scale;
                routePrograms[i] = PaintProgram(width, width, road);
                programLength += routePrograms[i].Length;
            }
            for (var i = 0; i < nodeDefinitionCount; i++)
            {
                int size = layout.Nodes[i].Node.EnvelopeHalfExtentDm * 2 * scale;
                nodePrograms[i] = PaintProgram(size, size, marker);
                programLength += nodePrograms[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: definitionCount,
                rules: definitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: plan.RouteTileCount + plan.Nodes.Count,
                overrides: 0,
                allocator);

            try
            {
                int programOffset = 0;
                int placementOffset = 0;

                for (var i = 0; i < plan.Routes.Count; i++)
                {
                    TopDownWorldVoxelRoutePlan physicalRoute = plan.Routes[i];
                    TopDownWorldRouteSpec route = physicalRoute.Route;
                    int width = route.CorridorWidthDm * scale;
                    int[] program = routePrograms[i];
                    CopyProgram(ref catalogue, programOffset, program);

                    catalogue.Definitions[i] = Definition(
                        "macro-route-" + i,
                        width,
                        width,
                        programOffset,
                        program.Length,
                        precedence: 6);

                    int firstPlacement = placementOffset;
                    for (var p = 0; p < physicalRoute.Tiles.Count; p++)
                    {
                        Int2 centre = physicalRoute.Tiles[p];
                        catalogue.ExplicitPlacements[placementOffset++] = new ExplicitPlacement
                        {
                            Position = new int3(
                                (centre.X - route.CorridorWidthDm / 2) * scale,
                                0,
                                (centre.Y - route.CorridorWidthDm / 2) * scale),
                            Orientation = 0,
                            OverrideOffset = 0,
                            OverrideCount = 0,
                        };
                    }
                    catalogue.Rules[i] = ExplicitRule(i, firstPlacement, placementOffset - firstPlacement);
                    programOffset += program.Length;
                }

                for (var i = 0; i < plan.Nodes.Count; i++)
                {
                    int definitionId = routeDefinitionCount + i;
                    TopDownWorldVoxelNodePlan physicalNode = plan.Nodes[i];
                    int half = physicalNode.Node.EnvelopeHalfExtentDm;
                    int size = half * 2 * scale;
                    int[] program = nodePrograms[i];
                    CopyProgram(ref catalogue, programOffset, program);

                    catalogue.Definitions[definitionId] = Definition(
                        "macro-node-" + i,
                        size,
                        size,
                        programOffset,
                        program.Length,
                        precedence: 5);

                    int firstPlacement = placementOffset;
                    catalogue.ExplicitPlacements[placementOffset++] = new ExplicitPlacement
                    {
                        Position = new int3(
                            (physicalNode.CentreDm.X - half) * scale,
                            0,
                            (physicalNode.CentreDm.Y - half) * scale),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                    catalogue.Rules[definitionId] = ExplicitRule(
                        definitionId, firstPlacement, 1);
                    programOffset += program.Length;
                }

                CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (result != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException(
                        "Top-down world voxel catalogue failed validation: " + result);
                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static IReadOnlyList<Int2> BuildRouteTiles(Int2 from, Int2 to)
        {
            var tiles = new List<Int2>();
            tiles.Add(from);

            // Deterministic Manhattan realization keeps each surface primitive axis-aligned. The
            // bend is backend geometry only; the semantic endpoints/connection remain authoritative.
            Int2 bend = new Int2(to.X, from.Y);
            AppendAxis(tiles, bend);
            AppendAxis(tiles, to);
            return tiles;
        }

        private static void AppendAxis(List<Int2> tiles, Int2 target)
        {
            Int2 current = tiles[tiles.Count - 1];
            if (current.X != target.X && current.Y != target.Y)
                throw new InvalidOperationException("Macro corridor segment must be axis-aligned.");

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

        private static FeatureDefinition Definition(
            string name,
            int width,
            int depth,
            int programOffset,
            int programLength,
            int precedence)
        {
            return new FeatureDefinition
            {
                Name = new FixedString64Bytes(name),
                Kind = FeatureKind.Landform,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(width, VerticalSearchVoxels, depth),
                MaxSlope = 32,
                Precedence = precedence,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 0,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = programOffset,
                ProgramLength = programLength,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 1,
            };
        }

        private static PlacementRule ExplicitRule(int definitionId, int offset, int count)
        {
            return new PlacementRule
            {
                DefinitionId = definitionId,
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
                ExplicitOffset = offset,
                ExplicitCount = count,
            };
        }

        private static int[] PaintProgram(int width, int depth, byte material)
        {
            return new[]
            {
                (int)ShapeOp.EmitBox,
                0,
                0, 0, 0,
                width, VerticalSearchVoxels, depth,
                material,
                0, 0,
                (int)PrimitiveMode.PaintSurface,
                (int)ShapeOp.End,
                0,
            };
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (var i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }
    }
}
