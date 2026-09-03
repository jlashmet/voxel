using System;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    public sealed class CombatServiceVitalityIntegrationTests
    {
        private static CharacterId Id(string key) => CharacterId.FromStableKey("combat-integration", key);

        [Test]
        public void Attack_ChangesCanonicalVitalityAndCombatProjectionOnly()
        {
            var playerId = Id("player");
            var enemyId = Id("enemy");
            var vitality = new VitalityRegistry();
            vitality.Register(VitalitySnapshot.Alive(playerId, 6));
            vitality.Register(VitalitySnapshot.Alive(enemyId, 6));

            var player = CombatParticipant.FromCharacter(playerId, CombatTeam.Player);
            var enemy = CombatParticipant.FromCharacter(enemyId, CombatTeam.Enemy);
            var combat = new CombatService(vitality);
            combat.BeginCombat(new CombatEncounterRequest("integration", new[] { player, enemy }));

            CombatCommandResult result = combat.TryExecute(new AttackCombatantCommand(player.Id, enemy.Id));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(vitality.TryGet(enemyId, out var state), Is.True);
            Assert.That(state.Current, Is.EqualTo(4));
            Assert.That(combat.TryGetHitPoints(enemy.Id, out var projected), Is.True);
            Assert.That(projected, Is.EqualTo(4));
            Assert.That(combat.IsAlive(enemy.Id), Is.True);
        }

        [Test]
        public void BattleCompletion_IsDrivenByVitalityDefeatWhileCombatOwnsWinnerPolicy()
        {
            var playerId = Id("winner-player");
            var enemyId = Id("winner-enemy");
            var vitality = new VitalityRegistry();
            vitality.Register(VitalitySnapshot.Alive(playerId, 6));
            vitality.Register(VitalitySnapshot.Alive(enemyId, 2));

            var player = CombatParticipant.FromCharacter(playerId, CombatTeam.Player);
            var enemy = CombatParticipant.FromCharacter(enemyId, CombatTeam.Enemy);
            var combat = new CombatService(vitality);
            combat.BeginCombat(new CombatEncounterRequest("winner", new[] { player, enemy }));

            CombatCommandResult result = combat.TryExecute(new AttackCombatantCommand(player.Id, enemy.Id));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(vitality.TryGet(enemyId, out var defeated), Is.True);
            Assert.That(defeated.IsDefeated, Is.True);
            Assert.That(combat.IsActive, Is.False);
            Assert.That(combat.WinningTeam, Is.EqualTo(CombatTeam.Player));
        }

        [Test]
        public void BeginCombat_RejectsLegacyOrUnregisteredParticipantsWithoutInventingLifeState()
        {
            var vitality = new VitalityRegistry();
            var registeredId = Id("registered");
            vitality.Register(VitalitySnapshot.Alive(registeredId, 6));
            var registered = CombatParticipant.FromCharacter(registeredId, CombatTeam.Player);
            var legacy = new CombatParticipant(new CombatParticipantId("legacy"), CombatTeam.Enemy);

            var legacyCombat = new CombatService(vitality);
            Assert.Throws<ArgumentException>(() =>
                legacyCombat.BeginCombat(new CombatEncounterRequest("legacy", new[] { registered, legacy })));

            var missing = CombatParticipant.FromCharacter(Id("missing"), CombatTeam.Enemy);
            var missingCombat = new CombatService(vitality);
            Assert.Throws<ArgumentException>(() =>
                missingCombat.BeginCombat(new CombatEncounterRequest("missing", new[] { registered, missing })));
            Assert.That(vitality.Capture(), Has.Length.EqualTo(1));
        }

        [Test]
        public void CombatRuntime_DependsOnVitalityApiButNotVitalityRuntime()
        {
            var references = typeof(CombatService).Assembly.GetReferencedAssemblies();
            Assert.That(Array.Exists(references, x => x.Name == "Game.Vitality.Api"), Is.True);
            Assert.That(Array.Exists(references, x => x.Name == "Game.Vitality.Runtime"), Is.False);
        }
    }
}
