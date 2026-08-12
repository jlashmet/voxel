using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

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
                shapeRadius: 9,
                shapeExtentsYz: 0x1234,
                material: 7,
                seed: 0xCAFEBABE,
                playerId: 4,
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
                1, int3.zero, 1, 1, 0, 0, 123, 1, 1);

            var packet = new byte[AlterationRequestPacket.PacketSize];
            Assert.That(AlterationRequestPacket.TryEncode(packet, in request), Is.True);

            packet[1] = (byte)ProtocolMessageKind.C_PlayerInput;
            Assert.That(AlterationRequestPacket.TryDecode(packet, out _), Is.False);

            var oversized = new byte[AlterationRequestPacket.PacketSize + 1];
            Assert.That(AlterationRequestPacket.TryEncode(oversized, in request), Is.False);
            Assert.That(AlterationRequestPacket.TryDecode(oversized, out _), Is.False);
        }
    }
}
