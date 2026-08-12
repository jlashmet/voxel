using System;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RegionConvergenceTests
    {
        [Test]
        public void SemanticHashIgnoresMixedBrickPoolIndex()
        {
            var tableA = new RegionTable(1, Allocator.TempJob);
            var tableB = new RegionTable(1, Allocator.TempJob);
            var poolA = new BrickPool(8, Allocator.TempJob);
            var poolB = new BrickPool(8, Allocator.TempJob);
            try
            {
                Region a = tableA.LoadRegion(int3.zero);
                Region b = tableB.LoadRegion(int3.zero);

                int poolAIndex = poolA.Allocate();
                poolA.FillBrick(poolAIndex, 3);

                // Consume one slot only on B so the semantically identical brick gets a different
                // allocator-local index.
                int throwaway = poolB.Allocate();
                poolB.FillBrick(throwaway, 9);
                int poolBIndex = poolB.Allocate();
                poolB.FillBrick(poolBIndex, 3);
                Assert.That(poolAIndex, Is.Not.EqualTo(poolBIndex));

                int brick = Region.BrickIndex(2, 3, 4);
                a.BrickRefs[brick] = BrickRef.FromPoolIndex(poolAIndex);
                b.BrickRefs[brick] = BrickRef.FromPoolIndex(poolBIndex);
                tableA.CommitRegion(a);
                tableB.CommitRegion(b);

                Assert.That(
                    SemanticRegionHasher.HashRegion(in a, in poolA),
                    Is.EqualTo(SemanticRegionHasher.HashRegion(in b, in poolB)));
            }
            finally
            {
                tableA.Dispose();
                tableB.Dispose();
                poolA.Dispose();
                poolB.Dispose();
            }
        }

        [Test]
        public void RegionEventLogKeepsMultipleEventsAtSameTickAndWrapsModulo()
        {
            var log = new RegionEventLog();
            log.Initialize(Allocator.TempJob);
            try
            {
                for (int i = 0; i < RegionEventLog.MaxEventsPerLog + 5; i++)
                {
                    uint tick = (uint)(i / 2 + 1);
                    var evt = new AlterationEvent(
                        AlterationEvent.KindExplosion,
                        tick,
                        new int3(i, 0, 0),
                        1,
                        0,
                        (uint)(i + 1),
                        1,
                        (ushort)(i + 1));
                    log.Push(tick, in evt);
                }

                uint lastTick = (uint)((RegionEventLog.MaxEventsPerLog + 4) / 2 + 1);
                var range = new NativeList<AlterationEvent>(8, Allocator.Temp);
                try
                {
                    Assert.That(log.TryCopyRange(lastTick - 2, lastTick, range), Is.True);
                    Assert.That(range.Length, Is.GreaterThanOrEqualTo(3));

                    bool foundSameTickPair = false;
                    for (int i = 1; i < range.Length; i++)
                        if (range[i - 1].tick == range[i].tick)
                            foundSameTickPair = true;
                    Assert.That(foundSameTickPair, Is.True);
                }
                finally
                {
                    range.Dispose();
                }
            }
            finally
            {
                log.Dispose();
            }
        }

        [Test]
        [Category("Networking")]
        public void ClientDriftProducesVerifiedTickScopedMismatchOverLoopback()
        {
            using var server = new AuthoritativeServerSession(
                serverSeed: 123,
                densityCap: new Validation.DensityCap(1f, 0),
                hashIntervalTicks: 1);
            using var client = new ClientNetworkRuntime();

            uint connectionId = 0;
            bool connected = false;
            bool verified = false;
            ServerConvergenceManager.VerifiedRegionMismatch verifiedMismatch = default;

            server.ConnectionOpened += (id, _) => connectionId = id;
            server.VerifiedRegionMismatch += mismatch =>
            {
                verified = true;
                verifiedMismatch = mismatch;
            };
            client.Connected += () => connected = true;

            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));
            Assert.That(client.Connect(server.LocalEndpoint), Is.True);
            PumpUntil(() => connected && connectionId != 0, () => Pump(client, server));

            var serverTable = new RegionTable(1, Allocator.TempJob);
            var clientTable = new RegionTable(1, Allocator.TempJob);
            var serverPool = new BrickPool(8, Allocator.TempJob);
            var clientPool = new BrickPool(8, Allocator.TempJob);
            ProtectedZones zones = default;
            var inputSink = new NoopInputSink();

            try
            {
                serverTable.LoadRegion(int3.zero);
                clientTable.LoadRegion(int3.zero);
                Assert.That(server.AuthenticateConnection(connectionId, 7, int3.zero, 64), Is.True);

                // Tick 1 hashes identical state: no mismatch.
                server.ProcessAuthoritativeTick(1, ref serverTable, ref serverPool, in zones, inputSink);
                PumpUntil(() => client.PendingRegionHashes == 1, () => Pump(client, server));
                client.ApplyReadyAuthoritativeEvents(ref clientTable, ref clientPool, out _);
                Assert.That(client.PendingRegionHashes, Is.Zero);
                Assert.That(server.ConvergenceInbox.PendingCount, Is.Zero);

                // Corrupt only the client material state.
                Assert.That(VoxelAccess.SetVoxel(
                    ref clientTable,
                    ref clientPool,
                    new int3(1, 1, 1),
                    5), Is.True);

                // Tick 2 hash reaches the ordered barrier, mismatch is sent immediately.
                server.ProcessAuthoritativeTick(2, ref serverTable, ref serverPool, in zones, inputSink);
                PumpUntil(() => client.PendingRegionHashes == 1, () => Pump(client, server));
                client.ApplyReadyAuthoritativeEvents(ref clientTable, ref clientPool, out _);

                PumpUntil(() => server.ConvergenceInbox.PendingCount == 1, () => Pump(client, server));

                // Tick 3 authenticates the report against the exact checkpoint the server issued.
                server.ProcessAuthoritativeTick(3, ref serverTable, ref serverPool, in zones, inputSink);
                Assert.That(verified, Is.True);
                Assert.That(verifiedMismatch.ConnectionId, Is.EqualTo(connectionId));
                Assert.That(verifiedMismatch.RegionCoord, Is.EqualTo(int3.zero));
                Assert.That(verifiedMismatch.HashTick, Is.EqualTo(2));
                Assert.That(verifiedMismatch.ClientHash, Is.Not.EqualTo(verifiedMismatch.ServerHash));
                Assert.That(server.Convergence.VerifiedMismatchCount, Is.EqualTo(1));
            }
            finally
            {
                serverTable.Dispose();
                clientTable.Dispose();
                serverPool.Dispose();
                clientPool.Dispose();
            }
        }

        private static void Pump(ClientNetworkRuntime client, AuthoritativeServerSession server)
        {
            client.PumpTransport();
            server.PumpTransport();
        }

        private static void PumpUntil(Func<bool> condition, Action pump)
        {
            const int maxAttempts = 100;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }
            Assert.That(condition(), Is.True, "Convergence loopback condition was not reached.");
        }

        private sealed class NoopInputSink : IAuthoritativePlayerInputSink
        {
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick) { }
        }
    }
}
