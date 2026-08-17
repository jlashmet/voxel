using System;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Active 17-role Kentridge structure catalogue with semantic geometry for every role.
    ///
    /// Generated houses already author semantic bytecode in KentridgeGrammarVoxelCatalogue. The five
    /// deliberately bespoke archetypes are replaced here with KentridgeBespokeVoxelPrograms while all
    /// stable identities, placements, rules and anchors remain owned by the shared grammar catalogue.
    /// This is a deterministic composition step, not a geometry-inference pass.
    /// </summary>
    internal static class KentridgeSemanticGrammarVoxelCatalogue
    {
        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            FeatureCatalogue source = KentridgeGrammarVoxelCatalogue.Build(
                seed, settings, Allocator.Temp);
            try
            {
                SettlementPlan plan = KentridgeDefinition.Build(seed);
                BuildingPlot[] plots = PlotsByRole(plan, source.Definitions.Length);
                var programs = new int[source.Definitions.Length][];
                var doors = new int3[source.Definitions.Length];
                var replaceAnchor = new bool[source.Definitions.Length];
                int programLength = 0;

                for (int roleId = 0; roleId < source.Definitions.Length; roleId++)
                {
                    BuildingPlot plot = plots[roleId];
                    KentridgeBuildingForm form = KentridgeBuildingGrammar.Resolve(plot, seed);
                    if (form.IsGenerated)
                    {
                        programs[roleId] = CopyProgram(in source, source.Definitions[roleId]);
                    }
                    else
                    {
                        IArchitectureStyleCompiler style =
                            BuiltInArchitectureStyles.Registry.Require(form.Intent.StyleId);
                        StructureGeometryProfile geometry =
                            style.ResolveGeometry(form.Intent, form.Inner);
                        KentridgeBespokeVoxelPrograms.Program bespoke =
                            KentridgeBespokeVoxelPrograms.Build(
                                form.Archetype,
                                plan.Theme,
                                settings,
                                geometry);
                        programs[roleId] = bespoke.Code;
                        doors[roleId] = bespoke.Door;
                        replaceAnchor[roleId] = true;
                    }

                    programLength += programs[roleId].Length;
                }

                FeatureCatalogue result = FeatureCatalogueBuilder.Allocate(
                    definitions: source.Definitions.Length,
                    rules: source.Rules.Length,
                    parameters: source.Parameters.Length,
                    anchors: source.Anchors.Length,
                    slots: source.Slots.Length,
                    programLength: programLength,
                    materials: source.Materials.Length,
                    explicitPlacements: source.ExplicitPlacements.Length,
                    overrides: source.ParameterOverrides.Length,
                    allocator);

                try
                {
                    Copy(source.Parameters, result.Parameters);
                    Copy(source.Anchors, result.Anchors);
                    Copy(source.Slots, result.Slots);
                    Copy(source.Materials, result.Materials);
                    Copy(source.Rules, result.Rules);
                    Copy(source.ExplicitPlacements, result.ExplicitPlacements);
                    Copy(source.ParameterOverrides, result.ParameterOverrides);

                    int programOffset = 0;
                    for (int roleId = 0; roleId < source.Definitions.Length; roleId++)
                    {
                        FeatureDefinition definition = source.Definitions[roleId];
                        int[] program = programs[roleId];
                        definition.ProgramOffset = programOffset;
                        definition.ProgramLength = program.Length;
                        result.Definitions[roleId] = definition;
                        for (int p = 0; p < program.Length; p++)
                            result.Program[programOffset + p] = program[p];

                        if (replaceAnchor[roleId])
                        {
                            AnchorSpec anchor = result.Anchors[definition.AnchorOffset];
                            anchor.LocalPosition = doors[roleId];
                            result.Anchors[definition.AnchorOffset] = anchor;
                        }

                        programOffset += program.Length;
                    }

                    CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref result);
                    if (load != CatalogueLoadResult.Ok)
                        throw new InvalidOperationException(
                            "Semantic Kentridge grammar catalogue failed validation: " + load);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
            finally
            {
                source.Dispose();
            }
        }

        private static BuildingPlot[] PlotsByRole(SettlementPlan plan, int definitionCount)
        {
            var plots = new BuildingPlot[definitionCount];
            var seen = new bool[definitionCount];
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if ((uint)plot.RoleId >= (uint)definitionCount)
                    throw new InvalidOperationException(
                        "Kentridge plot has out-of-range role id: " + plot.RoleId);
                if (seen[plot.RoleId])
                    throw new InvalidOperationException(
                        "Kentridge plot role appears twice: " + plot.RoleId);
                plots[plot.RoleId] = plot;
                seen[plot.RoleId] = true;
            }

            for (int roleId = 0; roleId < definitionCount; roleId++)
                if (!seen[roleId])
                    throw new InvalidOperationException(
                        "Kentridge is missing stable role id: " + roleId);
            return plots;
        }

        private static int[] CopyProgram(
            in FeatureCatalogue source,
            FeatureDefinition definition)
        {
            var program = new int[definition.ProgramLength];
            for (int i = 0; i < program.Length; i++)
                program[i] = source.Program[definition.ProgramOffset + i];
            return program;
        }

        private static void Copy<T>(NativeArray<T> source, NativeArray<T> target)
            where T : struct
        {
            for (int i = 0; i < source.Length; i++)
                target[i] = source[i];
        }
    }
}
