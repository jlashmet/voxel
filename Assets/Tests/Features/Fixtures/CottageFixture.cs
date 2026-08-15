using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features.Fixtures
{
    /// <summary>
    /// A hand-built catalogue holding one cottage definition.
    ///
    /// Deliberately written in code rather than parsed from the authoring format: the format is a
    /// separate concern with its own failure modes, and a fixture that depends on it cannot tell
    /// you whether the generator or the parser broke.
    ///
    /// The shape program comes from <see cref="CottageProgram"/>, written as opcodes by hand.
    /// Composition slots stay empty until US4.
    /// </summary>
    public static class CottageFixture
    {
        public const int CottageId = 0;

        public const byte MaterialStone = 1;
        public const byte MaterialWood = 2;
        public const byte MaterialGlass = 4;

        /// <summary>Parameter indices, in the order the definition declares them.</summary>
        public const int ParamWidth = 0;
        public const int ParamDepth = 1;
        public const int ParamWallHeight = 2;
        public const int ParamRoofPitch = 3;

        public static FeatureCatalogue Build(Allocator allocator)
        {
            var program = CottageProgram.Build();

            var catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 4,
                anchors: 2,
                slots: 0,
                programLength: program.Length,
                materials: 3,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            for (var i = 0; i < program.Length; i++) catalogue.Program[i] = program[i];

            catalogue.Parameters[ParamWidth] = new ParameterSpec
            {
                Name = "width", Min = 48, Max = 88, Quantum = 8, Default = 64,
            };
            catalogue.Parameters[ParamDepth] = new ParameterSpec
            {
                Name = "depth", Min = 48, Max = 88, Quantum = 8, Default = 64,
            };
            catalogue.Parameters[ParamWallHeight] = new ParameterSpec
            {
                Name = "wallHeight", Min = 24, Max = 40, Quantum = 4, Default = 32,
            };
            catalogue.Parameters[ParamRoofPitch] = new ParameterSpec
            {
                Name = "roofPitch", Min = 4, Max = 12, Quantum = 2, Default = 8,
            };

            catalogue.Anchors[0] = new AnchorSpec
            {
                Name = "door",
                LocalPosition = new int3(32, 0, 0),
                Facing = Facing.South,
                SnapToGround = true,
            };
            catalogue.Anchors[1] = new AnchorSpec
            {
                Name = "hearth",
                LocalPosition = new int3(32, 4, 32),
                Facing = Facing.Up,
            };

            catalogue.Materials[0] = MaterialStone;
            catalogue.Materials[1] = MaterialWood;
            catalogue.Materials[2] = MaterialGlass;

            catalogue.Definitions[CottageId] = new FeatureDefinition
            {
                Name = "cottage",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.LowestGround,

                // 9.6 m x 8 m x 9.6 m. Comfortably inside the budget ceiling, which every region
                // in the world pays for.
                Footprint = new int3(96, 80, 96),

                MaxSlope = 3,
                Precedence = 100,

                ParameterOffset = 0, ParameterCount = 4,
                AnchorOffset = 0, AnchorCount = 2,
                SlotOffset = 0, SlotCount = 0,
                ProgramOffset = 0, ProgramLength = program.Length,
                MaterialOffset = 0, MaterialCount = 3,

                MaxPrimitives = 64,
            };

            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = new int3(2048, 0, 3072),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = CottageId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 3,
                AcceptProbability = 12000,
                MinAltitude = 180,
                MaxAltitude = 320,
                MaxSlope = 3,
                MinSpacing = 128,
                ClusterMin = 4,
                ClusterMax = 9,
                ExclusionMask = 0,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            return catalogue;
        }
    }
}
