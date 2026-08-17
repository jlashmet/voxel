using System;
using Unity.Collections;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Deterministically concatenates already-authored feature catalogues into one immutable world
    /// catalogue. Pool offsets are rebased exactly once here so application composition does not
    /// duplicate catalogue plumbing.
    /// </summary>
    public static class FeatureCatalogueComposer
    {
        /// <summary>Convenience overload for the common application-composition case.</summary>
        public static FeatureCatalogue Combine(
            in FeatureCatalogue first,
            in FeatureCatalogue second,
            Allocator allocator)
        {
            return Concatenate(new[] { first, second }, allocator);
        }

        public static FeatureCatalogue Concatenate(
            FeatureCatalogue[] sources,
            Allocator allocator)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("At least one source catalogue is required.", nameof(sources));

            int definitions = 0, rules = 0, parameters = 0, anchors = 0, slots = 0;
            int programs = 0, materials = 0, placements = 0, overrides = 0;
            for (int i = 0; i < sources.Length; i++)
            {
                FeatureCatalogue source = sources[i];
                if (!source.IsCreated)
                    throw new ArgumentException("Every source catalogue must be created.", nameof(sources));

                definitions += source.Definitions.Length;
                rules += source.Rules.Length;
                parameters += source.Parameters.Length;
                anchors += source.Anchors.Length;
                slots += source.Slots.Length;
                programs += source.Program.Length;
                materials += source.Materials.Length;
                placements += source.ExplicitPlacements.Length;
                overrides += source.ParameterOverrides.Length;
            }

            FeatureCatalogue result = FeatureCatalogueBuilder.Allocate(
                definitions, rules, parameters, anchors, slots, programs,
                materials, placements, overrides, allocator);

            int d = 0, r = 0, p = 0, a = 0, s = 0;
            int code = 0, m = 0, e = 0, o = 0;
            for (int i = 0; i < sources.Length; i++)
                Append(in sources[i], ref result,
                    ref d, ref r, ref p, ref a, ref s,
                    ref code, ref m, ref e, ref o);

            CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref result);
            if (load != CatalogueLoadResult.Ok)
            {
                result.Dispose();
                throw new InvalidOperationException(
                    "Composed feature catalogue failed validation: " + load);
            }

            return result;
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
            Copy(source.Materials, target.Materials, materialOffset);
            Copy(source.ParameterOverrides, target.ParameterOverrides, overrideOffset);

            for (int i = 0; i < source.Definitions.Length; i++)
            {
                FeatureDefinition definition = source.Definitions[i];
                if (definition.ParameterCount > 0) definition.ParameterOffset += parameterOffset;
                if (definition.AnchorCount > 0) definition.AnchorOffset += anchorOffset;
                if (definition.SlotCount > 0) definition.SlotOffset += slotOffset;
                if (definition.MaterialCount > 0) definition.MaterialOffset += materialOffset;

                if (definition.ProgramLength > 0)
                {
                    for (int pc = 0; pc < definition.ProgramLength; pc++)
                        target.Program[programOffset + pc] =
                            source.Program[definition.ProgramOffset + pc];
                    definition.ProgramOffset = programOffset;
                    programOffset += definition.ProgramLength;
                }

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
            materialOffset += source.Materials.Length;
            placementOffset += source.ExplicitPlacements.Length;
            overrideOffset += source.ParameterOverrides.Length;
        }

        private static void Copy<T>(NativeArray<T> source, NativeArray<T> target, int offset)
            where T : struct
        {
            for (int i = 0; i < source.Length; i++)
                target[offset + i] = source[i];
        }
    }
}
