using System.Collections.Generic;
using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;

namespace Game.Sessions.Tests
{
    public sealed class PartySessionTests
    {
        private static SessionStartupConfiguration Config(int capacity = 4, bool joinInProgress = true) =>
            new SessionStartupConfiguration(capacity, "v1", "content-a", joinInProgress);

        private static JoinRequest Request(GameSessionId session, string applicant, bool jip = false, string version = "v1", string content = "content-a") =>
            new JoinRequest(session, applicant, version, content, jip);

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(6)]
        public void FormationUsesConfiguredCapacityAndUniqueStableSlots(int capacity)
        {
            var id = new GameSessionId("session-formation-" + capacity);
            var session = new PartySession(id, Config(capacity));
            var memberIds = new HashSet<PartyMemberId>();
            var slots = new HashSet<int>();

            for (int i = 0; i < capacity; i++)
            {
                JoinResult joined = session.Join(Request(id, "applicant-" + i));
                Assert.That(joined.Accepted, Is.True);
                Assert.That(memberIds.Add(joined.Member.MemberId), Is.True);
                Assert.That(slots.Add(joined.Member.Slot.Value), Is.True);
                Assert.That(joined.Member.Slot.Value, Is.EqualTo(i));
            }

            Assert.That(session.Join(Request(id, "overflow")).FailureReason, Is.EqualTo(JoinFailureReason.SessionFull));
        }

