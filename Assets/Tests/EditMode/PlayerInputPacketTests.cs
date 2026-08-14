using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Protocol;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlayerInputPacketTests
    {
        [Test]
        [Category("Bandwidth")]
        public void CompactInputIsSixteenBytePayloadAndEighteenBytePacket()
        {
            Assert.That(C_PlayerInput.WireSize, Is.EqualTo(16));
            Assert.That(PlayerInputPacket.PacketSize, Is.EqualTo(18));
        }

        [Test]
        public void SignedMovementAndActionBitsRoundTrip()
        {
            var input = new C_PlayerInput(
                tick: 123,
                sequence: 44,
                movement: new float2(-0.75f, 0.5f),
                viewDirection: math.normalize(new float3(-0.4f, 0.3f, 0.8f)),
                actions: C_PlayerInput.ActionBits.Move |
                         C_PlayerInput.ActionBits.Aim |
                         C_PlayerInput.ActionBits.UseMain,
                toolMaterial: 17,
                flags: 3);

            var packet = new byte[PlayerInputPacket.PacketSize];
            Assert.That(PlayerInputPacket.TryEncode(packet, in input), Is.True);
            Assert.That(PlayerInputPacket.TryDecode(packet, out var decoded), Is.True);
            Assert.That(decoded, Is.EqualTo(input));

            float2 movement = decoded.Movement();
            Assert.That(movement.x, Is.LessThan(0f));
            Assert.That(movement.y, Is.GreaterThan(0f));
            Assert.That(math.abs(movement.x + 0.75f), Is.LessThan(0.01f));
            Assert.That(math.abs(movement.y - 0.5f), Is.LessThan(0.01f));
            Assert.That(decoded.Actions.HasFlag(C_PlayerInput.ActionBits.UseMain), Is.True);
        }

        [Test]
        public void ViewDirectionRoundTripsWithinQuantisationError()
        {
            float3 expected = math.normalize(new float3(0.43f, -0.28f, 0.86f));
            var input = new C_PlayerInput(
                1,
                1,
                float2.zero,
                expected,
                C_PlayerInput.ActionBits.Aim,
                0);

            Span<byte> payload = stackalloc byte[C_PlayerInput.WireSize];
            input.Encode(payload);
            var decoded = C_PlayerInput.Decode(payload);
            float3 actual = decoded.ViewDirection();

            Assert.That(math.dot(expected, actual), Is.GreaterThan(0.999f));
        }

        [Test]
        public void PacketRejectsWrongKindAndTrailingBytes()
        {
            var input = new C_PlayerInput(
                1,
                2,
                new float2(1f, -1f),
                new float3(0f, 0f, 1f),
                C_PlayerInput.ActionBits.Move,
                0);

            var packet = new byte[PlayerInputPacket.PacketSize];
            Assert.That(PlayerInputPacket.TryEncode(packet, in input), Is.True);

            packet[1] = (byte)ProtocolMessageKind.C_AlterationRequest;
            Assert.That(PlayerInputPacket.TryDecode(packet, out _), Is.False);

            var oversized = new byte[PlayerInputPacket.PacketSize + 1];
            Assert.That(PlayerInputPacket.TryEncode(oversized, in input), Is.True);
            Assert.That(PlayerInputPacket.TryDecode(oversized, out _), Is.False);
        }
    }
}
