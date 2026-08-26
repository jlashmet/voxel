using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Adds a shallow masonry foundation skirt beneath each Kentridge building parcel.
    ///
    /// Earlier vertical prototypes used this stage to build every parcel all the way down to the
    /// analytic terrain. Once neighbourhood-scale district terraces became authoritative, those
    /// independent columns made the town read as a collection of pedestals. The stage now replaces
    /// only the upper skin of the shared hillside beneath each building. Plot grading runs afterward
    /// and covers the top portion, leaving a modest stone collar where a yard edge or local cut makes
    /// the foundation visible without recreating the giant parcel plinths.
    /// </summary>
    public static class KentridgeTerraceSupportCatalogue
    {
        private const int InsetDm = 2;
        private const int FoundationSkirtDm = 24;

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
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            int scale = settings.VoxelsPerDecimetre;
            var supports = new List<SupportBuild>(plan.Plots.Count - 1);

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Archetype == StructureArchetype.Well) continue;

                Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
                int width = Math.Max(1, (footprint.X - InsetDm * 2) * scale);
                int depth = Math.Max(1, (footprint.Z - InsetDm * 2) * scale);
                int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plan, plot, seed, scale);
                int height = FoundationSkirtDm * scale;

                supports.Add(new SupportBuild(
                    plot,
                    width,
                    depth,
                    height,
                    new int3(
                        (plot.PositionDm.X + InsetDm) * scale,
                        targetSurface - height,
                        (plot.PositionDm.Y + InsetDm) * scale)));
            }

            int count = supports.Count;
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: count,
                rules: count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: count * (ShapeOps.InstructionLength(ShapeOp.EmitBox)
                    + ShapeOps.InstructionLength(ShapeOp.End)),
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
                    Name = new FixedString64Bytes("kentridge-foundation-skirt-" + support.Plot.RoleId),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(support.Width, support.Height, support.Depth),
                    MaxSlope = 32,
                    // District terraces own the macro mass at 15; roads are 20-25; parcel grading is
                    // 40. This shallow collar belongs immediately before the local plot skin.
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

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge foundation skirt catalogue failed validation: " + result);
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
                0, 0,
                (int)PrimitiveMode.Fill,
                (int)ShapeOp.End,
                0,
            };
        }
    }
}
