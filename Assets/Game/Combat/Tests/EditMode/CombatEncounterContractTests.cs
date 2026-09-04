using Game.Characters.Api;
using Game.Combat.Api;
using Game.Encounters.Api;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    public sealed class CombatEncounterContractTests
    {
        [Test]
        public void StartRequest_PreservesEncounterAndCharacterIdentityWithoutRolePolicy()
        {
            var encounterId = new EncounterId("fixture:combat-contract");
            var player = CombatParticipant.FromCharacter(
                CharacterId.FromStableKey("fixture", "player"),
                CombatTeam.Player);
            var enemy = CombatParticipant.FromCharacter(
                CharacterId.FromStableKey("fixture", "enemy"),
                CombatTeam.Enemy);

            var request = new CombatStartRequest(encounterId, new[] { player, enemy });
            var result = new CombatStartResult(encounterId, new CombatSessionId(7));

            Assert.That(request.EncounterId, Is.EqualTo(encounterId));
            Assert.That(request.Participants.Count, Is.EqualTo(2));
            Assert.That(request.Participants[0].CharacterId, Is.EqualTo(player.CharacterId));
            Assert.That(request.Participants[1].CharacterId, Is.EqualTo(enemy.CharacterId));
            Assert.That(result.EncounterId, Is.EqualTo(encounterId));
            Assert.That(result.SessionId, Is.EqualTo(new CombatSessionId(7)));
        }

        [Test]
        public void ResolvedFact_CarriesCombatOutcomeWithoutEncounterResolutionPolicy()
        {
            var encounterId = new EncounterId("fixture:resolved-contract");
            var sessionId = new CombatSessionId(11);

            var resolved = new CombatResolved(encounterId, sessionId, CombatTeam.Player);

            Assert.That(resolved.EncounterId, Is.EqualTo(encounterId));
            Assert.That(resolved.SessionId, Is.EqualTo(sessionId));
            Assert.That(resolved.WinningTeam, Is.EqualTo(CombatTeam.Player));

            var encounterResolution = new EncounterResolution(EncounterResolutionResult.Completed, "combat won");
            Assert.That(encounterResolution.Result, Is.EqualTo(EncounterResolutionResult.Completed));
        }
    }
}
