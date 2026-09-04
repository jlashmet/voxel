using System.Collections.Generic;
using Game.Characters.Api;
using Game.Continuity.Api;
using Game.GameplayReplication.Api;
using Game.SessionPresentation.Api;
using Game.SessionPresentation.Runtime;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;

namespace Game.SessionPresentation.Tests
{
    public sealed class SessionPresentationProjectorTests
    {
        [Test]
        public void Reconnect_UpdatesSameDurableRowWithoutChangingSlotOrCharacter()
        {
            PartyMemberId memberId = new PartyMemberId("party:bravo");
            CharacterId characterId = new CharacterId("character:bravo");
            var sessions = new FakeSessionQuery(new PartyMemberSnapshot(memberId, new PlayerSlot(1), PartyLeadershipRole.Member, PartyPresenceState.Disconnected, SessionReadinessState.Joined, characterId));
            var application = new FakeApplicationQuery(4, false, new PartyMemberReadySnapshot(memberId, true));
            var continuity = new FakeContinuityQuery();
            continuity.Set(memberId, RecoveryState.Reconnecting);
            var replication = new FakeReplicationQuery();
            replication.Set(memberId, GameplaySynchronizationPhase.Synchronizing, 2);
            var projector = new SessionPresentationProjector(sessions, application, continuity, replication);

            PartyMemberPresentationSnapshot before = projector.CapturePartyScreen(default).Members[0];

            sessions.Set(new PartyMemberSnapshot(memberId, new PlayerSlot(1), PartyLeadershipRole.Member, PartyPresenceState.Connected, SessionReadinessState.Synchronized, characterId));
            continuity.Set(memberId, RecoveryState.Recovered);
            replication.Set(memberId, GameplaySynchronizationPhase.GameplayReady, 3);
            PartyMemberPresentationSnapshot after = projector.CapturePartyScreen(default).Members[0];

            Assert.That(after.MemberId, Is.EqualTo(before.MemberId));
            Assert.That(after.Slot, Is.EqualTo(before.Slot));
            Assert.That(after.CharacterId, Is.EqualTo(before.CharacterId));
            Assert.That(after.Connection, Is.EqualTo(MemberConnectionPresentationState.Connected));
            Assert.That(after.GameplayReady, Is.True);
        }

        [Test]
        public void Connected_DoesNotImplyGameplayReady()
        {
            PartyMemberId memberId = new PartyMemberId("party:alpha");
            var sessions = new FakeSessionQuery(new PartyMemberSnapshot(memberId, new PlayerSlot(0), PartyLeadershipRole.Leader, PartyPresenceState.Connected, SessionReadinessState.Connected));
            var application = new FakeApplicationQuery(4, false, new PartyMemberReadySnapshot(memberId, true));
            var continuity = new FakeContinuityQuery();
            var replication = new FakeReplicationQuery();
            replication.Set(memberId, GameplaySynchronizationPhase.Synchronizing, 1);
            var projector = new SessionPresentationProjector(sessions, application, continuity, replication);

            PartyMemberPresentationSnapshot row = projector.CapturePartyScreen(memberId).Members[0];

            Assert.That(row.Connection, Is.EqualTo(MemberConnectionPresentationState.Connected));
            Assert.That(row.ReadyToStart, Is.True);
            Assert.That(row.GameplayReady, Is.False);
            Assert.That(row.Readiness, Is.EqualTo(MemberReadinessPresentationState.Synchronizing));
        }

        [Test]
        public void ExplicitLeave_RemovesRosterRowWhileInterruptionKeepsDurableRow()
        {
            var session = new PartySession(
                new GameSessionId("session:test"),
                new SessionStartupConfiguration(4, "protocol", "content", true));
            JoinResult joined = session.Join(new JoinRequest(new GameSessionId("session:test"), "player-a", "protocol", "content"));
            var application = new PartySessionApplication(session, 4);
            var continuity = new FakeContinuityQuery();
            continuity.Set(joined.Member.MemberId, RecoveryState.ConnectionInterrupted);
            var projector = new SessionPresentationProjector(session, application, continuity, new FakeReplicationQuery());

            Assert.That(projector.CapturePartyScreen(joined.Member.MemberId).Members.Count, Is.EqualTo(1));
            Assert.That(projector.CapturePartyScreen(joined.Member.MemberId).Members[0].Connection, Is.EqualTo(MemberConnectionPresentationState.Interrupted));

            PartySessionCommandResult leave = application.Leave(joined.Member.MemberId);

            Assert.That(leave.Accepted, Is.True);
            Assert.That(projector.CapturePartyScreen(joined.Member.MemberId).Members.Count, Is.EqualTo(0));
        }

