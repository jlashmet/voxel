using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Selects the public-space backend for Kentridge. Organic plans are rasterized from inferred
    /// route polylines; legacy street plans retain the old directed-ramp adapter unchanged.
    /// </summary>
    public static class KentridgeDirectedTownSurfaceCatalogue
    {
        private const int RampAxisOperand = 6;

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            if (plan.Routes.Count > 0)
                return KentridgeOrganicCirculationCatalogue.Build(plan, seed, settings, allocator);

            FeatureCatalogue catalogue = KentridgeVerticalTownSurfaceCatalogue.Build(
                seed, settings, allocator);

            try
            {
                for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
                {
                    PlacementRule rule = catalogue.Rules[ruleIndex];
                    FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                    if (rule.ExplicitCount != 1)
                        throw new InvalidOperationException(
                            "Directed Kentridge public-space definitions must own exactly one placement: "
                            + definition.Name);

                    int placementIndex = rule.ExplicitOffset;
                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    int orientation = placement.Orientation & 3;
                    if (orientation == 0) continue;
                    if (orientation != 2)
                        throw new InvalidOperationException(
                            "Kentridge public roads only support direct or reversed half-turn ramps.");

                    ReverseRamps(ref catalogue, in definition);
                    placement.Orientation = 0;
                    catalogue.ExplicitPlacements[placementIndex] = placement;
                }
                return catalogue;
            }
            catch
            {
                catalogue.Dispose();
                throw;
            }
        }

        private static bool ReverseRamps(ref FeatureCatalogue catalogue,
                                         in FeatureDefinition definition)
        {
            bool found = false;
            int pc = definition.ProgramOffset;
            int end = definition.ProgramOffset + definition.ProgramLength;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)catalogue.Program[pc];
                int length = ShapeOps.InstructionLength(op);
                if (length < 0 || pc + length > end)
                    throw new InvalidOperationException(
                        "Malformed Kentridge public-space program while directing ramps.");
                if (op == ShapeOp.End) break;
                if (op == ShapeOp.EmitRamp)
                {
                    int axisIndex = pc + 2 + RampAxisOperand;
                    int axis = catalogue.Program[axisIndex];
                    int baseAxis = axis & ShapeOps.RampAxisMask;
                    if (baseAxis != 0 && baseAxis != 2)
                        throw new InvalidOperationException(
                            "Kentridge public road ramp used an unsupported axis: " + axis);
                    if ((axis & ShapeOps.ReverseRampBit) != 0)
                        throw new InvalidOperationException(
                            "Kentridge ramp was already marked reversed before direction adaptation.");
                    catalogue.Program[axisIndex] = axis | ShapeOps.ReverseRampBit;
                    found = true;
                }
                pc += length;
            }
            return found;
        }
    }

    /// <summary>
    /// Bounded terrain-following rasterizer for generic settlement routes. It samples each integer
    /// polyline at no more than half its width, so adjacent square patches overlap and form continuous
    /// diagonal/curved circulation without arbitrary-angle shape transforms or an unbounded pathfinder.
    /// </summary>
    internal static class KentridgeOrganicCirculationCatalogue
    {
        private const int SurfaceThicknessDm = 4;
        private const int ClearAboveDm = 24;
        private static readonly int[] WidthsDm = { 18, 20, 22, 26, 28 };

        private readonly struct RouteStamp
        {
            public readonly int WidthDm;
            public readonly Int2 PointDm;
            public RouteStamp(int widthDm, Int2 pointDm)
            {
                WidthDm = widthDm;
                PointDm = pointDm;
            }
        }

        public static FeatureCatalogue Build(
            SettlementPlan plan,
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            int scale = settings.VoxelsPerDecimetre;
            List<RouteStamp> stamps = Rasterize(plan.Routes);

            var groups = new List<RouteStamp>[WidthsDm.Length];
            int definitions = 0;
            int placements = 0;
            int programLength = 0;
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<RouteStamp>();
            for (int i = 0; i < stamps.Count; i++)
            {
                int group = WidthIndex(stamps[i].WidthDm);
                groups[group].Add(stamps[i]);
            }
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Count == 0) continue;
                definitions++;
                placements += groups[i].Count;
                programLength += RouteProgram(WidthsDm[i], settings).Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: definitions,
                rules: definitions,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: placements,
                overrides: 0,
                allocator);

            int definitionIndex = 0;
            int placementOffset = 0;
            int programOffset = 0;
            for (int group = 0; group < groups.Length; group++)
            {
                List<RouteStamp> groupStamps = groups[group];
                if (groupStamps.Count == 0) continue;
                int widthDm = WidthsDm[group];
                int[] program = RouteProgram(widthDm, settings);
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                int width = widthDm * scale;
                int fill = SurfaceThicknessDm * scale;
                int clear = ClearAboveDm * scale;
                catalogue.Definitions[definitionIndex] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-organic-route-" + widthDm),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(width, fill + clear, width),
                    MaxSlope = 32,
                    Precedence = 20,
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
                    MaxPrimitives = 2,
                };

                for (int i = 0; i < groupStamps.Count; i++)
                {
                    Int2 point = groupStamps[i].PointDm;
                    int surfaceY = TerrainQuery.HeightAt(point.X * scale, point.Y * scale, seed);
                    catalogue.ExplicitPlacements[placementOffset + i] = new ExplicitPlacement
                    {
                        Position = new int3(
                            point.X * scale - width / 2,
                            surfaceY - fill,
                            point.Y * scale - width / 2),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                }

                catalogue.Rules[definitionIndex] = new PlacementRule
                {
                    DefinitionId = definitionIndex,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 32,
                    MinSpacing = 0,
                    ClusterMin = 0,
                    ClusterMax = 0,
                    ExclusionMask = 0,
                    ExplicitOffset = placementOffset,
                    ExplicitCount = groupStamps.Count,
                };

                placementOffset += groupStamps.Count;
                programOffset += program.Length;
                definitionIndex++;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Organic Kentridge circulation catalogue failed validation: " + result);
            }
            return catalogue;
        }

        private static List<RouteStamp> Rasterize(IReadOnlyList<PlannedRoute> routes)
        {
            var result = new List<RouteStamp>(512);
            for (int r = 0; r < routes.Count; r++)
            {
                PlannedRoute route = routes[r];
                for (int p = 0; p + 1 < route.Points.Count; p++)
                {
                    Int2 a = route.Points[p];
                    Int2 b = route.Points[p + 1];
                    int dx = b.X - a.X;
                    int dz = b.Y - a.Y;
                    int extent = Math.Max(Math.Abs(dx), Math.Abs(dz));
                    int spacing = Math.Max(8, route.WidthDm / 2);
                    int steps = Math.Max(1, (extent + spacing - 1) / spacing);
                    int start = p == 0 ? 0 : 1;
                    for (int s = start; s <= steps; s++)
                    {
                        result.Add(new RouteStamp(
                            route.WidthDm,
                            new Int2(
                                a.X + dx * s / steps,
                                a.Y + dz * s / steps)));
                    }
                }
            }
            return result;
        }

        private static int WidthIndex(int widthDm)
        {
            for (int i = 0; i < WidthsDm.Length; i++)
                if (WidthsDm[i] == widthDm) return i;
            throw new InvalidOperationException("Unsupported organic route width: " + widthDm);
        }

        private static int[] RouteProgram(int widthDm, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int width = widthDm * s;
            int fill = SurfaceThicknessDm * s;
            int clear = ClearAboveDm * s;
            byte roadSurface = settings.Materials.Resolve(MaterialRole.RoadSurface);
            var code = new List<int>(30);
            EmitBox(code, 0, fill, 0, width, clear, width, 0, PrimitiveMode.Carve);
            EmitBox(code, 0, 0, 0, width, fill, width, roadSurface, PrimitiveMode.Fill);
            Emit(code, ShapeOp.End);
            return code.ToArray();
        }

        private static void EmitBox(
            List<int> code, int x, int y, int z, int sx, int sy, int sz,
            byte material, PrimitiveMode mode) =>
            Emit(code, ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);

        private static void Emit(List<int> code, ShapeOp op, params int[] operands)
        {
            code.Add((int)op);
            code.Add(0);
            code.AddRange(operands);
        }
    }
}