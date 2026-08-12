using System;
using System.Threading;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using VoxelEngine.Core.Storage;
using VoxelEngine.Net.Client;
using VoxelEngine.Net.Protocol;
using VoxelEngine.Net.Server;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class RegionResyncFallbackTests
    {
        [Test]
        [Category("Networking")]
        public void MissingCheckpointSignalsFullResyncAndClientStaysPaused()
        {
            using var server = new AuthoritativeServerSession(
                serverSeed: 77,
                densityCap: new Validation.DensityCap(1f, 0));
            using var client = new ClientNetworkRuntime();

            uint connectionId = 0;
            bool connected = false;
            server.ConnectionOpened += (id, _) => connectionId = id;
            client.Connected += () => connected = true;

            Assert.That(server.Listen(NetworkEndpoint.LoopbackIpv4.WithPort(0)), Is.EqualTo(0));
            Assert.That(client.Connect(server.LocalEndpoint), Is.True);
            PumpUntil(() => connected && connectionId != 0, () => Pump(client, server));

            var table = new RegionTable(1, Allocator.TempJob);
            var pool = new BrickPool(4, Allocator.TempJob);
            ProtectedZones zones = default;
            var inputSink = new NoopInputSink();
            try
            {
                table.LoadRegion(int3.zero);
                Assert.That(server.AuthenticateConnection(connectionId, 5, int3.zero, 64), Is.True);

                // Authenticated/still-interested drift report for a checkpoint this server never
                // issued. This is treated as unavailable history, not as permission to fabricate
                // current-state repair.
                var stale = new C_RegionHashMismatch(
                    int3.zero,
                    hashTick: 999,
                    clientHash: 0x11111111,
                    serverHash: 0x22222222);
                server.ConvergenceInbox.HandleRegionHashMismatch(connectionId, in stale);

                server.ProcessAuthoritativeTick(1, ref table, ref pool, in zones, inputSink);
                PumpUntil(() => client.IsFullRegionResyncRequired, () => Pump(client, server));

                Assert.That(client.LastResyncRequirement.regionCoord, Is.EqualTo(int3.zero));
                Assert.That(client.LastResyncRequirement.failedHashTick, Is.EqualTo(999));
                Assert.That(client.LastResyncRequirement.reason,
                    Is.EqualTo(S_RegionResyncRequired.Reason.CheckpointExpired));
                Assert.That(server.Convergence.ResyncRequiredCount, Is.EqualTo(1));

                Assert.That(client.ApplyReadyAuthoritativeEvents(
                    ref table,
                    ref pool,
                    out int appliedEvents), Is.Zero);
                Assert.That(appliedEvents, Is.Zero);

                client.ResetAfterAuthoritativeSnapshot();
                Assert.That(client.IsFullRegionResyncRequired, Is.False);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
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
            Assert.That(condition(), Is.True, "Full-resync fallback condition was not reached.");
        }

        private sealed class NoopInputSink : IAuthoritativePlayerInputSink
        {
            public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick) { }
        }
    }
}
