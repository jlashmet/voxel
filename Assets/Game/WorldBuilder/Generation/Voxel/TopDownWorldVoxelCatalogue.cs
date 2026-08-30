using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

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
                if (!string.Equals(_nodes[i].Node.Id, nodeId, StringComparison.Ordinal)) continue;
                centreDm = _nodes[i].CentreDm;
                return true;
            }
            centreDm = default;
            return false;
        }
    }

    /// <summary>
    /// Physical realization of a production WorldBuilder macro layout. Verified semantic routes are
    /// resolved by TopDownWorldRoadNetwork and lowered by WorldRoadNetworkVoxelCatalogue; this class
    /// owns only macro node markers plus a lightweight route-plan diagnostic view.
    /// </summary>
    public static class TopDownWorldVoxelCatalogue
    {
        public const int RouteTileStepDm = 30;
        private const int VerticalBandDm = 80;
        private const int MarkerMaxHalfExtentDm = 60;
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

            var routes = new List<TopDownWorldVoxelRoutePlan>();
            for (var i = 0; i < layout.Routes.Count; i++)
            {
                TopDownWorldRouteSpec route = layout.Routes[i];
                if (!route.IsHard) continue;
                if (!physicalById.TryGetValue(route.FromId, out Int2 from)
                    || !physicalById.TryGetValue(route.ToId, out Int2 to))
                    throw new InvalidOperationException("Macro route references an unrealized node: " + route.Key);
                routes.Add(new TopDownWorldVoxelRoutePlan(route, BuildRouteSamples(from, to)));
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
            WorldRoadNetwork network = TopDownWorldRoadNetwork.Build(
                layout, rootCentreDm, cellSizeDm, settings);

            FeatureCatalogue nodes = BuildNodeMarkers(plan, layout.Seed, settings, allocator);
            if (network.Routes.Count == 0) return nodes;

            FeatureCatalogue roads = default;
            try
            {
                roads = WorldRoadNetworkVoxelCatalogue.Build(network, settings, allocator, precedence: 6);
                return SettlementCatalogueCombiner.Combine(allocator, nodes, roads);
            }
            finally
            {
                if (nodes.IsCreated) nodes.Dispose();
                if (roads.IsCreated) roads.Dispose();
            }
        }

        private static FeatureCatalogue BuildNodeMarkers(
            TopDownWorldVoxelPlan plan,
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            int verticalBand = VerticalBandDm * scale;
            int definitionCount = plan.Nodes.Count;
            var programs = new int[definitionCount][];
            int programLength = 0;
            byte marker = settings.Materials.Resolve(MaterialRole.FoundationStone);
            for (int i = 0; i < definitionCount; i++)
            {
                int halfDm = Math.Min(plan.Nodes[i].Node.EnvelopeHalfExtentDm, MarkerMaxHalfExtentDm);
                int size = halfDm * 2 * scale;
                programs[i] = PaintProgram(size, verticalBand, size, marker);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: definitionCount,
                rules: definitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: definitionCount,
                overrides: 0,
                allocator);

            try
            {
                int programOffset = 0;
                for (int i = 0; i < definitionCount; i++)
                {
                    TopDownWorldVoxelNodePlan node = plan.Nodes[i];
                    int halfDm = Math.Min(node.Node.EnvelopeHalfExtentDm, MarkerMaxHalfExtentDm);
                    int size = halfDm * 2 * scale;
                    int[] program = programs[i];
                    CopyProgram(ref catalogue, programOffset, program);
                    catalogue.Definitions[i] = Definition(
                        "macro-node-" + i, size, verticalBand, size,
                        programOffset, program.Length, precedence: 5);

                    int ground = TerrainSampler.HeightAt(node.CentreDm.X * scale, node.CentreDm.Y * scale, seed);
                    catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                    {
                        Position = new int3(
                            (node.CentreDm.X - halfDm) * scale,
                            ground - verticalBand / 2,
                            (node.CentreDm.Y - halfDm) * scale),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                    catalogue.Rules[i] = ExplicitRule(i, i, 1);
                    programOffset += program.Length;
                }

                CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
                if (result != CatalogueLoadResult.Ok)
                    throw new InvalidOperationException("Top-down node marker catalogue failed validation: " + result);
                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static IReadOnlyList<Int2> BuildRouteSamples(Int2 from, Int2 to)
        {
            int dx = to.X - from.X;
            int dz = to.Y - from.Y;
            int extent = Math.Max(Math.Abs(dx), Math.Abs(dz));
            int steps = Math.Max(1, (extent + RouteTileStepDm - 1) / RouteTileStepDm);
            var samples = new List<Int2>(steps + 1);
            for (int step = 0; step <= steps; step++)
                samples.Add(new Int2(from.X + dx * step / steps, from.Y + dz * step / steps));
            return samples;
        }

        private static FeatureDefinition Definition(
            string name,
            int width,
            int height,
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
                Footprint = new int3(width, height, depth),
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

        private static int[] PaintProgram(int width, int height, int depth, byte material)
        {
            return new[]
            {
                (int)ShapeOp.EmitBox,
                0,
                0, 0, 0,
                width, height, depth,
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
