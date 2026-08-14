using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class AlterationRequestPacketTests
    {
        [Test]
        [Category("Bandwidth")]
        public void FramedRequestIsExactlyThirtyFourBytesAndRoundTrips()
        {
            var request = new C_AlterationRequest(
                tick: 12345,
                origin: new int3(-100, 200, 300),
                eventKind: 2,
                material: 7,
                shapeKind: 0x00100009,
                shapeData: 0x00000004,
                seed: 0xCAFEBABE,
                sequence: 99);

            var packet = new byte[AlterationRequestPacket.PacketSize];
            Assert.That(AlterationRequestPacket.PacketSize, Is.EqualTo(34));
            Assert.That(C_AlterationRequest.WireSize, Is.EqualTo(32));
            Assert.That(AlterationRequestPacket.TryEncode(packet, in request), Is.True);
            Assert.That(AlterationRequestPacket.TryDecode(packet, out var decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(request));
        }

        [Test]
        public void FramedRequestRejectsWrongKindAndTrailingBytes()
        {
            var request = new C_AlterationRequest(
                1, int3.zero, 1, 0, 1, 1, 123, 1);

            var packet = new byte[AlterationRequestPacket.PacketSize];
            Assert.That(AlterationRequestPacket.TryEncode(packet, in request), Is.True);

            packet[1] = (byte)ProtocolMessageKind.C_PlayerInput;
            Assert.That(AlterationRequestPacket.TryDecode(packet, out _), Is.False);

            var oversized = new byte[AlterationRequestPacket.PacketSize + 1];
            Assert.That(AlterationRequestPacket.TryEncode(oversized, in request), Is.False);
            Assert.That(AlterationRequestPacket.TryDecode(oversized, out _), Is.False);
        }

        [Test]
        public void ServerDispatcherUsesConnectionOwnedIdentityWhenMaterializingAuthority()
        {
            // Legacy compatibility caller supplies a deliberately spoofed playerId. It is ignored
            // by the request wire format and cannot survive encode/decode.
            var request = new C_AlterationRequest(
                tick: 50,
                origin: new int3(4, 5, 6),
                eventKind: 1,
                shapeRadius: 2,
                shapeExtentsYz: 0,
                material: 0,
                seed: 999,
                playerId: ushort.MaxValue,
                sequence: 7);

            var packet = new byte[AlterationRequestPacket.PacketSize];
            Assert.That(AlterationRequestPacket.TryEncode(packet, in request), Is.True);

            var handler = new RecordingHandler();
            Assert.That(ClientEventPacketReceiver.TryDispatch(123, packet, handler), Is.True);
            Assert.That(handler.ConnectionId, Is.EqualTo(123));

            var authoritative = handler.Request.ToAuthoritativeEvent(
                authoritativeTick: 51,
                authoritativePlayerId: 12,
                authoritativeSequence: 3,
                authoritativeSeed: 1001);

            Assert.That(authoritative.playerId, Is.EqualTo(12));
            Assert.That(authoritative.tick, Is.EqualTo(51));
            Assert.That(authoritative.sequence, Is.EqualTo(3));
            Assert.That(authoritative.seed, Is.EqualTo(1001));
            Assert.That(authoritative.shapeData, Is.EqualTo(2));
        }

        private sealed class RecordingHandler : IClientEventCommandHandler
        {
            public uint ConnectionId { get; private set; }
            public C_AlterationRequest Request { get; private set; }

            public void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request)
            {
                ConnectionId = connectionId;
                Request = request;
            }
        }
    }
}
