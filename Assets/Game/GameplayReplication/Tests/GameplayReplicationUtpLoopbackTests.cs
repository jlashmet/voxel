using System;
using System.Collections.Generic;
using System.Threading;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using Game.GameplayReplication.Transport;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;
using VoxelEngine.Storage.Runtime;

namespace Game.GameplayReplication.Tests
{
    public sealed class GameplayReplicationUtpLoopbackTests
    {
        [Test]
        [Category("Networking")]
        public void ExistingClientsRepairLateJoinerAndReconnectConvergeToCurrentGameplayTruth()
        {
            var characters = new MutableSource("characters", "hero/lifecycle", "Active");
            var inventory = new MutableSource("inventory", "gold", "1");
            var emitter = new GameplayStateServerEmitter(new IGameplayProjectionSource[] { characters, inventory });
            using var server = new AuthoritativeServerSession(
                serverSeed: 0x12345678,
                densityCap: new Validation.DensityCap(1f, 0),
                alterationApplier: new DeterministicAlterationApplier(),
                gameplayStateEmitter: emitter);

            var openedConnections = new List<uint>();
            server.ConnectionOpened += (id, _) => openedConnections.Add(id);
            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));

            using var first = ClientFixture.Connect(server, server.LocalEndpoint, openedConnections, expectedConnectionCount: 1);
            Assert.That(server.AuthenticateConnection(openedConnections[0], 1, int3.zero), Is.True);
            using var second = ClientFixture.Connect(server, server.LocalEndpoint, openedConnections, expectedConnectionCount: 2);
            Assert.That(server.AuthenticateConnection(openedConnections[1], 2, new int3(1, 0, 0)), Is.True);

            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(4, Allocator.TempJob);
            table.LoadRegion(int3.zero);
            try
            {
                Tick(server, 1, in table, in pool);
                PumpUntil(
                    () => first.State.Revision.Value == 1 && second.State.Revision.Value == 1,
                    () => Pump(server, first, second));
                AssertCurrent(first.State, 1, "Active", "1");
                AssertCurrent(second.State, 1, "Active", "1");

                characters.Set("hero/lifecycle", "Defeated");
                inventory.Set("gold", "9");
                Tick(server, 2, in table, in pool);
                PumpUntil(
                    () => first.State.Revision.Value == 2 && second.State.Revision.Value == 2,
                    () => Pump(server, first, second));
                AssertCurrent(first.State, 2, "Defeated", "9");
                AssertCurrent(second.State, 2, "Defeated", "9");

                // Force a semantic revision gap on one client, then prove its repair request travels
                // over the live UTP EVENT path and the next authoritative tick repairs all clients
                // with one coherent snapshot revision.
                var descriptors = new[]
                {
                    new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, true),
                    new GameplayProjectionDescriptor(new GameplayProjectionId("inventory"), 1, true)
                };
                var gap = new GameplayPublication(
                    new GameplayRevision(4),
                    GameplayPublicationKind.Delta,
                    new[]
                    {
                        new GameplayProjectionState(descriptors[0], new[] { new GameplayProjectionEntry("hero/lifecycle", "Defeated") }),
                        new GameplayProjectionState(descriptors[1], new[] { new GameplayProjectionEntry("gold", "9") })
                    });
                Assert.That(GameplayStatePacketCodec.TryEncode(gap, out byte[] gapPacket), Is.True);
                Assert.That(first.Handler.HandleGameplayStatePacket(gapPacket), Is.True);
                Assert.That(first.State.SynchronizationState, Is.EqualTo(GameplaySynchronizationState.RepairRequired));
                Assert.That(first.Handler.LastRepairRequestAccepted, Is.True);

                PumpUntil(
                    () => emitter.PendingRepairRequestCount == 1,
                    () => Pump(server, first, second));
                Tick(server, 3, in table, in pool);
                PumpUntil(
                    () => first.State.Revision.Value == 3 && second.State.Revision.Value == 3,
                    () => Pump(server, first, second));
                AssertCurrent(first.State, 3, "Defeated", "9");
                AssertCurrent(second.State, 3, "Defeated", "9");

                using var late = ClientFixture.Connect(server, server.LocalEndpoint, openedConnections, expectedConnectionCount: 3);
                Assert.That(server.AuthenticateConnection(openedConnections[2], 3, new int3(2, 0, 0)), Is.True);
                Tick(server, 4, in table, in pool);
                PumpUntil(
                    () => first.State.Revision.Value == 4 && second.State.Revision.Value == 4 && late.State.Revision.Value == 4,
                    () => Pump(server, first, second, late));
                AssertCurrent(late.State, 4, "Defeated", "9");

                uint oldSecondConnection = openedConnections[1];
                second.Host.Disconnect();
                PumpUntil(
                    () => !server.Players.TryGetByConnection(oldSecondConnection, out _),
                    () => Pump(server, first, second, late));

