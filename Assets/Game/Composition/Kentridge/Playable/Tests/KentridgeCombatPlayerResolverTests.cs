using Game.Characters.Api;
using Game.Characters.Runtime;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeCombatPlayerResolverTests
    {
        [Test]
        public void MultiplayerSlotZeroWinsOverSinglePlayerFallback()
        {
            var registry = new CharacterRegistry();
            CharacterId fallback = new CharacterId("single-player");
            CharacterId multiplayer = new CharacterId("kentridge-player-1");
            Create(registry, fallback);
            Create(registry, multiplayer);
            Assert.That(registry.Bind(multiplayer, new CharacterBinding("multiplayer-slot", "0")),
                Is.EqualTo(CharacterRegistryFailure.None));

            Assert.That(KentridgeCombatPlayerResolver.Resolve(registry, fallback), Is.EqualTo(multiplayer));
        }

        [Test]
        public void SinglePlayerFallbackRemainsWhenNoMultiplayerSlotExists()
        {
            var registry = new CharacterRegistry();
            CharacterId fallback = new CharacterId("single-player");
            Create(registry, fallback);

            Assert.That(KentridgeCombatPlayerResolver.Resolve(registry, fallback), Is.EqualTo(fallback));
        }

        private static void Create(CharacterRegistry registry, CharacterId id)
        {
            Assert.That(registry.Create(
                new CharacterDefinition(id, CharacterTraits.PlayerControlled | CharacterTraits.Combatant),
                new CharacterKinematicState(default, default, new CharacterVector3(0f, 0f, 1f)),
                out _), Is.EqualTo(CharacterRegistryFailure.None));
        }
    }
}
