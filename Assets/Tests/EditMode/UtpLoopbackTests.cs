using System;
using System.Threading;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Edits;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class UtpLoopbackTests
    {
        [Test]
        [Category("Networking")]
        public void LoopbackCarriesRequestAndAuthoritativeBatchAcrossConcreteHosts()
        {
            using var server = new UtpServerHost();
            using var client = new UtpClientHost();
            var serverHandler = new RecordingServerHandler();
            var clientHandler = new RecordingClientHandler();

            uint serverConnectionId = 0;
            bool clientConnected = false;
            server.ConnectionOpened += (id, _) => serverConnectionId = id;
            client.Connected += () => clientConnected = true;

            var listenEndpoint = NetworkEndpoint.LoopbackIpv4.WithPort(0);
            Assert.That(server.Listen(listenEndpoint), Is.EqualTo(0));
            Assert.That(server.LocalEndpoint.Port, Is.Not.EqualTo(0));
            Assert.That(client.Connect(server.LocalEndpoint), Is.True);

            PumpUntil(
                () => clientConnected && serverConnectionId != 0,
                () =>
                {
                    client.Pump(clientHandler);
                    server.Pump(serverHandler);
                });

            var request = new C_AlterationRequest(
                tick: 41,
                origin: new int3(510, 40, -12),
                eventKind: AlterationEvent.KindExplosion,
                material: 0,
                shapeKind: AlterationEvent.KindExplosion,
                shapeData: 3,
                seed: 0x12345678,
                sequence: 9);

            Assert.That(client.TrySendAlterationRequest(in request), Is.True);
            client.FlushSends();

            PumpUntil(
                () => serverHandler.RequestCount == 1,
                () =>
                {
                    server.Pump(serverHandler);
                    client.Pump(clientHandler);
                });

            Assert.That(serverHandler.ConnectionId, Is.EqualTo(serverConnectionId));
            Assert.That(serverHandler.LastRequest, Is.EqualTo(request));

            const uint authoritativeTick = 77;
            var evt = request.ToAuthoritativeEvent(
                authoritativeTick,
                authoritativePlayerId: 5,
                authoritativeSequence: 2,
                authoritativeSeed: 0xCAFEBABE);

            var sink = new AlterationBatchPacketSink(server);
            int3 encodingRegion = evt.origin >> 9;
            var events = new[] { evt };
            sink.SendBatch(serverConnectionId, encodingRegion, authoritativeTick, events);
            server.FlushSends();

            PumpUntil(
                () => clientHandler.BatchCount == 1,
                () =>
                {
                    client.Pump(clientHandler);
                    server.Pump(serverHandler);
                });

            Assert.That(clientHandler.LastEvent.tick, Is.EqualTo(authoritativeTick));
            Assert.That(clientHandler.LastEvent.playerId, Is.EqualTo(5));
            Assert.That(clientHandler.LastEvent.sequence, Is.EqualTo(2));
            Assert.That(clientHandler.LastEvent.seed, Is.EqualTo(0xCAFEBABE));
            Assert.That(clientHandler.LastEvent.origin, Is.EqualTo(request.origin));
        }

        private static void PumpUntil(Func<bool> condition, Action pump)
        {
            const int maxAttempts = 100;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }

            Assert.That(condition(), Is.True, "UTP loopback condition was not reached.");
        }

        private sealed class RecordingServerHandler : IClientEventCommandHandler
        {
            public int RequestCount { get; private set; }
            public uint ConnectionId { get; private set; }
            public C_AlterationRequest LastRequest { get; private set; }

            public void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request)
            {
                ConnectionId = connectionId;
                LastRequest = request;
                RequestCount++;
            }
        }

        private sealed class RecordingClientHandler : IUtpClientPacketHandler
        {
            public int BatchCount { get; private set; }
            public AlterationEvent LastEvent { get; private set; }

            public bool HandlePacket(UtpChannel channel, ReadOnlySpan<byte> packet)
            {
                if (channel != UtpChannel.Event ||
                    !ProtocolEnvelope.TryReadHeader(packet, out var kind, out int payloadOffset) ||
                    kind != ProtocolMessageKind.S_AlterationEventBatch)
                {
                    return false;
                }

                ReadOnlySpan<byte> payload = packet.Slice(payloadOffset);
                if (!S_AlterationEventBatch.TryDecodeHeader(payload, out var batch) ||
                    !S_AlterationEventBatch.TryDecodeEvent(payload, in batch, 0, out var evt))
                {
                    return false;
                }

                LastEvent = evt;
                BatchCount++;
                return true;
            }
        }
    }
}
