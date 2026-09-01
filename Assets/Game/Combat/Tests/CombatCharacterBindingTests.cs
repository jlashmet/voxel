using Game.Characters.Api;
using Game.Combat.Api;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    public sealed class CombatCharacterBindingTests
    {
        [Test]
        public void FromCharacter_UsesStableCharacterIdentityWithoutScenePolicy()
        {
            var characterId = CharacterId.FromStableKey("fixture", "non-kentridge-combatant");

            CombatParticipant participant = CombatParticipant.FromCharacter(characterId, CombatTeam.Enemy);

            Assert.That(participant.IsCharacterBacked, Is.True);
            Assert.That(participant.CharacterId, Is.EqualTo(characterId));
            Assert.That(participant.Id.Value, Is.EqualTo(characterId.Value));
            Assert.That(participant.Team, Is.EqualTo(CombatTeam.Enemy));
        }

        [Test]
        public void LegacyParticipant_RemainsValidUntilProductionCompositionMigrates()
        {
            var participant = new CombatParticipant(new CombatParticipantId("legacy-fixture"), CombatTeam.Player);

            Assert.That(participant.IsCharacterBacked, Is.False);
            Assert.That(participant.Id.Value, Is.EqualTo("legacy-fixture"));
            Assert.That(participant.Team, Is.EqualTo(CombatTeam.Player));
        }
    }
}
