using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlayerInputBundleTests
    {
        [Test]
        [Category("Bandwidth")]
        public void ThreeSampleBundleIsFiftyOneBytes()
        {
            Span<C_PlayerInput> samples = stackalloc C_PlayerInput[3];
            samples[0] = Input(10);
            samples[1] = Input(11);
            samples[2] = Input(12);

            Span<byte> packet = stackalloc byte[PlayerInputBundlePacket.MaxPacketSize];
            Assert.That(PlayerInputBundlePacket.TryEncode(packet, samples, out int bytesWritten), Is.True);
            Assert.That(bytesWritten, Is.EqualTo(51));
            Assert.That(PlayerInputBundlePacket.MaxPacketSize, Is.EqualTo(51));
        }

        [Test]
        public void OverlappingBundlesDispatchEachSequenceOnlyOnce()
        {
            var receiver = new ClientEphemeralPacketReceiver();
            var handler = new RecordingHandler();

            Dispatch(receiver, handler, 7, Input(10), Input(11), Input(12));
            Assert.That(handler.Count, Is.EqualTo(3));

            // Normal next packet repeats the previous two samples and adds one new sample.
            Dispatch(receiver, handler, 7, Input(11), Input(12), Input(13));
            Assert.That(handler.Count, Is.EqualTo(4));
            Assert.That(handler.Last.sequence, Is.EqualTo(13));
        }

        [Test]
        public void SequenceWraparoundRemainsNewer()
        {
            var receiver = new ClientEphemeralPacketReceiver();
            var handler = new RecordingHandler();

            Dispatch(receiver, handler, 4, Input(65534), Input(65535), Input(0));
            Assert.That(handler.Count, Is.EqualTo(3));
            Assert.That(handler.Last.sequence, Is.EqualTo(0));

            Dispatch(receiver, handler, 4, Input(65535), Input(0), Input(1));
            Assert.That(handler.Count, Is.EqualTo(4));
            Assert.That(handler.Last.sequence, Is.EqualTo(1));
        }

        [Test]
        public void NonMonotonicBundleFailsBeforePartialDispatch()
        {
            var receiver = new ClientEphemeralPacketReceiver();
            var handler = new RecordingHandler();
            Span<C_PlayerInput> samples = stackalloc C_PlayerInput[2];
            samples[0] = Input(20);
            samples[1] = Input(19);

            Span<byte> packet = stackalloc byte[PlayerInputBundlePacket.MaxPacketSize];
            Assert.That(PlayerInputBundlePacket.TryEncode(packet, samples, out int bytesWritten), Is.True);
            Assert.That(receiver.TryDispatch(3, packet.Slice(0, bytesWritten), handler), Is.False);
            Assert.That(handler.Count, Is.EqualTo(0));
        }

        [Test]
        public void DisconnectResetAllowsFreshSequenceWindow()
        {
            var receiver = new ClientEphemeralPacketReceiver();
            var handler = new RecordingHandler();

            Dispatch(receiver, handler, 9, Input(500));
            Assert.That(handler.Count, Is.EqualTo(1));

            receiver.RemoveConnection(9);
            Dispatch(receiver, handler, 9, Input(1));
            Assert.That(handler.Count, Is.EqualTo(2));
            Assert.That(handler.Last.sequence, Is.EqualTo(1));
        }

        private static void Dispatch(
            ClientEphemeralPacketReceiver receiver,
            RecordingHandler handler,
            uint connectionId,
            params C_PlayerInput[] inputs)
        {
            Span<byte> packet = stackalloc byte[PlayerInputBundlePacket.MaxPacketSize];
            Assert.That(PlayerInputBundlePacket.TryEncode(packet, inputs, out int bytesWritten), Is.True);
            Assert.That(receiver.TryDispatch(connectionId, packet.Slice(0, bytesWritten), handler), Is.True);
        }

        private static C_PlayerInput Input(ushort sequence)
        {
            return new C_PlayerInput(
                tick: sequence,
                sequence: sequence,
                movement: new float2(0.25f, -0.5f),
                viewDirection: new float3(0f, 0f, 1f),
                actions: C_PlayerInput.ActionBits.Move,
                toolMaterial: 0);
        }

        private sealed class RecordingHandler : IClientInputCommandHandler
        {
            public int Count { get; private set; }
            public C_PlayerInput Last { get; private set; }

            public void HandlePlayerInput(uint connectionId, in C_PlayerInput input)
            {
                Last = input;
                Count++;
            }
        }
    }
}
