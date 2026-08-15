using System;
using System.Threading;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Net.Runtime.Transport;

using VoxelEngine.Edits.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class PlayerStateLoopbackTests
    {
        [Test]
        [Category("Networking")]
        public void EphemeralSnapshotReconcilesOutsideTransportCallback()
        {
            using var server = new UtpServerHost();
            using var client = new ClientNetworkRuntime(new DeterministicAlterationApplier());
            var serverHandler = new RecordingServerHandler();
            var prediction = new RecordingPredictionAdapter();

            uint serverConnectionId = 0;
            bool clientConnected = false;
            server.ConnectionOpened += (id, _) => serverConnectionId = id;
            client.Connected += () => clientConnected = true;
            client.ConfigureLocalPrediction(5, prediction);

            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));
            Assert.That(client.Connect(server.LocalEndpoint), Is.True);

            PumpUntil(
                () => clientConnected && serverConnectionId != 0,
                () => PumpBoth(client, server, serverHandler));

            var input = new C_PlayerInput(
                tick: 20,
                sequence: 10,
                movement: new float2(1f, 0f),
                viewDirection: new float3(0f, 0f, 1f),
                actions: C_PlayerInput.ActionBits.Move,
                toolMaterial: 0);

            Assert.That(client.TrySendPlayerInput(in input), Is.True);
            client.FlushSends();
            PumpUntil(
                () => serverHandler.InputCount == 1,
                () => PumpBoth(client, server, serverHandler));
            Assert.That(client.PendingPredictionInputs, Is.EqualTo(1));

            S_PlayerState state = S_PlayerState.Create(
                5, 22, 1,
                new float3(4.25f, 3f, -2f),
                new float3(2f, 0f, 0f),
                1000,
                S_PlayerState.StateFlags.Grounded,
                true,
                10);

            Span<S_PlayerState> states = stackalloc S_PlayerState[1];
            states[0] = state;
            Span<byte> packet = stackalloc byte[PlayerStateBundlePacket.MaxPacketSize];
            Assert.That(PlayerStateBundlePacket.TryEncode(packet, states, out int written), Is.True);
            Assert.That(server.TrySend(serverConnectionId, UtpChannel.Ephemeral, packet.Slice(0, written)), Is.True);
            server.FlushSends();

            PumpUntil(
                () => client.PendingPlayerStateUpdates == 1,
                () => PumpBoth(client, server, serverHandler));

            // Transport dispatch only queued protocol state; prediction has not been mutated yet.
            Assert.That(prediction.ApplyCount, Is.Zero);
            Assert.That(client.PendingPredictionInputs, Is.EqualTo(1));

            Assert.That(client.ApplyPlayerStateUpdates(out int replayed), Is.EqualTo(1));
            Assert.That(replayed, Is.Zero);
            Assert.That(prediction.ApplyCount, Is.EqualTo(1));
            Assert.That(prediction.LastState, Is.EqualTo(state));
            Assert.That(client.PendingPredictionInputs, Is.Zero);
        }

        private static void PumpBoth(
            ClientNetworkRuntime client,
            UtpServerHost server,
            RecordingServerHandler handler)
        {
            client.PumpTransport();
            server.Pump(handler, handler);
        }

        private static void PumpUntil(Func<bool> condition, Action pump)
        {
            const int maxAttempts = 100;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }

            Assert.That(condition(), Is.True, "UTP player-state loopback condition was not reached.");
        }

        private sealed class RecordingServerHandler : IClientEventCommandHandler, IClientInputCommandHandler
        {
            public int InputCount { get; private set; }
            public void HandleAlterationRequest(uint connectionId, in C_AlterationRequest request) { }
            public void HandlePlayerInput(uint connectionId, in C_PlayerInput input) => InputCount++;
        }

        private sealed class RecordingPredictionAdapter : IClientPredictionAdapter
        {
            public int ApplyCount { get; private set; }
            public S_PlayerState LastState { get; private set; }
            public void ApplyAuthoritativeState(in S_PlayerState state)
            {
                LastState = state;
                ApplyCount++;
            }
            public void ReplayInput(in C_PlayerInput input) { }
        }
    }
}
