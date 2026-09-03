using System.Collections.Generic;
using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Encounters.Api;
using Game.Encounters.Runtime;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    public sealed class EncounterCombatIntegrationTests
    {
        [Test]
        public void RequiredCombat_MapsMembershipAndAppliesOneTerminalFactIdempotently()
        {
            CharacterId playerId = CharacterId.FromStableKey("fixture", "player");
            CharacterId enemyId = CharacterId.FromStableKey("fixture", "enemy");
            var characters = new FixtureCharacterQuery(playerId, enemyId);
            var encounters = new EncounterRegistry(characters);
            var encounterId = new EncounterId("fixture:encounter-combat");

            Assert.That(encounters.Register(
                new EncounterDefinition(encounterId, EncounterCombatPolicy.Required, "fixture"),
                out _), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(encounters.Join(encounterId,
                new EncounterParticipant(playerId, EncounterParticipantOwnership.Persistent, "player"),
                out _), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(encounters.Join(encounterId,
                new EncounterParticipant(enemyId, EncounterParticipantOwnership.EncounterOwned, "enemy"),
                out _), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(encounters.Activate(
                new EncounterActivationRequest(encounterId, "fixture-trigger"),
                out _), Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(encounters.TryTakeCombatRequest(out EncounterCombatRequest encounterRequest), Is.True);

            var combatParticipants = new List<CombatParticipant>(encounterRequest.Participants.Count);
            for (int i = 0; i < encounterRequest.Participants.Count; i++)
            {
                EncounterParticipant participant = encounterRequest.Participants[i];
                CombatTeam team = participant.Role == "player" ? CombatTeam.Player : CombatTeam.Enemy;
                combatParticipants.Add(CombatParticipant.FromCharacter(participant.CharacterId, team));
            }

            var combat = new CombatService();
            IEncounterCombatCoordinator coordinator = new EncounterCombatCoordinator(combat);
            CombatStartResult start = coordinator.Start(new CombatStartRequest(encounterRequest.EncounterId, combatParticipants));
            Assert.That(start.EncounterId, Is.EqualTo(encounterId));

            var driver = new CombatAiBattleDriver(combat, 41);
            CombatTeam winner = driver.RunToCompletion(64);
            Assert.That(coordinator.TryTakeResolved(out CombatResolved resolved), Is.True);
            Assert.That(resolved.EncounterId, Is.EqualTo(encounterId));
            Assert.That(resolved.SessionId, Is.EqualTo(start.SessionId));
            Assert.That(resolved.WinningTeam, Is.EqualTo(winner));
            Assert.That(coordinator.TryTakeResolved(out _), Is.False,
                "Combat must emit one terminal fact for a session even when polled repeatedly.");

            EncounterResolution encounterResolution = resolved.WinningTeam == CombatTeam.Player
                ? new EncounterResolution(EncounterResolutionResult.Completed, "player team won combat")
                : new EncounterResolution(EncounterResolutionResult.Failed, "enemy team won combat");

            Assert.That(encounters.ApplyCombatResolved(encounterId, encounterResolution, out EncounterSnapshot snapshot),
                Is.EqualTo(EncounterMutationFailure.None));
            Assert.That(snapshot.Lifecycle, Is.EqualTo(EncounterLifecycleState.Resolved));
            Assert.That(snapshot.Resolution.HasValue, Is.True);
            Assert.That(snapshot.Resolution.Value.Result, Is.EqualTo(encounterResolution.Result));

            Assert.That(encounters.ApplyCombatResolved(encounterId, encounterResolution, out EncounterSnapshot repeated),
                Is.EqualTo(EncounterMutationFailure.None),
                "The Encounter consumer must accept an identical repeated terminal fact idempotently.");
            Assert.That(repeated.Revision, Is.EqualTo(snapshot.Revision),
                "Idempotent repeated resolution must not mutate Encounter authority again.");
        }

        private sealed class FixtureCharacterQuery : ICharacterQuery
        {
            private readonly Dictionary<CharacterId, CharacterSnapshot> _snapshots =
                new Dictionary<CharacterId, CharacterSnapshot>();

            public FixtureCharacterQuery(params CharacterId[] ids)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    CharacterId id = ids[i];
                    _snapshots.Add(id, new CharacterSnapshot(
                        new CharacterDefinition(id, CharacterTraits.Combatant),
                        CharacterLifecycleState.Active,
                        default,
                        1));
                }
            }

            public IReadOnlyList<CharacterSnapshot> GetAll() => new List<CharacterSnapshot>(_snapshots.Values).AsReadOnly();

            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot) => _snapshots.TryGetValue(id, out snapshot);

            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                id = default;
                return false;
            }
        }
    }
}