        [Test]
        public void MultiMemberProjection_OrdersByStableSlotAndPreservesCharacterBindings()
        {
            var a = new PartyMemberId("party:a");
            var b = new PartyMemberId("party:b");
            var c = new PartyMemberId("party:c");
            var sessions = new FakeSessionQuery(
                new PartyMemberSnapshot(c, new PlayerSlot(2), PartyLeadershipRole.Member, PartyPresenceState.Connected, SessionReadinessState.Connected, new CharacterId("character:c")),
                new PartyMemberSnapshot(a, new PlayerSlot(0), PartyLeadershipRole.Leader, PartyPresenceState.Connected, SessionReadinessState.Connected, new CharacterId("character:a")),
                new PartyMemberSnapshot(b, new PlayerSlot(1), PartyLeadershipRole.Member, PartyPresenceState.Connected, SessionReadinessState.Connected, new CharacterId("character:b")));
            var projector = new SessionPresentationProjector(sessions, new FakeApplicationQuery(4, false), new FakeContinuityQuery(), new FakeReplicationQuery());

            PartyScreenPresentationSnapshot result = projector.CapturePartyScreen(a);

            Assert.That(result.Members[0].MemberId, Is.EqualTo(a));
            Assert.That(result.Members[1].MemberId, Is.EqualTo(b));
            Assert.That(result.Members[2].MemberId, Is.EqualTo(c));
            Assert.That(result.Members[1].CharacterId, Is.EqualTo(new CharacterId("character:b")));
        }

        [Test]
        public void PartyScreenAndHud_ConsumeSameSemanticRows()
        {
            PartyMemberId leader = new PartyMemberId("party:leader");
            PartyMemberId teammate = new PartyMemberId("party:teammate");
            var sessions = new FakeSessionQuery(
                new PartyMemberSnapshot(leader, new PlayerSlot(0), PartyLeadershipRole.Leader, PartyPresenceState.Connected, SessionReadinessState.Synchronized, new CharacterId("character:leader")),
                new PartyMemberSnapshot(teammate, new PlayerSlot(1), PartyLeadershipRole.Member, PartyPresenceState.Connected, SessionReadinessState.Synchronized, new CharacterId("character:teammate")));
            var replication = new FakeReplicationQuery();
            replication.Set(leader, GameplaySynchronizationPhase.GameplayReady, 4);
            replication.Set(teammate, GameplaySynchronizationPhase.GameplayReady, 4);
            var application = new FakeApplicationQuery(4, false,
                new PartyMemberReadySnapshot(leader, true),
                new PartyMemberReadySnapshot(teammate, true));
            var projector = new SessionPresentationProjector(sessions, application, new FakeContinuityQuery(), replication);

            PartyScreenPresentationSnapshot party = projector.CapturePartyScreen(leader);
            TeammateHudPresentationSnapshot hud = projector.CaptureTeammateHud(leader);

            Assert.That(hud.Members.Count, Is.EqualTo(party.Members.Count));
            for (int i = 0; i < party.Members.Count; i++)
            {
                Assert.That(hud.Members[i].MemberId, Is.EqualTo(party.Members[i].MemberId));
                Assert.That(hud.Members[i].CharacterId, Is.EqualTo(party.Members[i].CharacterId));
                Assert.That(hud.Members[i].GameplayReady, Is.EqualTo(party.Members[i].GameplayReady));
            }
            Assert.That(party.Lifecycle, Is.EqualTo(SessionPresentationLifecycle.ReadyToStart));
            Assert.That(party.CanStart, Is.True);
        }

        [Test]
        public void IntentRouter_ForwardsOnlySemanticSessionCommands()
        {
            PartyMemberId memberId = new PartyMemberId("party:local");
            var commands = new RecordingCommands();
            var router = new SessionPresentationIntentRouter(commands);

            router.Request(SessionPresentationIntent.SetReady(memberId, true));
            router.Request(SessionPresentationIntent.Start(memberId));
            router.Request(SessionPresentationIntent.Leave(memberId));

            Assert.That(commands.ReadyMember, Is.EqualTo(memberId));
            Assert.That(commands.ReadyValue, Is.True);
            Assert.That(commands.StartMember, Is.EqualTo(memberId));
            Assert.That(commands.LeaveMember, Is.EqualTo(memberId));
        }

