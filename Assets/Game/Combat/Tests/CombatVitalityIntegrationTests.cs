using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Vitality.Api;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    public sealed class CombatVitalityIntegrationTests
    {
        [Test]
        public void CharacterBackedCombat_UsesInjectedVitalityForDamageAndReads()
        {
            CharacterId playerId = CharacterId.FromStableKey("fixture", "player");
            CharacterId enemyId = CharacterId.FromStableKey("fixture", "enemy");
            var vitality = new FixtureVitalityService();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(playerId, 6)), Is.True);
            Assert.That(vitality.Register(VitalitySnapshot.Alive(enemyId, 4)), Is.True);

            var combat = new CombatService(vitality);
            CombatParticipant player = CombatParticipant.FromCharacter(playerId, CombatTeam.Player);
            CombatParticipant enemy = CombatParticipant.FromCharacter(enemyId, CombatTeam.Enemy);
            combat.BeginCombat(new CombatEncounterRequest("fixture:vitality", new[] { player, enemy }));

            Assert.That(combat.IsVitalityBacked, Is.True);
            Assert.That(combat.TryGetHitPoints(enemy.Id, out int initial), Is.True);
            Assert.That(initial, Is.EqualTo(4), "Combat must preserve pre-existing actor Vitality instead of resetting a private HP store.");

            CombatCommandResult attack = combat.TryExecute(new AttackCombatantCommand(player.Id, enemy.Id));
            Assert.That(attack.Succeeded, Is.True);
            Assert.That(vitality.DamageCallCount, Is.EqualTo(1));
            Assert.That(vitality.LastDamage.Target, Is.EqualTo(enemyId));
            Assert.That(vitality.LastDamage.Amount, Is.EqualTo(2));
            Assert.That(combat.TryGetHitPoints(enemy.Id, out int afterAttack), Is.True);
            Assert.That(afterAttack, Is.EqualTo(2));

            vitality.Set(new VitalitySnapshot(enemyId, 0, 4, true));
            Assert.That(combat.IsAlive(enemy.Id), Is.False,
                "Combat life reads must observe Vitality directly rather than shadowing character health locally.");
        }

        [Test]
        public void CharacterBackedCombat_LethalVitalityDamageSettlesWinner()
        {
            CharacterId playerId = CharacterId.FromStableKey("fixture", "player-lethal");
            CharacterId enemyId = CharacterId.FromStableKey("fixture", "enemy-lethal");
            var vitality = new FixtureVitalityService();
            vitality.Register(VitalitySnapshot.Alive(playerId, 6));
            vitality.Register(VitalitySnapshot.Alive(enemyId, 2));

            var combat = new CombatService(vitality);
            CombatParticipant player = CombatParticipant.FromCharacter(playerId, CombatTeam.Player);
            CombatParticipant enemy = CombatParticipant.FromCharacter(enemyId, CombatTeam.Enemy);
            combat.BeginCombat(new CombatEncounterRequest("fixture:vitality-lethal", new[] { player, enemy }));

            CombatCommandResult result = combat.TryExecute(new AttackCombatantCommand(player.Id, enemy.Id));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(combat.State, Is.EqualTo(CombatLifecycleState.Completed));
            Assert.That(combat.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(vitality.TryGet(enemyId, out VitalitySnapshot enemyState), Is.True);
            Assert.That(enemyState.IsDefeated, Is.True);
            Assert.That(enemyState.Current, Is.Zero);
        }

        private sealed class FixtureVitalityService : IVitalityService
        {
            private readonly Dictionary<CharacterId, VitalitySnapshot> _states =
                new Dictionary<CharacterId, VitalitySnapshot>();

            public event Action<DefeatEvent> Defeated;
            public int DamageCallCount { get; private set; }
            public DamageRequest LastDamage { get; private set; }

            public bool Register(VitalitySnapshot initialState)
            {
                if (_states.ContainsKey(initialState.CharacterId)) return false;
                _states.Add(initialState.CharacterId, initialState);
                return true;
            }

            public bool Remove(CharacterId characterId) => _states.Remove(characterId);

            public bool TryGet(CharacterId characterId, out VitalitySnapshot snapshot) =>
                _states.TryGetValue(characterId, out snapshot);

            public DamageResult ApplyDamage(DamageRequest request)
            {
                DamageCallCount++;
                LastDamage = request;

                if (request.Amount <= 0)
                    return new DamageResult(false, DamageRejectionReason.InvalidAmount, 0, default, false);

                VitalitySnapshot current;
                if (!_states.TryGetValue(request.Target, out current))
                    return new DamageResult(false, DamageRejectionReason.UnknownCharacter, 0, default, false);
                if (current.IsDefeated)
                    return new DamageResult(false, DamageRejectionReason.AlreadyDefeated, 0, current, false);

                int applied = Math.Min(current.Current, request.Amount);
                int remaining = current.Current - applied;
                bool defeated = remaining == 0;
                var next = new VitalitySnapshot(request.Target, remaining, current.Maximum, defeated);
                _states[request.Target] = next;
                if (defeated) Defeated?.Invoke(new DefeatEvent(request.Target, next));
                return new DamageResult(true, DamageRejectionReason.None, applied, next, defeated);
            }

            public VitalitySnapshot[] Capture()
            {
                var snapshots = new VitalitySnapshot[_states.Count];
                _states.Values.CopyTo(snapshots, 0);
                return snapshots;
            }

            public VitalityRestoreResult Restore(VitalitySnapshot[] snapshots)
            {
                if (snapshots == null)
                    return new VitalityRestoreResult(false, VitalityRestoreRejectionReason.NullSnapshotSet);

                var replacement = new Dictionary<CharacterId, VitalitySnapshot>();
                for (int i = 0; i < snapshots.Length; i++)
                {
                    if (replacement.ContainsKey(snapshots[i].CharacterId))
                        return new VitalityRestoreResult(false, VitalityRestoreRejectionReason.DuplicateCharacter);
                    replacement.Add(snapshots[i].CharacterId, snapshots[i]);
                }

                _states.Clear();
                foreach (KeyValuePair<CharacterId, VitalitySnapshot> pair in replacement)
                    _states.Add(pair.Key, pair.Value);
                return new VitalityRestoreResult(true, VitalityRestoreRejectionReason.None);
            }

            public void Set(VitalitySnapshot snapshot)
            {
                _states[snapshot.CharacterId] = snapshot;
            }
        }
    }
}
