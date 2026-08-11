using System;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Composes Kentridge generation stages into the single immutable catalogue understood by the
    /// voxel engine. Ordering is intentional and observable: roads/plaza first, prepared building
    /// plots second, structures last.
    /// </summary>
    public static class KentridgeCombinedVoxelCatalogue
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            FeatureCatalogue publicSpaces =
                KentridgeTownSurfaceCatalogue.Build(seed, settings, Allocator.Temp);
            FeatureCatalogue plotSurfaces =
                KentridgePlotSurfaceCatalogue.Build(seed, settings, Allocator.Temp);
            FeatureCatalogue buildings =
                KentridgeVoxelCatalogue.Build(seed, settings, Allocator.Temp);

            try
            {
                FeatureCatalogue result = CatalogueLoader.Allocate(
                    definitions: publicSpaces.Definitions.Length
                               + plotSurfaces.Definitions.Length
                               + buildings.Definitions.Length,
                    rules: publicSpaces.Rules.Length
                         + plotSurfaces.Rules.Length
                         + buildings.Rules.Length,
                    parameters: publicSpaces.Parameters.Length
                              + plotSurfaces.Parameters.Length
                              + buildings.Parameters.Length,
                    anchors: publicSpaces.Anchors.Length
                           + plotSurfaces.Anchors.Length
                           + buildings.Anchors.Length,
                    slots: publicSpaces.Slots.Length
                         + plotSurfaces.Slots.Length
                         + buildings.Slots.Length,
                    programLength: publicSpaces.Program.Length
                                 + plotSurfaces.Program.Length
                                 + buildings.Program.Length,
                    materials: publicSpaces.Materials.Length
                             + plotSurfaces.Materials.Length
                             + buildings.Materials.Length,
                    explicitPlacements: publicSpaces.ExplicitPlacements.Length
                                      + plotSurfaces.ExplicitPlacements.Length
                                      + buildings.ExplicitPlacements.Length,
                    overrides: publicSpaces.ParameterOverrides.Length
                             + plotSurfaces.ParameterOverrides.Length
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

                Append(in publicSpaces, ref result,
                    ref definitionOffset, ref ruleOffset, ref parameterOffset,
                    ref anchorOffset, ref slotOffset, ref programOffset,
                    ref materialOffset, ref placementOffset, ref overrideOffset);
                Append(in plotSurfaces, ref result,
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
                publicSpaces.Dispose();
                plotSurfaces.Dispose();
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
