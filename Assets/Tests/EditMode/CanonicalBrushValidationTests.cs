using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CanonicalBrushValidationTests
    {
        [Test]
        public void LegacyRequestConstructorMapsBrushIntoCanonicalNonOverlappingShapeBits()
        {
            var request = new C_AlterationRequest(
                tick: 12,
                origin: new int3(1, 2, 3),
                eventKind: AlterationEvent.KindBrush,
                shapeRadius: 8,
                shapeExtentsYz: (ushort)((16 << 8) | 4),
                material: 7,
                seed: 99,
                playerId: 65535,
                sequence: 5);

            Assert.That(BrushShapeCodec.ShapeType(request.shapeKind), Is.EqualTo(BrushShapeCodec.ShapeCube));
            Assert.That(BrushShapeCodec.ExtentsBricks(request.shapeKind), Is.EqualTo(new int3(8, 16, 4)));
            Assert.That(request.shapeData, Is.Zero);
            Assert.That(BrushShapeCodec.Validate(request.shapeKind, request.shapeData), Is.True);
        }

        [Test]
        public void PlacementAttachedOnlyAtFaceCornerPassesAuthoritativeValidation()
        {
            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(8, Allocator.TempJob);
            var players = new ServerPlayerRegistry();
            try
            {
                table.LoadRegion(int3.zero);
                var mutationStorage = new RegionMutationStore(in table, in pool);

                // Canonical 1-brick brush centered at (4,4,4) occupies [0..7]^3. The only support
                // is at (8,0,0), on the +X face corner rather than its center.
                Assert.That(VoxelAccess.SetVoxel(
                    ref table,
                    ref pool,
                    new int3(8, 0, 0),
                    2), Is.True);

                Assert.That(players.TryRegisterAuthenticated(
                    connectionId: 1,
                    playerId: 9,
                    authoritativePositionVoxels: new int3(24, 4, 4),
                    reachVoxels: 64,
                    canAlterWorld: true), Is.True);
                Assert.That(players.TryGetByConnection(1, out var player), Is.True);

                var evt = AlterationEvent.CreateCubeBrush(
                    tick: 1,
                    origin: new int3(4, 4, 4),
                    extentXBricks: 1,
                    extentYBricks: 1,
                    extentZBricks: 1,
                    material: 5,
                    seed: 1,
                    playerId: 9,
                    sequence: 1);

                var result = AuthoritativeAlterationValidator.Validate(
                    in evt,
                    in player,
                    players,
                    mutationStorage,
                    new DeterministicAlterationApplier(),
                    ref table,
                    in pool,
                    new Validation.DensityCap(1f, 0));

                Assert.That(result, Is.EqualTo(Validation.ValidationResult.Success));
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
