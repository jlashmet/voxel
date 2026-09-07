using Game.Characters.Api;
using Game.Characters.Runtime;
using Game.WorldObjects.Api;
using Game.WorldObjects.Runtime;
using NUnit.Framework;
using VoxelEngine.Net.Runtime.Protocol;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeAuthoritativeGameplayCommandSinkTests
    {
        [Test]
        public void UseMainDelegatesToCanonicalWorldInteractionForDurableCharacter()
        {
            var registry = new CharacterRegistry();
            var actor = new CharacterId("multiplayer-command-actor");
            var position = new CharacterVector3(4f, 0f, 9f);
            Assert.That(registry.Create(
                new CharacterDefinition(actor, CharacterTraits.PlayerControlled),
                new CharacterKinematicState(position, default, new CharacterVector3(0f, 0f, 1f)),
                out _), Is.EqualTo(CharacterRegistryFailure.None));
            var objects = new WorldObjectRegistry();
            var door = new DoorToggleObject(new WorldObjectId("command-door"), position);
            Assert.That(objects.TryRegister(door), Is.True);
            var sink = new KentridgeAuthoritativeGameplayCommandSink(
                new InteractionClickedProcessor(registry, objects));
            var input = new C_PlayerInput { actions = (ushort)C_PlayerInput.ActionBits.UseMain };

            sink.Apply(actor, in input, 17);

            Assert.That(door.IsOpen, Is.True);
        }

        [Test]
        public void UnrelatedInputDoesNotInvokeWorldInteraction()
        {
            var registry = new CharacterRegistry();
            var actor = new CharacterId("multiplayer-command-idle");
            var position = new CharacterVector3(2f, 0f, 3f);
            Assert.That(registry.Create(
                new CharacterDefinition(actor, CharacterTraits.PlayerControlled),
                new CharacterKinematicState(position, default, new CharacterVector3(0f, 0f, 1f)),
                out _), Is.EqualTo(CharacterRegistryFailure.None));
            var objects = new WorldObjectRegistry();
            var door = new DoorToggleObject(new WorldObjectId("idle-door"), position);
            Assert.That(objects.TryRegister(door), Is.True);
            var sink = new KentridgeAuthoritativeGameplayCommandSink(
                new InteractionClickedProcessor(registry, objects));
            var input = new C_PlayerInput { actions = (ushort)C_PlayerInput.ActionBits.Aim };

            sink.Apply(actor, in input, 18);

            Assert.That(door.IsOpen, Is.False);
        }
    }
}
