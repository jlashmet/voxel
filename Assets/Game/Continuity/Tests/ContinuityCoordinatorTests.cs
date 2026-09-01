using Game.Characters.Api;
using Game.Continuity.Api;
using Game.Continuity.Runtime;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;

namespace Game.Continuity.Tests
{
    public sealed class ContinuityCoordinatorTests
    {
        [Test]
        public void FastReconnectChangesConnectionButPreservesDurableIdentity()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "opaque-reconnect-token");
            Assert.That(fixture.Session.Disconnect(new TransportConnectionHandle("11")), Is.True);
            Assert.That(fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 10), Is.True);

            ReconnectResult result = fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("99"), 12);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Path, Is.EqualTo(RecoveryPath.FastRepair));
            Assert.That(fixture.Session.TryGetMember(fixture.Member.MemberId, out PartyMemberSnapshot rebound), Is.True);
            Assert.That(rebound.MemberId, Is.EqualTo(fixture.Member.MemberId));
            Assert.That(rebound.Slot, Is.EqualTo(fixture.Member.Slot));
            Assert.That(rebound.CharacterId, Is.EqualTo(fixture.Character));
            Assert.That(fixture.Session.TryResolveConnection(new TransportConnectionHandle("99"), out PartyMemberId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(fixture.Member.MemberId));
            Assert.That(fixture.Coordinator.MarkGameplayReady(fixture.Member.MemberId), Is.True);
            Assert.That(fixture.Coordinator.TryGetRecovery(fixture.Member.MemberId, out RecoverySnapshot recovery), Is.True);
            Assert.That(recovery.State, Is.EqualTo(RecoveryState.Recovered));
        }

        [Test]
        public void RepairWindowMissSelectsFullResynchronizationWithoutChangingIdentity()
        {
            Fixture fixture = CreateFixture();
            ReconnectCredential credential = fixture.Coordinator.IssueCredential(fixture.Member.MemberId, "token-full");
            fixture.Session.Disconnect(new TransportConnectionHandle("11"));
            fixture.Coordinator.ObserveUnexpectedLoss(fixture.Member.MemberId, 10);

            ReconnectResult result = fixture.Coordinator.BeginReconnect(new ReconnectRequest(credential), new RuntimeConnectionHandle("77"), 20);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Path, Is.EqualTo(RecoveryPath.FullResynchronization));
            Assert.That(fixture.Coordinator.TryGetRecovery(fixture.Member.MemberId, out RecoverySnapshot recovery), Is.True);
            Assert.That(recovery.State, Is.EqualTo(RecoveryState.Resynchronizing));
            Assert.That(result.Member.MemberId, Is.EqualTo(fixture.Member.MemberId));
            Assert.That(result.Member.Slot, Is.EqualTo(fixture.Member.Slot));
            Assert.That(result.Member.CharacterId, Is.EqualTo(fixture.Character));
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
            var terminal = new TerminalFixture();
            var coordinator = new ContinuityCoordinator(session, new ContinuityPolicy(30, 5), admission, terminal);
            return new Fixture(session, coordinator, admission, terminal, member, character);
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

        private sealed class TerminalFixture : IContinuityTerminalPolicySink
        {
            public int LeaveCount { get; private set; }
            public int ExpiredCount { get; private set; }
            public void OnExplicitLeave(PartyMemberId memberId) { LeaveCount++; }
            public void OnRecoveryExpired(PartyMemberId memberId) { ExpiredCount++; }
        }

        private readonly struct Fixture
        {
            public PartySession Session { get; }
            public ContinuityCoordinator Coordinator { get; }
            public SessionAdmissionFixture Admission { get; }
            public TerminalFixture Terminal { get; }
            public PartyMemberSnapshot Member { get; }
            public CharacterId Character { get; }
            public Fixture(PartySession session, ContinuityCoordinator coordinator, SessionAdmissionFixture admission, TerminalFixture terminal, PartyMemberSnapshot member, CharacterId character)
            {
                Session = session;
                Coordinator = coordinator;
                Admission = admission;
                Terminal = terminal;
                Member = member;
                Character = character;
            }
        }
    }
}
