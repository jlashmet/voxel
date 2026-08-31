using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features.Fixtures
{
    /// <summary>
    /// Minimal production-path structural graph: one explicit root whose typed CallSlot attaches a
    /// child exactly at the next logical region boundary. The child is never an explicit placement,
    /// so authoritative voxels in ChildRegion can only come from structural composition.
    /// </summary>
    public static class StructuralCompositionFixture
    {
        public const int RootId = 0;
        public const int ChildId = 1;
        public const uint RootPieceId = 0x510001u;
        public const uint ChildPieceId = 0x510002u;
        public const uint SocketId = 0x510101u;
        public const ulong StructuralType = 1UL << 20;
        public const byte RootMaterial = 1;
        public const byte ChildMaterial = 2;

        public static readonly int3 RootFootprint = new(64, 16, 16);
        public static readonly int3 ChildFootprint = new(16, 16, 16);
        public static readonly int3 RootPosition = new(VoxelGrid.RegionVoxelEdge - 64, 0, 0);
        public static readonly int3 ChildPosition = new(VoxelGrid.RegionVoxelEdge, 0, 0);
        public static readonly int3 RootRegion = int3.zero;
        public static readonly int3 ChildRegion = new(1, 0, 0);

        public static FeatureCatalogue Build(Allocator allocator)
        {
            int[] rootProgram = new ProgramBuilder()
                .Box(0, 0, 0, 8, 8, 8, RootMaterial, PrimitiveMode.Fill)
                .Emit(ShapeOp.CallSlot, 0, 0)
                .End()
                .Build();
            int[] childProgram = new ProgramBuilder()
                .Box(0, 0, 0, 16, 8, 16, ChildMaterial, PrimitiveMode.Fill)
                .End()
                .Build();

            var catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 2,
                rules: 1,
                parameters: 0,
                anchors: 0,
                slots: 1,
                programLength: rootProgram.Length + childProgram.Length,
                materials: 2,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            int pc = 0;
            for (int i = 0; i < rootProgram.Length; i++) catalogue.Program[pc++] = rootProgram[i];
            int childProgramOffset = pc;
            for (int i = 0; i < childProgram.Length; i++) catalogue.Program[pc++] = childProgram[i];

            catalogue.Materials[0] = RootMaterial;
            catalogue.Materials[1] = ChildMaterial;

            catalogue.Definitions[RootId] = new FeatureDefinition
            {
                Name = "structural-root",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = RootFootprint,
                MaxSlope = 0,
                Precedence = 100,
                StructuralPiece = Piece(
                    RootPieceId, StructuralSocketRole.BridgeSpan, Facing.West),
                SlotOffset = 0,
                SlotCount = 1,
                ProgramOffset = 0,
                ProgramLength = rootProgram.Length,
                MaterialOffset = 0,
                MaterialCount = 1,
                MaxPrimitives = 1,
            };

            catalogue.Definitions[ChildId] = new FeatureDefinition
            {
                Name = "structural-child",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = ChildFootprint,
                MaxSlope = 0,
                Precedence = 100,
                StructuralPiece = Piece(
                    ChildPieceId, StructuralSocketRole.BridgeSpan, Facing.West),
                SlotOffset = 1,
                SlotCount = 0,
                ProgramOffset = childProgramOffset,
                ProgramLength = childProgram.Length,
                MaterialOffset = 1,
                MaterialCount = 1,
                MaxPrimitives = 1,
            };

            catalogue.Slots[0] = new SlotSpec
            {
                Name = "east-span",
                SocketId = SocketId,
                Role = StructuralSocketRole.BridgeSpan,
                Offers = StructuralType,
                Accepts = StructuralType,
                LocalPosition = new int3(RootFootprint.x, 0, 0),
                Facing = Facing.East,
                DefinitionId = ChildId,
                LocalMin = new int3(RootFootprint.x, 0, 0),
                LocalMax = new int3(RootFootprint.x, 0, 0),
                ClearanceMin = int3.zero,
                ClearanceMax = int3.zero,
                CountMin = 1,
                CountMax = 1,
                Capacity = 1,
                Spacing = 0,
                Flags = StructuralSocketFlags.Required,
            };

            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = RootPosition,
                Orientation = 0,
            };

            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = RootId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 1,
                AcceptProbability = 65536,
                MinAltitude = -FeatureBudget.MaxFootprintVoxels,
                MaxAltitude = FeatureBudget.MaxFootprintVoxels,
                MaxSlope = 0,
                MinSpacing = 0,
                ClusterMin = 1,
                ClusterMax = 1,
                ExclusionMask = 0,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            return catalogue;
        }

        private static StructuralPieceSpec Piece(
            uint pieceId, StructuralSocketRole role, Facing facing) => new()
        {
            PieceId = pieceId,
            Role = role,
            Offers = StructuralType,
            Accepts = StructuralType,
            LocalPosition = int3.zero,
            Facing = facing,
            ClearanceMin = int3.zero,
            ClearanceMax = int3.zero,
        };
    }
}
