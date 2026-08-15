using System;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Edits.Runtime;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Runtime.Client;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RegionStateLoopbackTests
    {
        [Test]
        [Category("Networking")]
        public void ExpiredRepairEscalatesToBulkCurrentStateAndResumesEventAuthority()
        {
            using var server = new AuthoritativeServerSession(
                serverSeed: 0x12345678,
                densityCap: new Validation.DensityCap(1f, 0),
                alterationApplier: new DeterministicAlterationApplier());
            using var client = new ClientNetworkRuntime(new DeterministicAlterationApplier());

            uint connectionId = 0;
            bool connected = false;
            server.ConnectionOpened += (id, _) => connectionId = id;
            client.Connected += () => connected = true;

            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));
            Assert.That(client.Connect(server.LocalEndpoint), Is.True);
            PumpUntil(() => connected && connectionId != 0, () => Pump(client, server));

            var serverTable = new RegionTable(2, Allocator.TempJob);
            var clientTable = new RegionTable(2, Allocator.TempJob);
            var serverPool = new BrickPool(16, Allocator.TempJob);
            var clientPool = new BrickPool(16, Allocator.TempJob);
            ProtectedZones zones = default;
            var inputSink = new NoopInputSink();

            try
            {
                serverTable.LoadRegion(int3.zero);
                clientTable.LoadRegion(int3.zero);
                Assert.That(server.AuthenticateConnection(connectionId, 9, int3.zero, 64), Is.True);

                int3 differingVoxel = new int3(20, 20, 20);
                Assert.That(VoxelAccess.SetVoxel(ref serverTable, ref serverPool, differingVoxel, 5), Is.True);
                Assert.That(VoxelAccess.SetVoxel(ref clientTable, ref clientPool, differingVoxel, 9), Is.True);

                // Simulate a real client surfacing a hash from history older than the server's
                // retained exact checkpoints. The server cannot safely synthesize that old state,
                // so it escalates to current semantic state instead.
                var expired = new C_RegionHashMismatch(
                    int3.zero,
                    hashTick: 1,
                    clientHash: 0x11111111,
                    serverHash: 0x22222222);
                server.ConvergenceInbox.HandleRegionHashMismatch(connectionId, in expired);

                server.ProcessAuthoritativeTick(100, ref serverTable, ref serverPool, in zones, inputSink);
                PumpUntil(
                    () => client.IsFullRegionResyncRequired && server.RegionStateInbox.PendingCount == 1,
                    () => Pump(client, server));

                Assert.That(client.FullSnapshotWaitPending, Is.True);
                Assert.That(server.Convergence.ResyncRequiredCount, Is.EqualTo(1));

                // Tick 101 captures the authoritative current region only after the fixed-tick world
                // state is final, queues the EVENT fence, and sends the semantic bytes over BULK.
                server.ProcessAuthoritativeTick(101, ref serverTable, ref serverPool, in zones, inputSink);
                PumpUntil(
                    () => client.CompletedFullStateTransfers == 1 && client.PendingRegionStateFences == 1,
                    () => Pump(client, server));

                Assert.That(client.ApplyReadyAuthoritativeEvents(
                    ref clientTable,
                    ref clientPool,
                    out int appliedEvents), Is.GreaterThanOrEqualTo(0));
                Assert.That(appliedEvents, Is.Zero);

                Assert.That(client.IsFullRegionResyncRequired, Is.False);
                Assert.That(client.FullSnapshotWaitPending, Is.False);
                Assert.That(client.SnapshotCatchupActive, Is.False,
                    "Matching EVENT fence should end duplicate suppression even when no old EVENT batch was queued.");
                Assert.That(client.PendingRegionStateFences, Is.Zero);
                Assert.That(VoxelAccess.GetVoxel(ref clientTable, in clientPool, differingVoxel), Is.EqualTo(5));

                Assert.That(serverTable.TryGetRegion(int3.zero, out Region serverRegion), Is.True);
                Assert.That(clientTable.TryGetRegion(int3.zero, out Region clientRegion), Is.True);
                Assert.That(
                    SemanticRegionHasher.HashRegion(in clientRegion, in clientPool),
                    Is.EqualTo(SemanticRegionHasher.HashRegion(in serverRegion, in serverPool)));

                Assert.That(server.BulkRegionState.CompletedTransfers, Is.EqualTo(1));
                Assert.That(server.BulkRegionState.PendingTransferCount, Is.Zero);
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
            const int maxAttempts = 200;
            for (int i = 0; i < maxAttempts && !condition(); i++)
            {
                pump();
                Thread.Sleep(1);
            }
            Assert.That(condition(), Is.True, "Full-state loopback condition was not reached.");
        }

        private sealed class NoopInputSink : IAuthoritativePlayerInputSink
        {
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick) { }
        }
    }
}
