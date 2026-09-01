using System;
using Game.Characters.Api;

namespace Game.Characters.Runtime
{
    public sealed class CharacterMovementRuntime : ICharacterMovementRuntime
    {
        private readonly ICharacterRegistry _registry;

        public CharacterMovementRuntime(ICharacterRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public CharacterRegistryFailure Step(
            CharacterId id,
            CharacterMovementCommand command,
            ICharacterMovementResolver resolver,
            out CharacterSnapshot snapshot)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (!_registry.TryGet(id, out CharacterSnapshot current))
            {
                snapshot = null;
                return CharacterRegistryFailure.UnknownCharacterId;
            }

            CharacterKinematicState resolved = resolver.Resolve(current, command);
            return _registry.UpdateKinematics(id, resolved, out snapshot);
        }
    }
}
