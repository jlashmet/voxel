using System.Collections.Generic;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using NUnit.Framework;

namespace Game.GameplayReplication.Tests
{
    public sealed class GameplayReplicationRuntimeTests
    {
        [Test]
        public void PublicationBuilderProducesStableProjectionOrderAndMonotonicRevision()
        {
            var z = new StubSource("zeta", true, "b", "2", "a", "1");
            var a = new StubSource("alpha", true, "d", "4", "c", "3");
            var builder = new GameplayPublicationBuilder(new IGameplayProjectionSource[] { z, a });

            GameplayPublication first = builder.PublishSnapshot();
            GameplayPublication second = builder.PublishDelta();

            Assert.That(first.Revision.Value, Is.EqualTo(1));
            Assert.That(second.Revision.Value, Is.EqualTo(2));
            Assert.That(first.Projections[0].Descriptor.Id.Value, Is.EqualTo("alpha"));
            Assert.That(first.Projections[1].Descriptor.Id.Value, Is.EqualTo("zeta"));
            Assert.That(first.Projections[0].Entries[0].Key, Is.EqualTo("c"));
            Assert.That(first.Projections[0].Entries[1].Key, Is.EqualTo("d"));
        }

        [Test]
        public void ClientRejectsDuplicateAndGapThenSnapshotRepairsToCurrentTruth()
        {
            var descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, true);
            var client = new GameplayReplicationReadState(new[] { descriptor });

            Assert.That(client.Apply(Publication(1, GameplayPublicationKind.Snapshot, descriptor, "hero", "alive")), Is.EqualTo(GameplayApplyResult.Applied));
            Assert.That(client.GameplayReady, Is.True);
            Assert.That(client.Apply(Publication(1, GameplayPublicationKind.Delta, descriptor, "hero", "dead")), Is.EqualTo(GameplayApplyResult.DuplicateOrStale));
            Assert.That(client.Apply(Publication(3, GameplayPublicationKind.Delta, descriptor, "hero", "dead")), Is.EqualTo(GameplayApplyResult.GapDetected));
            Assert.That(client.SynchronizationState, Is.EqualTo(GameplaySynchronizationState.RepairRequired));
            Assert.That(client.GameplayReady, Is.False);

            Assert.That(client.Apply(Publication(5, GameplayPublicationKind.Snapshot, descriptor, "hero", "dead")), Is.EqualTo(GameplayApplyResult.Applied));
            Assert.That(client.Revision.Value, Is.EqualTo(5));
            Assert.That(client.GameplayReady, Is.True);
            Assert.That(client.TryGetProjection(descriptor.Id, out GameplayProjectionState repaired), Is.True);
            Assert.That(repaired.Entries[0].Value, Is.EqualTo("dead"));
        }

        [Test]
        public void GameplayReadyRequiresEveryConfiguredBarrierNotConnectivity()
        {
            var characters = new GameplayProjectionDescriptor(new GameplayProjectionId("characters"), 1, true);
            var inventory = new GameplayProjectionDescriptor(new GameplayProjectionId("inventory"), 1, true);
            var optional = new GameplayProjectionDescriptor(new GameplayProjectionId("cosmetic-debug"), 1, false);
            var client = new GameplayReplicationReadState(new[] { characters, inventory, optional });

            Assert.That(client.GameplayReady, Is.False);
            Assert.That(client.Apply(new GameplayPublication(new GameplayRevision(1), GameplayPublicationKind.Snapshot, new[] { State(characters, "hero", "alive") })), Is.EqualTo(GameplayApplyResult.Applied));
            Assert.That(client.GameplayReady, Is.False);
            Assert.That(client.Apply(new GameplayPublication(new GameplayRevision(2), GameplayPublicationKind.Delta, new[] { State(inventory, "gold", "10") })), Is.EqualTo(GameplayApplyResult.Applied));
            Assert.That(client.GameplayReady, Is.True);
        }

        [Test]
        public void SchemaMismatchRequiresRepairAndDoesNotOverwriteCurrentTruth()
        {
            var expected = new GameplayProjectionDescriptor(new GameplayProjectionId("combat"), 1, true);
            var incompatible = new GameplayProjectionDescriptor(expected.Id, 2, true);
            var client = new GameplayReplicationReadState(new[] { expected });
            Assert.That(client.Apply(Publication(1, GameplayPublicationKind.Snapshot, expected, "session", "open")), Is.EqualTo(GameplayApplyResult.Applied));

            Assert.That(client.Apply(Publication(2, GameplayPublicationKind.Delta, incompatible, "session", "closed")), Is.EqualTo(GameplayApplyResult.IncompatibleProjection));
            Assert.That(client.SynchronizationState, Is.EqualTo(GameplaySynchronizationState.RepairRequired));
            Assert.That(client.TryGetProjection(expected.Id, out GameplayProjectionState current), Is.True);
            Assert.That(current.Entries[0].Value, Is.EqualTo("open"));
        }

        private static GameplayPublication Publication(long revision, GameplayPublicationKind kind, GameplayProjectionDescriptor descriptor, string key, string value)
            => new GameplayPublication(new GameplayRevision(revision), kind, new[] { State(descriptor, key, value) });

        private static GameplayProjectionState State(GameplayProjectionDescriptor descriptor, string key, string value)
            => new GameplayProjectionState(descriptor, new[] { new GameplayProjectionEntry(key, value) });

        private sealed class StubSource : IGameplayProjectionSource
        {
            private readonly GameplayProjectionEntry[] _entries;
            public StubSource(string id, bool required, params string[] pairs)
            {
                Descriptor = new GameplayProjectionDescriptor(new GameplayProjectionId(id), 1, required);
                var entries = new List<GameplayProjectionEntry>();
                for (int i = 0; i < pairs.Length; i += 2) entries.Add(new GameplayProjectionEntry(pairs[i], pairs[i + 1]));
                _entries = entries.ToArray();
            }
            public GameplayProjectionDescriptor Descriptor { get; }
            public GameplayProjectionState Capture() => new GameplayProjectionState(Descriptor, _entries);
        }
    }
}
