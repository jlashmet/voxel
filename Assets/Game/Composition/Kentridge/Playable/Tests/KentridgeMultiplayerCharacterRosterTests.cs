using Game.Characters.Api;
using Game.Characters.Runtime;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeMultiplayerCharacterRosterTests
    {
        [Test]
        public void EnsureCapacityCreatesDeterministicProductionCharactersAndBindings()
        {
            var registry = new CharacterRegistry();
            var roster = new KentridgeMultiplayerCharacterRoster(registry);

            roster.EnsureCapacity(3);

            Assert.That(registry.GetAll().Count, Is.EqualTo(3));
            for (int slot = 0; slot < 3; slot++)
            {
                CharacterId expected = KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(slot);
                Assert.That(registry.TryGet(expected, out CharacterSnapshot snapshot), Is.True);
                Assert.That(snapshot.Definition.HasTrait(CharacterTraits.PlayerControlled), Is.True);
                Assert.That(snapshot.Definition.HasTrait(CharacterTraits.Combatant), Is.True);
                Assert.That(snapshot.Kinematics.Position, Is.EqualTo(new CharacterVector3(slot, 0f, 0f)));
                Assert.That(registry.TryResolve(
                    new CharacterBinding("multiplayer-slot", slot.ToString()), out CharacterId bySlot), Is.True);
                Assert.That(bySlot, Is.EqualTo(expected));
                Assert.That(registry.TryResolve(
                    new CharacterBinding("combat-participant", expected.Value), out CharacterId byCombat), Is.True);
                Assert.That(byCombat, Is.EqualTo(expected));
            }
        }

        [Test]
        public void CompositionCanChooseInitialPlacementWithoutChangingDurableIdentity()
        {
            var registry = new CharacterRegistry();
            var spawn = new CharacterVector3(12f, 3f, -4f);
            var roster = new KentridgeMultiplayerCharacterRoster(registry, _ => spawn);

            roster.EnsureCapacity(3);

            for (int slot = 0; slot < 3; slot++)
            {
                CharacterId expected = KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(slot);
                Assert.That(registry.TryGet(expected, out CharacterSnapshot snapshot), Is.True);
                Assert.That(snapshot.Kinematics.Position, Is.EqualTo(spawn));
                Assert.That(registry.TryResolve(
                    new CharacterBinding("multiplayer-slot", slot.ToString()), out CharacterId bySlot), Is.True);
                Assert.That(bySlot, Is.EqualTo(expected));
            }
        }

        [Test]
        public void PartySessionBindsDurableMemberToTheSameAuthoritativeCharacter()
        {
            var registry = new CharacterRegistry();
            var roster = new KentridgeMultiplayerCharacterRoster(registry);
            roster.EnsureCapacity(3);
            var sessionId = new GameSessionId("gamesystem25-roster");
            var session = new PartySession(
                sessionId,
                new SessionStartupConfiguration(3, "v1", "content", true),
                registry);

            JoinResult joined = session.Join(new JoinRequest(sessionId, "host", "v1", "content"));
            Assert.That(joined.Accepted, Is.True);
            CharacterId expected = KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(joined.Member.Slot.Value);

            Assert.That(session.BindCharacter(joined.Member.MemberId, expected), Is.True);
            Assert.That(session.TryGetMember(joined.Member.MemberId, out PartyMemberSnapshot member), Is.True);
            Assert.That(member.CharacterId, Is.EqualTo(expected));
            Assert.That(registry.TryResolve(
                new CharacterBinding("party-member", joined.Member.MemberId.Value), out CharacterId byMember), Is.True);
            Assert.That(byMember, Is.EqualTo(expected));
        }

        [Test]
        public void EnsureCapacityIsIdempotentForExistingCompatibleCharacters()
        {
            var registry = new CharacterRegistry();
            var roster = new KentridgeMultiplayerCharacterRoster(registry);

            roster.EnsureCapacity(4);
            roster.EnsureCapacity(4);

            Assert.That(registry.GetAll().Count, Is.EqualTo(4));
            Assert.That(registry.TryResolve(
                new CharacterBinding("multiplayer-slot", "3"), out CharacterId last), Is.True);
            Assert.That(last, Is.EqualTo(KentridgeMultiplayerCharacterRoster.CharacterIdForSlot(3)));
        }
    }
}
