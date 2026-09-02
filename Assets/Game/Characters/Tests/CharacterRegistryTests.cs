using System.Collections.Generic;
using Game.Characters.Api;
using Game.Characters.Runtime;
using NUnit.Framework;

namespace Game.Characters.Tests
{
    public sealed class CharacterRegistryTests
    {
        [Test]
        public void SharedRegistryComposesPlayerNpcAndEnemyWithoutTypeSpecificAuthority()
        {
            var registry = new CharacterRegistry();
            CharacterId player = CharacterId.FromStableKey("player", "fixture-slot-0");
            CharacterId npc = CharacterId.FromStableKey("npc", "fixture-guide");
            CharacterId enemy = CharacterId.FromStableKey("enemy", "fixture-bandit-1");

            Assert.That(Create(registry, player, CharacterTraits.PlayerControlled | CharacterTraits.Combatant), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(Create(registry, npc, CharacterTraits.ConversationCapable | CharacterTraits.Recruitable), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(Create(registry, enemy, CharacterTraits.Combatant), Is.EqualTo(CharacterRegistryFailure.None));

            IReadOnlyList<CharacterSnapshot> all = registry.GetAll();
            Assert.That(all.Count, Is.EqualTo(3));
            Assert.That(all[0].Id, Is.EqualTo(enemy));
            Assert.That(all[1].Id, Is.EqualTo(npc));
            Assert.That(all[2].Id, Is.EqualTo(player));
            Assert.That(all[0].GetType(), Is.EqualTo(all[1].GetType()));
            Assert.That(all[1].GetType(), Is.EqualTo(all[2].GetType()));
        }

        [Test]
        public void IdentityUniquenessUnknownAndRemovalPolicyAreDeterministic()
        {
            var registry = new CharacterRegistry();
            CharacterId id = CharacterId.FromStableKey("npc", "stable-one");
            CharacterDefinition definition = new CharacterDefinition(id, CharacterTraits.ConversationCapable);

            CharacterSnapshot created;
            Assert.That(registry.Create(definition, State(1f), out created), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.Create(definition, State(2f), out _), Is.EqualTo(CharacterRegistryFailure.DuplicateCharacterId));
            Assert.That(registry.UpdateKinematics(CharacterId.FromStableKey("npc", "missing"), State(3f), out _), Is.EqualTo(CharacterRegistryFailure.UnknownCharacterId));

            Assert.That(registry.Remove(id), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.TryGet(id, out _), Is.False);
            Assert.That(registry.Create(definition, State(4f), out _), Is.EqualTo(CharacterRegistryFailure.RetiredCharacterId));
        }

        [Test]
        public void BindingUniquenessAndResolutionAreDeterministic()
        {
            var registry = new CharacterRegistry();
            CharacterId first = CharacterId.FromStableKey("npc", "first");
            CharacterId second = CharacterId.FromStableKey("npc", "second");
            Create(registry, first, CharacterTraits.ConversationCapable);
            Create(registry, second, CharacterTraits.ConversationCapable);
            var binding = new CharacterBinding("world-npc", "same-source");

            Assert.That(registry.Bind(first, binding), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.Bind(first, binding), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.Bind(second, binding), Is.EqualTo(CharacterRegistryFailure.DuplicateBinding));
            Assert.That(registry.TryResolve(binding, out CharacterId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(first));
        }

        [Test]
        public void DefeatDoesNotRemoveCharacter()
        {
            var registry = new CharacterRegistry();
            CharacterId id = CharacterId.FromStableKey("enemy", "durable-enemy");
            Create(registry, id, CharacterTraits.Combatant);

            Assert.That(registry.MarkDefeated(id, out CharacterSnapshot defeated), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(defeated.Lifecycle, Is.EqualTo(CharacterLifecycleState.Defeated));
            Assert.That(registry.TryGet(id, out CharacterSnapshot stillPresent), Is.True);
            Assert.That(stillPresent.Lifecycle, Is.EqualTo(CharacterLifecycleState.Defeated));
            Assert.That(registry.MarkDefeated(id, out _), Is.EqualTo(CharacterRegistryFailure.CharacterAlreadyDefeated));
            Assert.That(registry.Remove(id), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.TryGet(id, out _), Is.False);
        }

        [Test]
        public void PersistenceRoundTripPreservesStableIdsBindingsStateAndTombstones()
        {
            var source = new CharacterRegistry();
            CharacterId player = CharacterId.FromStableKey("player", "one");
            CharacterId retired = CharacterId.FromStableKey("npc", "retired");
            Create(source, player, CharacterTraits.PlayerControlled | CharacterTraits.Combatant);
            Create(source, retired, CharacterTraits.ConversationCapable);
            var binding = new CharacterBinding("session-player", "42");
            Assert.That(source.Bind(player, binding), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(source.UpdateKinematics(player, State(7f), out _), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(source.Remove(retired), Is.EqualTo(CharacterRegistryFailure.None));

            CharacterRegistryState state = source.CaptureState();
            var restored = new CharacterRegistry();
            Assert.That(restored.RestoreState(state), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(restored.TryGet(player, out CharacterSnapshot playerSnapshot), Is.True);
            Assert.That(playerSnapshot.Kinematics.Position.X, Is.EqualTo(7f));
            Assert.That(restored.TryResolve(binding, out CharacterId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(player));
            Assert.That(restored.Create(new CharacterDefinition(retired, CharacterTraits.ConversationCapable), State(0f), out _), Is.EqualTo(CharacterRegistryFailure.RetiredCharacterId));
        }

        [Test]
        public void HeadlessMovementResolverUpdatesAuthoritativeSnapshotWithoutGameObject()
        {
            var registry = new CharacterRegistry();
            CharacterId id = CharacterId.FromStableKey("player", "headless");
            Create(registry, id, CharacterTraits.PlayerControlled);
            var movement = new CharacterMovementRuntime(registry);
            var resolver = new LinearMovementResolver();
            var command = new CharacterMovementCommand(new CharacterVector3(2f, 0f, -1f), false, false, 0.5f);

            Assert.That(movement.Step(id, command, resolver, out CharacterSnapshot moved), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(moved.Kinematics.Position, Is.EqualTo(new CharacterVector3(1f, 0f, -0.5f)));
            Assert.That(moved.Kinematics.Velocity, Is.EqualTo(new CharacterVector3(2f, 0f, -1f)));
            Assert.That(moved.Kinematics.Facing, Is.EqualTo(new CharacterVector3(2f, 0f, -1f)));
        }

        [Test]
        public void IndependentNonKentridgeFixtureUsesSameBindingAndLifecycleContracts()
        {
            var registry = new CharacterRegistry();
            CharacterId player = CharacterId.FromStableKey("player", "showcase-like-2");
            Assert.That(Create(registry, player, CharacterTraits.PlayerControlled | CharacterTraits.Combatant), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.Bind(player, new CharacterBinding("session-player", "2")), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.Bind(player, new CharacterBinding("replication-entity", "2")), Is.EqualTo(CharacterRegistryFailure.None));
            Assert.That(registry.TryResolve(new CharacterBinding("session-player", "2"), out CharacterId resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(player));
            Assert.That(registry.TryGet(player, out CharacterSnapshot snapshot), Is.True);
            Assert.That(snapshot.Definition.HasTrait(CharacterTraits.PlayerControlled), Is.True);
        }

        private static CharacterRegistryFailure Create(CharacterRegistry registry, CharacterId id, CharacterTraits traits)
        {
            return registry.Create(new CharacterDefinition(id, traits), State(0f), out _);
        }

        private static CharacterKinematicState State(float x)
        {
            return new CharacterKinematicState(
                new CharacterVector3(x, 0f, 0f),
                new CharacterVector3(0f, 0f, 0f),
                new CharacterVector3(0f, 0f, 1f));
        }

        private sealed class LinearMovementResolver : ICharacterMovementResolver
        {
            public CharacterKinematicState Resolve(CharacterSnapshot current, CharacterMovementCommand command)
            {
                CharacterVector3 wish = command.WishDirection;
                CharacterVector3 position = current.Kinematics.Position;
                return new CharacterKinematicState(
                    new CharacterVector3(
                        position.X + wish.X * command.DeltaSeconds,
                        position.Y + wish.Y * command.DeltaSeconds,
                        position.Z + wish.Z * command.DeltaSeconds),
                    wish,
                    wish);
            }
        }
    }
}
