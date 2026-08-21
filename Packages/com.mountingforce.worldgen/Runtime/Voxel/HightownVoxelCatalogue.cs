using System;
using Unity.Collections;
using MountingForce.WorldGen.Content.Hightown;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Voxel realization for Hightown.
    ///
    /// This runs the same town pass Kentridge does — ground cover, terracing, street surfaces,
    /// circulation, frontages, urban fabric — against Hightown's plan and theme. That reuse is the
    /// point: a second town built by a second pipeline would drift from the first, and the whole
    /// reason the pass was made settlement-generic is so the two towns differ by their plan and
    /// theme rather than by their code.
    ///
    /// It deliberately does not go through <see cref="KentridgeCombinedVoxelCatalogue"/>: that entry
    /// point also emits Kentridge's authored hidden spaces and asserts the Kentridge theme, neither
    /// of which Hightown has.
    /// </summary>
    public static class HightownVoxelCatalogue
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings, Allocator allocator) =>
            Build(HightownDefinition.Build(seed), settings, allocator);

        public static FeatureCatalogue Build(
            SettlementPlan plan,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!string.Equals(plan.Theme.Id, HightownDefinition.Id, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Hightown voxel realization requires a Hightown settlement plan, but received '" +
                    plan.Theme.Id + "'.",
                    nameof(plan));

            return KentridgeCombinedVoxelCatalogueCanonical.Build(
                plan.Seed,
                settings.For(plan),
                allocator);
        }
    }
}
