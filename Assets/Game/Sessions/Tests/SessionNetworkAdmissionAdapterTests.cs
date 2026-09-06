using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;
using VoxelEngine.Net.Api;

namespace Game.Sessions.Tests
{
    public sealed class SessionNetworkAdmissionAdapterTests
    {
        [Test]
        public void DurableMemberSlotAndCharacterSurviveConnectionChanges()
        {
            var id = new GameSessionId("adapter-session");
            var session = new PartySession(id, new SessionStartupConfiguration(4, "v1", "content-a", true));
            PartyMemberSnapshot joined = session.Join(new JoinRequest(id, "alice", "v1", "content-a")).Member;
            var character = new CharacterId("character:alice");
            Assert.That(session.BindCharacter(joined.MemberId, character), Is.True);
            var network = new RecordingAdmission();
            var adapter = new SessionNetworkAdmissionAdapter(session, network);

            Assert.That(adapter.Authenticate(joined.MemberId, 11, new NetworkSpawnPosition(1, 2, 3), 8, true), Is.True);
            Assert.That(network.LastNetworkPlayerId, Is.EqualTo(1));
            Assert.That(adapter.Disconnect(11), Is.True);
            Assert.That(adapter.Authenticate(joined.MemberId, 99, new NetworkSpawnPosition(4, 5, 6), 8, true), Is.True);

            Assert.That(session.TryGetMember(joined.MemberId, out PartyMemberSnapshot rebound), Is.True);
            Assert.That(rebound.MemberId, Is.EqualTo(joined.MemberId));
            Assert.That(rebound.Slot, Is.EqualTo(joined.Slot));
            Assert.That(rebound.CharacterId, Is.EqualTo(character));
            Assert.That(network.LastConnectionId, Is.EqualTo(99));
            Assert.That(network.LastNetworkPlayerId, Is.EqualTo(1));
            Assert.That(adapter.Disconnect(11), Is.False, "A late old disconnect cannot remove the replacement.");
            Assert.That(session.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(99), out var owner), Is.True);
            Assert.That(owner, Is.EqualTo(joined.MemberId));
        }

