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
    /// geometry policy to the generic voxel realiser. No Kentridge policy is required by
    /// ArchitectureGeometryCatalogue itself.
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
                    StructureForm form = style.ResolveStructure(intent, plan.Theme, seed);
                    profiles[plot.RoleId] = style.ResolveGeometry(intent, form);
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
