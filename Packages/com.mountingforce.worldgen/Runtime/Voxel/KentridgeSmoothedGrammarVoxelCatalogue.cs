using System;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Kentridge's composition adapter. Settlement and architecture resolve the same semantic forms
    /// used by gameplay; this backend supplies reusable low-level geometry profiles to the generic
    /// voxel realiser. No Kentridge policy is required by ArchitectureGeometryCatalogue itself.
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
                IStructureGeometryProfileResolver resolver =
                    HumanSettlementGeometryProfileResolver.Instance;

                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if ((uint)plot.RoleId >= (uint)profiles.Length)
                        throw new InvalidOperationException(
                            "Kentridge role is outside the grammar catalogue: " + plot.RoleId);

                    StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
                    StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, seed);
                    profiles[plot.RoleId] = resolver.Resolve(intent, form);
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
