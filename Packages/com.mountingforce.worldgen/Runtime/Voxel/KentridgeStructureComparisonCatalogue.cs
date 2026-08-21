using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Produces a two-instance catalogue for the structure comparison showcase. Both instances
    /// share the same role, seed, orientation, altitude, footprint, and material projection; only
    /// the architecture variant differs.
    /// </summary>
    public static class KentridgeStructureComparisonCatalogue
    {
        public const int RoleCount = 17;
        public const int StageAltitudeVoxels = 32;
        public const int StageMarginVoxels = 20;
        public const int PairGapVoxels = 40;
        public const int StructureInsetVoxels = 32;

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            int roleId,
            Allocator allocator)
        {
            if ((uint)roleId >= RoleCount)
                throw new ArgumentOutOfRangeException(nameof(roleId));

            FeatureCatalogue legacy = KentridgeGrammarVoxelCatalogue.Build(
                seed, settings, KentridgeArchitectureVariant.LegacyBaseline, Allocator.Temp);
            FeatureCatalogue current = KentridgeGrammarVoxelCatalogue.Build(
                seed, settings, KentridgeArchitectureVariant.Current, Allocator.Temp);
            try
            {
                FeatureDefinition legacyDefinition = legacy.Definitions[roleId];
                int[] legacyProgram = InsetProgram(in legacy, roleId);
                int[] currentProgram = InsetProgram(in current, roleId);
                int programLength = legacyProgram.Length + currentProgram.Length;

                FeatureCatalogue pair = FeatureCatalogueBuilder.Allocate(
                    definitions: 2,
                    rules: 2,
                    parameters: 0,
                    anchors: 2,
                    slots: 0,
                    programLength: programLength,
                    materials: 0,
                    explicitPlacements: 2,
                    overrides: 0,
                    allocator);

                int comparisonWidth = legacyDefinition.Footprint.x + 2 * StructureInsetVoxels;
                int currentX = StageMarginVoxels + comparisonWidth + PairGapVoxels;
                CopyDefinition(in legacy, roleId, legacyProgram, "original", 0, 0, ref pair);
                CopyDefinition(in current, roleId, currentProgram, "modified", 1,
                    legacyProgram.Length, ref pair);

                pair.ExplicitPlacements[0] = new ExplicitPlacement
                {
                    Position = new int3(
                        StageMarginVoxels, StageAltitudeVoxels, StageMarginVoxels),
                    Orientation = 0,
                };
                pair.ExplicitPlacements[1] = new ExplicitPlacement
                {
                    Position = new int3(
                        currentX, StageAltitudeVoxels, StageMarginVoxels),
                    Orientation = 0,
                };

                for (int i = 0; i < 2; i++)
                {
                    pair.Rules[i] = new PlacementRule
                    {
                        DefinitionId = i,
                        CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                        AttemptsPerCell = 0,
                        AcceptProbability = 0,
                        MinAltitude = 0,
                        MaxAltitude = 1024,
                        MaxSlope = 0,
                        MinSpacing = 0,
                        ClusterMin = 0,
                        ClusterMax = 0,
                        ExclusionMask = 0,
                        ExplicitOffset = i,
                        ExplicitCount = 1,
                    };
                }

                CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref pair);
                if (load != CatalogueLoadResult.Ok)
                {
                    pair.Dispose();
                    throw new InvalidOperationException(
                        "Kentridge comparison catalogue failed validation: " + load);
                }

                return pair;
            }
            finally
            {
                legacy.Dispose();
                current.Dispose();
            }
        }

        public static string RoleDisplayName(int roleId)
        {
            if ((uint)roleId >= RoleCount)
                throw new ArgumentOutOfRangeException(nameof(roleId));

            return ((MountingForce.WorldGen.Content.Kentridge.KentridgeRole)roleId)
                .ToString()
                .Replace("Shop", " Shop")
                .Replace("House", " House");
        }

        private static void CopyDefinition(
            in FeatureCatalogue source,
            int sourceDefinitionId,
            int[] program,
            string suffix,
            int targetDefinitionId,
            int targetProgramOffset,
            ref FeatureCatalogue target)
        {
            FeatureDefinition definition = source.Definitions[sourceDefinitionId];
            for (int i = 0; i < program.Length; i++)
                target.Program[targetProgramOffset + i] = program[i];

            AnchorSpec anchor = source.Anchors[definition.AnchorOffset];
            anchor.LocalPosition += new int3(StructureInsetVoxels, 0, StructureInsetVoxels);
            target.Anchors[targetDefinitionId] = anchor;

            definition.Name = new FixedString64Bytes(
                KentridgeStructureComparisonCatalogue.RoleDisplayName(sourceDefinitionId)
                    .ToLowerInvariant().Replace(" ", "-") + "-" + suffix);
            definition.BasePlane = BasePlaneRule.FixedAltitude;
            definition.FixedAltitude = StageAltitudeVoxels;
            definition.Footprint += new int3(
                2 * StructureInsetVoxels, 0, 2 * StructureInsetVoxels);
            definition.ProgramOffset = targetProgramOffset;
            definition.ProgramLength = program.Length;
            definition.ParameterOffset = 0;
            definition.ParameterCount = 0;
            definition.AnchorOffset = targetDefinitionId;
            definition.AnchorCount = 1;
            definition.SlotOffset = 0;
            definition.SlotCount = 0;
            definition.MaterialOffset = 0;
            definition.MaterialCount = 0;
            target.Definitions[targetDefinitionId] = definition;
        }

        private static int[] InsetProgram(in FeatureCatalogue source, int definitionId)
        {
            FeatureDefinition definition = source.Definitions[definitionId];
            var program = new int[definition.ProgramLength];
            for (int i = 0; i < program.Length; i++)
                program[i] = source.Program[definition.ProgramOffset + i];
            return ShapeProgramComposition.Translate(
                program, new int3(StructureInsetVoxels, 0, StructureInsetVoxels));
        }
    }
}
