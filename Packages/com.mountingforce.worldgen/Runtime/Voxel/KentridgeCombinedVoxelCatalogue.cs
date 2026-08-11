using System;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Composes Kentridge generation stages into the single immutable catalogue understood by the
    /// voxel engine. Ordering is intentional and observable: themed ground cover first, roads and
    /// plaza second, prepared building plots third, frontage paths fourth, structures last.
    /// </summary>
    public static class KentridgeCombinedVoxelCatalogue
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            FeatureCatalogue groundCover =
                KentridgeGroundCoverCatalogue.Build(seed, settings, Allocator.Temp);
            FeatureCatalogue publicSpaces =
                KentridgeTownSurfaceCatalogue.Build(seed, settings, Allocator.Temp);
            FeatureCatalogue plotSurfaces =
                KentridgePlotSurfaceCatalogue.Build(seed, settings, Allocator.Temp);
            FeatureCatalogue frontagePaths =
                KentridgeFrontagePathCatalogue.Build(seed, settings, Allocator.Temp);
            FeatureCatalogue buildings =
                KentridgeVoxelCatalogue.Build(seed, settings, Allocator.Temp);

            try
            {
                FeatureCatalogue result = CatalogueLoader.Allocate(
                    definitions: groundCover.Definitions.Length
                               + publicSpaces.Definitions.Length
                               + plotSurfaces.Definitions.Length
                               + frontagePaths.Definitions.Length
                               + buildings.Definitions.Length,
                    rules: groundCover.Rules.Length
                         + publicSpaces.Rules.Length
                         + plotSurfaces.Rules.Length
                         + frontagePaths.Rules.Length
                         + buildings.Rules.Length,
                    parameters: groundCover.Parameters.Length
                              + publicSpaces.Parameters.Length
                              + plotSurfaces.Parameters.Length
                              + frontagePaths.Parameters.Length
                              + buildings.Parameters.Length,
                    anchors: groundCover.Anchors.Length
                           + publicSpaces.Anchors.Length
                           + plotSurfaces.Anchors.Length
                           + frontagePaths.Anchors.Length
                           + buildings.Anchors.Length,
                    slots: groundCover.Slots.Length
                         + publicSpaces.Slots.Length
                         + plotSurfaces.Slots.Length
                         + frontagePaths.Slots.Length
                         + buildings.Slots.Length,
                    programLength: groundCover.Program.Length
                                 + publicSpaces.Program.Length
                                 + plotSurfaces.Program.Length
                                 + frontagePaths.Program.Length
                                 + buildings.Program.Length,
                    materials: groundCover.Materials.Length
                             + publicSpaces.Materials.Length
                             + plotSurfaces.Materials.Length
                             + frontagePaths.Materials.Length
                             + buildings.Materials.Length,
                    explicitPlacements: groundCover.ExplicitPlacements.Length
                                      + publicSpaces.ExplicitPlacements.Length
                                      + plotSurfaces.ExplicitPlacements.Length
                                      + frontagePaths.ExplicitPlacements.Length
                                      + buildings.ExplicitPlacements.Length,
                    overrides: groundCover.ParameterOverrides.Length
                             + publicSpaces.ParameterOverrides.Length
                             + plotSurfaces.ParameterOverrides.Length
                             + frontagePaths.ParameterOverrides.Length
                             + buildings.ParameterOverrides.Length,
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

                Append(in groundCover, ref result,
                    ref definitionOffset, ref ruleOffset, ref parameterOffset,
                    ref anchorOffset, ref slotOffset, ref programOffset,
                    ref materialOffset, ref placementOffset, ref overrideOffset);
                Append(in publicSpaces, ref result,
                    ref definitionOffset, ref ruleOffset, ref parameterOffset,
                    ref anchorOffset, ref slotOffset, ref programOffset,
                    ref materialOffset, ref placementOffset, ref overrideOffset);
                Append(in plotSurfaces, ref result,
                    ref definitionOffset, ref ruleOffset, ref parameterOffset,
                    ref anchorOffset, ref slotOffset, ref programOffset,
                    ref materialOffset, ref placementOffset, ref overrideOffset);
                Append(in frontagePaths, ref result,
                    ref definitionOffset, ref ruleOffset, ref parameterOffset,
                    ref anchorOffset, ref slotOffset, ref programOffset,
                    ref materialOffset, ref placementOffset, ref overrideOffset);
                Append(in buildings, ref result,
                    ref definitionOffset, ref ruleOffset, ref parameterOffset,
                    ref anchorOffset, ref slotOffset, ref programOffset,
                    ref materialOffset, ref placementOffset, ref overrideOffset);

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
                groundCover.Dispose();
                publicSpaces.Dispose();
                plotSurfaces.Dispose();
                frontagePaths.Dispose();
                buildings.Dispose();
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
