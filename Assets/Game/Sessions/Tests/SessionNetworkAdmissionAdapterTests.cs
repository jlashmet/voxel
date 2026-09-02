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
        }

        [Test]
        public void FailedNetworkAdmissionRollsBackConnectedState()
        {
            var id = new GameSessionId("adapter-failure");
            var session = new PartySession(id, new SessionStartupConfiguration(2, "v1", "content-a", true));
            PartyMemberSnapshot joined = session.Join(new JoinRequest(id, "alice", "v1", "content-a")).Member;
            var adapter = new SessionNetworkAdmissionAdapter(session, new RecordingAdmission { Accept = false });

            Assert.That(adapter.Authenticate(joined.MemberId, 7, new NetworkSpawnPosition(0, 0, 0), 8, true), Is.False);
            Assert.That(session.TryGetMember(joined.MemberId, out PartyMemberSnapshot current), Is.True);
            Assert.That(current.Presence, Is.EqualTo(PartyPresenceState.Disconnected));
            Assert.That(current.Readiness, Is.EqualTo(SessionReadinessState.Joined));
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

        private sealed class RecordingAdmission : IAuthoritativePlayerAdmission
        {
            public bool Accept { get; set; } = true;
            public uint LastConnectionId { get; private set; }
            public ushort LastNetworkPlayerId { get; private set; }

            public bool AuthenticateNetworkPlayer(uint connectionId, ushort networkPlayerId, NetworkSpawnPosition authoritativePosition, int reachVoxels, bool canAlterWorld)
            {
                LastConnectionId = connectionId;
                LastNetworkPlayerId = networkPlayerId;
                return Accept;
            }
        }
    }
}
