using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Characters.Api;
using Game.Composition.Kentridge.Playable.Validation;
using Game.Continuity.Api;
using Game.GameplayReplication.Api;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests.EditMode
{
    public sealed class MultiplayerDiagnosticSnapshotTests
    {
        [Test]
        public void Capture_CopiesDurableIdentityReplicationAndRecoveryState()
        {
            var sessionId = new GameSessionId("session-25");
            var memberA = new PartyMemberId("member-a");
            var memberB = new PartyMemberId("member-b");
            var members = new[]
            {
                new PartyMemberSnapshot(
                    memberB,
                    new PlayerSlot(1),
                    PartyLeadershipRole.Member,
                    PartyPresenceState.Connected,
                    SessionReadinessState.Synchronized,
                    new CharacterId("character-b")),
                new PartyMemberSnapshot(
                    memberA,
                    new PlayerSlot(0),
                    PartyLeadershipRole.Leader,
                    PartyPresenceState.Connected,
                    SessionReadinessState.GameplayReady,
                    new CharacterId("character-a"))
            };
            var party = new FakePartySessionQuery(new PartyRosterSnapshot(sessionId, members));

            var projectionId = new GameplayProjectionId("inventory");
            var replication = new FakeReplicationReadState(
                new GameplayRevision(12),
                GameplaySynchronizationState.Synchronized,
                true,
                new GameplayProjectionState(
                    new GameplayProjectionDescriptor(projectionId, 3, true),
                    new[]
                    {
                        new GameplayProjectionEntry("gold", "7"),
                        new GameplayProjectionEntry("potion", "2")
                    }));
            var continuity = new FakeContinuityQuery(
                memberB,
                new RecoverySnapshot(memberB, RecoveryState.Resynchronizing, 42.5));

            var source = new MultiplayerDiagnosticSnapshotSource(
                party,
                replication,
                continuity,
                new[] { projectionId });

            MultiplayerDiagnosticSnapshot captured = source.Capture();

            // Mutate the fake live sources after capture. Durable evidence must retain copied values.
            members[0] = new PartyMemberSnapshot(
                new PartyMemberId("changed-member"),
                new PlayerSlot(3),
                PartyLeadershipRole.Leader,
                PartyPresenceState.Disconnected,
                SessionReadinessState.Joined,
                new CharacterId("changed-character"));
            replication.Replace(new GameplayRevision(99), false, null);
            continuity.Replace(memberB, new RecoverySnapshot(memberB, RecoveryState.Left, 0));

            Assert.That(captured.SessionId, Is.EqualTo("session-25"));
            Assert.That(captured.GameplayRevision, Is.EqualTo(12));
            Assert.That(captured.SynchronizationState, Is.EqualTo(GameplaySynchronizationState.Synchronized));
            Assert.That(captured.GameplayReady, Is.True);

            Assert.That(captured.Members.Count, Is.EqualTo(2));
            Assert.That(captured.Members[0].MemberId, Is.EqualTo("member-a"));
            Assert.That(captured.Members[0].Slot, Is.EqualTo(0));
            Assert.That(captured.Members[0].CharacterId, Is.EqualTo("character-a"));
            Assert.That(captured.Members[0].HasRecovery, Is.False);
            Assert.That(captured.Members[1].MemberId, Is.EqualTo("member-b"));
            Assert.That(captured.Members[1].HasRecovery, Is.True);
            Assert.That(captured.Members[1].RecoveryState, Is.EqualTo(RecoveryState.Resynchronizing));
            Assert.That(captured.Members[1].GraceDeadline, Is.EqualTo(42.5));

            Assert.That(captured.Projections.Count, Is.EqualTo(1));
            Assert.That(captured.Projections[0].ProjectionId, Is.EqualTo("inventory"));
            Assert.That(captured.Projections[0].SchemaVersion, Is.EqualTo(3));
            Assert.That(captured.Projections[0].RequiredForGameplayReady, Is.True);
            Assert.That(captured.Projections[0].Entries.Count, Is.EqualTo(2));
            Assert.That(captured.Projections[0].Entries[0].Key, Is.EqualTo("gold"));
            Assert.That(captured.Projections[0].Entries[0].Value, Is.EqualTo("7"));
        }

        [Test]
        public void DiagnosticContract_ExposesNoTransientTransportOrMutationSurface()
        {
            Type[] evidenceTypes =
            {
                typeof(MultiplayerDiagnosticSnapshot),
                typeof(MultiplayerMemberDiagnostic),
                typeof(MultiplayerProjectionDiagnostic),
                typeof(MultiplayerProjectionEntryDiagnostic)
            };
            string[] forbiddenTerms = { "transport", "connection", "socket", "endpoint", "token", "command" };

            for (int typeIndex = 0; typeIndex < evidenceTypes.Length; typeIndex++)
            {
                Type type = evidenceTypes[typeIndex];
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    PropertyInfo property = properties[propertyIndex];
                    Assert.That(property.SetMethod, Is.Null, type.Name + "." + property.Name + " must be read-only.");
                    string surface = PublicTypeSurface(property).ToLowerInvariant();
                    for (int termIndex = 0; termIndex < forbiddenTerms.Length; termIndex++)
                        Assert.That(surface, Does.Not.Contain(forbiddenTerms[termIndex]), type.Name + "." + property.Name);
                }

                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                {
                    MethodInfo method = methods[methodIndex];
                    Assert.That(method.IsSpecialName, Is.True, type.Name + " exposes unexpected public method " + method.Name);
                }
            }
        }

        private static string PublicTypeSurface(PropertyInfo property)
        {
            var names = new List<string> { property.Name, property.PropertyType.Name };
            Type[] genericArguments = property.PropertyType.GetGenericArguments();
            for (int i = 0; i < genericArguments.Length; i++)
                names.Add(genericArguments[i].Name);
            return string.Join(" ", names);
        }

        private sealed class FakePartySessionQuery : IPartySessionQuery
        {
            private PartyRosterSnapshot _roster;

            public FakePartySessionQuery(PartyRosterSnapshot roster) { _roster = roster; }
            public PartyRosterSnapshot Snapshot() => _roster;

            public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
            {
                for (int i = 0; i < _roster.Members.Count; i++)
                {
                    if (_roster.Members[i].MemberId != memberId) continue;
                    member = _roster.Members[i];
                    return true;
                }
                member = default;
                return false;
            }
        }

        private sealed class FakeReplicationReadState : IGameplayReplicationReadState
        {
            private GameplayProjectionState _projection;

            public FakeReplicationReadState(
                GameplayRevision revision,
                GameplaySynchronizationState synchronizationState,
                bool gameplayReady,
                GameplayProjectionState projection)
            {
                Revision = revision;
                SynchronizationState = synchronizationState;
                GameplayReady = gameplayReady;
                _projection = projection;
            }

            public GameplayRevision Revision { get; private set; }
            public GameplaySynchronizationState SynchronizationState { get; }
            public bool GameplayReady { get; private set; }

            public bool TryGetProjection(GameplayProjectionId id, out GameplayProjectionState state)
            {
                if (_projection != null && _projection.Descriptor.Id == id)
                {
                    state = _projection;
                    return true;
                }
                state = null;
                return false;
            }

            public void Replace(GameplayRevision revision, bool gameplayReady, GameplayProjectionState projection)
            {
                Revision = revision;
                GameplayReady = gameplayReady;
                _projection = projection;
            }
        }

        private sealed class FakeContinuityQuery : IContinuityQuery
        {
            private PartyMemberId _memberId;
            private RecoverySnapshot _recovery;

            public FakeContinuityQuery(PartyMemberId memberId, RecoverySnapshot recovery)
            {
                _memberId = memberId;
                _recovery = recovery;
            }

            public bool TryGetRecovery(PartyMemberId memberId, out RecoverySnapshot recovery)
            {
                if (memberId == _memberId)
                {
                    recovery = _recovery;
                    return true;
                }
                recovery = default;
                return false;
            }

            public void Replace(PartyMemberId memberId, RecoverySnapshot recovery)
            {
                _memberId = memberId;
                _recovery = recovery;
            }
        }
    }
}
