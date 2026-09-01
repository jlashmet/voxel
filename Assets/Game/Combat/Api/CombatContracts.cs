using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;

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
        public CharacterId CharacterId { get; }
        public bool IsCharacterBacked => CharacterId.IsValid;
        public CombatTeam Team { get; }

        public CombatParticipant(CombatParticipantId id, CombatTeam team)
        {
            if (!id.IsValid) throw new ArgumentException("Participant id is required.", nameof(id));
            Id = id;
            CharacterId = default;
            Team = team;
        }

        public CombatParticipant(CombatParticipantId id, CharacterId characterId, CombatTeam team)
        {
            if (!id.IsValid) throw new ArgumentException("Participant id is required.", nameof(id));
            if (!characterId.IsValid) throw new ArgumentException("Character id is required.", nameof(characterId));
            Id = id;
            CharacterId = characterId;
            Team = team;
        }

        public static CombatParticipant FromCharacter(CharacterId characterId, CombatTeam team)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character id is required.", nameof(characterId));
            return new CombatParticipant(new CombatParticipantId(characterId.Value), characterId, team);
        }
    }

    public sealed class CombatStartRequest
    {
        public EncounterId EncounterId { get; }
        public IReadOnlyList<CombatParticipant> Participants { get; }

        public CombatStartRequest(EncounterId encounterId, IReadOnlyList<CombatParticipant> participants)
        {
            if (!encounterId.IsValid) throw new ArgumentException("Encounter id is required.", nameof(encounterId));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (participants.Count < 2) throw new ArgumentException("Combat requires at least two participants.", nameof(participants));

            var copy = new CombatParticipant[participants.Count];
            for (int i = 0; i < participants.Count; i++)
                copy[i] = participants[i] ?? throw new ArgumentException("Combat participant cannot be null.", nameof(participants));

            EncounterId = encounterId;
            Participants = Array.AsReadOnly(copy);
        }
    }

    public readonly struct CombatStartResult
    {
        public EncounterId EncounterId { get; }
        public CombatSessionId SessionId { get; }

        public CombatStartResult(EncounterId encounterId, CombatSessionId sessionId)
        {
            if (!encounterId.IsValid) throw new ArgumentException("Encounter id is required.", nameof(encounterId));
            if (!sessionId.IsValid) throw new ArgumentException("Combat session id is required.", nameof(sessionId));
            EncounterId = encounterId;
            SessionId = sessionId;
        }
    }

    public readonly struct CombatResolved
    {
        public EncounterId EncounterId { get; }
        public CombatSessionId SessionId { get; }
        public CombatTeam WinningTeam { get; }

        public CombatResolved(EncounterId encounterId, CombatSessionId sessionId, CombatTeam winningTeam)
        {
            if (!encounterId.IsValid) throw new ArgumentException("Encounter id is required.", nameof(encounterId));
            if (!sessionId.IsValid) throw new ArgumentException("Combat session id is required.", nameof(sessionId));
            EncounterId = encounterId;
            SessionId = sessionId;
            WinningTeam = winningTeam;
        }
    }

    public interface IEncounterCombatCoordinator
    {
        CombatStartResult Start(CombatStartRequest request);
        bool TryTakeResolved(out CombatResolved resolved);
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

    public interface ICombatService
    {
        bool IsActive { get; }
        CombatLifecycleState State { get; }
        CombatSessionId ActiveSessionId { get; }
        IReadOnlyList<CombatParticipant> ActiveParticipants { get; }
        CombatSessionId BeginCombat(CombatEncounterRequest request);
        void CompleteCombat();
    }
}
