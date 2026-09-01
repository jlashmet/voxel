using System;

namespace Game.Characters.Api
{
    public readonly struct CharacterMovementCommand
    {
        public CharacterVector3 WishDirection { get; }
        public bool Sprint { get; }
        public bool Jump { get; }
        public float DeltaSeconds { get; }

        public CharacterMovementCommand(
            CharacterVector3 wishDirection,
            bool sprint,
            bool jump,
            float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            WishDirection = wishDirection;
            Sprint = sprint;
            Jump = jump;
            DeltaSeconds = deltaSeconds;
        }
    }

    /// <summary>
    /// Semantic world/collision adapter. Implementations may use the engine's existing movement and
    /// world-query mechanics, but those implementation types never enter Characters.Api.
    /// </summary>
    public interface ICharacterMovementResolver
    {
        CharacterKinematicState Resolve(CharacterSnapshot current, CharacterMovementCommand command);
    }

    public interface ICharacterMovementRuntime
    {
        CharacterRegistryFailure Step(
            CharacterId id,
            CharacterMovementCommand command,
            ICharacterMovementResolver resolver,
            out CharacterSnapshot snapshot);
    }
}
