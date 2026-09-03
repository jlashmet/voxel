using Game.Characters.Api;
using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Input.Api;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CombatInputModuleBoundaryTests
    {
        [Test]
        public void SyntheticReader_DrivesCombatMoveThroughDeviceNeutralBoundary()
        {
            var playerCharacter = new CharacterId("boundary-player");
            var enemyCharacter = new CharacterId("boundary-enemy");
            var player = new CombatParticipantId(playerCharacter.Value);
            var enemy = new CombatParticipantId(enemyCharacter.Value);
            var vitality = new VitalityRegistry();
            Assert.That(vitality.Register(VitalitySnapshot.Alive(playerCharacter, 6)), Is.True);
            Assert.That(vitality.Register(VitalitySnapshot.Alive(enemyCharacter, 6)), Is.True);
            var combat = new CombatService(vitality);
            combat.BeginCombat(new CombatEncounterRequest(
                "combat-input-boundary-regression",
                new[]
                {
                    CombatParticipant.FromCharacter(playerCharacter, CombatTeam.Player),
                    CombatParticipant.FromCharacter(enemyCharacter, CombatTeam.Enemy)
                }));

            var input = new SyntheticInputReader(new PlayerInputSnapshot(
                1f,
                0f,
                0f,
                0f,
                false,
                false,
                false,
                false));
            var controller = new CombatInputController(combat, input, new LocalPlayerId(0), player);

            CombatCommandResult result = controller.Tick(0f);

            Assert.That(result.Succeeded, Is.True, result.RejectReason);
            Assert.That(input.ReadCount, Is.EqualTo(1), "Combat must consume the semantic input reader exactly once for the action.");
            Assert.That(combat.TryGetGridPosition(player, out CombatGridPosition playerPosition), Is.True);
            Assert.That(playerPosition, Is.EqualTo(new CombatGridPosition(1, 0)),
                "The synthetic semantic move must reach the authoritative combat state without a Unity device read.");
            Assert.That(combat.TryGetGridPosition(enemy, out CombatGridPosition enemyPosition), Is.True);
            Assert.That(enemyPosition, Is.EqualTo(new CombatGridPosition(3, -1)),
                "Driving player input through the boundary must not disturb unrelated combatants.");
        }

        private sealed class SyntheticInputReader : IPlayerInputReader
        {
            private readonly PlayerInputSnapshot _snapshot;

            public int ReadCount { get; private set; }

            public SyntheticInputReader(PlayerInputSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public PlayerInputSnapshot Read(LocalPlayerId player)
            {
                ReadCount++;
                return _snapshot;
            }
        }
    }
}