        [Test]
        public void SessionsApplication_StartRequiresLeaderIntentReadyAndGameplayReady()
        {
            var session = new PartySession(
                new GameSessionId("session:commands"),
                new SessionStartupConfiguration(4, "protocol", "content", true));
            PartyMemberSnapshot leader = session.Join(new JoinRequest(new GameSessionId("session:commands"), "leader", "protocol", "content")).Member;
            PartyMemberSnapshot teammate = session.Join(new JoinRequest(new GameSessionId("session:commands"), "teammate", "protocol", "content")).Member;
            session.BindConnection(leader.MemberId, new TransportConnectionHandle("connection-1"));
            session.BindConnection(teammate.MemberId, new TransportConnectionHandle("connection-2"));
            session.MarkSynchronized(leader.MemberId);
            session.MarkSynchronized(teammate.MemberId);
            session.MarkGameplayReady(leader.MemberId);
            session.MarkGameplayReady(teammate.MemberId);
            var application = new PartySessionApplication(session, 4);
            application.SetReady(leader.MemberId, true);
            application.SetReady(teammate.MemberId, true);

            Assert.That(application.RequestStart(teammate.MemberId).Failure, Is.EqualTo(PartySessionCommandFailure.NotLeader));
            Assert.That(application.RequestStart(leader.MemberId).Accepted, Is.True);
            Assert.That(application.Snapshot().GameplayStarted, Is.True);
        }

        private sealed class FakeSessionQuery : IPartySessionQuery
        {
            private PartyMemberSnapshot[] _members;
            public FakeSessionQuery(params PartyMemberSnapshot[] members) { _members = members; }
            public void Set(params PartyMemberSnapshot[] members) { _members = members; }
            public PartyRosterSnapshot Snapshot() => new PartyRosterSnapshot(new GameSessionId("session:projection"), _members);
            public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
            {
                for (int i = 0; i < _members.Length; i++)
                    if (_members[i].MemberId == memberId) { member = _members[i]; return true; }
                member = default;
                return false;
            }
        }

        private sealed class FakeApplicationQuery : IPartySessionApplicationQuery
        {
            private readonly int _capacity;
            private readonly bool _started;
            private readonly PartyMemberReadySnapshot[] _ready;
            public FakeApplicationQuery(int capacity, bool started, params PartyMemberReadySnapshot[] ready)
            {
                _capacity = capacity;
                _started = started;
                _ready = ready;
            }
            public PartySessionApplicationSnapshot Snapshot() => new PartySessionApplicationSnapshot(_capacity, _started, _ready);
        }

        private sealed class FakeContinuityQuery : IContinuityQuery
        {
            private readonly Dictionary<PartyMemberId, RecoverySnapshot> _states = new Dictionary<PartyMemberId, RecoverySnapshot>();
            public void Set(PartyMemberId memberId, RecoveryState state) => _states[memberId] = new RecoverySnapshot(memberId, state, 0);
            public bool TryGetRecovery(PartyMemberId memberId, out RecoverySnapshot recovery) => _states.TryGetValue(memberId, out recovery);
        }

        private sealed class FakeReplicationQuery : IGameplayReplicationClientState
        {
            private readonly Dictionary<PartyMemberId, GameplaySynchronizationStatus> _states = new Dictionary<PartyMemberId, GameplaySynchronizationStatus>();
            public void Set(PartyMemberId memberId, GameplaySynchronizationPhase phase, ulong revision) =>
                _states[memberId] = new GameplaySynchronizationStatus(phase, new GameplayRevision(revision));
            public void RequestRecovery(PartyMemberId memberId, GameplayRecoveryMode mode) { }
            public bool TryGetSynchronization(PartyMemberId memberId, out GameplaySynchronizationStatus status) => _states.TryGetValue(memberId, out status);
            public bool TryGetCurrent<TState>(PartyMemberId memberId, out GameplayProjectionSnapshot<TState> snapshot) where TState : struct
            {
                snapshot = default;
                return false;
            }
        }

        private sealed class RecordingCommands : IPartySessionApplicationCommands
        {
            public PartyMemberId ReadyMember { get; private set; }
            public bool ReadyValue { get; private set; }
            public PartyMemberId StartMember { get; private set; }
            public PartyMemberId LeaveMember { get; private set; }
            public PartySessionCommandResult SetReady(PartyMemberId memberId, bool ready) { ReadyMember = memberId; ReadyValue = ready; return PartySessionCommandResult.Accept(); }
            public PartySessionCommandResult RequestStart(PartyMemberId memberId) { StartMember = memberId; return PartySessionCommandResult.Accept(); }
            public PartySessionCommandResult Leave(PartyMemberId memberId) { LeaveMember = memberId; return PartySessionCommandResult.Accept(); }
        }
    }
}
