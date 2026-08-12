using System;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Composes Kentridge's deterministic generation stages into the single immutable catalogue
    /// consumed by the voxel engine. Stage order is semantic: terrain first, circulation and built
    /// hillside fabric next, local plot/detail passes after that, and stable gameplay structures last.
    /// </summary>
    public static class KentridgeCombinedVoxelCatalogue
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            FeatureCatalogue[] stages =
            {
                KentridgeGroundCoverCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeDistrictTerraceCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeDirectedTownSurfaceCatalogue.Build(seed, settings, Allocator.Temp),

                // Secondary contour circulation belongs to urban organisation, not stable gameplay
                // streets. It links the central upper ascent to the east ridge without moving roles.
                KentridgeUrbanCirculationCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalConnectorCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeTerraceSupportCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(
                    seed, settings, Allocator.Temp),
                KentridgeFrontagePathCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeStreetDressingCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeVerticalPlacementAdapter.BuildPlotDressing(
                    seed, settings, Allocator.Temp),
                KentridgeVerticalPlacementAdapter.BuildTownDressing(
                    seed, settings, Allocator.Temp),

                // The semantic block plan compiles to varied anonymous buildings. Named large anchors
                // get derived downhill service bays where their plot support would otherwise remain
                // bare, then the access layer joins embedded door levels back to upper courts.
                KentridgeUrbanFabricCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeAnchorUndercroftCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeUrbanAccessCatalogue.Build(seed, settings, Allocator.Temp),

                // Secondary hard architecture comes immediately before gameplay buildings. Where an
                // embedded dwelling touches a named building, the stable role building wins last.
                KentridgeHillsideArchitectureCatalogue.Build(seed, settings, Allocator.Temp),
                KentridgeGrammarVoxelCatalogue.Build(seed, settings, Allocator.Temp),
            };

            try
            {
                int definitions = 0;
                int rules = 0;
                int parameters = 0;
                int anchors = 0;
                int slots = 0;
                int programLength = 0;
                int materials = 0;
                int explicitPlacements = 0;
                int overrides = 0;

                for (int i = 0; i < stages.Length; i++)
                {
                    FeatureCatalogue stage = stages[i];
                    definitions += stage.Definitions.Length;
                    rules += stage.Rules.Length;
                    parameters += stage.Parameters.Length;
                    anchors += stage.Anchors.Length;
                    slots += stage.Slots.Length;
                    programLength += stage.Program.Length;
                    materials += stage.Materials.Length;
                    explicitPlacements += stage.ExplicitPlacements.Length;
                    overrides += stage.ParameterOverrides.Length;
                }

                FeatureCatalogue result = CatalogueLoader.Allocate(
                    definitions,
                    rules,
                    parameters,
                    anchors,
                    slots,
                    programLength,
                    materials,
                    explicitPlacements,
                    overrides,
                    allocator);

                int definitionOffset = 0;
                int ruleOffset = 0;
                int parameterOffset = 0;
                int anchorOffset = 0;
                int slotOffset = 0;
                int programOffset = 0;
                int materialOffset = 0;
                int placementOffset = 0;
                int overrideOffset = 0;

                for (int i = 0; i < stages.Length; i++)
                {
                    FeatureCatalogue stage = stages[i];
                    Append(in stage, ref result,
                        ref definitionOffset, ref ruleOffset, ref parameterOffset,
                        ref anchorOffset, ref slotOffset, ref programOffset,
                        ref materialOffset, ref placementOffset, ref overrideOffset);
                }

                CatalogueLoadResult load = CatalogueLoader.Finalise(ref result);
                if (load != CatalogueLoadResult.Ok)
                {
                    result.Dispose();
                    throw new InvalidOperationException(
                        "Combined Kentridge catalogue failed validation: " + load);
                }

                return result;
            }
            finally
            {
                for (int i = 0; i < stages.Length; i++)
                    if (stages[i].IsCreated) stages[i].Dispose();
            }
        }

        private static void Append(
            in FeatureCatalogue source,
            ref FeatureCatalogue target,
            ref int definitionOffset,
            ref int ruleOffset,
            ref int parameterOffset,
            ref int anchorOffset,
            ref int slotOffset,
            ref int programOffset,
            ref int materialOffset,
            ref int placementOffset,
            ref int overrideOffset)
        {
            Copy(source.Parameters, target.Parameters, parameterOffset);
            Copy(source.Anchors, target.Anchors, anchorOffset);
            Copy(source.Program, target.Program, programOffset);
            Copy(source.Materials, target.Materials, materialOffset);
            Copy(source.ParameterOverrides, target.ParameterOverrides, overrideOffset);

            for (int i = 0; i < source.Definitions.Length; i++)
            {
                FeatureDefinition definition = source.Definitions[i];
                if (definition.ParameterCount > 0) definition.ParameterOffset += parameterOffset;
                if (definition.AnchorCount > 0) definition.AnchorOffset += anchorOffset;
                if (definition.SlotCount > 0) definition.SlotOffset += slotOffset;
                if (definition.ProgramLength > 0) definition.ProgramOffset += programOffset;
                if (definition.MaterialCount > 0) definition.MaterialOffset += materialOffset;
                target.Definitions[definitionOffset + i] = definition;
            }

            for (int i = 0; i < source.Rules.Length; i++)
            {
                PlacementRule rule = source.Rules[i];
                rule.DefinitionId += definitionOffset;
                if (rule.ExplicitCount > 0) rule.ExplicitOffset += placementOffset;
                target.Rules[ruleOffset + i] = rule;
            }

            for (int i = 0; i < source.ExplicitPlacements.Length; i++)
            {
                ExplicitPlacement placement = source.ExplicitPlacements[i];
                if (placement.OverrideCount > 0) placement.OverrideOffset += overrideOffset;
                target.ExplicitPlacements[placementOffset + i] = placement;
            }

            for (int i = 0; i < source.Slots.Length; i++)
            {
                SlotSpec slot = source.Slots[i];
                slot.DefinitionId += definitionOffset;
                target.Slots[slotOffset + i] = slot;
            }

            definitionOffset += source.Definitions.Length;
            ruleOffset += source.Rules.Length;
            parameterOffset += source.Parameters.Length;
            anchorOffset += source.Anchors.Length;
            slotOffset += source.Slots.Length;
            programOffset += source.Program.Length;
            materialOffset += source.Materials.Length;
            placementOffset += source.ExplicitPlacements.Length;
            overrideOffset += source.ParameterOverrides.Length;
        }

        private static void Copy<T>(NativeArray<T> source, NativeArray<T> target, int offset)
            where T : struct
        {
            for (int i = 0; i < source.Length; i++) target[offset + i] = source[i];
        }
    }
}
