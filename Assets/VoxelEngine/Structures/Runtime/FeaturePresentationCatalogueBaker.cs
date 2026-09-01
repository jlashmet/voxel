using System;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Builds derived presentation data from the same immutable catalogue placements consumed by
    /// region generation. This is a catalogue-level lifecycle hook: producers author normal feature
    /// definitions/placements only, while presentation baking remains a generic consequence of that
    /// canonical data rather than a per-object registration step.
    /// </summary>
    public static class FeaturePresentationCatalogueBaker
    {
        /// <summary>
        /// Creates a sparse presentation manifest for every finite explicit feature placement in the
        /// catalogue. Structural roots are expanded through the canonical composition planner first,
        /// so their independently bounded child pieces follow the same presentation path as ordinary
        /// features without requiring resident voxel regions.
        /// </summary>
        public static FeaturePresentationManifest Build(
            in FeatureCatalogue catalogue,
            uint worldSeed,
            int sectorSizeVoxels = FeaturePresentationManifest.DefaultSectorSizeVoxels)
        {
            var manifest = new FeaturePresentationManifest(sectorSizeVoxels);
            Populate(in catalogue, worldSeed, manifest);
            return manifest;
        }

        /// <summary>
        /// Replays the catalogue's canonical finite placements into <paramref name="manifest"/>.
        /// No storage/read source is accepted here by design: presentation derivation must remain
        /// independent of detailed-region generation and residency.
        /// </summary>
        public static int Populate(
            in FeatureCatalogue catalogue,
            uint worldSeed,
            FeaturePresentationManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (!catalogue.IsCreated) return 0;

            var baker = new FeaturePresentationBaker();
            using var structuralInstances = new NativeList<StructuralInstance>(16, Allocator.Temp);
            int bakedCount = 0;

            for (int ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                PlacementRule rule = catalogue.Rules[ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)catalogue.DefinitionCount)
                    continue;

                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                for (int localPlacementIndex = 0; localPlacementIndex < rule.ExplicitCount; localPlacementIndex++)
                {
                    int placementIndex = rule.ExplicitOffset + localPlacementIndex;
                    if ((uint)placementIndex >= (uint)catalogue.ExplicitPlacements.Length)
                        continue;

                    ExplicitPlacement placement = catalogue.ExplicitPlacements[placementIndex];
                    if (definition.StructuralPiece.PieceId == 0)
                    {
                        if (baker.TryBake(
                                in catalogue,
                                worldSeed,
                                rule.DefinitionId,
                                in placement,
                                out FeaturePresentationBake bake))
                        {
                            manifest.Upsert(bake);
                            bakedCount++;
                        }

                        continue;
                    }

                    StructuralCompositionReport composition = StructuralCompositionPlanner.ExpandRoot(
                        in catalogue,
                        worldSeed,
                        rule.DefinitionId,
                        in placement,
                        structuralInstances);
                    if (composition.Result != StructuralCompositionResult.Ok)
                        continue;

                    for (int instanceIndex = 0; instanceIndex < structuralInstances.Length; instanceIndex++)
                    {
                        StructuralInstance instance = structuralInstances[instanceIndex];
                        if ((uint)instance.DefinitionId >= (uint)catalogue.DefinitionCount)
                            continue;

                        ExplicitPlacement childPlacement = instance.Placement;
                        if (!baker.TryBake(
                                in catalogue,
                                worldSeed,
                                instance.DefinitionId,
                                in childPlacement,
                                out FeaturePresentationBake childBake))
                            continue;

                        manifest.Upsert(childBake);
                        bakedCount++;
                    }
                }
            }

            return bakedCount;
        }
    }
}
