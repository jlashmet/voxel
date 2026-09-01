using System;

namespace Game.Characters.Api
{
    public enum CharacterLifecycleState
    {
        Active = 0,
        Defeated = 1
    }

    public readonly struct CharacterVector3 : IEquatable<CharacterVector3>
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public CharacterVector3(float x, float y, float z)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
                throw new ArgumentOutOfRangeException(nameof(x), "Character vectors must be finite.");
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(CharacterVector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is CharacterVector3 other && Equals(other);
        public override int GetHashCode() => ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public static bool operator ==(CharacterVector3 left, CharacterVector3 right) => left.Equals(right);
        public static bool operator !=(CharacterVector3 left, CharacterVector3 right) => !left.Equals(right);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct CharacterKinematicState : IEquatable<CharacterKinematicState>
    {
        public CharacterVector3 Position { get; }
        public CharacterVector3 Velocity { get; }
        public CharacterVector3 Facing { get; }

        public CharacterKinematicState(CharacterVector3 position, CharacterVector3 velocity, CharacterVector3 facing)
        {
            Position = position;
            Velocity = velocity;
            Facing = facing;
        }

        public bool Equals(CharacterKinematicState other) =>
            Position.Equals(other.Position) && Velocity.Equals(other.Velocity) && Facing.Equals(other.Facing);
        public override bool Equals(object obj) => obj is CharacterKinematicState other && Equals(other);
        public override int GetHashCode() => ((Position.GetHashCode() * 397) ^ Velocity.GetHashCode()) * 397 ^ Facing.GetHashCode();
    }

    /// <summary>Immutable authoritative gameplay state; contains no Unity or presentation objects.</summary>
    public sealed class CharacterSnapshot
    {
        public CharacterDefinition Definition { get; }
        public CharacterId Id => Definition.Id;
        public CharacterLifecycleState Lifecycle { get; }
        public CharacterKinematicState Kinematics { get; }
        public ulong Revision { get; }

        public CharacterSnapshot(
            CharacterDefinition definition,
            CharacterLifecycleState lifecycle,
            CharacterKinematicState kinematics,
            ulong revision)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Lifecycle = lifecycle;
            Kinematics = kinematics;
            Revision = revision;
        }
    }
}
