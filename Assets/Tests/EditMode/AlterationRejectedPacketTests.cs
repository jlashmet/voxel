using NUnit.Framework;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class AlterationRejectedPacketTests
    {
        [Test]
        public void RejectionIsTenByteFramedEventPacket()
        {
            Assert.That(AlterationRejectedPacket.PacketSize, Is.EqualTo(10));

            var rejection = new S_AlterationRejected(
                55,
                7,
                S_AlterationRejected.Reason.OutOfReach);

            var packet = new byte[AlterationRejectedPacket.PacketSize];
            Assert.That(AlterationRejectedPacket.TryEncode(packet, in rejection), Is.True);
            Assert.That(AlterationRejectedPacket.TryDecode(packet, out var decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(rejection));
            Assert.That(packet[0], Is.EqualTo(ProtocolEnvelope.CurrentVersion));
            Assert.That(packet[1], Is.EqualTo((byte)ProtocolMessageKind.S_AlterationRejected));
        }

        [Test]
        public void RejectionRejectsWrongKindAndTrailingBytes()
        {
            var rejection = new S_AlterationRejected(
                1,
                1,
                S_AlterationRejected.Reason.InvalidTarget);

            var packet = new byte[AlterationRejectedPacket.PacketSize];
            Assert.That(AlterationRejectedPacket.TryEncode(packet, in rejection), Is.True);

            packet[1] = (byte)ProtocolMessageKind.S_RegionHash;
            Assert.That(AlterationRejectedPacket.TryDecode(packet, out _), Is.False);

            var oversized = new byte[AlterationRejectedPacket.PacketSize + 1];
            Assert.That(AlterationRejectedPacket.TryEncode(oversized, in rejection), Is.True);
            Assert.That(AlterationRejectedPacket.TryDecode(oversized, out _), Is.False);
        }
    }
}
