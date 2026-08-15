using System;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DeterministicNetworkParityTests
    {
        [Test]
        [Category("Networking")]
        public void AuthoritativeExplosionProducesIdenticalServerAndClientVoxelState()
        {
            using var server = new AuthoritativeServerSession(
                serverSeed: 0xA11CE55u,
                densityCap: new Validation.DensityCap(1f, 0),
                alterationApplier: new DeterministicAlterationApplier());
            using var client = new ClientNetworkRuntime(new DeterministicAlterationApplier());

            uint connectionId = 0;
            bool clientConnected = false;
            server.ConnectionOpened += (id, _) => connectionId = id;
            client.Connected += () => clientConnected = true;

            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));
            Assert.That(client.Connect(server.LocalEndpoint), Is.True);

            PumpUntil(
                () => clientConnected && connectionId != 0,
                () =>
                {
                    client.PumpTransport();
                    server.PumpTransport();
                });

            var serverTable = new RegionTable(1, Allocator.TempJob);
            var clientTable = new RegionTable(1, Allocator.TempJob);
            var serverPool = new BrickPool(16, Allocator.TempJob);
            var clientPool = new BrickPool(16, Allocator.TempJob);
            ProtectedZones zones = default;

            try
            {
                SeedSameSolidBrick(ref serverTable, 3);
                SeedSameSolidBrick(ref clientTable, 3);

                var origin = new int3(96, 96, 96);
                Assert.That(server.AuthenticateConnection(
                    connectionId,
                    playerId: 7,
                    authoritativePositionVoxels: origin,
                    reachVoxels: 64), Is.True);

                var request = new C_AlterationRequest(
                    tick: 20,
                    origin,
                    eventKind: AlterationEvent.KindExplosion,
                    material: 0,
                    shapeKind: AlterationEvent.KindExplosion,
                    shapeData: 1,
                    seed: 0xDEADBEEFu,
                    sequence: 1);

                Assert.That(client.TrySendAlterationRequest(in request), Is.True);
                client.FlushSends();

                PumpUntil(
                    () => server.CommandInbox.PendingAlterations == 1,
                    () =>
                    {
                        server.PumpTransport();
                        client.PumpTransport();
                    });

                var inputSink = new NoopInputSink();
                var applier = new DeterministicAlterationApplier();
                server.ProcessAuthoritativeTick(
                    20,
                    new RegionReadSource(in serverTable, in serverPool), new RegionMutationStore(in serverTable, in serverPool), new RegionReadSource(in serverTable, in serverPool), in zones,
                    inputSink,
                    applier);

                PumpUntil(
                    () => client.PendingAuthoritativeEvents == 1,
                    () =>
                    {
                        client.PumpTransport();
                        server.PumpTransport();
                    });

                Assert.That(client.ApplyReadyAuthoritativeEvents(new RegionMutationStore(in clientTable, in clientPool), new RegionReadSource(in clientTable, in clientPool), new RegionSnapshotMutationStore(in clientTable, in clientPool), out int appliedEvents), Is.EqualTo(1));
                Assert.That(appliedEvents, Is.EqualTo(1));
                Assert.That(client.PendingAuthoritativeEvents, Is.Zero);

                // Compare every voxel in the partially damaged source brick. The two pools are
                // independent; parity is defined by world material, not by internal pool indices.
                for (int z = 96; z < 104; z++)
                for (int y = 96; y < 104; y++)
                for (int x = 96; x < 104; x++)
                {
                    int3 voxel = new int3(x, y, z);
                    Assert.That(
                        VoxelAccess.GetVoxel(ref clientTable, in clientPool, voxel),
                        Is.EqualTo(VoxelAccess.GetVoxel(ref serverTable, in serverPool, voxel)),
                        $"Voxel mismatch at {voxel}");
                }

                Assert.That(serverPool.AllocatedCount, Is.EqualTo(clientPool.AllocatedCount));
                Assert.That(serverPool.AllocatedCount, Is.EqualTo(1));
            }
            finally
            {
                serverTable.Dispose();
                clientTable.Dispose();
                serverPool.Dispose();
                clientPool.Dispose();
            }
        }

        private static void SeedSameSolidBrick(ref RegionTable table, byte material)
        {
            Region region = table.LoadRegion(int3.zero);
            int index = Region.BrickIndex(12, 12, 12);
            region.BrickRefs[index] = BrickRef.Uniform(material);
            table.CommitRegion(region);
        }

        private static void PumpUntil(Func<bool> condition, Action pump)
        {
            const int maxAttempts = 100;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }

            Assert.That(condition(), Is.True, "Deterministic network parity condition was not reached.");
        }

        private sealed class NoopInputSink : IAuthoritativePlayerInputSink
        {
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick)
            {
            }
        }
    }
}