        [Test]
        public void RebindingConnectionPreservesMemberSlotAndCharacterIdentity()
        {
            var id = new GameSessionId("session-rebind");
            var session = new PartySession(id, Config());
            PartyMemberSnapshot joined = session.Join(Request(id, "alice")).Member;
            var character = new CharacterId("character:alice");
            Assert.That(session.BindCharacter(joined.MemberId, character), Is.True);
            Assert.That(session.BindConnection(joined.MemberId, new TransportConnectionHandle("socket-1")), Is.True);
            Assert.That(session.Disconnect(new TransportConnectionHandle("socket-1")), Is.True);
            Assert.That(session.BindConnection(joined.MemberId, new TransportConnectionHandle("socket-9")), Is.True);

            Assert.That(session.TryGetMember(joined.MemberId, out PartyMemberSnapshot rebound), Is.True);
            Assert.That(rebound.MemberId, Is.EqualTo(joined.MemberId));
            Assert.That(rebound.Slot, Is.EqualTo(joined.Slot));
            Assert.That(rebound.CharacterId, Is.EqualTo(character));
            Assert.That(session.TryResolveConnection(new TransportConnectionHandle("socket-9"), out PartyMemberId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(joined.MemberId));
        }

        [Test]
        public void ConnectedButUnsynchronizedCannotBecomeGameplayReadyOrLaunch()
        {
            var id = new GameSessionId("session-ready");
            var session = new PartySession(id, Config());
            PartyMemberSnapshot member = session.Join(Request(id, "alice")).Member;
            Assert.That(session.BindConnection(member.MemberId, new TransportConnectionHandle("socket-a")), Is.True);
            Assert.That(session.MarkGameplayReady(member.MemberId), Is.False);
            Assert.That(session.CanLaunch(), Is.False);
            Assert.That(session.MarkSynchronized(member.MemberId), Is.True);
            Assert.That(session.MarkGameplayReady(member.MemberId), Is.True);
            Assert.That(session.CanLaunch(), Is.True);
        }

        [Test]
        public void JoinInProgressPreservesExistingMembersAndRejectsIncompatibleJoin()
        {
            var id = new GameSessionId("session-jip");
            var session = new PartySession(id, Config(3, true));
            PartyMemberSnapshot first = session.Join(Request(id, "alice")).Member;
            Assert.That(session.BindConnection(first.MemberId, new TransportConnectionHandle("socket-a")), Is.True);
            Assert.That(session.MarkSynchronized(first.MemberId), Is.True);
            Assert.That(session.MarkGameplayReady(first.MemberId), Is.True);
            Assert.That(session.StartGameplay(), Is.True);

            JoinResult second = session.Join(Request(id, "bob", jip: true));
            Assert.That(second.Accepted, Is.True);
            Assert.That(second.Member.Slot.Value, Is.EqualTo(1));
            Assert.That(session.TryGetMember(first.MemberId, out PartyMemberSnapshot preserved), Is.True);
            Assert.That(preserved.Slot, Is.EqualTo(first.Slot));
            Assert.That(session.Join(Request(id, "wrong-version", jip: true, version: "v2")).FailureReason, Is.EqualTo(JoinFailureReason.ProtocolVersionMismatch));
            Assert.That(session.Join(Request(id, "wrong-content", jip: true, content: "content-b")).FailureReason, Is.EqualTo(JoinFailureReason.ContentMismatch));
        }

        [Test]
        public void JoinInProgressPolicyCanRejectAfterGameplayStarts()
        {
            var id = new GameSessionId("session-no-jip");
            var session = new PartySession(id, Config(2, false));
            PartyMemberSnapshot first = session.Join(Request(id, "alice")).Member;
            session.BindConnection(first.MemberId, new TransportConnectionHandle("socket-a"));
            session.MarkSynchronized(first.MemberId);
            session.MarkGameplayReady(first.MemberId);
            Assert.That(session.StartGameplay(), Is.True);
            Assert.That(session.Join(Request(id, "bob", jip: true)).FailureReason, Is.EqualTo(JoinFailureReason.JoinInProgressDisabled));
        }

        [Test]
        public void OldestRemainingMemberBecomesLeaderWithoutAuthoritySideEffects()
        {
            var id = new GameSessionId("session-leader");
            var session = new PartySession(id, Config());
            PartyMemberSnapshot first = session.Join(Request(id, "alice")).Member;
            PartyMemberSnapshot second = session.Join(Request(id, "bob")).Member;
            PartyMemberSnapshot third = session.Join(Request(id, "carol")).Member;
            Assert.That(first.LeadershipRole, Is.EqualTo(PartyLeadershipRole.Leader));
            Assert.That(session.Remove(first.MemberId), Is.True);
            Assert.That(session.TryGetMember(second.MemberId, out PartyMemberSnapshot successor), Is.True);
            Assert.That(successor.LeadershipRole, Is.EqualTo(PartyLeadershipRole.Leader));
            Assert.That(session.TryGetMember(third.MemberId, out PartyMemberSnapshot other), Is.True);
            Assert.That(other.LeadershipRole, Is.EqualTo(PartyLeadershipRole.Member));
        }

        [Test]
        public void CharacterBindingIsStableUniqueAndUsesSemanticCharacterBinding()
        {
            var bindingWriter = new RecordingBindingWriter();
            var id = new GameSessionId("session-character");
            var session = new PartySession(id, Config(), bindingWriter);
            PartyMemberSnapshot first = session.Join(Request(id, "alice")).Member;
            PartyMemberSnapshot second = session.Join(Request(id, "bob")).Member;
            var character = new CharacterId("character:alice");

            Assert.That(session.BindCharacter(first.MemberId, character), Is.True);
            Assert.That(session.BindCharacter(second.MemberId, character), Is.False);
            Assert.That(bindingWriter.LastBinding.Scope, Is.EqualTo("party-member"));
            Assert.That(bindingWriter.LastBinding.Key, Is.EqualTo(first.MemberId.Value));
            Assert.That(session.TryGetMember(first.MemberId, out PartyMemberSnapshot snapshot), Is.True);
            Assert.That(snapshot.CharacterId, Is.EqualTo(character));
        }

        [Test]
        public void HeadlessJoinProviderUsesOnlySemanticSessionAndMemberIdentity()
        {
            IJoinProvider provider = new DeterministicJoinProvider();
            var session = new GameSessionId("headless-session");
            var member = new PartyMemberId("headless-session:member:2");
            JoinConnectionInfo info = provider.ResolveConnection(session, member);
            Assert.That(info.Endpoint, Is.EqualTo("loopback://headless-session"));
            Assert.That(info.AdmissionToken, Is.EqualTo("headless-session:member:2"));
        }

        private sealed class RecordingBindingWriter : ICharacterBindingWriter
        {
            public CharacterBinding LastBinding { get; private set; }
            public CharacterRegistryFailure Bind(CharacterId id, CharacterBinding binding) { LastBinding = binding; return CharacterRegistryFailure.None; }
        }

        private sealed class DeterministicJoinProvider : IJoinProvider
        {
            public JoinConnectionInfo ResolveConnection(GameSessionId sessionId, PartyMemberId memberId) =>
                new JoinConnectionInfo("loopback://" + sessionId.Value, memberId.Value);
        }
    }
}
