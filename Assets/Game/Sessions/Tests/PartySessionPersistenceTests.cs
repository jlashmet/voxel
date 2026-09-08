using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;

namespace Game.Sessions.Tests
{
    public sealed class PartySessionPersistenceTests
    {
        [Test]
        public void RestorePreservesDurableIdentityButNotRemoteTransportState()
        {
            var sessionId = new GameSessionId("rehost-session");
            var configuration = new SessionStartupConfiguration(3, "protocol", "content", true);
            var source = new PartySession(sessionId, configuration);

            PartyMemberSnapshot host = source.Join(new JoinRequest(sessionId, "host", "protocol", "content")).Member;
            PartyMemberSnapshot remote = source.Join(new JoinRequest(sessionId, "client-a", "protocol", "content")).Member;
            Assert.That(source.BindConnection(host.MemberId, new TransportConnectionHandle("old-host")), Is.True);
            Assert.That(source.BindConnection(remote.MemberId, new TransportConnectionHandle("old-remote")), Is.True);
            Assert.That(source.MarkSynchronized(host.MemberId), Is.True);
            Assert.That(source.MarkGameplayReady(host.MemberId), Is.True);
            Assert.That(source.MarkSynchronized(remote.MemberId), Is.True);
            Assert.That(source.MarkGameplayReady(remote.MemberId), Is.True);
            Assert.That(source.BindCharacter(host.MemberId, new CharacterId("hero-host")), Is.True);
            Assert.That(source.BindCharacter(remote.MemberId, new CharacterId("hero-a")), Is.True);
            PartySessionStateCapture capture = source.CaptureState();

            var restored = new PartySession(sessionId, configuration);
            PartyMemberSnapshot freshHost = restored.Join(new JoinRequest(sessionId, "host", "protocol", "content")).Member;
            var freshConnection = new TransportConnectionHandle("fresh-host");
            Assert.That(restored.BindConnection(freshHost.MemberId, freshConnection), Is.True);
            Assert.That(restored.MarkSynchronized(freshHost.MemberId), Is.True);
            Assert.That(restored.MarkGameplayReady(freshHost.MemberId), Is.True);
            Assert.That(restored.BindCharacter(freshHost.MemberId, new CharacterId("hero-host")), Is.True);

            Assert.That(restored.RestoreState(capture), Is.EqualTo(PartySessionRestoreFailure.None));
            PartyRosterSnapshot roster = restored.Snapshot();
            Assert.That(roster.Members.Count, Is.EqualTo(2));
            Assert.That(roster.Members[0].MemberId, Is.EqualTo(host.MemberId));
            Assert.That(roster.Members[0].Slot, Is.EqualTo(host.Slot));
            Assert.That(roster.Members[0].CharacterId, Is.EqualTo(new CharacterId("hero-host")));
            Assert.That(roster.Members[0].Presence, Is.EqualTo(PartyPresenceState.Connected));
            Assert.That(roster.Members[0].Readiness, Is.EqualTo(SessionReadinessState.GameplayReady));
            Assert.That(roster.Members[1].MemberId, Is.EqualTo(remote.MemberId));
            Assert.That(roster.Members[1].Slot, Is.EqualTo(remote.Slot));
            Assert.That(roster.Members[1].CharacterId, Is.EqualTo(new CharacterId("hero-a")));
            Assert.That(roster.Members[1].Presence, Is.EqualTo(PartyPresenceState.Disconnected));
            Assert.That(roster.Members[1].Readiness, Is.EqualTo(SessionReadinessState.Joined));
            Assert.That(restored.TryResolveConnection(freshConnection, out PartyMemberId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(host.MemberId));
            Assert.That(restored.TryResolveConnection(new TransportConnectionHandle("old-remote"), out _), Is.False);

            Assert.That(restored.Remove(remote.MemberId), Is.True);
            JoinResult replacement = restored.Join(new JoinRequest(sessionId, "client-b", "protocol", "content", true));
            Assert.That(replacement.Accepted, Is.True);
            Assert.That(replacement.Member.MemberId.Value, Is.EqualTo("rehost-session:member:3"));
            Assert.That(replacement.Member.Slot.Value, Is.EqualTo(1));
        }
    }
}
