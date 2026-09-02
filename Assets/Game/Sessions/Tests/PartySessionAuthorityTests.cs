using System;
using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;

namespace Game.Sessions.Tests
{
    public sealed class PartySessionAuthorityTests
    {
        [Test]
        public void ExplicitLeadershipPolicyRequiresExplicitTransferAfterLeaderRemoval()
        {
            var id = new GameSessionId("explicit-leader");
            var config = new SessionStartupConfiguration(3, "v1", "content-a", true, LeaderTransferPolicy.ExplicitOnly);
            var session = new PartySession(id, config);
            PartyMemberSnapshot first = session.Join(new JoinRequest(id, "alice", "v1", "content-a")).Member;
            PartyMemberSnapshot second = session.Join(new JoinRequest(id, "bob", "v1", "content-a")).Member;
            PartyMemberSnapshot third = session.Join(new JoinRequest(id, "carol", "v1", "content-a")).Member;

            Assert.That(session.Remove(first.MemberId), Is.True);
            Assert.That(session.TryGetMember(second.MemberId, out PartyMemberSnapshot before), Is.True);
            Assert.That(before.LeadershipRole, Is.EqualTo(PartyLeadershipRole.Member));
            Assert.That(session.TryGetMember(third.MemberId, out PartyMemberSnapshot otherBefore), Is.True);
            Assert.That(otherBefore.LeadershipRole, Is.EqualTo(PartyLeadershipRole.Member));

            Assert.That(session.TransferLeadership(third.MemberId), Is.True);
            Assert.That(session.TryGetMember(third.MemberId, out PartyMemberSnapshot successor), Is.True);
            Assert.That(successor.LeadershipRole, Is.EqualTo(PartyLeadershipRole.Leader));
            Assert.That(session.TryGetMember(second.MemberId, out PartyMemberSnapshot other), Is.True);
            Assert.That(other.LeadershipRole, Is.EqualTo(PartyLeadershipRole.Member));
        }

        [Test]
        public void DuplicateExternalCharacterBindingIsNotClaimedBySession()
        {
            var id = new GameSessionId("duplicate-binding");
            var writer = new RejectingBindingWriter();
            var session = new PartySession(id, new SessionStartupConfiguration(2, "v1", "content-a", true), writer);
            PartyMemberSnapshot member = session.Join(new JoinRequest(id, "alice", "v1", "content-a")).Member;

            Assert.That(session.BindCharacter(member.MemberId, new CharacterId("character:alice")), Is.False);
            Assert.That(session.TryGetMember(member.MemberId, out PartyMemberSnapshot current), Is.True);
            Assert.That(current.HasCharacter, Is.False);
        }

        [Test]
        public void DefaultStartupConfigurationCannotBypassValidation()
        {
            Assert.Throws<ArgumentException>(() => new PartySession(new GameSessionId("invalid-config"), default));
        }

        private sealed class RejectingBindingWriter : ICharacterBindingWriter
        {
            public CharacterRegistryFailure Bind(CharacterId id, CharacterBinding binding) => CharacterRegistryFailure.DuplicateBinding;
        }
    }
}
