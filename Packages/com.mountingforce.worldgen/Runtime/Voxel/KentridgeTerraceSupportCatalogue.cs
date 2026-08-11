using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Builds the visible masonry mass beneath raised Kentridge parcels.
    ///
    /// The ordinary plot-surface pass only needs enough material to grade a yard. Once Kentridge
    /// owns a deliberately stacked skyline, some parcels sit many metres above the analytic terrain;
    /// moving the shallow pad alone would create floating lawns. This pass turns those height
    /// differences into architectural retaining plinths before the normal green terrace skin runs.
    /// Lower plots receive only a shallow buried footing, while upper civic/noble plots expose tall
    /// stone faces that make the elevation bands readable from the streets below.
    /// </summary>
    public static class KentridgeTerraceSupportCatalogue
    {
        private const int InsetDm = 2;
        private const int BuriedFootingDm = 8;

        private readonly struct SupportBuild
        {
            public readonly BuildingPlot Plot;
            public readonly int Width;
            public readonly int Depth;
            public readonly int Height;
            public readonly int3 Position;

            public SupportBuild(BuildingPlot plot, int width, int depth, int height, int3 position)
            {
                Plot = plot;
                Width = width;
                Depth = depth;
                Height = height;
                Position = position;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            int scale = settings.VoxelsPerDecimetre;
            var supports = new List<SupportBuild>(plan.Plots.Count - 1);

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Archetype == StructureArchetype.Well) continue;

                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                int width = Math.Max(1, (footprint.X - InsetDm * 2) * scale);
                int depth = Math.Max(1, (footprint.Z - InsetDm * 2) * scale);
                int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plot, seed, scale);
                int naturalLowest = KentridgeVerticalProfile.NaturalLowestUnderPlot(plot, seed, scale);

                int exposedRise = Math.Max(0, targetSurface - naturalLowest);
                int height = exposedRise + BuriedFootingDm * scale;
                int y = targetSurface - height;

                supports.Add(new SupportBuild(
                    plot,
                    width,
                    depth,
                    height,
                    new int3(
                        (plot.PositionDm.X + InsetDm) * scale,
                        y,
                        (plot.PositionDm.Y + InsetDm) * scale)));
            }

            int count = supports.Count;
            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
                definitions: count,
                rules: count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: count * 12,
                materials: 0,
                explicitPlacements: count,
                overrides: 0,
                allocator);

            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            int programOffset = 0;

            for (int i = 0; i < count; i++)
            {
                SupportBuild support = supports[i];
                int[] program = BoxProgram(support.Width, support.Height, support.Depth, stone);
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-terrace-support-" + support.Plot.RoleId),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(support.Width, support.Height, support.Depth),
                    MaxSlope = 32,
                    // Roads are 20-25 and plot grading is 40. Retaining mass belongs between them.
                    Precedence = 35,
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
                    Position = support.Position,
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

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge terrace support catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static int[] BoxProgram(int width, int height, int depth, byte material)
        {
            return new[]
            {
                (int)ShapeOp.EmitBox,
                0,
                0, 0, 0,
                width, height, depth,
                material,
                (int)PrimitiveMode.Fill,
                (int)ShapeOp.End,
                0,
            };
        }
    }
}