        [Test]
        public void FailedNetworkAdmissionLeavesJoinedStateAndEventsUntouched()
        {
            var f = new Fixture();
            f.Network.Accept = false;
            PartyMemberSnapshot before = f.Member();
            Assert.That(f.Authenticate(), Is.False);
            AssertSame(before, f.Member());
            Assert.That(f.Member().Presence, Is.EqualTo(PartyPresenceState.Joined));
            Assert.That(f.Events, Is.Empty, "Rejected Net admission cannot publish MemberConnected.");
            Assert.That(f.Session.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(7), out _), Is.False);
        }

        [Test]
        public void StableSlotMapsToTransientNetworkActorId()
        {
            var id = new GameSessionId("adapter-slots");
            var session = new PartySession(id, new SessionStartupConfiguration(6, "v1", "content-a", true));
            session.Join(new JoinRequest(id, "alice", "v1", "content-a"));
            PartyMemberSnapshot second = session.Join(new JoinRequest(id, "bob", "v1", "content-a")).Member;
            var network = new RecordingAdmission();
            var adapter = new SessionNetworkAdmissionAdapter(session, network);

            Assert.That(adapter.Authenticate(second.MemberId, 50, new NetworkSpawnPosition(0, 0, 0), 8, false), Is.True);
            Assert.That(second.Slot.Value, Is.EqualTo(1));
            Assert.That(network.LastNetworkPlayerId, Is.EqualTo(2));
            Assert.That(second.MemberId.Value, Does.Not.Contain("50"));
        }

        [TestCase(SessionReadinessState.Connected)]
        [TestCase(SessionReadinessState.Synchronized)]
        [TestCase(SessionReadinessState.GameplayReady)]
        public void SameConnectionRetryDoesNotResetReadinessOrPublishAnotherConnection(SessionReadinessState readiness)
        {
            var f = new Fixture();
            f.Connect(readiness);
            PartyMemberSnapshot before = f.Member();
            f.Events.Clear();
            Assert.That(f.Authenticate(), Is.True);
            AssertSame(before, f.Member());
            Assert.That(f.Events, Is.Empty);
            Assert.That(f.Network.Calls, Is.EqualTo(2), "Net must confirm the identity, not just a Sessions handle.");
        }

        [Test]
        public void RejectedSameConnectionRetryCannotDisconnectTheLiveMember()
        {
            var f = new Fixture();
            f.Connect(SessionReadinessState.GameplayReady);
            PartyMemberSnapshot before = f.Member();
            f.Events.Clear();
            f.Network.Accept = false;
            Assert.That(f.Authenticate(), Is.False);
            AssertSame(before, f.Member());
            Assert.That(f.Events, Is.Empty);
            Assert.That(f.Session.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(7), out var owner), Is.True);
            Assert.That(owner, Is.EqualTo(f.MemberId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ThrowingAdmissionCannotChangeMembershipAndDoesNotPoisonTheNextAttempt(bool connected)
        {
            var f = new Fixture();
            if (connected) f.Connect(SessionReadinessState.GameplayReady);
            PartyMemberSnapshot before = f.Member();
            f.Events.Clear();
            f.Network.Throw = true;
            Assert.Throws<InvalidOperationException>(() => f.Authenticate());
            AssertSame(before, f.Member());
            Assert.That(f.Events, Is.Empty);
            f.Network.Throw = false;
            Assert.That(f.Authenticate(), Is.True, "The bounded reentrancy guard must unwind on failure.");
        }

        [Test]
        public void LiveMemberCannotBeMovedToAnotherConnectionBeforeDisconnect()
        {
            var f = new Fixture();
            f.Connect(SessionReadinessState.GameplayReady);
            PartyMemberSnapshot before = f.Member();
            int calls = f.Network.Calls;
            f.Events.Clear();
            Assert.That(f.Adapter.Authenticate(f.MemberId, 99, new NetworkSpawnPosition(99, 0, 0), 40, true), Is.False);
            AssertSame(before, f.Member());
            Assert.That(f.Network.Calls, Is.EqualTo(calls), "Reject before granting a second network actor.");
            Assert.That(f.Events, Is.Empty);
            Assert.That(f.Session.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(7), out _), Is.True);
            Assert.That(f.Session.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(99), out _), Is.False);
        }

        [Test]
        public void ConnectionOwnedByAnotherMemberIsRejectedBeforeNetworkAdmission()
        {
            var f = new Fixture();
            f.Connect(SessionReadinessState.GameplayReady);
            PartyMemberSnapshot other = f.Session.Join(new JoinRequest(f.SessionId, "bob", "v1", "content-a")).Member;
            f.Events.Clear();
            int calls = f.Network.Calls;
            Assert.That(f.Adapter.Authenticate(other.MemberId, 7, new NetworkSpawnPosition(0, 0, 0), 8, false), Is.False);
            Assert.That(f.Network.Calls, Is.EqualTo(calls));
            Assert.That(f.Events, Is.Empty);
            Assert.That(f.Session.TryGetMember(other.MemberId, out var after), Is.True);
            AssertSame(other, after);
            Assert.That(f.Member().Readiness, Is.EqualTo(SessionReadinessState.GameplayReady));
        }

        [Test]
        public void NetworkConfirmationPrecedesObservableSessionsConnection()
        {
            var f = new Fixture();
            bool lowerConfirmed = false;
            f.Network.BeforeReturn = () =>
            {
                Assert.That(f.Member().Presence, Is.EqualTo(PartyPresenceState.Joined));
                Assert.That(f.Events, Is.Empty);
                lowerConfirmed = true;
            };
            f.Session.Changed += change =>
            {
                if (change.Kind == SessionLifecycleEventKind.MemberConnected)
                    Assert.That(lowerConfirmed, Is.True, "Observers must not see an unconfirmed connection.");
            };
            Assert.That(f.Authenticate(), Is.True);
            Assert.That(f.Events.Count, Is.EqualTo(1));
            Assert.That(f.Events[0].Kind, Is.EqualTo(SessionLifecycleEventKind.MemberConnected));
            Assert.That(f.Member().Readiness, Is.EqualTo(SessionReadinessState.Connected));
        }

        [Test]
        public void FailedReplacementAfterInterruptionPreservesDurableIdentityAndDisconnectedState()
        {
            var f = new Fixture();
            f.Connect(SessionReadinessState.GameplayReady);
            Assert.That(f.Adapter.Disconnect(7), Is.True);
            PartyMemberSnapshot before = f.Member();
            f.Events.Clear();
            f.Network.Accept = false;
            Assert.That(f.Adapter.Authenticate(f.MemberId, 99, new NetworkSpawnPosition(0, 0, 0), 8, false), Is.False);
            AssertSame(before, f.Member());
            Assert.That(f.Events, Is.Empty);
            Assert.That(f.Member().Presence, Is.EqualTo(PartyPresenceState.Disconnected));
        }

        [Test]
        public void SessionsHandleAloneIsNotProofOfNetworkAuthentication()
        {
            var f = new Fixture();
            Assert.That(f.Session.BindConnection(f.MemberId, SessionNetworkAdmissionAdapter.FromConnectionId(7)), Is.True);
            f.Network.Accept = false;
            f.Events.Clear();
            Assert.That(f.Authenticate(), Is.False);
            Assert.That(f.Network.Calls, Is.EqualTo(1));
            Assert.That(f.Events, Is.Empty);
        }

        [TestCase(0u, 8)]
        [TestCase(7u, 0)]
        [TestCase(7u, -1)]
        public void InvalidTransportInputsDoNotReachNetworkOrChangeSessions(uint connection, int reach)
        {
            var f = new Fixture();
            Assert.That(f.Adapter.Authenticate(f.MemberId, connection, new NetworkSpawnPosition(0, 0, 0), reach, false), Is.False);
            Assert.That(f.Network.Calls, Is.Zero);
            Assert.That(f.Events, Is.Empty);
            Assert.That(f.Member().Presence, Is.EqualTo(PartyPresenceState.Joined));
        }

        [Test]
        public void UnknownMemberDoesNotReachNetwork()
        {
            var f = new Fixture();
            Assert.That(f.Adapter.Authenticate(new PartyMemberId("absent"), 7, new NetworkSpawnPosition(0, 0, 0), 8, false), Is.False);
            Assert.That(f.Network.Calls, Is.Zero);
            Assert.That(f.Events, Is.Empty);
        }

        [Test]
        public void ReentrantAdmissionCannotCreateAnotherInFlightBinding()
        {
            var f = new Fixture();
            f.Network.BeforeReturn = () => Assert.That(f.Authenticate(), Is.False);
            Assert.That(f.Authenticate(), Is.True);
            Assert.That(f.Network.Calls, Is.EqualTo(1));
            Assert.That(f.Events.Count, Is.EqualTo(1));
        }

        private static void AssertSame(PartyMemberSnapshot expected, PartyMemberSnapshot actual)
        {
            Assert.That(actual.MemberId, Is.EqualTo(expected.MemberId));
            Assert.That(actual.Slot, Is.EqualTo(expected.Slot));
            Assert.That(actual.CharacterId, Is.EqualTo(expected.CharacterId));
            Assert.That(actual.LeadershipRole, Is.EqualTo(expected.LeadershipRole));
            Assert.That(actual.Presence, Is.EqualTo(expected.Presence));
            Assert.That(actual.Readiness, Is.EqualTo(expected.Readiness));
        }

        private sealed class Fixture
        {
            public readonly GameSessionId SessionId = new GameSessionId("adapter-invariant");
            public readonly PartySession Session;
            public readonly RecordingAdmission Network = new RecordingAdmission();
            public readonly SessionNetworkAdmissionAdapter Adapter;
            public readonly PartyMemberId MemberId;
            public readonly List<SessionLifecycleEvent> Events = new List<SessionLifecycleEvent>();
            public Fixture()
            {
                Session = new PartySession(SessionId, new SessionStartupConfiguration(2, "v1", "content-a", true));
                MemberId = Session.Join(new JoinRequest(SessionId, "alice", "v1", "content-a")).Member.MemberId;
                Assert.That(Session.BindCharacter(MemberId, new CharacterId("character:alice")), Is.True);
                Adapter = new SessionNetworkAdmissionAdapter(Session, Network);
                Session.Changed += Events.Add;
            }
            public PartyMemberSnapshot Member()
            {
                Assert.That(Session.TryGetMember(MemberId, out PartyMemberSnapshot member), Is.True);
                return member;
            }
            public bool Authenticate() => Adapter.Authenticate(MemberId, 7, new NetworkSpawnPosition(1, 2, 3), 8, false);
            public void Connect(SessionReadinessState readiness)
            {
                Assert.That(Authenticate(), Is.True);
                if (readiness >= SessionReadinessState.Synchronized) Assert.That(Session.MarkSynchronized(MemberId), Is.True);
                if (readiness == SessionReadinessState.GameplayReady) Assert.That(Session.MarkGameplayReady(MemberId), Is.True);
            }
        }

        // Only failure/timing inputs cross this port. PartySession and its production adapter own
        // all Sessions mutations; the owned player probe also exercises the real Net implementation.
        private sealed class RecordingAdmission : IAuthoritativePlayerAdmission
        {
            public bool Accept { get; set; } = true;
            public bool Throw;
            public Action BeforeReturn;
            public int Calls;
            public uint LastConnectionId { get; private set; }
            public ushort LastNetworkPlayerId { get; private set; }

            public bool AuthenticateNetworkPlayer(uint connectionId, ushort networkPlayerId, NetworkSpawnPosition authoritativePosition, int reachVoxels, bool canAlterWorld)
            {
                Calls++;
                LastConnectionId = connectionId;
                LastNetworkPlayerId = networkPlayerId;
                BeforeReturn?.Invoke();
                if (Throw) throw new InvalidOperationException("Injected admission failure.");
                return Accept;
            }
        }
    }
}
