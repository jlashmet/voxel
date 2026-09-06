using System;
using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Net.Runtime.Transport;
using VoxelEngine.Net.Validation;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SessionAdmissionTransportTests
    {
        [TestCase(1, false)]
        [TestCase(16, false)]
        [TestCase(SessionAdmissionPacket.MaxPayloadBytes, false)]
        [TestCase(1, true)]
        [TestCase(16, true)]
        [TestCase(SessionAdmissionPacket.MaxPayloadBytes, true)]
        public void DirectionSpecificPacketsRoundTripWithinExistingEventBudget(int length, bool reply)
        {
            var input = new byte[length];
            for (int i = 0; i < input.Length; i++) input[i] = (byte)(i * 37);
            var buffer = new byte[SessionAdmissionPacket.MaxPacketBytes];
            int written;
            bool encoded = reply
                ? SessionAdmissionPacket.TryEncodeReply(buffer, input, out written)
                : SessionAdmissionPacket.TryEncodeRequest(buffer, input, out written);
            Assert.That(encoded, Is.True);
            Assert.That(written, Is.EqualTo(SessionAdmissionPacket.HeaderSize + length));
            Assert.That(written, Is.LessThanOrEqualTo(ChannelSetup.k_MaxEventPacketBytes));
            ReadOnlySpan<byte> packet = buffer.AsSpan(0, written);
            ReadOnlySpan<byte> decoded;
            bool accepted = reply
                ? SessionAdmissionPacket.TryDecodeReply(packet, out decoded)
                : SessionAdmissionPacket.TryDecodeRequest(packet, out decoded);
            Assert.That(accepted, Is.True);
            Assert.That(decoded.ToArray(), Is.EqualTo(input));
            bool wrongDirection = reply
                ? SessionAdmissionPacket.TryDecodeRequest(packet, out decoded)
                : SessionAdmissionPacket.TryDecodeReply(packet, out decoded);
            Assert.That(wrongDirection, Is.False);
            Assert.That(decoded.Length, Is.Zero);
        }

        [TestCase(0, SessionAdmissionPacket.MaxPacketBytes)]
        [TestCase(SessionAdmissionPacket.MaxPayloadBytes + 1, SessionAdmissionPacket.MaxPacketBytes + 1)]
        [TestCase(10, SessionAdmissionPacket.HeaderSize + 9)]
        public void EncoderRejectsInvalidSizesWithoutPartialWrite(int length, int destinationSize)
        {
            var input = new byte[length];
            var buffer = new byte[destinationSize];
            for (int i = 0; i < buffer.Length; i++) buffer[i] = 0xCA;
            Assert.That(SessionAdmissionPacket.TryEncodeRequest(buffer, input, out int written), Is.False);
            Assert.That(written, Is.Zero);
            Assert.That(Array.TrueForAll(buffer, value => value == 0xCA), Is.True);
            Assert.That(SessionAdmissionPacket.TryEncodeReply(buffer, input, out written), Is.False);
            Assert.That(written, Is.Zero);
            Assert.That(Array.TrueForAll(buffer, value => value == 0xCA), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EncodingSupportsOverlappingCallerOwnedSpans(bool reply)
        {
            byte[] input = { 7, 6, 5, 4, 3, 2, 1 };
            var buffer = new byte[SessionAdmissionPacket.HeaderSize + input.Length];
            input.CopyTo(buffer, 0);
            int written;
            bool encoded = reply
                ? SessionAdmissionPacket.TryEncodeReply(buffer, buffer.AsSpan(0, input.Length), out written)
                : SessionAdmissionPacket.TryEncodeRequest(buffer, buffer.AsSpan(0, input.Length), out written);
            Assert.That(encoded, Is.True);
            ReadOnlySpan<byte> payload;
            bool decoded = reply
                ? SessionAdmissionPacket.TryDecodeReply(buffer.AsSpan(0, written), out payload)
                : SessionAdmissionPacket.TryDecodeRequest(buffer.AsSpan(0, written), out payload);
            Assert.That(decoded, Is.True);
            Assert.That(payload.ToArray(), Is.EqualTo(input));
        }

        [TestCase("truncated")]
        [TestCase("trailing")]
        [TestCase("empty")]
        [TestCase("oversize-length")]
        [TestCase("version")]
        [TestCase("reply-direction")]
        [TestCase("unknown-kind")]
        [TestCase("header-only")]
        public void InvalidAdmissionNeverReachesServerCapability(string defect)
        {
            byte[] packet = Request(new byte[] { 42, 0, 0, 0, 99 });
            switch (defect)
            {
                case "truncated": Array.Resize(ref packet, packet.Length - 1); break;
                case "trailing": Array.Resize(ref packet, packet.Length + 1); break;
                case "empty": packet[2] = 0; packet[3] = 0; break;
                case "oversize-length": packet[2] = 255; packet[3] = 255; break;
                case "version": packet[0] = (byte)(ProtocolEnvelope.CurrentVersion + 1); break;
                case "reply-direction": packet[1] = (byte)ProtocolMessageKind.S_SessionAdmission; break;
                case "unknown-kind": packet[1] = 255; break;
                case "header-only": Array.Resize(ref packet, SessionAdmissionPacket.HeaderSize); break;
            }
            var handler = new AdmissionRecorder();
            Assert.That(SessionAdmissionPacket.TryDecodeRequest(packet, out ReadOnlySpan<byte> payload), Is.False);
            Assert.That(payload.Length, Is.Zero);
            Assert.That(ClientEventPacketReceiver.TryDispatch(123, packet, handler), Is.False);
            Assert.That(handler.AdmissionCalls, Is.Zero);
            Assert.That(handler.AlterationCalls, Is.Zero);
        }

        [Test]
        public void ServerUsesTransportSenderIdentityAndRequiresExplicitCapability()
        {
            // The opaque payload may claim any identity. Net supplies its real connection separately.
            byte[] payload = { 42, 0, 0, 0, 99 };
            byte[] packet = Request(payload);
            var handler = new AdmissionRecorder();
            Assert.That(ClientEventPacketReceiver.TryDispatch(123, packet, handler), Is.True);
            Assert.That(handler.ConnectionId, Is.EqualTo(123));
            Assert.That(handler.Payload, Is.EqualTo(payload));
            Assert.That(handler.AdmissionCalls, Is.EqualTo(1));
            Assert.That(handler.AlterationCalls, Is.Zero);
            Assert.That(ClientEventPacketReceiver.TryDispatch(0, packet, handler), Is.False);
            Assert.That(handler.AdmissionCalls, Is.EqualTo(1));

            var withoutAdmission = new EventRecorder();
            Assert.That(ClientEventPacketReceiver.TryDispatch(123, packet, withoutAdmission), Is.False);
            Assert.That(withoutAdmission.AlterationCalls, Is.Zero);
        }

        [Test]
        public void AdmissionQueueBackpressureIsNotSilentlyAccepted()
        {
            var handler = new AdmissionRecorder { Accept = false };
            Assert.That(ClientEventPacketReceiver.TryDispatch(123, Request(new byte[] { 1 }), handler), Is.False);
            Assert.That(handler.AdmissionCalls, Is.EqualTo(1));
            Assert.That(handler.Payload, Is.Null);
        }

        [TestCase(UtpChannel.Ephemeral)]
        [TestCase(UtpChannel.Repair)]
        [TestCase(UtpChannel.Bulk)]
        public void RepliesAreEventOnlyAndDoNotEnterWorldReplication(UtpChannel wrongChannel)
        {
            var recorder = new ReplyRecorder();
            using var client = new ClientNetworkRuntime(new DeterministicAlterationApplier(), sessionAdmissionHandler: recorder);
            var dispatch = (IUtpClientPacketHandler)client;
            byte[] packet = Reply(new byte[] { 9, 8, 7 });
            Assert.That(dispatch.HandlePacket(wrongChannel, packet), Is.False);
            Assert.That(recorder.Calls, Is.Zero);
            Assert.That(dispatch.HandlePacket(UtpChannel.Event, packet), Is.True);
            Assert.That(recorder.Calls, Is.EqualTo(1));
            Assert.That(recorder.Payload, Is.EqualTo(new byte[] { 9, 8, 7 }));
            Assert.That(dispatch.HandlePacket(UtpChannel.Event, Request(new byte[] { 1 })), Is.False);
            Assert.That(recorder.Calls, Is.EqualTo(1));
            Assert.That(client.LocalPlayerId, Is.Zero);
            Assert.That(client.PendingAuthoritativeEvents, Is.Zero);
            Assert.That(client.PendingPlayerStateUpdates, Is.Zero);
        }

        [Test]
        public void MissingOrRejectingReplyHandlerFailsClosed()
        {
            byte[] packet = Reply(new byte[] { 1 });
            using var noHandler = new ClientNetworkRuntime(new DeterministicAlterationApplier());
            Assert.That(((IUtpClientPacketHandler)noHandler).HandlePacket(UtpChannel.Event, packet), Is.False);
            var recorder = new ReplyRecorder { Accept = false };
            using var rejects = new ClientNetworkRuntime(new DeterministicAlterationApplier(), sessionAdmissionHandler: recorder);
            Assert.That(((IUtpClientPacketHandler)rejects).HandlePacket(UtpChannel.Event, packet), Is.False);
            Assert.That(recorder.Payload, Is.Null);
            Assert.That(rejects.LocalPlayerId, Is.Zero);
        }

        [Test]
        public void DisconnectedRuntimeCannotSendOrInventConnection()
        {
            using var client = new ClientNetworkRuntime(new DeterministicAlterationApplier());
            using var server = new ServerNetworkRuntime(new EventRecorder());
            byte[] payload = { 1 };
            Assert.That(client.TrySendSessionAdmission(payload), Is.False);
            Assert.That(server.TrySendSessionAdmissionReply(0, payload), Is.False);
            Assert.That(server.TrySendSessionAdmissionReply(123, payload), Is.False);
            Assert.That(server.ConnectionCount, Is.Zero);
            Assert.That(client.LocalPlayerId, Is.Zero);
        }

        [Test]
        [Category("Networking")]
        public void ProductionRuntimeLoopbackIsolatesTwoClientsAndReplacesTransientConnection()
        {
            using var probe = new SessionAdmissionTransportProbe();
            var clock = Stopwatch.StartNew();
            while (!probe.Complete && clock.ElapsedMilliseconds < 5000)
            {
                probe.Step();
                Thread.Yield();
            }
            Assert.That(probe.Complete, Is.True, "Monotonic deadline expired in " + probe.PhaseDescription);
            Assert.That(probe.ReceivedRequestCount, Is.EqualTo(3));
            Assert.That(probe.DistinctSenders, Is.True);
            Assert.That(probe.IsolatedReplies, Is.True);
            Assert.That(probe.ReplacedConnection, Is.True);
        }

        private static byte[] Request(byte[] payload)
        {
            var packet = new byte[SessionAdmissionPacket.HeaderSize + payload.Length];
            Assert.That(SessionAdmissionPacket.TryEncodeRequest(packet, payload, out _), Is.True);
            return packet;
        }

        private static byte[] Reply(byte[] payload)
        {
            var packet = new byte[SessionAdmissionPacket.HeaderSize + payload.Length];
            Assert.That(SessionAdmissionPacket.TryEncodeReply(packet, payload, out _), Is.True);
            return packet;
        }

        private class EventRecorder : IClientEventCommandHandler
        {
            public int AlterationCalls;
            public void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request) => AlterationCalls++;
        }

        private sealed class AdmissionRecorder : EventRecorder, IClientSessionAdmissionHandler
        {
            public bool Accept = true;
            public int AdmissionCalls;
            public uint ConnectionId;
            public byte[] Payload;
            public bool TryEnqueueSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
            {
                AdmissionCalls++;
                if (!Accept) return false;
                ConnectionId = connectionId;
                Payload = payload.ToArray();
                return true;
            }
        }

        private sealed class ReplyRecorder : IServerSessionAdmissionHandler
        {
            public bool Accept = true;
            public int Calls;
            public byte[] Payload;
            public bool TryEnqueueSessionAdmissionReply(ReadOnlySpan<byte> payload)
            {
                Calls++;
                if (!Accept) return false;
                Payload = payload.ToArray();
                return true;
            }
        }
    }
}
