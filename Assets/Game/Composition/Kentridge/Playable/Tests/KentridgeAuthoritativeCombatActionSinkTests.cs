using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeAuthoritativeCombatActionSinkTests
    {
        [Test]
        public void DurablePlayerAttackUsesExistingCombatAuthorityAndDamagesDeterministicEnemy()
        {
            CharacterId player = new CharacterId("kentridge-player-1");
            CharacterId enemyB = new CharacterId("enemy-b");
            CharacterId enemyA = new CharacterId("enemy-a");
            var vitality = new VitalityRegistry();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(player, 6)), Is.True);
            Assert.That(vitality.Register(VitalitySnapshot.Alive(enemyB, 6)), Is.True);
            Assert.That(vitality.Register(VitalitySnapshot.Alive(enemyA, 6)), Is.True);

            var combat = new CombatService(vitality);
            combat.BeginCombat(new CombatEncounterRequest(
                "authenticated-combat",
                new[]
                {
                    CombatParticipant.FromCharacter(player, CombatTeam.Player),
                    CombatParticipant.FromCharacter(enemyB, CombatTeam.Enemy),
                    CombatParticipant.FromCharacter(enemyA, CombatTeam.Enemy)
                }));
            var sink = new KentridgeAuthoritativeCombatActionSink(() => combat);

            Assert.That(sink.TryAttack(player), Is.True);

            Assert.That(sink.LastTargetCharacterId, Is.EqualTo(enemyA));
            Assert.That(vitality.TryGet(enemyA, out VitalitySnapshot damaged), Is.True);
            Assert.That(damaged.Current, Is.EqualTo(4));
            Assert.That(damaged.Maximum, Is.EqualTo(6));
            Assert.That(vitality.TryGet(enemyB, out VitalitySnapshot untouched), Is.True);
            Assert.That(untouched.Current, Is.EqualTo(6));
            Assert.That(combat.ActionCount, Is.EqualTo(1));
        }
    }
}
