using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;

namespace Game.Encounters.Runtime
{
    public sealed class EncounterRegistry : IEncounterRegistry
    {
        private sealed class Record
        {
            public EncounterDefinition Definition;
            public EncounterLifecycleState Lifecycle;
            public readonly SortedDictionary<CharacterId, EncounterParticipant> Participants =
                new SortedDictionary<CharacterId, EncounterParticipant>();
            public EncounterResolution? Resolution;
            public string ActivationCause = string.Empty;
            public string RealizationId = string.Empty;
            public ulong Revision;
        }

        private readonly ICharacterQuery _characters;
        private readonly SortedDictionary<EncounterId, Record> _records = new SortedDictionary<EncounterId, Record>();
        private readonly Queue<EncounterCombatRequest> _combatRequests = new Queue<EncounterCombatRequest>();
        private readonly List<EncounterFact> _facts = new List<EncounterFact>();
        private ulong _sequence;

        public event Action<EncounterEvent> Changed;

        public EncounterRegistry(ICharacterQuery characters)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
        }

        public EncounterMutationFailure Register(EncounterDefinition definition, out EncounterSnapshot snapshot)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_records.ContainsKey(definition.Id))
            {
                snapshot = BuildSnapshot(_records[definition.Id]);
                return EncounterMutationFailure.DuplicateEncounter;
            }

            var record = new Record
            {
                Definition = definition,
                Lifecycle = EncounterLifecycleState.Inactive,
                Revision = 1
            };
            _records.Add(definition.Id, record);
            Emit(definition.Id, EncounterEventKind.Registered);
            snapshot = BuildSnapshot(record);
            return EncounterMutationFailure.None;
        }

        public EncounterMutationFailure Activate(EncounterActivationRequest request, out EncounterSnapshot snapshot)
        {
            if (!TryRecord(request.EncounterId, out Record record, out snapshot))
                return EncounterMutationFailure.UnknownEncounter;

            if (record.Lifecycle == EncounterLifecycleState.Active)
            {
                snapshot = BuildSnapshot(record);
                return EncounterMutationFailure.None;
            }
            if (record.Lifecycle != EncounterLifecycleState.Inactive)
                return Fail(record, EncounterMutationFailure.InvalidTransition, out snapshot);

            record.Lifecycle = EncounterLifecycleState.Active;
            record.ActivationCause = request.SemanticCause;
            record.RealizationId = request.RealizationId;
            Touch(record);
            _facts.Add(new EncounterFact(record.Definition.Id, EncounterFactKind.Activated, request.SemanticCause));
            Emit(record.Definition.Id, EncounterEventKind.Activated);

            if (record.Definition.CombatPolicy == EncounterCombatPolicy.Required)
                _combatRequests.Enqueue(new EncounterCombatRequest(record.Definition.Id, SortedParticipants(record)));

            snapshot = BuildSnapshot(record);
            return EncounterMutationFailure.None;
        }

        public EncounterMutationFailure Join(EncounterId id, EncounterParticipant participant, out EncounterSnapshot snapshot)
        {
            if (!TryRecord(id, out Record record, out snapshot))
                return EncounterMutationFailure.UnknownEncounter;
            if (record.Lifecycle == EncounterLifecycleState.Resolved || record.Lifecycle == EncounterLifecycleState.Cleaned)
                return Fail(record, EncounterMutationFailure.InvalidTransition, out snapshot);

            if (!_characters.TryGet(participant.CharacterId, out CharacterSnapshot character))
                return Fail(record, EncounterMutationFailure.UnknownCharacter, out snapshot);
            if (character.Lifecycle == CharacterLifecycleState.Defeated)
                return Fail(record, EncounterMutationFailure.DefeatedCharacter, out snapshot);

            if (record.Participants.TryGetValue(participant.CharacterId, out EncounterParticipant existing))
            {
                snapshot = BuildSnapshot(record);
                return existing.Ownership == participant.Ownership &&
                       string.Equals(existing.Role, participant.Role, StringComparison.Ordinal)
                    ? EncounterMutationFailure.None
                    : EncounterMutationFailure.DuplicateParticipant;
            }

            record.Participants.Add(participant.CharacterId, participant);
            Touch(record);
            Emit(id, EncounterEventKind.ParticipantJoined, participant.CharacterId);
            snapshot = BuildSnapshot(record);
            return EncounterMutationFailure.None;
        }

        public EncounterMutationFailure Leave(EncounterId id, CharacterId characterId, out EncounterSnapshot snapshot)
        {
            if (!TryRecord(id, out Record record, out snapshot))
                return EncounterMutationFailure.UnknownEncounter;
            if (record.Lifecycle == EncounterLifecycleState.Resolved || record.Lifecycle == EncounterLifecycleState.Cleaned)
                return Fail(record, EncounterMutationFailure.InvalidTransition, out snapshot);

            if (!record.Participants.Remove(characterId))
            {
                snapshot = BuildSnapshot(record);
                return EncounterMutationFailure.None;
            }

            Touch(record);
            Emit(id, EncounterEventKind.ParticipantLeft, characterId);
            snapshot = BuildSnapshot(record);
            return EncounterMutationFailure.None;
        }

        public EncounterMutationFailure BeginResolution(EncounterId id, EncounterResolution resolution, out EncounterSnapshot snapshot)
        {
            if (!TryRecord(id, out Record record, out snapshot))
                return EncounterMutationFailure.UnknownEncounter;
            if (record.Lifecycle == EncounterLifecycleState.Resolving)
            {
                snapshot = BuildSnapshot(record);
                return SameResolution(record.Resolution, resolution)
                    ? EncounterMutationFailure.None
                    : EncounterMutationFailure.ConflictingResolution;
            }
            if (record.Lifecycle != EncounterLifecycleState.Active)
                return Fail(record, EncounterMutationFailure.InvalidTransition, out snapshot);

            record.Lifecycle = EncounterLifecycleState.Resolving;
            record.Resolution = resolution;
            Touch(record);
            Emit(id, EncounterEventKind.Resolving);
            snapshot = BuildSnapshot(record);
            return EncounterMutationFailure.None;
        }

        public EncounterMutationFailure ResolveWithoutCombat(EncounterId id, EncounterResolution resolution, out EncounterSnapshot snapshot)
        {
            if (!TryRecord(id, out Record record, out snapshot))
                return EncounterMutationFailure.UnknownEncounter;
            if (record.Definition.CombatPolicy != EncounterCombatPolicy.None)
                return Fail(record, EncounterMutationFailure.CombatRequired, out snapshot);
            return Resolve(record, resolution, out snapshot);
        }

        public EncounterMutationFailure ApplyCombatResolved(EncounterId id, EncounterResolution resolution, out EncounterSnapshot snapshot)
        {
            if (!TryRecord(id, out Record record, out snapshot))
                return EncounterMutationFailure.UnknownEncounter;
            if (record.Definition.CombatPolicy != EncounterCombatPolicy.Required)
                return Fail(record, EncounterMutationFailure.CombatNotExpected, out snapshot);
            return Resolve(record, resolution, out snapshot);
        }

        public EncounterMutationFailure Cleanup(EncounterId id, out EncounterSnapshot snapshot)
        {
            if (!TryRecord(id, out Record record, out snapshot))
                return EncounterMutationFailure.UnknownEncounter;
            if (record.Lifecycle == EncounterLifecycleState.Cleaned)
            {
                snapshot = BuildSnapshot(record);
                return EncounterMutationFailure.None;
            }
            if (record.Lifecycle != EncounterLifecycleState.Resolved)
                return Fail(record, EncounterMutationFailure.InvalidTransition, out snapshot);

            foreach (KeyValuePair<CharacterId, EncounterParticipant> pair in record.Participants)
            {
                if (pair.Value.Ownership == EncounterParticipantOwnership.EncounterOwned)
                    _facts.Add(new EncounterFact(id, EncounterFactKind.CleanupCharacter, pair.Value.Role, pair.Key));
            }
            record.Participants.Clear();
            record.Lifecycle = EncounterLifecycleState.Cleaned;
            Touch(record);
            _facts.Add(new EncounterFact(id, EncounterFactKind.Cleaned, record.Resolution.HasValue ? record.Resolution.Value.Reason : string.Empty));
            Emit(id, EncounterEventKind.Cleaned);
            snapshot = BuildSnapshot(record);
            return EncounterMutationFailure.None;
        }

        public bool TryTakeCombatRequest(out EncounterCombatRequest request)
        {
            if (_combatRequests.Count == 0)
            {
                request = null;
                return false;
            }
            request = _combatRequests.Dequeue();
            return true;
        }

        public IReadOnlyList<EncounterFact> DrainFacts()
        {
            EncounterFact[] copy = _facts.ToArray();
            _facts.Clear();
            return Array.AsReadOnly(copy);
        }

        public bool TryGet(EncounterId id, out EncounterSnapshot snapshot)
        {
            if (!_records.TryGetValue(id, out Record record))
            {
                snapshot = null;
                return false;
            }
            snapshot = BuildSnapshot(record);
            return true;
        }

        public IReadOnlyList<EncounterSnapshot> GetAll()
        {
            var copy = new EncounterSnapshot[_records.Count];
            int index = 0;
            foreach (KeyValuePair<EncounterId, Record> pair in _records)
                copy[index++] = BuildSnapshot(pair.Value);
            return Array.AsReadOnly(copy);
        }

        public EncounterRegistrySnapshot Capture() => new EncounterRegistrySnapshot(GetAll(), _sequence);

        public EncounterMutationFailure Restore(EncounterRegistrySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var restored = new SortedDictionary<EncounterId, Record>();
            for (int i = 0; i < snapshot.Encounters.Count; i++)
            {
                EncounterSnapshot source = snapshot.Encounters[i];
                if (source == null || !source.Id.IsValid || restored.ContainsKey(source.Id))
                    return EncounterMutationFailure.InvalidSnapshot;
                if (!LifecycleShapeValid(source))
                    return EncounterMutationFailure.InvalidSnapshot;

                var record = new Record
                {
                    Definition = source.Definition,
                    Lifecycle = source.Lifecycle,
                    Resolution = source.Resolution,
                    ActivationCause = source.ActivationCause,
                    RealizationId = source.RealizationId,
                    Revision = source.Revision
                };
                for (int p = 0; p < source.Membership.Participants.Count; p++)
                {
                    EncounterParticipant participant = source.Membership.Participants[p];
                    if (record.Participants.ContainsKey(participant.CharacterId))
                        return EncounterMutationFailure.InvalidSnapshot;
                    record.Participants.Add(participant.CharacterId, participant);
                }
                restored.Add(source.Id, record);
            }

            _records.Clear();
            foreach (KeyValuePair<EncounterId, Record> pair in restored) _records.Add(pair.Key, pair.Value);
            _combatRequests.Clear();
            _facts.Clear();
            _sequence = snapshot.Sequence;
            foreach (KeyValuePair<EncounterId, Record> pair in _records)
                Emit(pair.Key, EncounterEventKind.Restored);
            return EncounterMutationFailure.None;
        }

        private EncounterMutationFailure Resolve(Record record, EncounterResolution resolution, out EncounterSnapshot snapshot)
        {
            if (record.Lifecycle == EncounterLifecycleState.Resolved)
            {
                snapshot = BuildSnapshot(record);
                return SameResolution(record.Resolution, resolution)
                    ? EncounterMutationFailure.None
                    : EncounterMutationFailure.ConflictingResolution;
            }
            if (record.Lifecycle != EncounterLifecycleState.Active && record.Lifecycle != EncounterLifecycleState.Resolving)
                return Fail(record, EncounterMutationFailure.InvalidTransition, out snapshot);
            if (record.Resolution.HasValue && !SameResolution(record.Resolution, resolution))
                return Fail(record, EncounterMutationFailure.ConflictingResolution, out snapshot);

            record.Resolution = resolution;
            record.Lifecycle = EncounterLifecycleState.Resolved;
            Touch(record);
            _facts.Add(new EncounterFact(record.Definition.Id, EncounterFactKind.Resolution,
                resolution.Result + ":" + resolution.Reason));
            Emit(record.Definition.Id, EncounterEventKind.Resolved);
            snapshot = BuildSnapshot(record);
            return EncounterMutationFailure.None;
        }

        private static bool SameResolution(EncounterResolution? existing, EncounterResolution requested) =>
            existing.HasValue && existing.Value.Result == requested.Result &&
            string.Equals(existing.Value.Reason, requested.Reason, StringComparison.Ordinal);

        private static bool LifecycleShapeValid(EncounterSnapshot snapshot)
        {
            bool terminal = snapshot.Lifecycle == EncounterLifecycleState.Resolved || snapshot.Lifecycle == EncounterLifecycleState.Cleaned;
            return terminal == snapshot.Resolution.HasValue &&
                   (snapshot.Lifecycle == EncounterLifecycleState.Inactive || !string.IsNullOrWhiteSpace(snapshot.ActivationCause));
        }

        private static IReadOnlyList<EncounterParticipant> SortedParticipants(Record record)
        {
            var participants = new EncounterParticipant[record.Participants.Count];
            int index = 0;
            foreach (KeyValuePair<CharacterId, EncounterParticipant> pair in record.Participants)
                participants[index++] = pair.Value;
            return Array.AsReadOnly(participants);
        }

        private static EncounterSnapshot BuildSnapshot(Record record) => new EncounterSnapshot(
            record.Definition,
            record.Lifecycle,
            new EncounterMembershipSnapshot(SortedParticipants(record)),
            record.Resolution,
            record.ActivationCause,
            record.RealizationId,
            record.Revision);

        private bool TryRecord(EncounterId id, out Record record, out EncounterSnapshot snapshot)
        {
            if (_records.TryGetValue(id, out record))
            {
                snapshot = BuildSnapshot(record);
                return true;
            }
            snapshot = null;
            return false;
        }

        private static EncounterMutationFailure Fail(Record record, EncounterMutationFailure failure, out EncounterSnapshot snapshot)
        {
            snapshot = BuildSnapshot(record);
            return failure;
        }

        private static void Touch(Record record) => record.Revision++;

        private void Emit(EncounterId id, EncounterEventKind kind, CharacterId characterId = default)
        {
            _sequence++;
            Changed?.Invoke(new EncounterEvent(_sequence, id, kind, characterId));
        }
    }
}
