using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Continuity.Api;
using Game.Continuity.Runtime;
using Game.GameplayReplication.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;

namespace Game.Continuity.Tests
{
    public sealed class ContinuityCoordinatorTests
    {
        [Test]
        public void FastReconnectChangesConnectionButPreservesDurableIdentityAndWaitsForGameplayReady()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "opaque-reconnect-token");
            Assert.That(fixture.Session.Disconnect(new TransportConnectionHandle("11")), Is.True);
            Assert.That(fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 10), Is.True);

            ReconnectResult result = fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("99"), 12);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Path, Is.EqualTo(RecoveryPath.FastRepair));
            Assert.That(fixture.Replication.LastRequestedMode, Is.EqualTo(GameplayRecoveryMode.Repair));
            Assert.That(fixture.Session.TryGetMember(fixture.Member.MemberId, out PartyMemberSnapshot rebound), Is.True);
            Assert.That(rebound.MemberId, Is.EqualTo(fixture.Member.MemberId));
            Assert.That(rebound.Slot, Is.EqualTo(fixture.Member.Slot));
            Assert.That(rebound.CharacterId, Is.EqualTo(fixture.Character));
            Assert.That(fixture.Session.TryResolveConnection(new TransportConnectionHandle("99"), out PartyMemberId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(fixture.Member.MemberId));
            Assert.That(fixture.Coordinator.MarkGameplayReady(fixture.Member.MemberId), Is.False, "Transport rebind alone cannot restore gameplay authority.");

            fixture.Replication.SetSynchronization(fixture.Member.MemberId, GameplaySynchronizationPhase.GameplayReady, 7);
            Assert.That(fixture.Coordinator.MarkGameplayReady(fixture.Member.MemberId), Is.True);
            Assert.That(fixture.Coordinator.TryGetRecovery(fixture.Member.MemberId, out RecoverySnapshot recovery), Is.True);
            Assert.That(recovery.State, Is.EqualTo(RecoveryState.Recovered));
        }

        [Test]
        public void RepairWindowMissRequestsFullSnapshotWithoutChangingIdentity()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "token-full");
            fixture.Session.Disconnect(new TransportConnectionHandle("11"));
            fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 10);

            ReconnectResult result = fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("77"), 20);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Path, Is.EqualTo(RecoveryPath.FullResynchronization));
            Assert.That(fixture.Replication.LastRequestedMode, Is.EqualTo(GameplayRecoveryMode.FullSnapshot));
            Assert.That(fixture.Coordinator.TryGetRecovery(fixture.Member.MemberId, out RecoverySnapshot recovery), Is.True);
            Assert.That(recovery.State, Is.EqualTo(RecoveryState.Resynchronizing));
            Assert.That(result.Member.MemberId, Is.EqualTo(fixture.Member.MemberId));
            Assert.That(result.Member.Slot, Is.EqualTo(fixture.Member.Slot));
            Assert.That(result.Member.CharacterId, Is.EqualTo(fixture.Character));
        }

        [Test]
        public void StateMutatedWhileAbsentIsReadAsCurrentTruthAfterFullResync()
        {
            Fixture fixture = CreateFixture();
            fixture.Replication.SetCurrent(fixture.Member.MemberId, new TestGameplayState(100, 1, 3), 1);
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "state-token");
            fixture.Session.Disconnect(new TransportConnectionHandle("11"));
            fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 10);

            fixture.Replication.SetCurrent(fixture.Member.MemberId, new TestGameplayState(42, 4, 9), 2);
            ReconnectResult result = fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("88"), 20);

            Assert.That(result.Path, Is.EqualTo(RecoveryPath.FullResynchronization));
            Assert.That(fixture.Coordinator.MarkGameplayReady(fixture.Member.MemberId), Is.False);
            fixture.Replication.SetSynchronization(fixture.Member.MemberId, GameplaySynchronizationPhase.GameplayReady, 2);
            Assert.That(fixture.Coordinator.MarkGameplayReady(fixture.Member.MemberId), Is.True);
            Assert.That(fixture.Replication.TryGetCurrent<TestGameplayState>(fixture.Member.MemberId, out GameplayProjectionSnapshot<TestGameplayState> snapshot), Is.True);
            Assert.That(snapshot.Revision, Is.EqualTo(new GameplayRevision(2)));
            Assert.That(snapshot.State.Vitality, Is.EqualTo(42));
            Assert.That(snapshot.State.InventoryCount, Is.EqualTo(4));
            Assert.That(snapshot.State.ProgressionStep, Is.EqualTo(9));
        }

        [Test]
        public void RepeatedReconnectCannotAllocateDuplicateCharacter()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "token-once");
            fixture.Session.Disconnect(new TransportConnectionHandle("11"));
            fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 1);
            Assert.That(fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("12"), 2).Accepted, Is.True);

            ReconnectResult duplicate = fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("13"), 3);

            Assert.That(duplicate.FailureReason, Is.EqualTo(ReconnectFailureReason.NotRecoverable));
            PartyRosterSnapshot roster = fixture.Session.Snapshot();
            Assert.That(roster.Members.Count, Is.EqualTo(1));
            Assert.That(roster.Members[0].CharacterId, Is.EqualTo(fixture.Character));
        }

        [Test]
        public void ExplicitLeaveSkipsGraceAndCannotRecover()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "leave-token");
            Assert.That(fixture.Coordinator.ExplicitLeave(fixture.Member.MemberId), Is.True);
            Assert.That(fixture.Terminal.LeaveCount, Is.EqualTo(1));
            Assert.That(fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("22"), 2).FailureReason,
                Is.EqualTo(ReconnectFailureReason.CredentialRejected));
            Assert.That(fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 3), Is.False);
        }

        [Test]
        public void GraceExpirationInvalidatesCredentialAndHandsCleanupBackToOwner()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "expire-token");
            fixture.Session.Disconnect(new TransportConnectionHandle("11"));
            fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 10);

            Assert.That(fixture.Coordinator.ExpireInterrupted(41), Is.EqualTo(1));
            Assert.That(fixture.Terminal.ExpiredCount, Is.EqualTo(1));
            Assert.That(fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("44"), 42).Accepted, Is.False);
            Assert.That(fixture.Session.TryGetMember(fixture.Member.MemberId, out PartyMemberSnapshot preserved), Is.True);
            Assert.That(preserved.CharacterId, Is.EqualTo(fixture.Character), "Continuity hands final removal policy to the owning system instead of fabricating identity changes.");
        }

        [Test]
        public void InvalidCredentialCannotHijackMember()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "secret-a");
            fixture.Session.Disconnect(new TransportConnectionHandle("11"));
            fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 5);
            var forged = new ReconnectCredential(credential.SessionId, credential.MemberId, "wrong-secret");

            Assert.That(fixture.Coordinator.BeginReconnect(new ReconnectRequest(forged), new RuntimeConnectionHandle("55"), 6).FailureReason,
                Is.EqualTo(ReconnectFailureReason.CredentialRejected));
            Assert.That(fixture.Admission.BindCount, Is.EqualTo(0));
        }

        private static Fixture CreateFixture()
        {
            var id = new GameSessionId("continuity-session");
            var session = new PartySession(id, new SessionStartupConfiguration(4, "v1", "content-a", true));
            PartyMemberSnapshot member = session.Join(new JoinRequest(id, "alice", "v1", "content-a")).Member;
            var character = new CharacterId("character:alice");
            Assert.That(session.BindCharacter(member.MemberId, character), Is.True);
            Assert.That(session.BindConnection(member.MemberId, new TransportConnectionHandle("11")), Is.True);
            var admission = new SessionAdmissionFixture(session);
            var replication = new ReplicationFixture();
            var terminal = new TerminalFixture();
            var coordinator = new ContinuityCoordinator(session, new ContinuityPolicy(30, 5), admission, replication, terminal);
            return new Fixture(session, coordinator, admission, replication, terminal, member, character);
        }

        private sealed class SessionAdmissionFixture : IReconnectTransportAdmission
        {
            private readonly PartySession _session;
            public int BindCount { get; private set; }
            public SessionAdmissionFixture(PartySession session) { _session = session; }
            public bool TryBind(PartyMemberSnapshot member, RuntimeConnectionHandle connection)
            {
                BindCount++;
                return _session.BindConnection(member.MemberId, new TransportConnectionHandle(connection.Value));
            }
        }

        private sealed class ReplicationFixture : IGameplayReplicationClientState
        {
            private readonly Dictionary<PartyMemberId, GameplaySynchronizationStatus> _synchronization = new Dictionary<PartyMemberId, GameplaySynchronizationStatus>();
            private readonly Dictionary<string, object> _current = new Dictionary<string, object>();

            public GameplayRecoveryMode LastRequestedMode { get; private set; }
            public int RequestCount { get; private set; }

            public void RequestRecovery(PartyMemberId memberId, GameplayRecoveryMode mode)
            {
                RequestCount++;
                LastRequestedMode = mode;
                _synchronization[memberId] = new GameplaySynchronizationStatus(GameplaySynchronizationPhase.Synchronizing, default);
            }

            public bool TryGetSynchronization(PartyMemberId memberId, out GameplaySynchronizationStatus status) => _synchronization.TryGetValue(memberId, out status);

            public bool TryGetCurrent<TState>(PartyMemberId memberId, out GameplayProjectionSnapshot<TState> snapshot) where TState : struct
            {
                if (_current.TryGetValue(Key<TState>(memberId), out object value))
                {
                    snapshot = (GameplayProjectionSnapshot<TState>)value;
                    return true;
                }
                snapshot = default;
                return false;
            }

            public void SetSynchronization(PartyMemberId memberId, GameplaySynchronizationPhase phase, ulong revision)
                => _synchronization[memberId] = new GameplaySynchronizationStatus(phase, new GameplayRevision(revision));

            public void SetCurrent<TState>(PartyMemberId memberId, TState state, ulong revision) where TState : struct
                => _current[Key<TState>(memberId)] = new GameplayProjectionSnapshot<TState>(new GameplayRevision(revision), state);

            private static string Key<TState>(PartyMemberId memberId) where TState : struct
                => memberId.Value + "|" + typeof(TState).FullName;
        }

        private sealed class TerminalFixture : IContinuityTerminalPolicySink
        {
            public int LeaveCount { get; private set; }
            public int ExpiredCount { get; private set; }
            public void OnExplicitLeave(PartyMemberId memberId) { LeaveCount++; }
            public void OnRecoveryExpired(PartyMemberId memberId) { ExpiredCount++; }
        }

        private readonly struct TestGameplayState
        {
            public int Vitality { get; }
            public int InventoryCount { get; }
            public int ProgressionStep { get; }
            public TestGameplayState(int vitality, int inventoryCount, int progressionStep)
            {
                Vitality = vitality;
                InventoryCount = inventoryCount;
                ProgressionStep = progressionStep;
            }
        }

        private readonly struct Fixture
        {
            public PartySession Session { get; }
            public ContinuityCoordinator Coordinator { get; }
            public SessionAdmissionFixture Admission { get; }
            public ReplicationFixture Replication { get; }
            public TerminalFixture Terminal { get; }
            public PartyMemberSnapshot Member { get; }
            public CharacterId Character { get; }
            public Fixture(PartySession session, ContinuityCoordinator coordinator, SessionAdmissionFixture admission, ReplicationFixture replication, TerminalFixture terminal, PartyMemberSnapshot member, CharacterId character)
            {
                Session = session;
                Coordinator = coordinator;
                Admission = admission;
                Replication = replication;
                Terminal = terminal;
                Member = member;
                Character = character;
            }
        }
    }
}
