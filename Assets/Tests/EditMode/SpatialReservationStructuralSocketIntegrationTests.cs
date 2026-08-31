using System;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SpatialReservationStructuralSocketIntegrationTests
    {
        private const uint Seed = 0x53504345u;
        private static readonly ReservationBoundsDm Window =
            new ReservationBoundsDm(-20, -20, -20, 40, 40, 40);

        [Test]
        public void AcceptedTypedSocketUsesSharedReservationClearanceAgainstExternalWorldClaims()
        {
            FeatureCatalogue catalogue = BuildTypedSocketCatalogue(Allocator.Temp);
            try
            {
                using var instances = new NativeList<StructuralInstance>(Allocator.Temp);
                using var decisions = new NativeList<StructuralAttachmentDecision>(Allocator.Temp);
                StructuralCompositionReport report = StructuralCompositionPlanner.ExpandRoot(
                    in catalogue,
                    Seed,
                    0,
                    catalogue.ExplicitPlacements[0],
                    instances,
                    decisions);

                Assert.That(report.Result, Is.EqualTo(StructuralCompositionResult.Ok));
                Assert.That(report.ChildCount, Is.EqualTo(1));
                Assert.That(decisions.Length, Is.EqualTo(1));
                Assert.That(decisions[0].Accepted, Is.True);

                SlotSpec socket = catalogue.Slots[0];
                StructuralAttachmentDecision decision = decisions[0];
                byte parentOrientation = instances[decision.ParentIndex].Orientation;
                SpatialReservation clearance = StructuralSocketReservationAdapter.ClearanceClaim(
                    in socket,
                    in decision,
                    parentOrientation,
                    voxelsPerDecimetre: 2,
                    ownerId: "fixture:typed-socket-clearance");

                Assert.That(clearance.Bounds, Is.EqualTo(
                    new ReservationBoundsDm(6, 0, -2, 10, 4, 2)),
                    "WorldBuilder clearance must be derived from the production attachment point and typed socket volume.");

                SpatialReservation blocker = WorldBuilderReservationFactory.BuildingFootprint(
                    "fixture:external-building",
                    new Int2(7, -1),
                    new Int3(1, 3, 2));
                SpatialReservationSnapshot blocked = SpatialReservationSnapshot.Create(
                    new[] { blocker }, Window);
                ReservationQueryResult rejected = StructuralSocketReservationAdapter.QueryClearance(
                    blocked,
                    in socket,
                    in decision,
                    parentOrientation,
                    voxelsPerDecimetre: 2,
                    categoryMask: ReservationCategory.Building,
                    ownerId: "fixture:typed-socket-clearance");

                Assert.That(rejected.Decision, Is.EqualTo(ReservationDecision.Rejected));
                Assert.That(rejected.Reason, Is.EqualTo(ReservationReasonCode.HardOccupancyConflict));
                StringAssert.Contains("owner=fixture:external-building", rejected.Describe());

                SpatialReservation verticallySeparated = WorldBuilderReservationFactory.BuildingFootprint(
                    "fixture:building-above-clearance",
                    new Int2(7, -1),
                    new Int3(1, 2, 2),
                    baseYDm: 5);
                SpatialReservationSnapshot separated = SpatialReservationSnapshot.Create(
                    new[] { verticallySeparated }, Window);
                ReservationQueryResult accepted = StructuralSocketReservationAdapter.QueryClearance(
                    separated,
                    in socket,
                    in decision,
                    parentOrientation,
                    voxelsPerDecimetre: 2,
                    categoryMask: ReservationCategory.Building,
                    ownerId: "fixture:typed-socket-clearance");

                Assert.That(accepted.IsAccepted, Is.True, accepted.Describe());
                Assert.That(accepted.Reason, Is.EqualTo(ReservationReasonCode.NoIntersection));
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static FeatureCatalogue BuildTypedSocketCatalogue(Allocator allocator)
        {
            int[] rootProgram = new ProgramBuilder()
                .Box(0, 0, 0, 8, 8, 8, 1, PrimitiveMode.Fill)
                .Emit(ShapeOp.CallSlot, 0, 0)
                .End()
                .Build();
            int[] childProgram = new ProgramBuilder()
                .Box(0, 0, 0, 8, 8, 8, 2, PrimitiveMode.Fill)
                .End()
                .Build();

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
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
            catalogue.Materials[0] = 1;
            catalogue.Materials[1] = 2;

            const ulong structuralType = 1UL << 20;
            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = "reservation-root",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(16, 8, 8),
                MaxSlope = 0,
                Precedence = 100,
                StructuralPiece = Piece(0x710001u, structuralType, Facing.West),
                SlotOffset = 0,
                SlotCount = 1,
                ProgramOffset = 0,
                ProgramLength = rootProgram.Length,
                MaterialOffset = 0,
                MaterialCount = 1,
                MaxPrimitives = 1,
            };
            catalogue.Definitions[1] = new FeatureDefinition
            {
                Name = "reservation-child",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(8, 8, 8),
                MaxSlope = 0,
                Precedence = 100,
                StructuralPiece = Piece(0x710002u, structuralType, Facing.West),
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
                Name = "east-child",
                SocketId = 0x710101u,
                Role = StructuralSocketRole.BridgeSpan,
                Offers = structuralType,
                Accepts = structuralType,
                LocalPosition = new int3(16, 0, 0),
                Facing = Facing.East,
                DefinitionId = 1,
                LocalMin = new int3(16, 0, 0),
                LocalMax = new int3(16, 0, 0),
                ClearanceMin = new int3(-4, 0, -4),
                ClearanceMax = new int3(4, 8, 4),
                CountMin = 1,
                CountMax = 1,
                Capacity = 1,
                Flags = StructuralSocketFlags.Required,
            };
            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = int3.zero,
                Orientation = 0,
            };
            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = 0,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 1,
                AcceptProbability = 65536,
                MinAltitude = -FeatureBudget.MaxFootprintVoxels,
                MaxAltitude = FeatureBudget.MaxFootprintVoxels,
                MaxSlope = 0,
                ClusterMin = 1,
                ClusterMax = 1,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (load != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException("Typed socket reservation fixture failed validation: " + load);
            }
            return catalogue;
        }

        private static StructuralPieceSpec Piece(uint pieceId, ulong structuralType, Facing facing) => new StructuralPieceSpec
        {
            PieceId = pieceId,
            Role = StructuralSocketRole.BridgeSpan,
            Offers = structuralType,
            Accepts = structuralType,
            LocalPosition = int3.zero,
            Facing = facing,
            ClearanceMin = int3.zero,
            ClearanceMax = int3.zero,
        };
    }
}
