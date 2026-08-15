using Unity.Collections;
using VoxelEngine.Core.Features;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    internal static partial class KentridgeCombinedVoxelCatalogueCanonical
    {
        private static void Append(in FeatureCatalogue source, ref FeatureCatalogue target,
            ref int definitionOffset, ref int ruleOffset, ref int parameterOffset,
            ref int anchorOffset, ref int slotOffset, ref int programOffset,
            ref int materialOffset, ref int placementOffset, ref int overrideOffset)
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
                if (definition.ProgramLength > 0)
                {
                    int written = KentridgeShapeProgramCompatibility.CopyDefinition(
                        source.Program,
                        definition.ProgramOffset,
                        definition.ProgramLength,
                        target.Program,
                        programOffset,
                        definition.Name.ToString());
                    definition.ProgramOffset = programOffset;
                    definition.ProgramLength = written;
                    programOffset += written;
                }
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
