using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Encounters.Api;
using Game.GameplayReplication.Adapters;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using Game.Inventory.Api;
using Game.Sessions.Api;
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
        public void ExistingAuthorityAdaptersProduceStableSemanticProjections()
        {
            var hero = Character("character:hero", CharacterLifecycleState.Active, 7, 3f);
            var enemy = Character("character:enemy", CharacterLifecycleState.Defeated, 9, -2f);
            var characters = new CharactersGameplayProjectionSource(new CharacterQueryFixture(hero, enemy));

            var encounterId = new EncounterId("encounter:ridge");
            var encounter = new EncounterSnapshot(
                new EncounterDefinition(encounterId, EncounterCombatPolicy.Required, "ambush"),
                EncounterLifecycleState.Active,
                new EncounterMembershipSnapshot(new[]
                {
                    new EncounterParticipant(hero.Id, EncounterParticipantOwnership.Persistent, "player"),
                    new EncounterParticipant(enemy.Id, EncounterParticipantOwnership.EncounterOwned, "enemy")
                }),
                null,
                "trigger:ridge",
                "ridge-realization",
                4);
            var encounters = new EncounterGameplayProjectionSource(new EncounterQueryFixture(encounter));

            var combat = new CombatGameplayProjectionSource(new CombatFixture(
                new CombatSessionId(3),
                new CombatParticipant(new CombatParticipantId(enemy.Id.Value), CombatTeam.Enemy),
                new CombatParticipant(new CombatParticipantId(hero.Id.Value), CombatTeam.Player)));

            var inventory = new InventoryGameplayProjectionSource(new InventoryFixture(
                new InventoryItemSnapshot(new ItemDefinition(new ItemRef("wood"), "Wood"), 5),
                new InventoryItemSnapshot(new ItemDefinition(new ItemRef("ore"), "Ore"), 2)));

            var builder = new GameplayPublicationBuilder(new IGameplayProjectionSource[] { inventory, combat, characters, encounters });
            GameplayPublication publication = builder.PublishSnapshot();

            Assert.That(publication.Projections[0].Descriptor.Id.Value, Is.EqualTo("characters"));
            Assert.That(publication.Projections[1].Descriptor.Id.Value, Is.EqualTo("combat"));
            Assert.That(publication.Projections[2].Descriptor.Id.Value, Is.EqualTo("encounters"));
            Assert.That(publication.Projections[3].Descriptor.Id.Value, Is.EqualTo("inventory"));
            Assert.That(publication.Projections[0].Entries[0].Key, Is.EqualTo("character:enemy/facing"));
            Assert.That(publication.Projections[2].Entries[0].Key, Is.EqualTo("encounter:ridge/activation-cause"));
            Assert.That(publication.Projections[3].Entries[0].Key, Is.EqualTo("ore"));
            Assert.That(publication.Projections[3].Entries[0].Value, Is.EqualTo("2"));
        }

        [Test]
        public void SessionsProjectionUsesDurableIdentityAndStableSlotOrder()
        {
            var hero = new CharacterId("character:hero");
            var scout = new CharacterId("character:scout");
            var source = new SessionsGameplayProjectionSource(new SessionQueryFixture(
                new GameSessionId("session:co-op"),
                new PartyMemberSnapshot(new PartyMemberId("member:b"), new PlayerSlot(3), PartyLeadershipRole.Member, PartyPresenceState.Connected, SessionReadinessState.GameplayReady, scout),
                new PartyMemberSnapshot(new PartyMemberId("member:a"), new PlayerSlot(0), PartyLeadershipRole.Leader, PartyPresenceState.Connected, SessionReadinessState.Synchronized, hero)));

            GameplayProjectionState state = source.Capture();

            Assert.That(state.Descriptor.Id.Value, Is.EqualTo("sessions"));
            Assert.That(state.Entries[0].Key, Is.EqualTo("session-id"));
            Assert.That(state.Entries[0].Value, Is.EqualTo("session:co-op"));
            Assert.That(state.Entries[1].Key, Is.EqualTo("slot/0/character-id"));
            Assert.That(state.Entries[1].Value, Is.EqualTo("character:hero"));
            Assert.That(state.Entries[2].Key, Is.EqualTo("slot/0/leadership"));
            Assert.That(state.Entries[5].Key, Is.EqualTo("slot/0/readiness"));
            Assert.That(state.Entries[6].Key, Is.EqualTo("slot/3/character-id"));
            Assert.That(state.Entries[6].Value, Is.EqualTo("character:scout"));
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

        private static CharacterSnapshot Character(string id, CharacterLifecycleState lifecycle, ulong revision, float x)
        {
            var kinematics = new CharacterKinematicState(
                new CharacterVector3(x, 1f, 2f),
                new CharacterVector3(0f, 0f, 0f),
                new CharacterVector3(0f, 0f, 1f));
            return new CharacterSnapshot(new CharacterDefinition(new CharacterId(id), CharacterTraits.Combatant), lifecycle, kinematics, revision);
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

        private sealed class CharacterQueryFixture : ICharacterQuery
        {
            private readonly CharacterSnapshot[] _snapshots;
            public CharacterQueryFixture(params CharacterSnapshot[] snapshots) => _snapshots = snapshots;
            public IReadOnlyList<CharacterSnapshot> GetAll() => _snapshots;
            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot)
            {
                foreach (CharacterSnapshot candidate in _snapshots)
                {
                    if (candidate.Id == id) { snapshot = candidate; return true; }
                }
                snapshot = null;
                return false;
            }
            public bool TryResolve(CharacterBinding binding, out CharacterId id) { id = default; return false; }
        }

        private sealed class EncounterQueryFixture : IEncounterQuery
        {
            private readonly EncounterSnapshot[] _snapshots;
            public EncounterQueryFixture(params EncounterSnapshot[] snapshots) => _snapshots = snapshots;
            public IReadOnlyList<EncounterSnapshot> GetAll() => _snapshots;
            public bool TryGet(EncounterId id, out EncounterSnapshot snapshot)
            {
                foreach (EncounterSnapshot candidate in _snapshots)
                {
                    if (candidate.Id == id) { snapshot = candidate; return true; }
                }
                snapshot = null;
                return false;
            }
        }

        private sealed class CombatFixture : ICombatService
        {
            private readonly CombatParticipant[] _participants;
            public CombatFixture(CombatSessionId session, params CombatParticipant[] participants)
            {
                ActiveSessionId = session;
                _participants = participants;
            }
            public bool IsActive => true;
            public CombatLifecycleState State => CombatLifecycleState.Active;
            public CombatSessionId ActiveSessionId { get; }
            public IReadOnlyList<CombatParticipant> ActiveParticipants => _participants;
            public CombatSessionId BeginCombat(CombatEncounterRequest request) => throw new NotSupportedException();
            public void CompleteCombat() => throw new NotSupportedException();
        }

        private sealed class InventoryFixture : IInventoryRuntime
        {
            private readonly InventoryItemSnapshot[] _items;
            public InventoryFixture(params InventoryItemSnapshot[] items) => _items = items;
            public bool TryAddUnique(ItemRef item) => throw new NotSupportedException();
            public void Add(ItemRef item, int quantity = 1) => throw new NotSupportedException();
            public int Count(ItemRef item) => throw new NotSupportedException();
            public IReadOnlyList<InventoryItemSnapshot> Snapshot() => _items;
        }

        private sealed class SessionQueryFixture : IPartySessionQuery
        {
            private readonly PartyRosterSnapshot _snapshot;
            public SessionQueryFixture(GameSessionId sessionId, params PartyMemberSnapshot[] members)
                => _snapshot = new PartyRosterSnapshot(sessionId, members);
            public PartyRosterSnapshot Snapshot() => _snapshot;
            public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
            {
                foreach (PartyMemberSnapshot candidate in _snapshot.Members)
                {
                    if (candidate.MemberId == memberId) { member = candidate; return true; }
                }
                member = default;
                return false;
            }
        }
    }
}
