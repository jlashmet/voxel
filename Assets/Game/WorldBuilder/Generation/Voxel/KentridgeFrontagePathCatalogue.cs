using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Paints short pedestrian/service approaches from every Kentridge building frontage to the
    /// street that owns that frontage. The semantic planner decides where plots and streets live;
    /// this backend derives the connective surface from those relationships instead of hard-coding
    /// another set of coordinates.
    ///
    /// Paths are material-only PaintSurface features. They run after road/plot grading, so they
    /// follow the already-prepared yard and street surface without introducing another cut/fill
    /// layer. Buildings run after this pass and naturally cover the small path overlap beneath
    /// each doorway.
    /// </summary>
    public static class KentridgeFrontagePathCatalogue
    {
        private const int RoadOverlapDm = 3;
        private const int BuildingOverlapDm = 10;
        /// <summary>
        /// How far above and below the plot a path looks for the graded surface it paints onto.
        ///
        /// This used to be the entire world height. That was harmless while terrain topped out
        /// around 49 m, but it is a feature *footprint*, not a search cursor, and footprints are
        /// budgeted (FeatureBudget.MaxFootprintVoxels). Once terrain reached mountain scale the
        /// same expression asked for a feature kilometres tall and the catalogue stopped
        /// loading. A frontage path only ever sits on ground that road and plot grading has
        /// already levelled, so the window it needs is local to the plot and independent of how
        /// tall the world happens to be.
        /// </summary>
        private const int VerticalSearchVoxels = 256;
        private static int ProgramLengthPerPath =>
            ShapeOps.InstructionLength(ShapeOp.EmitBox)
            + ShapeOps.InstructionLength(ShapeOp.End);

        private readonly struct PathRect
        {
            public readonly int RoleId;
            public readonly int Xdm;
            public readonly int Zdm;
            public readonly int WidthDm;
            public readonly int DepthDm;

            public PathRect(int roleId, int xDm, int zDm, int widthDm, int depthDm)
            {
                RoleId = roleId;
                Xdm = xDm;
                Zdm = zDm;
                WidthDm = widthDm;
                DepthDm = depthDm;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            int scale = settings.VoxelsPerDecimetre;
            var paths = new List<PathRect>(plan.Plots.Count - 1);

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Archetype == StructureArchetype.Well) continue;
                paths.Add(BuildPath(plan, plot));
            }

            int count = paths.Count;
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: count,
                rules: count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: count * ProgramLengthPerPath,
                materials: 0,
                explicitPlacements: count,
                overrides: 0,
                allocator);

            byte surface = settings.Materials.Resolve(MaterialRole.RoadSurface);
            int programOffset = 0;

            for (int i = 0; i < count; i++)
            {
                PathRect path = paths[i];
                int width = path.WidthDm * scale;
                int depth = path.DepthDm * scale;
                int[] program = PaintProgram(width, depth, surface);
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-frontage-" + path.RoleId),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(width, VerticalSearchVoxels, depth),
                    MaxSlope = 32,
                    Precedence = 60,
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
                    Position = new int3(path.Xdm * scale, 0, path.Zdm * scale),
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
                    MaxAltitude = 1024,
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
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge frontage path catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static PathRect BuildPath(SettlementPlan plan, BuildingPlot plot)
        {
            Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
            int width = PathWidthDm(plot.Archetype);
            int frontX = plot.PositionDm.X + footprint.X / 2;
            int frontZ = plot.PositionDm.Y + footprint.Z / 2;

            switch (plot.Frontage)
            {
                case FrontageDirection.South:
                {
                    int buildingFront = plot.PositionDm.Y;
                    int roadEdge = FindHorizontalRoadEdge(
                        plan, frontX, buildingFront, plot.Frontage);
                    int z0 = roadEdge - RoadOverlapDm;
                    int z1 = buildingFront + BuildingOverlapDm;
                    return new PathRect(
                        plot.RoleId, frontX - width / 2, z0,
                        width, Math.Max(1, z1 - z0));
                }

                case FrontageDirection.North:
                {
                    int buildingFront = plot.PositionDm.Y + footprint.Z;
                    int roadEdge = FindHorizontalRoadEdge(
                        plan, frontX, buildingFront, plot.Frontage);
                    int z0 = buildingFront - BuildingOverlapDm;
                    int z1 = roadEdge + RoadOverlapDm;
                    return new PathRect(
                        plot.RoleId, frontX - width / 2, z0,
                        width, Math.Max(1, z1 - z0));
                }

                case FrontageDirection.West:
                {
                    int buildingFront = plot.PositionDm.X;
                    int roadEdge = FindVerticalRoadEdge(
                        plan, frontZ, buildingFront, plot.Frontage);
                    int x0 = roadEdge - RoadOverlapDm;
                    int x1 = buildingFront + BuildingOverlapDm;
                    return new PathRect(
                        plot.RoleId, x0, frontZ - width / 2,
                        Math.Max(1, x1 - x0), width);
                }

                case FrontageDirection.East:
                {
                    int buildingFront = plot.PositionDm.X + footprint.X;
                    int roadEdge = FindVerticalRoadEdge(
                        plan, frontZ, buildingFront, plot.Frontage);
                    int x0 = buildingFront - BuildingOverlapDm;
                    int x1 = roadEdge + RoadOverlapDm;
                    return new PathRect(
                        plot.RoleId, x0, frontZ - width / 2,
                        Math.Max(1, x1 - x0), width);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(plot.Frontage));
            }
        }

        private static int FindHorizontalRoadEdge(SettlementPlan plan, int frontX,
                                                  int buildingFrontZ,
                                                  FrontageDirection frontage)
        {
            int bestDistance = int.MaxValue;
            int bestEdge = 0;

            for (int s = 0; s < plan.Streets.Count; s++)
            {
                PlannedStreet street = plan.Streets[s];
                for (int p = 0; p + 1 < street.Points.Count; p++)
                {
                    Int2 a = street.Points[p];
                    Int2 b = street.Points[p + 1];
                    if (a.Y != b.Y) continue;

                    int minX = Math.Min(a.X, b.X) - street.WidthDm / 2;
                    int maxX = Math.Max(a.X, b.X) + street.WidthDm / 2;
                    if (frontX < minX || frontX > maxX) continue;

                    int edge;
                    int distance;
                    if (frontage == FrontageDirection.South)
                    {
                        edge = a.Y + street.WidthDm / 2;
                        if (edge > buildingFrontZ) continue;
                        distance = buildingFrontZ - edge;
                    }
                    else
                    {
                        edge = a.Y - street.WidthDm / 2;
                        if (edge < buildingFrontZ) continue;
                        distance = edge - buildingFrontZ;
                    }

                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestEdge = edge;
                }
            }

            if (bestDistance == int.MaxValue)
                throw new InvalidOperationException(
                    "No horizontal frontage street found for building at "
                    + frontX + "," + buildingFrontZ + ".");
            return bestEdge;
        }

        private static int FindVerticalRoadEdge(SettlementPlan plan, int frontZ,
                                                int buildingFrontX,
                                                FrontageDirection frontage)
        {
            int bestDistance = int.MaxValue;
            int bestEdge = 0;

            for (int s = 0; s < plan.Streets.Count; s++)
            {
                PlannedStreet street = plan.Streets[s];
                for (int p = 0; p + 1 < street.Points.Count; p++)
                {
                    Int2 a = street.Points[p];
                    Int2 b = street.Points[p + 1];
                    if (a.X != b.X) continue;

                    int minZ = Math.Min(a.Y, b.Y) - street.WidthDm / 2;
                    int maxZ = Math.Max(a.Y, b.Y) + street.WidthDm / 2;
                    if (frontZ < minZ || frontZ > maxZ) continue;

                    int edge;
                    int distance;
                    if (frontage == FrontageDirection.West)
                    {
                        edge = a.X + street.WidthDm / 2;
                        if (edge > buildingFrontX) continue;
                        distance = buildingFrontX - edge;
                    }
                    else
                    {
                        edge = a.X - street.WidthDm / 2;
                        if (edge < buildingFrontX) continue;
                        distance = edge - buildingFrontX;
                    }

                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestEdge = edge;
                }
            }

            if (bestDistance == int.MaxValue)
                throw new InvalidOperationException(
                    "No vertical frontage street found for building at "
                    + buildingFrontX + "," + frontZ + ".");
            return bestEdge;
        }

        private static int PathWidthDm(StructureArchetype archetype)
        {
            switch (archetype)
            {
                case StructureArchetype.Townhouse: return 10;
                case StructureArchetype.WideHouse: return 12;
                case StructureArchetype.Shop: return 14;
                case StructureArchetype.Inn: return 18;
                case StructureArchetype.Warehouse: return 30;
                case StructureArchetype.Mansion: return 24;
                case StructureArchetype.Church: return 20;
                default: return 10;
            }
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
    }
}
