using System;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Composes Kentridge's public-space pass before its building pass while presenting the voxel
    /// engine with the single immutable catalogue it already understands.
    /// </summary>
    public static class KentridgeCombinedVoxelCatalogue
    {
        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            FeatureCatalogue surfaces =
                KentridgeTownSurfaceCatalogue.Build(seed, settings, Allocator.Temp);
            FeatureCatalogue buildings =
                KentridgeVoxelCatalogue.Build(seed, settings, Allocator.Temp);

            try
            {
                FeatureCatalogue result = CatalogueLoader.Allocate(
                    definitions: surfaces.Definitions.Length + buildings.Definitions.Length,
                    rules: surfaces.Rules.Length + buildings.Rules.Length,
                    parameters: surfaces.Parameters.Length + buildings.Parameters.Length,
                    anchors: surfaces.Anchors.Length + buildings.Anchors.Length,
                    slots: surfaces.Slots.Length + buildings.Slots.Length,
                    programLength: surfaces.Program.Length + buildings.Program.Length,
                    materials: surfaces.Materials.Length + buildings.Materials.Length,
                    explicitPlacements:
                        surfaces.ExplicitPlacements.Length + buildings.ExplicitPlacements.Length,
                    overrides:
                        surfaces.ParameterOverrides.Length + buildings.ParameterOverrides.Length,
                    allocator);

                CopyPools(in surfaces, in buildings, ref result);
                CopyDefinitions(in surfaces, in buildings, ref result);
                CopyRules(in surfaces, in buildings, ref result);
                CopyPlacements(in surfaces, in buildings, ref result);
                CopySlots(in surfaces, in buildings, ref result);

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
                surfaces.Dispose();
                buildings.Dispose();
            }
        }

        private static void CopyPools(
            in FeatureCatalogue surfaces,
            in FeatureCatalogue buildings,
            ref FeatureCatalogue result)
        {
            Copy(surfaces.Parameters, result.Parameters, 0);
            Copy(buildings.Parameters, result.Parameters, surfaces.Parameters.Length);

            Copy(surfaces.Anchors, result.Anchors, 0);
            Copy(buildings.Anchors, result.Anchors, surfaces.Anchors.Length);

            Copy(surfaces.Program, result.Program, 0);
            Copy(buildings.Program, result.Program, surfaces.Program.Length);

            Copy(surfaces.Materials, result.Materials, 0);
            Copy(buildings.Materials, result.Materials, surfaces.Materials.Length);

            Copy(surfaces.ParameterOverrides, result.ParameterOverrides, 0);
            Copy(buildings.ParameterOverrides, result.ParameterOverrides,
                 surfaces.ParameterOverrides.Length);
        }

        private static void CopyDefinitions(
            in FeatureCatalogue surfaces,
            in FeatureCatalogue buildings,
            ref FeatureCatalogue result)
        {
            for (int i = 0; i < surfaces.Definitions.Length; i++)
                result.Definitions[i] = surfaces.Definitions[i];

            int definitionOffset = surfaces.Definitions.Length;
            int parameterOffset = surfaces.Parameters.Length;
            int anchorOffset = surfaces.Anchors.Length;
            int slotOffset = surfaces.Slots.Length;
            int programOffset = surfaces.Program.Length;
            int materialOffset = surfaces.Materials.Length;

            for (int i = 0; i < buildings.Definitions.Length; i++)
            {
                FeatureDefinition definition = buildings.Definitions[i];
                if (definition.ParameterCount > 0) definition.ParameterOffset += parameterOffset;
                if (definition.AnchorCount > 0) definition.AnchorOffset += anchorOffset;
                if (definition.SlotCount > 0) definition.SlotOffset += slotOffset;
                if (definition.ProgramLength > 0) definition.ProgramOffset += programOffset;
                if (definition.MaterialCount > 0) definition.MaterialOffset += materialOffset;
                result.Definitions[definitionOffset + i] = definition;
            }
        }

        private static void CopyRules(
            in FeatureCatalogue surfaces,
            in FeatureCatalogue buildings,
            ref FeatureCatalogue result)
        {
            for (int i = 0; i < surfaces.Rules.Length; i++)
                result.Rules[i] = surfaces.Rules[i];

            int ruleOffset = surfaces.Rules.Length;
            int definitionOffset = surfaces.Definitions.Length;
            int placementOffset = surfaces.ExplicitPlacements.Length;

            for (int i = 0; i < buildings.Rules.Length; i++)
            {
                PlacementRule rule = buildings.Rules[i];
                rule.DefinitionId += definitionOffset;
                if (rule.ExplicitCount > 0) rule.ExplicitOffset += placementOffset;
                result.Rules[ruleOffset + i] = rule;
            }
        }

        private static void CopyPlacements(
            in FeatureCatalogue surfaces,
            in FeatureCatalogue buildings,
            ref FeatureCatalogue result)
        {
            for (int i = 0; i < surfaces.ExplicitPlacements.Length; i++)
                result.ExplicitPlacements[i] = surfaces.ExplicitPlacements[i];

            int placementOffset = surfaces.ExplicitPlacements.Length;
            int overrideOffset = surfaces.ParameterOverrides.Length;

            for (int i = 0; i < buildings.ExplicitPlacements.Length; i++)
            {
                ExplicitPlacement placement = buildings.ExplicitPlacements[i];
                if (placement.OverrideCount > 0) placement.OverrideOffset += overrideOffset;
                result.ExplicitPlacements[placementOffset + i] = placement;
            }
        }

        private static void CopySlots(
            in FeatureCatalogue surfaces,
            in FeatureCatalogue buildings,
            ref FeatureCatalogue result)
        {
            for (int i = 0; i < surfaces.Slots.Length; i++)
                result.Slots[i] = surfaces.Slots[i];

            int slotOffset = surfaces.Slots.Length;
            int definitionOffset = surfaces.Definitions.Length;

            for (int i = 0; i < buildings.Slots.Length; i++)
            {
                SlotSpec slot = buildings.Slots[i];
                slot.DefinitionId += definitionOffset;
                result.Slots[slotOffset + i] = slot;
            }
        }

        private static void Copy<T>(NativeArray<T> source, NativeArray<T> target, int offset)
            where T : struct
        {
            for (int i = 0; i < source.Length; i++)
                target[offset + i] = source[i];
        }
    }
}
