using System;
using Game.Sessions.Api;

namespace Game.GameplayReplication.Api
{
    /// <summary>Monotonic authoritative gameplay revision. Revision zero means no authoritative state has been applied yet.</summary>
    public readonly struct GameplayRevision : IEquatable<GameplayRevision>, IComparable<GameplayRevision>
    {
        public ulong Value { get; }
        public bool IsValid => Value > 0;
        public GameplayRevision(ulong value) => Value = value;
        public int CompareTo(GameplayRevision other) => Value.CompareTo(other.Value);
        public bool Equals(GameplayRevision other) => Value == other.Value;
        public override bool Equals(object obj) => obj is GameplayRevision other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(GameplayRevision left, GameplayRevision right) => left.Equals(right);
        public static bool operator !=(GameplayRevision left, GameplayRevision right) => !left.Equals(right);
    }

    public enum GameplaySynchronizationPhase
    {
        Synchronizing = 0,
        GameplayReady = 1
    }

    public enum GameplayRecoveryMode
    {
        Repair = 0,
        FullSnapshot = 1
    }

    /// <summary>Read-only semantic synchronization state for one durable party member.</summary>
    public readonly struct GameplaySynchronizationStatus
    {
        public GameplaySynchronizationPhase Phase { get; }
        public GameplayRevision Revision { get; }
        public bool GameplayReady => Phase == GameplaySynchronizationPhase.GameplayReady;

        public GameplaySynchronizationStatus(GameplaySynchronizationPhase phase, GameplayRevision revision)
        {
            Phase = phase;
            Revision = revision;
        }
    }

    /// <summary>Typed current-state projection at one authoritative revision. Transport encoding remains private.</summary>
    public readonly struct GameplayProjectionSnapshot<TState> where TState : struct
    {
        public GameplayRevision Revision { get; }
        public TState State { get; }

        public GameplayProjectionSnapshot(GameplayRevision revision, TState state)
        {
            Revision = revision;
            State = state;
        }
    }

    /// <summary>
    /// Semantic client-side replication surface used by composition layers such as Continuity.
    /// Implementations own transport/tick/serialization/repair details; callers only request recovery and read current truth.
    /// </summary>
    public interface IGameplayReplicationClientState
    {
        void RequestRecovery(PartyMemberId memberId, GameplayRecoveryMode mode);
        bool TryGetSynchronization(PartyMemberId memberId, out GameplaySynchronizationStatus status);
        bool TryGetCurrent<TState>(PartyMemberId memberId, out GameplayProjectionSnapshot<TState> snapshot) where TState : struct;
    }
}
