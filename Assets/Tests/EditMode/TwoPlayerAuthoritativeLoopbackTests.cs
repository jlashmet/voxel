using System;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TwoPlayerAuthoritativeLoopbackTests
    {
        [Test]
        [Category("Networking")]
        public void TwoClientsSeeBothPlayersAndConvergeAfterOneAuthoritativeExplosion()
        {
            using var server = new AuthoritativeServerSession(
                serverSeed: 0xC001C0DEu,
                densityCap: new Validation.DensityCap(1f, VoxelDimensions.BricksPerRegion),
                maxConnections: 2,
                playerStateIntervalTicks: 1);
            using var clientOne = new ClientNetworkRuntime();
            using var clientTwo = new ClientNetworkRuntime();

            uint connectionOne = 0;
            uint connectionTwo = 0;
            int openedConnections = 0;
            server.ConnectionOpened += (id, _) =>
            {
                openedConnections++;
                if (openedConnections == 1) connectionOne = id;
                else if (openedConnections == 2) connectionTwo = id;
            };

            bool clientOneConnected = false;
            bool clientTwoConnected = false;
            clientOne.Connected += () => clientOneConnected = true;
            clientTwo.Connected += () => clientTwoConnected = true;

            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));

            // Connect sequentially so the externally-authenticated player IDs are unambiguous.
            Assert.That(clientOne.Connect(server.LocalEndpoint), Is.True);
            PumpUntil(
                () => clientOneConnected && connectionOne != 0,
                () => Pump(server, clientOne));

            int3 playerOnePosition = new int3(96, 96, 96);
            Assert.That(server.AuthenticateConnection(
                connectionOne,
                playerId: 1,
                authoritativePositionVoxels: playerOnePosition,
                reachVoxels: 64), Is.True);

            Assert.That(clientTwo.Connect(server.LocalEndpoint), Is.True);
            PumpUntil(
                () => clientTwoConnected && connectionTwo != 0,
                () => Pump(server, clientOne, clientTwo));

            int3 playerTwoPosition = new int3(100, 96, 96);
            Assert.That(server.AuthenticateConnection(
                connectionTwo,
                playerId: 2,
                authoritativePositionVoxels: playerTwoPosition,
                reachVoxels: 64), Is.True);

            Assert.That(server.UpdateAuthoritativePlayerKinematics(
                connectionOne,
                (float3)playerOnePosition,
                new float3(1f, 0f, 0f),
                viewYaw: 1000,
                stateFlags: S_PlayerState.StateFlags.Grounded), Is.True);
            Assert.That(server.UpdateAuthoritativePlayerKinematics(
                connectionTwo,
                (float3)playerTwoPosition,
                new float3(-1f, 0f, 0f),
                viewYaw: 2000,
                stateFlags: S_PlayerState.StateFlags.Grounded), Is.True);

            var serverTable = new RegionTable(1, Allocator.TempJob);
            var clientOneTable = new RegionTable(1, Allocator.TempJob);
            var clientTwoTable = new RegionTable(1, Allocator.TempJob);
            var serverPool = new BrickPool(16, Allocator.TempJob);
            var clientOnePool = new BrickPool(16, Allocator.TempJob);
            var clientTwoPool = new BrickPool(16, Allocator.TempJob);
            ProtectedZones zones = default;

            try
            {
                SeedSameSolidBrick(ref serverTable, 3);
                SeedSameSolidBrick(ref clientOneTable, 3);
                SeedSameSolidBrick(ref clientTwoTable, 3);

                var request = new C_AlterationRequest(
                    tick: 20,
                    origin: playerTwoPosition,
                    eventKind: AlterationEvent.KindExplosion,
                    material: VoxelDimensions.MaterialEmpty,
                    shapeKind: AlterationEvent.KindExplosion,
                    shapeData: 1,
                    seed: 0,
                    sequence: 1);
                Assert.That(clientTwo.TrySendAlterationRequest(in request), Is.True);
                clientTwo.FlushSends();

                PumpUntil(
                    () => server.CommandInbox.PendingAlterations == 1,
                    () => Pump(server, clientOne, clientTwo));

                var inputSink = new NoopInputSink();
                server.ProcessAuthoritativeTick(
                    20,
                    ref serverTable,
                    ref serverPool,
                    in zones,
                    inputSink);

                PumpUntil(
                    () => clientOne.PendingPlayerStateUpdates == 2 &&
                          clientTwo.PendingPlayerStateUpdates == 2 &&
                          clientOne.PendingAuthoritativeEvents == 1 &&
                          clientTwo.PendingAuthoritativeEvents == 1,
                    () => Pump(server, clientOne, clientTwo));

                Assert.That(clientOne.TrySampleRemotePlayer(1, 1f, out RemotePlayerSample oneSeesOne), Is.True);
                Assert.That(clientOne.TrySampleRemotePlayer(2, 1f, out RemotePlayerSample oneSeesTwo), Is.True);
                Assert.That(clientTwo.TrySampleRemotePlayer(1, 1f, out RemotePlayerSample twoSeesOne), Is.True);
                Assert.That(clientTwo.TrySampleRemotePlayer(2, 1f, out RemotePlayerSample twoSeesTwo), Is.True);
                Assert.That(oneSeesOne.PositionVoxels, Is.EqualTo((float3)playerOnePosition));
                Assert.That(oneSeesTwo.PositionVoxels, Is.EqualTo((float3)playerTwoPosition));
                Assert.That(twoSeesOne.PositionVoxels, Is.EqualTo((float3)playerOnePosition));
                Assert.That(twoSeesTwo.PositionVoxels, Is.EqualTo((float3)playerTwoPosition));

                Assert.That(clientOne.ApplyPlayerStateUpdates(out _), Is.EqualTo(2));
                Assert.That(clientTwo.ApplyPlayerStateUpdates(out _), Is.EqualTo(2));

                Assert.That(clientOne.ApplyReadyAuthoritativeEvents(
                    ref clientOneTable,
                    ref clientOnePool,
                    out int clientOneEvents), Is.EqualTo(1));
                Assert.That(clientTwo.ApplyReadyAuthoritativeEvents(
                    ref clientTwoTable,
                    ref clientTwoPool,
                    out int clientTwoEvents), Is.EqualTo(1));
                Assert.That(clientOneEvents, Is.EqualTo(1));
                Assert.That(clientTwoEvents, Is.EqualTo(1));

                for (int z = 96; z < 104; z++)
                for (int y = 96; y < 104; y++)
                for (int x = 96; x < 104; x++)
                {
                    int3 voxel = new int3(x, y, z);
                    byte serverMaterial = VoxelAccess.GetVoxel(ref serverTable, in serverPool, voxel);
                    Assert.That(
                        VoxelAccess.GetVoxel(ref clientOneTable, in clientOnePool, voxel),
                        Is.EqualTo(serverMaterial),
                        $"Player 1 world mismatch at {voxel}");
                    Assert.That(
                        VoxelAccess.GetVoxel(ref clientTwoTable, in clientTwoPool, voxel),
                        Is.EqualTo(serverMaterial),
                        $"Player 2 world mismatch at {voxel}");
                }
            }
            finally
            {
                serverTable.Dispose();
                clientOneTable.Dispose();
                clientTwoTable.Dispose();
                serverPool.Dispose();
                clientOnePool.Dispose();
                clientTwoPool.Dispose();
            }
        }

        private static void SeedSameSolidBrick(ref RegionTable table, byte material)
        {
            Region region = table.LoadRegion(int3.zero);
            int index = Region.BrickIndex(12, 12, 12);
            region.BrickRefs[index] = BrickRef.Uniform(material);
            table.CommitRegion(region);
        }

        private static void Pump(
            AuthoritativeServerSession server,
            ClientNetworkRuntime first,
            ClientNetworkRuntime second = null)
        {
            first.PumpTransport();
            second?.PumpTransport();
            server.PumpTransport();
        }

        private static void PumpUntil(Func<bool> condition, Action pump)
        {
            const int maxAttempts = 200;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }

            Assert.That(condition(), Is.True, "Two-player authoritative loopback condition was not reached.");
        }

        private sealed class NoopInputSink : IAuthoritativePlayerInputSink
        {
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick)
            {
            }
        }
    }
}
