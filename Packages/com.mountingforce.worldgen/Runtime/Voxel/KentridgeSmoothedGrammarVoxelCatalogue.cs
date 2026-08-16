using System;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Kentridge's composition adapter. Settlement and architecture resolve the same semantic forms
    /// used by gameplay; the selected architecture style supplies renderer-neutral low-level
    /// geometry policy to the generic voxel realiser.
    ///
    /// Generated houses already author semantic foundation/shell/opening/detail/roof bytecode through
    /// ArchitectureShapeProgramBuilder, so they deliberately bypass compatibility rewriting here.
    /// Only the copied bespoke legacy source programs still need ArchitectureGeometryCatalogue to
    /// infer roles until those landmark programs are migrated to semantic authoring too.
    /// </summary>
    internal static class KentridgeSmoothedGrammarVoxelCatalogue
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
                var profiles = new StructureGeometryProfile[source.Definitions.Length];

                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if ((uint)plot.RoleId >= (uint)profiles.Length)
                        throw new InvalidOperationException(
                            "Kentridge role is outside the grammar catalogue: " + plot.RoleId);

                    StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                    IArchitectureStyleCompiler style =
                        BuiltInArchitectureStyles.Registry.Require(intent.StyleId);
                    StructureForm form = ArchitectureCompiler.Resolve(
                        intent,
                        plan.Theme,
                        seed,
                        BuiltInArchitectureStyles.Registry);

                    profiles[plot.RoleId] = form.IsGenerated
                        ? StructureGeometryProfile.Sharp
                        : style.ResolveGeometry(intent, form);
                }

                return ArchitectureGeometryCatalogue.Apply(
                    in source,
                    plan.Theme,
                    settings,
                    profiles,
                    allocator);
            }
            finally
            {
                source.Dispose();
            }
        }
    }
}
