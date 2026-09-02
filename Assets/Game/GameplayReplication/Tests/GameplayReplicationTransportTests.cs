using System;
using System.Collections.Generic;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using Game.GameplayReplication.Transport;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Runtime.Server;

namespace Game.GameplayReplication.Tests
{
    public sealed class GameplayReplicationTransportTests
    {
        [Test]
        public void GameplayPacketCodecRoundTripsSemanticPublication()
        {
            var descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("inventory"), 2, true);
            var source = new StubSource(descriptor, "gold", "7", "wood", "3");
            var publication = new GameplayPublicationBuilder(new[] { source }).PublishSnapshot();

            Assert.That(GameplayStatePacketCodec.TryEncode(publication, out byte[] packet), Is.True);
            Assert.That(GameplayStatePacketCodec.TryDecode(packet, out GameplayPublication decoded), Is.True);
            Assert.That(decoded.Revision.Value, Is.EqualTo(1));
            Assert.That(decoded.Kind, Is.EqualTo(GameplayPublicationKind.Snapshot));
            Assert.That(decoded.Projections.Count, Is.EqualTo(1));
            Assert.That(decoded.Projections[0].Descriptor.Id.Value, Is.EqualTo("inventory"));
            Assert.That(decoded.Projections[0].Descriptor.SchemaVersion, Is.EqualTo(2));
            Assert.That(decoded.Projections[0].Entries[0].Key, Is.EqualTo("gold"));
            Assert.That(decoded.Projections[0].Entries[0].Value, Is.EqualTo("7"));
        }

        [Test]
        public void ServerEmitterUsesOneRevisionAndSnapshotsWhenConnectionSetExpands()
        {
            var descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, true);
            var emitter = new GameplayStateServerEmitter(new[] { new StubSource(descriptor, "hero/lifecycle", "Active") });
            var players = new ServerPlayerRegistry();
            var sink = new CapturingPacketSink();

            Assert.That(players.TryRegisterAuthenticated(10, 1, new int3(0, 0, 0)), Is.True);
            emitter.Emit(100, players, sink);
            Assert.That(sink.Packets.Count, Is.EqualTo(1));
            AssertPublication(sink.Packets[0].Packet, 1, GameplayPublicationKind.Snapshot);

            sink.Clear();
            emitter.Emit(101, players, sink);
            Assert.That(sink.Packets.Count, Is.EqualTo(1));
            AssertPublication(sink.Packets[0].Packet, 2, GameplayPublicationKind.Delta);

            Assert.That(players.TryRegisterAuthenticated(20, 2, new int3(1, 0, 0)), Is.True);
            sink.Clear();
            emitter.Emit(102, players, sink);
            Assert.That(sink.Packets.Count, Is.EqualTo(2));
            Assert.That(sink.Packets[0].ConnectionId, Is.EqualTo(10));
            Assert.That(sink.Packets[1].ConnectionId, Is.EqualTo(20));
            AssertPublication(sink.Packets[0].Packet, 3, GameplayPublicationKind.Snapshot);
            AssertPublication(sink.Packets[1].Packet, 3, GameplayPublicationKind.Snapshot);
        }

        [Test]
        public void ClientHandlerTreatsGapAsValidPacketAndSignalsRepair()
        {
            var descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, true);
            var readState = new GameplayReplicationReadState(new[] { descriptor });
            var handler = new GameplayStateClientPacketHandler(readState);
            GameplayApplyResult? repair = null;
            handler.RepairRequired += result => repair = result;

            Assert.That(GameplayStatePacketCodec.TryEncode(Publication(1, GameplayPublicationKind.Snapshot, descriptor, "hero", "Active"), out byte[] first), Is.True);
            Assert.That(handler.HandleGameplayStatePacket(first), Is.True);
            Assert.That(readState.GameplayReady, Is.True);

            Assert.That(GameplayStatePacketCodec.TryEncode(Publication(3, GameplayPublicationKind.Delta, descriptor, "hero", "Defeated"), out byte[] gap), Is.True);
            Assert.That(handler.HandleGameplayStatePacket(gap), Is.True);
            Assert.That(handler.LastApplyResult, Is.EqualTo(GameplayApplyResult.GapDetected));
            Assert.That(repair, Is.EqualTo(GameplayApplyResult.GapDetected));
            Assert.That(readState.SynchronizationState, Is.EqualTo(GameplaySynchronizationState.RepairRequired));
        }

        private static GameplayPublication Publication(long revision, GameplayPublicationKind kind, GameplayProjectionDescriptor descriptor, string key, string value) =>
            new GameplayPublication(
                new GameplayRevision(revision),
                kind,
                new[] { new GameplayProjectionState(descriptor, new[] { new GameplayProjectionEntry(key, value) }) });

        private static void AssertPublication(byte[] packet, long revision, GameplayPublicationKind kind)
        {
            Assert.That(GameplayStatePacketCodec.TryDecode(packet, out GameplayPublication publication), Is.True);
            Assert.That(publication.Revision.Value, Is.EqualTo(revision));
            Assert.That(publication.Kind, Is.EqualTo(kind));
        }

        private sealed class StubSource : IGameplayProjectionSource
        {
            private readonly GameplayProjectionEntry[] _entries;

            public StubSource(GameplayProjectionDescriptor descriptor, params string[] pairs)
            {
                Descriptor = descriptor;
                var entries = new List<GameplayProjectionEntry>();
                for (int i = 0; i < pairs.Length; i += 2)
                    entries.Add(new GameplayProjectionEntry(pairs[i], pairs[i + 1]));
                _entries = entries.ToArray();
            }

            public GameplayProjectionDescriptor Descriptor { get; }
            public GameplayProjectionState Capture() => new GameplayProjectionState(Descriptor, _entries);
        }

        private sealed class CapturingPacketSink : IGameplayStatePacketSink
        {
            public readonly List<CapturedPacket> Packets = new List<CapturedPacket>();

            public bool SendGameplayStatePacket(uint connectionId, ReadOnlySpan<byte> packet)
            {
                Packets.Add(new CapturedPacket(connectionId, packet.ToArray()));
                return true;
            }

            public void Clear() => Packets.Clear();
        }

        private readonly struct CapturedPacket
        {
            public CapturedPacket(uint connectionId, byte[] packet)
            {
                ConnectionId = connectionId;
                Packet = packet;
            }

            public uint ConnectionId { get; }
            public byte[] Packet { get; }
        }
    }
}
