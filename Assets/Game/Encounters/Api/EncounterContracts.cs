using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.Encounters.Api
{
    public readonly struct EncounterId : IEquatable<EncounterId>, IComparable<EncounterId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public EncounterId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Encounter id is required.", nameof(value));
            Value = value;
        }

        public int CompareTo(EncounterId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(EncounterId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EncounterId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(EncounterId left, EncounterId right) => left.Equals(right);
        public static bool operator !=(EncounterId left, EncounterId right) => !left.Equals(right);
    }

    public enum EncounterLifecycleState
    {
        Inactive = 0,
        Active = 1,
        Resolving = 2,
        Resolved = 3,
        Cleaned = 4
    }

    public enum EncounterParticipantOwnership
    {
        Persistent = 0,
        EncounterOwned = 1
    }

    public enum EncounterCombatPolicy
    {
        None = 0,
        Required = 1
    }

    public enum EncounterResolutionResult
    {
        Completed = 0,
        Failed = 1,
        Abandoned = 2
    }

    public enum EncounterMutationFailure
    {
        None = 0,
        UnknownEncounter = 1,
        DuplicateEncounter = 2,
        InvalidTransition = 3,
        UnknownCharacter = 4,
        DefeatedCharacter = 5,
        DuplicateParticipant = 6,
        MissingParticipant = 7,
        CombatRequired = 8,
        CombatNotExpected = 9,
        ConflictingResolution = 10,
        InvalidSnapshot = 11
    }

    public sealed class EncounterDefinition
    {
        public EncounterId Id { get; }
        public EncounterCombatPolicy CombatPolicy { get; }
        public string SemanticKind { get; }

        public EncounterDefinition(EncounterId id, EncounterCombatPolicy combatPolicy, string semanticKind)
        {
            if (!id.IsValid) throw new ArgumentException("Encounter id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(semanticKind)) throw new ArgumentException("Semantic kind is required.", nameof(semanticKind));
            Id = id;
            CombatPolicy = combatPolicy;
            SemanticKind = semanticKind;
        }
    }

    public readonly struct EncounterActivationRequest
    {
        public EncounterId EncounterId { get; }
        public string SemanticCause { get; }
        public string RealizationId { get; }

        public EncounterActivationRequest(EncounterId encounterId, string semanticCause, string realizationId = "")
        {
            if (!encounterId.IsValid) throw new ArgumentException("Encounter id is required.", nameof(encounterId));
            if (string.IsNullOrWhiteSpace(semanticCause)) throw new ArgumentException("Semantic activation cause is required.", nameof(semanticCause));
            EncounterId = encounterId;
            SemanticCause = semanticCause;
            RealizationId = realizationId ?? string.Empty;
        }
    }

    public readonly struct EncounterParticipant
    {
        public CharacterId CharacterId { get; }
        public EncounterParticipantOwnership Ownership { get; }
        public string Role { get; }

        public EncounterParticipant(CharacterId characterId, EncounterParticipantOwnership ownership, string role)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character id is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Participant role is required.", nameof(role));
            CharacterId = characterId;
            Ownership = ownership;
            Role = role;
        }
    }

    public sealed class EncounterMembershipSnapshot
    {
        public IReadOnlyList<EncounterParticipant> Participants { get; }

        public EncounterMembershipSnapshot(IReadOnlyList<EncounterParticipant> participants)
        {
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            var copy = new EncounterParticipant[participants.Count];
            for (int i = 0; i < participants.Count; i++) copy[i] = participants[i];
            Participants = Array.AsReadOnly(copy);
        }
    }

    public readonly struct EncounterResolution
    {
        public EncounterResolutionResult Result { get; }
        public string Reason { get; }

        public EncounterResolution(EncounterResolutionResult result, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Resolution reason is required.", nameof(reason));
            Result = result;
            Reason = reason;
        }
    }

    public sealed class EncounterSnapshot
    {
        public EncounterDefinition Definition { get; }
        public EncounterLifecycleState Lifecycle { get; }
        public EncounterMembershipSnapshot Membership { get; }
        public EncounterResolution? Resolution { get; }
        public string ActivationCause { get; }
        public string RealizationId { get; }
        public ulong Revision { get; }

        public EncounterId Id => Definition.Id;

        public EncounterSnapshot(
            EncounterDefinition definition,
            EncounterLifecycleState lifecycle,
            EncounterMembershipSnapshot membership,
            EncounterResolution? resolution,
            string activationCause,
            string realizationId,
            ulong revision)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Membership = membership ?? throw new ArgumentNullException(nameof(membership));
            Lifecycle = lifecycle;
            Resolution = resolution;
            ActivationCause = activationCause ?? string.Empty;
            RealizationId = realizationId ?? string.Empty;
            Revision = revision;
        }
    }

    public enum EncounterEventKind
    {
        Registered = 0,
        Activated = 1,
        ParticipantJoined = 2,
        ParticipantLeft = 3,
        Resolving = 4,
        Resolved = 5,
        Cleaned = 6,
        Restored = 7
    }

    public readonly struct EncounterEvent
    {
        public ulong Sequence { get; }
        public EncounterId EncounterId { get; }
        public EncounterEventKind Kind { get; }
        public CharacterId CharacterId { get; }

        public EncounterEvent(ulong sequence, EncounterId encounterId, EncounterEventKind kind, CharacterId characterId = default)
        {
            Sequence = sequence;
            EncounterId = encounterId;
            Kind = kind;
            CharacterId = characterId;
        }
    }

    public enum EncounterFactKind
    {
        Activated = 0,
        Resolution = 1,
        CleanupCharacter = 2,
        Cleaned = 3
    }

    public readonly struct EncounterFact
    {
        public EncounterId EncounterId { get; }
        public EncounterFactKind Kind { get; }
        public string SemanticValue { get; }
        public CharacterId CharacterId { get; }

        public EncounterFact(EncounterId encounterId, EncounterFactKind kind, string semanticValue, CharacterId characterId = default)
        {
            EncounterId = encounterId;
            Kind = kind;
            SemanticValue = semanticValue ?? string.Empty;
            CharacterId = characterId;
        }
    }

    public sealed class EncounterCombatRequest
    {
        public EncounterId EncounterId { get; }
        public IReadOnlyList<EncounterParticipant> Participants { get; }

        public EncounterCombatRequest(EncounterId encounterId, IReadOnlyList<EncounterParticipant> participants)
        {
            if (!encounterId.IsValid) throw new ArgumentException("Encounter id is required.", nameof(encounterId));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            var copy = new EncounterParticipant[participants.Count];
            for (int i = 0; i < participants.Count; i++) copy[i] = participants[i];
            EncounterId = encounterId;
            Participants = Array.AsReadOnly(copy);
        }
    }

    public sealed class EncounterRegistrySnapshot
    {
        public IReadOnlyList<EncounterSnapshot> Encounters { get; }
        public ulong Sequence { get; }

        public EncounterRegistrySnapshot(IReadOnlyList<EncounterSnapshot> encounters, ulong sequence)
        {
            if (encounters == null) throw new ArgumentNullException(nameof(encounters));
            var copy = new EncounterSnapshot[encounters.Count];
            for (int i = 0; i < encounters.Count; i++) copy[i] = encounters[i] ?? throw new ArgumentException("Snapshot encounter cannot be null.", nameof(encounters));
            Encounters = Array.AsReadOnly(copy);
            Sequence = sequence;
        }
    }

    public interface IEncounterQuery
    {
        bool TryGet(EncounterId id, out EncounterSnapshot snapshot);
        IReadOnlyList<EncounterSnapshot> GetAll();
    }

    public interface IEncounterRegistry : IEncounterQuery
    {
        event Action<EncounterEvent> Changed;
        EncounterMutationFailure Register(EncounterDefinition definition, out EncounterSnapshot snapshot);
        EncounterMutationFailure Activate(EncounterActivationRequest request, out EncounterSnapshot snapshot);
        EncounterMutationFailure Join(EncounterId id, EncounterParticipant participant, out EncounterSnapshot snapshot);
        EncounterMutationFailure Leave(EncounterId id, CharacterId characterId, out EncounterSnapshot snapshot);
        EncounterMutationFailure BeginResolution(EncounterId id, EncounterResolution resolution, out EncounterSnapshot snapshot);
        EncounterMutationFailure ResolveWithoutCombat(EncounterId id, EncounterResolution resolution, out EncounterSnapshot snapshot);
        EncounterMutationFailure ApplyCombatResolved(EncounterId id, EncounterResolution resolution, out EncounterSnapshot snapshot);
        EncounterMutationFailure Cleanup(EncounterId id, out EncounterSnapshot snapshot);
        bool TryTakeCombatRequest(out EncounterCombatRequest request);
        IReadOnlyList<EncounterFact> DrainFacts();
        EncounterRegistrySnapshot Capture();
        EncounterMutationFailure Restore(EncounterRegistrySnapshot snapshot);
    }
}
