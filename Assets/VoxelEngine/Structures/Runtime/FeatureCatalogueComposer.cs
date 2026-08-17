using System;
using Unity.Collections;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Build-time composition for already-authored immutable feature catalogues. Offsets and
    /// definition references are remapped once here so application composition roots do not need
    /// archetype-specific merge code.
    /// </summary>
    public static class FeatureCatalogueComposer
    {
        public static FeatureCatalogue Combine(
            in FeatureCatalogue first,
            in FeatureCatalogue second,
            Allocator allocator)
        {
            FeatureCatalogue combined = FeatureCatalogueBuilder.Allocate(
                definitions: first.Definitions.Length + second.Definitions.Length,
                rules: first.Rules.Length + second.Rules.Length,
                parameters: first.Parameters.Length + second.Parameters.Length,
                anchors: first.Anchors.Length + second.Anchors.Length,
                slots: first.Slots.Length + second.Slots.Length,
                programLength: first.Program.Length + second.Program.Length,
                materials: first.Materials.Length + second.Materials.Length,
                explicitPlacements: first.ExplicitPlacements.Length + second.ExplicitPlacements.Length,
                overrides: first.ParameterOverrides.Length + second.ParameterOverrides.Length,
                allocator);

            var offsets = new Offsets();
            Append(in first, ref combined, ref offsets);
            Append(in second, ref combined, ref offsets);

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref combined);
            if (result == CatalogueLoadResult.Ok)
                return combined;

            combined.Dispose();
            throw new InvalidOperationException(
                "Combined feature catalogue failed validation: " + result);
        }

        private static void Append(
            in FeatureCatalogue source,
            ref FeatureCatalogue target,
            ref Offsets offsets)
        {
            Copy(source.Parameters, target.Parameters, offsets.Parameters);
            Copy(source.Anchors, target.Anchors, offsets.Anchors);
            Copy(source.Slots, target.Slots, offsets.Slots);
            Copy(source.Program, target.Program, offsets.Program);
            Copy(source.Materials, target.Materials, offsets.Materials);
            Copy(source.ExplicitPlacements, target.ExplicitPlacements, offsets.Placements);
            Copy(source.ParameterOverrides, target.ParameterOverrides, offsets.Overrides);

            for (int i = 0; i < source.Definitions.Length; i++)
            {
                FeatureDefinition definition = source.Definitions[i];
                if (definition.ParameterCount > 0) definition.ParameterOffset += offsets.Parameters;
                if (definition.AnchorCount > 0) definition.AnchorOffset += offsets.Anchors;
                if (definition.SlotCount > 0) definition.SlotOffset += offsets.Slots;
                if (definition.ProgramLength > 0) definition.ProgramOffset += offsets.Program;
                if (definition.MaterialCount > 0) definition.MaterialOffset += offsets.Materials;
                target.Definitions[offsets.Definitions + i] = definition;
            }

            for (int i = 0; i < source.Rules.Length; i++)
            {
                PlacementRule rule = source.Rules[i];
                rule.DefinitionId += offsets.Definitions;
                if (rule.ExplicitCount > 0) rule.ExplicitOffset += offsets.Placements;
                target.Rules[offsets.Rules + i] = rule;
            }

            for (int i = 0; i < source.Slots.Length; i++)
            {
                SlotSpec slot = source.Slots[i];
                slot.DefinitionId += offsets.Definitions;
                target.Slots[offsets.Slots + i] = slot;
            }

            for (int i = 0; i < source.ExplicitPlacements.Length; i++)
            {
                ExplicitPlacement placement = source.ExplicitPlacements[i];
                if (placement.OverrideCount > 0) placement.OverrideOffset += offsets.Overrides;
                target.ExplicitPlacements[offsets.Placements + i] = placement;
            }

            offsets.Definitions += source.Definitions.Length;
            offsets.Rules += source.Rules.Length;
            offsets.Parameters += source.Parameters.Length;
            offsets.Anchors += source.Anchors.Length;
            offsets.Slots += source.Slots.Length;
            offsets.Program += source.Program.Length;
            offsets.Materials += source.Materials.Length;
            offsets.Placements += source.ExplicitPlacements.Length;
            offsets.Overrides += source.ParameterOverrides.Length;
        }

        private static void Copy<T>(NativeArray<T> source, NativeArray<T> target, int offset)
            where T : struct
        {
            for (int i = 0; i < source.Length; i++)
                target[offset + i] = source[i];
        }

        private struct Offsets
        {
            public int Definitions;
            public int Rules;
            public int Parameters;
            public int Anchors;
            public int Slots;
            public int Program;
            public int Materials;
            public int Placements;
            public int Overrides;
        }
    }
}
