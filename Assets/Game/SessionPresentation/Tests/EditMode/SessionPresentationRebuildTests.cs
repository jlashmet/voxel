using Game.Characters.Api;
using Game.Continuity.Api;
using Game.GameplayReplication.Api;
using Game.SessionPresentation.Api;
using Game.SessionPresentation.Runtime;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.SessionPresentation.Tests
{
    public sealed class SessionPresentationRebuildTests
    {
        [Test]
        public void CharacterBindingChange_UpdatesSameDurableRowWithoutGameObjectIdentity()
        {
            PartyMemberId memberId = new PartyMemberId("party:character-change");
            var sessions = new MutableSessionQuery(
                new PartyMemberSnapshot(
                    memberId,
                    new PlayerSlot(2),
                    PartyLeadershipRole.Member,
                    PartyPresenceState.Connected,
                    SessionReadinessState.Synchronized,
                    new CharacterId("character:old")));
            var application = new StaticApplicationQuery(4, new PartyMemberReadySnapshot(memberId, true));
            var projector = new SessionPresentationProjector(
                sessions,
                application,
                new MutableContinuityQuery(),
                new MutableReplicationQuery());

            PartyMemberPresentationSnapshot before = projector.CapturePartyScreen(default).Members[0];
            sessions.Set(
                new PartyMemberSnapshot(
                    memberId,
                    new PlayerSlot(2),
                    PartyLeadershipRole.Member,
                    PartyPresenceState.Connected,
                    SessionReadinessState.Synchronized,
                    new CharacterId("character:new")));
            PartyMemberPresentationSnapshot after = projector.CapturePartyScreen(default).Members[0];

            Assert.That(after.MemberId, Is.EqualTo(before.MemberId));
            Assert.That(after.Slot, Is.EqualTo(before.Slot));
            Assert.That(after.CharacterId, Is.EqualTo(new CharacterId("character:new")));
        }

        [Test]
        public void NewProjector_RebuildsCurrentSemanticStateWithoutTransportHistory()
        {
            PartyMemberId memberId = new PartyMemberId("party:rebuild");
            CharacterId characterId = new CharacterId("character:rebuild");
            var sessions = new MutableSessionQuery(
                new PartyMemberSnapshot(
                    memberId,
                    new PlayerSlot(1),
                    PartyLeadershipRole.Member,
                    PartyPresenceState.Disconnected,
                    SessionReadinessState.Joined,
                    characterId));
            var application = new StaticApplicationQuery(4, new PartyMemberReadySnapshot(memberId, true));
            var continuity = new MutableContinuityQuery();
            continuity.Set(memberId, RecoveryState.Reconnecting);
            var replication = new MutableReplicationQuery();
            replication.Set(memberId, GameplaySynchronizationPhase.Synchronizing, 5);

            var beforeNavigation = new SessionPresentationProjector(sessions, application, continuity, replication);
            PartyMemberPresentationSnapshot interrupted = beforeNavigation.CapturePartyScreen(default).Members[0];
            Assert.That(interrupted.Connection, Is.EqualTo(MemberConnectionPresentationState.Reconnecting));

            sessions.Set(
                new PartyMemberSnapshot(
                    memberId,
                    new PlayerSlot(1),
                    PartyLeadershipRole.Member,
                    PartyPresenceState.Connected,
                    SessionReadinessState.Synchronized,
                    characterId));
            continuity.Set(memberId, RecoveryState.Recovered);
            replication.Set(memberId, GameplaySynchronizationPhase.GameplayReady, 6);

            var rebuiltAfterNavigation = new SessionPresentationProjector(sessions, application, continuity, replication);
            PartyMemberPresentationSnapshot rebuilt = rebuiltAfterNavigation.CapturePartyScreen(default).Members[0];

            Assert.That(rebuilt.MemberId, Is.EqualTo(memberId));
            Assert.That(rebuilt.Slot, Is.EqualTo(new PlayerSlot(1)));
            Assert.That(rebuilt.CharacterId, Is.EqualTo(characterId));
            Assert.That(rebuilt.Connection, Is.EqualTo(MemberConnectionPresentationState.Connected));
            Assert.That(rebuilt.GameplayReady, Is.True);
        }

        private sealed class MutableSessionQuery : IPartySessionQuery
        {
            private PartyMemberSnapshot _member;
            public MutableSessionQuery(PartyMemberSnapshot member) { _member = member; }
            public void Set(PartyMemberSnapshot member) { _member = member; }
            public PartyRosterSnapshot Snapshot() =>
                new PartyRosterSnapshot(new GameSessionId("session:rebuild-tests"), new[] { _member });
            public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
            {
                if (_member.MemberId == memberId)
                {
                    member = _member;
                    return true;
                }
                member = default;
                return false;
            }
        }

        private sealed class StaticApplicationQuery : IPartySessionApplicationQuery
        {
            private readonly int _capacity;
            private readonly PartyMemberReadySnapshot[] _ready;
            public StaticApplicationQuery(int capacity, params PartyMemberReadySnapshot[] ready)
            {
                _capacity = capacity;
                _ready = ready;
            }
            public PartySessionApplicationSnapshot Snapshot() =>
                new PartySessionApplicationSnapshot(_capacity, false, _ready);
        }

        private sealed class MutableContinuityQuery : IContinuityQuery
        {
            private PartyMemberId _memberId;
            private RecoveryState _state;
            private bool _hasValue;
            public void Set(PartyMemberId memberId, RecoveryState state)
            {
                _memberId = memberId;
                _state = state;
                _hasValue = true;
            }
            public bool TryGetRecovery(PartyMemberId memberId, out RecoverySnapshot recovery)
            {
                if (_hasValue && memberId == _memberId)
                {
                    recovery = new RecoverySnapshot(memberId, _state, 0);
                    return true;
                }
                recovery = default;
                return false;
            }
        }

        private sealed class MutableReplicationQuery : IGameplayReplicationClientState
        {
            private PartyMemberId _memberId;
            private GameplaySynchronizationStatus _status;
            private bool _hasValue;
            public void Set(PartyMemberId memberId, GameplaySynchronizationPhase phase, ulong revision)
            {
                _memberId = memberId;
                _status = new GameplaySynchronizationStatus(phase, new GameplayRevision(revision));
                _hasValue = true;
            }
            public void RequestRecovery(PartyMemberId memberId, GameplayRecoveryMode mode) { }
            public bool TryGetSynchronization(PartyMemberId memberId, out GameplaySynchronizationStatus status)
            {
                if (_hasValue && memberId == _memberId)
                {
                    status = _status;
                    return true;
                }
                status = default;
                return false;
            }
            public bool TryGetCurrent<TState>(PartyMemberId memberId, out GameplayProjectionSnapshot<TState> snapshot) where TState : struct
            {
                snapshot = default;
                return false;
            }
        }
    }
}