                using var reconnected = ClientFixture.Connect(server, server.LocalEndpoint, openedConnections, expectedConnectionCount: 4);
                uint newSecondConnection = openedConnections[3];
                Assert.That(newSecondConnection, Is.Not.EqualTo(oldSecondConnection));
                Assert.That(server.AuthenticateConnection(newSecondConnection, 2, new int3(1, 0, 0)), Is.True);

                inventory.Set("gold", "12");
                Tick(server, 5, in table, in pool);
                PumpUntil(
                    () => first.State.Revision.Value == 5 && late.State.Revision.Value == 5 && reconnected.State.Revision.Value == 5,
                    () => Pump(server, first, late, reconnected));
                AssertCurrent(first.State, 5, "Defeated", "12");
                AssertCurrent(late.State, 5, "Defeated", "12");
                AssertCurrent(reconnected.State, 5, "Defeated", "12");
                Assert.That(reconnected.Handler.LastApplyResult, Is.EqualTo(GameplayApplyResult.Applied));
                Assert.That(reconnected.State.GameplayReady, Is.True);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static void Tick(AuthoritativeServerSession server, uint tick, in RegionTable table, in BrickPool pool)
        {
            ProtectedZones zones = default;
            var read = new RegionReadSource(in table, in pool);
            var mutations = new RegionMutationStore(in table, in pool);
            server.ProcessAuthoritativeTick(tick, read, mutations, read, in zones, new NoInputSink());
        }

        private static void AssertCurrent(GameplayReplicationReadState state, long revision, string lifecycle, string gold)
        {
            Assert.That(state.Revision.Value, Is.EqualTo(revision));
            Assert.That(state.GameplayReady, Is.True);
            Assert.That(state.TryGetProjection(new GameplayProjectionId("characters"), out GameplayProjectionState characters), Is.True);
            Assert.That(characters.Entries[0].Value, Is.EqualTo(lifecycle));
            Assert.That(state.TryGetProjection(new GameplayProjectionId("inventory"), out GameplayProjectionState inventory), Is.True);
            Assert.That(inventory.Entries[0].Value, Is.EqualTo(gold));
        }

        private static void Pump(AuthoritativeServerSession server, params ClientFixture[] clients)
        {
            server.PumpTransport();
            for (int i = 0; i < clients.Length; i++)
                clients[i].Host.PumpTransport();
        }

        private static void PumpUntil(Func<bool> condition, Action pump)
        {
            const int maxAttempts = 200;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }
            Assert.That(condition(), Is.True, "Gameplay replication UTP loopback condition was not reached.");
        }

        private sealed class ClientFixture : IDisposable
        {
            private ClientFixture(ClientNetworkRuntime host, GameplayReplicationReadState state, GameplayStateClientPacketHandler handler)
            {
                Host = host;
                State = state;
                Handler = handler;
            }

            public ClientNetworkRuntime Host { get; }
            public GameplayReplicationReadState State { get; }
            public GameplayStateClientPacketHandler Handler { get; }

            public static ClientFixture Connect(
                AuthoritativeServerSession server,
                NetworkEndpoint endpoint,
                List<uint> openedConnections,
                int expectedConnectionCount)
            {
                var descriptors = new[]
                {
                    new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, true),
                    new GameplayProjectionDescriptor(new GameplayProjectionId("inventory"), 1, true)
                };
                var state = new GameplayReplicationReadState(descriptors);
                var gameplayHandler = new GameplayStateClientPacketHandler(state);
                var host = new ClientNetworkRuntime(
                    new DeterministicAlterationApplier(),
                    gameplayStateHandler: gameplayHandler);
                gameplayHandler.BindRepairRequester(request => host.TryRequestGameplayStateRepair(request));
                bool connected = false;
                host.Connected += () => connected = true;
                Assert.That(host.Connect(endpoint), Is.True);
                var fixture = new ClientFixture(host, state, gameplayHandler);
                PumpUntil(
                    () => connected && openedConnections.Count >= expectedConnectionCount,
                    () =>
                    {
                        server.PumpTransport();
                        host.PumpTransport();
                    });
                return fixture;
            }

            public void Dispose() => Host.Dispose();
        }

        private sealed class MutableSource : IGameplayProjectionSource
        {
            private string _key;
            private string _value;

            public MutableSource(string id, string key, string value)
            {
                Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId(id), 1, true);
                _key = key;
                _value = value;
            }

            public GameplayProjectionDescriptor Descriptor { get; }
            public void Set(string key, string value) { _key = key; _value = value; }
            public GameplayProjectionState Capture() =>
                new GameplayProjectionState(Descriptor, new[] { new GameplayProjectionEntry(_key, _value) });
        }

        private sealed class NoInputSink : IAuthoritativePlayerInputSink
        {
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick) { }
        }
    }
}
