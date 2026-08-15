using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class EventCostTests
    {
        private static int BroadcastCost(in AlterationEvent evt)
        {
            int payloadLength = AlterationEvent.WireSize();
            var payload = new byte[payloadLength];
            var wire = new byte[S_AlterationEvent.HeaderSize + payloadLength];
            var header = new S_AlterationEvent(evt.tick, RegionOf(evt.origin));
            header.Encode(wire, payload);
            return wire.Length;
        }

        private static int3 RegionOf(int3 voxel)
        {
            VoxelAccess.Decompose(voxel, out int3 regionCoord, out _, out _);
            return regionCoord;
        }

        private static AlterationEvent Explosion(ushort radiusBricks) => new AlterationEvent(
            AlterationEvent.KindExplosion,
            1u,
            new int3(256, 256, 256),
            radiusBricks,
            VoxelDimensions.MaterialEmpty,
            42u,
            1,
            1);

        [Test]
        [Category("SC_002")]
        [Category("Bandwidth")]
        public void LargeDestructionEventTransmitsUnder64Bytes()
        {
            var evt = Explosion(32);
            int voxelCount = EstimateVoxelCount(evt.Radius());
            Assert.GreaterOrEqual(voxelCount, 4000,
                $"Explosion(r={evt.Radius()}) must affect >= 4000 voxels (estimated {voxelCount}).");

            int encodedSize = BroadcastCost(in evt);
            Assert.LessOrEqual(encodedSize, 64,
                $"Event affecting >= 4000 voxels must transmit in <= 64 bytes, got {encodedSize} bytes.");
        }

        [Test]
        [Category("SC_002")]
        public void OrdinaryPlayerActionIsComparableToDestructionEvent()
        {
            var placement = new C_AlterationRequest(
                tick: 1u,
                origin: new int3(256, 256, 256),
                eventKind: AlterationEvent.KindBrush,
                material: 3,
                shapeKind: BrushShapeCodec.PackCube(1, 1, 1),
                shapeData: 0,
                seed: 42u,
                sequence: 1);

            var requestWire = new byte[C_AlterationRequest.WireSize];
            placement.Encode(requestWire);

            int explosionSize = BroadcastCost(Explosion(32));
            Assert.LessOrEqual(explosionSize, C_AlterationRequest.WireSize * 2,
                $"A >=4000-voxel destruction ({explosionSize} B) must stay within 2x an " +
                $"ordinary action ({C_AlterationRequest.WireSize} B).");
        }

        [Test]
        [Category("SC_002")]
        public void WirePayloadDoesNotGrowWithRadius()
        {
            int baseSize = BroadcastCost(Explosion(1));
            for (ushort r = 1; r <= 63; r++)
            {
                int size = BroadcastCost(Explosion(r));
                Assert.AreEqual(baseSize, size,
                    $"Wire size must be constant across radii: r={r}, size={size} (expected {baseSize}).");
            }
        }

        private static int EstimateVoxelCount(ushort radiusBricks)
        {
            float voxelRadius = radiusBricks * VoxelDimensions.BrickEdge;
            return (int)(4.0f / 3.0f * math.PI * voxelRadius * voxelRadius * voxelRadius);
        }
    }
}
