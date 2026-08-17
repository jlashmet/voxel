using System;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Production Kentridge building catalogue at the settlement-to-structure seam. SettlementPlan
    /// remains authoritative for stable roles, envelopes, frontage and placement. Generated house
    /// forms are realised by shared house presets/compiler; deliberately bespoke landmarks retain
    /// the existing landmark programs until their own shared archetype migrations land.
    /// </summary>
    public static class KentridgeSharedStructureVoxelCatalogue
    {
        private const int DefinitionCount = 17;
        private const int FoundationSinkDm = 5;

        private sealed class CompiledProgram
        {
            public int[] Code;
            public int3 Door;
            public int3 Hearth;
            public bool HasHearth;
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            ArchitectureTheme theme = plan.Theme;
            int scale = settings.VoxelsPerDecimetre;
            BuildingPlot[] plots = PlotsByRole(plan);
            var programs = new CompiledProgram[DefinitionCount];
            int programLength = 0;
            int anchorCount = 0;

            for (int roleId = 0; roleId < DefinitionCount; roleId++)
            {
                BuildingPlot plot = plots[roleId];
                StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                StructureForm form = ArchitectureCompiler.Resolve(intent, theme, seed);
                programs[roleId] = form.IsGenerated
                    ? SharedGeneratedProgram(plot, form, theme, settings, seed)
                    : BespokeProgram(intent, form, theme, settings);
                programLength += programs[roleId].Code.Length;
                anchorCount += programs[roleId].HasHearth ? 2 : 1;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: DefinitionCount,
                rules: DefinitionCount,
                parameters: 0,
                anchors: anchorCount,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: DefinitionCount,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int anchorOffset = 0;
            for (int roleId = 0; roleId < DefinitionCount; roleId++)
            {
                BuildingPlot plot = plots[roleId];
                CompiledProgram program = programs[roleId];
                for (int p = 0; p < program.Code.Length; p++)
                    catalogue.Program[programOffset + p] = program.Code[p];

                Int3 footprintDm = KentridgeDefinition.FootprintDm(plot.Archetype);
                int3 footprint = new int3(
                    footprintDm.X * scale,
                    footprintDm.Y * scale,
                    footprintDm.Z * scale);
                KentridgeRole role = (KentridgeRole)roleId;

                catalogue.Anchors[anchorOffset] = new AnchorSpec
                {
                    Name = plot.Archetype == StructureArchetype.Well ? "interaction" : "door",
                    LocalPosition = program.Door,
                    Facing = Facing.South,
                    SnapToGround = false,
                };
                if (program.HasHearth)
                {
                    catalogue.Anchors[anchorOffset + 1] = new AnchorSpec
                    {
                        Name = "hearth",
                        LocalPosition = program.Hearth,
                        Facing = Facing.Up,
                        SnapToGround = false,
                    };
                }

                catalogue.Definitions[roleId] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes(
                        "kentridge-role-" + role.ToString().ToLowerInvariant()),
                    Kind = FeatureKind.Structure,
                    BasePlane = BasePlaneRule.LowestGround,
                    Footprint = footprint,
                    MaxSlope = plot.Archetype == StructureArchetype.Well ? 2 : 3,
                    Precedence = plot.Archetype == StructureArchetype.Mansion ? 130 : 100,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = anchorOffset,
                    AnchorCount = program.HasHearth ? 2 : 1,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Code.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = 256,
                };

                int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plot, seed, scale);
                catalogue.ExplicitPlacements[roleId] = new ExplicitPlacement
                {
                    Position = new int3(
                        plot.PositionDm.X * scale,
                        targetSurface - FoundationSinkDm * scale,
                        plot.PositionDm.Y * scale),
                    Orientation = (byte)plot.Frontage,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };

                catalogue.Rules[roleId] = new PlacementRule
                {
                    DefinitionId = roleId,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 3,
                    MinSpacing = 0,
                    ClusterMin = 0,
                    ClusterMax = 0,
                    ExclusionMask = 0,
                    ExplicitOffset = roleId,
                    ExplicitCount = 1,
                };

                programOffset += program.Code.Length;
                anchorOffset += program.HasHearth ? 2 : 1;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge shared-structure catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static CompiledProgram SharedGeneratedProgram(
            BuildingPlot plot,
            StructureForm form,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            uint seed)
        {
            KentridgeSharedHouseProgram.Program program =
                KentridgeSharedHouseProgram.Build(plot, form, theme, settings, seed);
            return new CompiledProgram
            {
                Code = program.Code,
                Door = program.Door,
                Hearth = program.Hearth,
                HasHearth = true,
            };
        }

        private static CompiledProgram BespokeProgram(
            StructureIntent intent,
            StructureForm form,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings)
        {
            IArchitectureStyleCompiler style =
                BuiltInArchitectureStyles.Registry.Require(intent.StyleId);
            StructureGeometryProfile geometry = style.ResolveGeometry(intent, form);
            KentridgeBespokeVoxelPrograms.Program program =
                KentridgeBespokeVoxelPrograms.Build(form.Archetype, theme, settings, geometry);
            return new CompiledProgram
            {
                Code = program.Code,
                Door = program.Door,
                HasHearth = false,
            };
        }

        private static BuildingPlot[] PlotsByRole(SettlementPlan plan)
        {
            var plots = new BuildingPlot[DefinitionCount];
            var seen = new bool[DefinitionCount];

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.RoleId < 0 || plot.RoleId >= DefinitionCount)
                    throw new InvalidOperationException(
                        "Kentridge plot has out-of-range role id: " + plot.RoleId);
                if (seen[plot.RoleId])
                    throw new InvalidOperationException(
                        "Kentridge plot role appears twice: " + plot.RoleId);
                plots[plot.RoleId] = plot;
                seen[plot.RoleId] = true;
            }

            for (int roleId = 0; roleId < DefinitionCount; roleId++)
                if (!seen[roleId])
                    throw new InvalidOperationException(
                        "Kentridge is missing stable role id: " + roleId);

            return plots;
        }
    }
}
