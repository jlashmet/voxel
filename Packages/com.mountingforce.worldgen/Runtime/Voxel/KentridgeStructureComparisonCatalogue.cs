using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

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
                FeatureDefinition currentDefinition = current.Definitions[roleId];
                int programLength = legacyDefinition.ProgramLength + currentDefinition.ProgramLength;

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

                int currentX = StageMarginVoxels + legacyDefinition.Footprint.x + PairGapVoxels;
                CopyDefinition(in legacy, roleId, "original", 0, 0, ref pair);
                CopyDefinition(in current, roleId, "modified", 1,
                    legacyDefinition.ProgramLength, ref pair);

                pair.ExplicitPlacements[0] = new ExplicitPlacement
                {
                    Position = new int3(StageMarginVoxels, 0, StageMarginVoxels),
                    Orientation = 0,
                };
                pair.ExplicitPlacements[1] = new ExplicitPlacement
                {
                    Position = new int3(currentX, 0, StageMarginVoxels),
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
            string suffix,
            int targetDefinitionId,
            int targetProgramOffset,
            ref FeatureCatalogue target)
        {
            FeatureDefinition definition = source.Definitions[sourceDefinitionId];
            for (int i = 0; i < definition.ProgramLength; i++)
                target.Program[targetProgramOffset + i] =
                    source.Program[definition.ProgramOffset + i];

            AnchorSpec anchor = source.Anchors[definition.AnchorOffset];
            target.Anchors[targetDefinitionId] = anchor;

            definition.Name = new FixedString64Bytes(
                KentridgeStructureComparisonCatalogue.RoleDisplayName(sourceDefinitionId)
                    .ToLowerInvariant().Replace(" ", "-") + "-" + suffix);
            definition.BasePlane = BasePlaneRule.FixedAltitude;
            definition.FixedAltitude = StageAltitudeVoxels;
            definition.ProgramOffset = targetProgramOffset;
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
    }
}
