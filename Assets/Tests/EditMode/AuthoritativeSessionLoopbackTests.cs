using System;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class AuthoritativeSessionLoopbackTests
    {
        [Test]
        [Category("Networking")]
        public void AuthenticatedSessionCarriesInputAcceptanceAndRejectionEndToEnd()
        {
            using var server = new AuthoritativeServerSession(
                serverSeed: 0x12345678,
                densityCap: new Validation.DensityCap(1f, 0));
            using var client = new UtpClientHost();

            var clientHandler = new RecordingClientHandler();
            var inputSink = new RecordingInputSink();
            var applier = new AcceptingApplier();
            uint serverConnectionId = 0;
            bool clientConnected = false;

            server.ConnectionOpened += (id, _) => serverConnectionId = id;
            client.Connected += () => clientConnected = true;

            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));
            Assert.That(client.Connect(server.LocalEndpoint), Is.True);

            PumpUntil(
                () => clientConnected && serverConnectionId != 0,
                () =>
                {
                    client.Pump(clientHandler);
                    server.PumpTransport();
                });

            Assert.That(server.AuthenticateConnection(
                serverConnectionId,
                playerId: 7,
                authoritativePositionVoxels: int3.zero,
                reachVoxels: 64), Is.True);

            var input = new C_PlayerInput(
                tick: 10,
                sequence: 1,
                movement: new float2(0.5f, 0.25f),
                viewDirection: new float3(0f, 0f, 1f),
                actions: C_PlayerInput.ActionBits.Move,
                toolMaterial: 0);
            Assert.That(client.TrySendPlayerInput(in input), Is.True);

            var acceptedRequest = Request(10, 1, new int3(32, 32, 32), 0xDEADBEEF);
            Assert.That(client.TrySendAlterationRequest(in acceptedRequest), Is.True);
            client.FlushSends();

            PumpUntil(
                () => server.CommandInbox.PendingInputs > 0 && server.CommandInbox.PendingAlterations > 0,
                () =>
                {
                    server.PumpTransport();
                    client.Pump(clientHandler);
                });

            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(4, Allocator.TempJob);
            ProtectedZones zones = default;
            table.LoadRegion(int3.zero);
            try
            {
                server.ProcessAuthoritativeTick(10, ref table, ref pool, in zones, inputSink, applier);

                PumpUntil(
                    () => clientHandler.BatchCount == 1,
                    () =>
                    {
                        client.Pump(clientHandler);
                        server.PumpTransport();
                    });

                Assert.That(inputSink.Count, Is.EqualTo(1));
                Assert.That(inputSink.LastPlayerId, Is.EqualTo(7));
                Assert.That(clientHandler.LastEvent.playerId, Is.EqualTo(7));
                Assert.That(clientHandler.LastEvent.tick, Is.EqualTo(10));
                Assert.That(clientHandler.LastEvent.sequence, Is.EqualTo(1));
                Assert.That(clientHandler.LastEvent.seed, Is.Not.EqualTo(acceptedRequest.seed));

                var rejectedRequest = Request(11, 2, new int3(1000, 0, 0), 123);
                Assert.That(client.TrySendAlterationRequest(in rejectedRequest), Is.True);
                client.FlushSends();

                PumpUntil(
                    () => server.CommandInbox.PendingAlterations > 0,
                    () =>
                    {
                        server.PumpTransport();
                        client.Pump(clientHandler);
                    });

                server.ProcessAuthoritativeTick(11, ref table, ref pool, in zones, inputSink, applier);

                PumpUntil(
                    () => clientHandler.RejectionCount == 1,
                    () =>
                    {
                        client.Pump(clientHandler);
                        server.PumpTransport();
                    });

                Assert.That(clientHandler.LastRejection.playerId, Is.EqualTo(7));
                Assert.That(clientHandler.LastRejection.tick, Is.EqualTo(11));
                Assert.That(clientHandler.LastRejection.ReasonEnum(),
                    Is.EqualTo(S_AlterationRejected.Reason.OutOfReach));
                Assert.That(applier.Count, Is.EqualTo(1));
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static C_AlterationRequest Request(uint tick, ushort sequence, int3 origin, uint requestedSeed) =>
            new C_AlterationRequest(
                tick,
                origin,
                AlterationEvent.KindExplosion,
                material: 0,
                shapeKind: AlterationEvent.KindExplosion,
                shapeData: 1,
                seed: requestedSeed,
                sequence: sequence);

        private static void PumpUntil(Func<bool> condition, Action pump)
        {
            const int maxAttempts = 100;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }
            Assert.That(condition(), Is.True, "Authoritative UTP loopback condition was not reached.");
        }

        private sealed class RecordingInputSink : IAuthoritativePlayerInputSink
        {
            public int Count { get; private set; }
            public ushort LastPlayerId { get; private set; }
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick)
            {
                Count++;
                LastPlayerId = playerId;
            }
        }

        private sealed class AcceptingApplier : IAuthoritativeAlterationApplier
        {
            public int Count { get; private set; }
            public bool TryApplyAlteration(IRegionMutationStore storage, in AlterationEvent evt)
            {
                Count++;
                return true;
            }
        }

        private sealed class RecordingClientHandler : IUtpClientPacketHandler
        {
            public int BatchCount { get; private set; }
            public int RejectionCount { get; private set; }
            public AlterationEvent LastEvent { get; private set; }
            public S_AlterationRejected LastRejection { get; private set; }

            public bool HandlePacket(UtpChannel channel, ReadOnlySpan<byte> packet)
            {
                if (channel != UtpChannel.Event ||
                    !ProtocolEnvelope.TryReadHeader(packet, out ProtocolMessageKind kind, out int payloadOffset))
                    return false;

                if (kind == ProtocolMessageKind.S_AlterationRejected)
                {
                    if (!AlterationRejectedPacket.TryDecode(packet, out S_AlterationRejected rejection))
                        return false;
                    LastRejection = rejection;
                    RejectionCount++;
                    return true;
                }

                if (kind != ProtocolMessageKind.S_AlterationEventBatch)
                    return false;

                ReadOnlySpan<byte> payload = packet.Slice(payloadOffset);
                if (!S_AlterationEventBatch.TryDecodeHeader(payload, out var batch) ||
                    !S_AlterationEventBatch.TryDecodeEvent(payload, in batch, 0, out AlterationEvent evt))
                    return false;

                LastEvent = evt;
                BatchCount++;
                return true;
            }
        }
    }
}
