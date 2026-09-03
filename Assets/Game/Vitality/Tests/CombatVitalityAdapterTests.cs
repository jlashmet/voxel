using System;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;

namespace Game.Vitality.Tests
{
    public sealed class CombatVitalityAdapterTests
    {
        [Test]
        public void CharacterBackedParticipant_ReadsAndDamagesCanonicalVitality()
        {
            var character = CharacterId.FromStableKey("combat", "target");
            var participant = CombatParticipant.FromCharacter(character, CombatTeam.Enemy);
            var vitality = new VitalityRegistry();
            vitality.Register(VitalitySnapshot.Alive(character, 6));
            var adapter = new CombatVitalityAdapter(vitality);

            Assert.That(adapter.IsAlive(participant), Is.True);
            var result = adapter.ApplyDamage(participant, 2);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.State.CharacterId, Is.EqualTo(character));
            Assert.That(result.State.Current, Is.EqualTo(4));
            Assert.That(adapter.TryGetState(participant, out var state), Is.True);
            Assert.That(state.Current, Is.EqualTo(4));
        }

        [Test]
        public void LegacyParticipant_DoesNotInventCharacterIdentity()
        {
            var vitality = new VitalityRegistry();
            var adapter = new CombatVitalityAdapter(vitality);
            var legacy = new CombatParticipant(new CombatParticipantId("legacy"), CombatTeam.Player);

            Assert.That(adapter.TryGetState(legacy, out _), Is.False);
            Assert.That(adapter.IsAlive(legacy), Is.False);
            Assert.Throws<InvalidOperationException>(() => adapter.ApplyDamage(legacy, 2));
            Assert.That(vitality.Capture(), Is.Empty);
        }
    }
}
