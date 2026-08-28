using Game.Combat.Api;
using Game.Combat.Runtime;
using Game.Input.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class CombatInputModuleBoundaryTests
    {
        [Test]
        public void SyntheticReader_DrivesCombatMoveThroughDeviceNeutralBoundary()
        {
            var player = new CombatParticipantId("boundary-player");
            var enemy = new CombatParticipantId("boundary-enemy");
            var combat = new CombatService();
            combat.BeginCombat(new CombatEncounterRequest(
                "combat-input-boundary-regression",
                new[]
                {
                    new CombatParticipant(player, CombatTeam.Player),
                    new CombatParticipant(enemy, CombatTeam.Enemy)
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
