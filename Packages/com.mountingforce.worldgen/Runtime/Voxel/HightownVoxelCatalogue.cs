using System;
using Unity.Collections;
using MountingForce.WorldGen.Content.Hightown;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Voxel realization for Hightown.
    ///
    /// This runs the settlement-bound core of Kentridge's town pass — ground cover, street
    /// surfaces, plot preparation, frontage paths, market treatment, dressing, and shared
    /// structures — against Hightown's plan and theme. Kentridge's absolute authored terraces,
    /// circulation, and urban landmarks remain Kentridge content; including them here would emit a
    /// second copy at Kentridge's coordinates rather than adapt them to Hightown.
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
