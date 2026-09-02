using System;
using System.Collections.Generic;

namespace Game.Combat.Api
{
    public readonly struct CombatSessionId : IEquatable<CombatSessionId>
    {
        public int Value { get; }
        public bool IsValid => Value > 0;

        public CombatSessionId(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public bool Equals(CombatSessionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CombatSessionId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? "CombatSession(" + Value + ")" : "CombatSession(None)";
    }

    public readonly struct CombatParticipantId : IEquatable<CombatParticipantId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public CombatParticipantId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Participant id is required.", nameof(value));
            Value = value;
        }

        public bool Equals(CombatParticipantId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CombatParticipantId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public enum CombatTeam
    {
        Player = 0,
        Enemy = 1
    }

    public sealed class CombatParticipant
    {
        public CombatParticipantId Id { get; }
        public CombatTeam Team { get; }

        public CombatParticipant(CombatParticipantId id, CombatTeam team)
        {
            if (!id.IsValid) throw new ArgumentException("Participant id is required.", nameof(id));
            Id = id;
            Team = team;
        }
    }

    public sealed class CombatEncounterRequest
    {
        public string EncounterKey { get; }
        public IReadOnlyList<CombatParticipant> Participants { get; }

        public CombatEncounterRequest(string encounterKey, IReadOnlyList<CombatParticipant> participants)
        {
            if (string.IsNullOrWhiteSpace(encounterKey)) throw new ArgumentException("Encounter key is required.", nameof(encounterKey));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (participants.Count < 2) throw new ArgumentException("Combat requires at least two participants.", nameof(participants));

            var copy = new CombatParticipant[participants.Count];
            for (int i = 0; i < participants.Count; i++)
                copy[i] = participants[i] ?? throw new ArgumentException("Combat participant cannot be null.", nameof(participants));

            EncounterKey = encounterKey;
            Participants = Array.AsReadOnly(copy);
        }
    }

    public enum CombatLifecycleState
    {
        Idle = 0,
        Active = 1,
        Completed = 2
    }

    /// <summary>
    /// Semantic combat authority/read boundary used by non-combat modules. Concrete combat runtime
    /// types stay behind the composition layer.
    /// </summary>
    public interface ICombatService
    {
        bool IsActive { get; }
        CombatLifecycleState State { get; }
        CombatSessionId ActiveSessionId { get; }
        IReadOnlyList<CombatParticipant> ActiveParticipants { get; }
        int TurnNumber { get; }
        bool IsAlive(CombatParticipantId participant);
        CombatSessionId BeginCombat(CombatEncounterRequest request);
        void CompleteCombat();
    }

    /// <summary>Minimal semantic tactical execution boundary for autonomous combat actors.</summary>
    public interface ICombatTacticalDriver
    {
        bool Step();
    }
}
