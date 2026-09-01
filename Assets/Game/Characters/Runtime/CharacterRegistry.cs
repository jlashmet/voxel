using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.Characters.Runtime
{
    /// <summary>Single authoritative identity/lifecycle/state registry for every gameplay character.</summary>
    public sealed class CharacterRegistry : ICharacterRegistry, ICharacterRegistryPersistence
    {
        private readonly Dictionary<CharacterId, CharacterSnapshot> _characters =
            new Dictionary<CharacterId, CharacterSnapshot>();
        private readonly Dictionary<CharacterBinding, CharacterId> _bindings =
            new Dictionary<CharacterBinding, CharacterId>();
        private readonly Dictionary<CharacterId, List<CharacterBinding>> _bindingsByCharacter =
            new Dictionary<CharacterId, List<CharacterBinding>>();
        private readonly HashSet<CharacterId> _retired = new HashSet<CharacterId>();
        private ulong _eventSequence;

        public event Action<CharacterEvent> Changed;

        public IReadOnlyList<CharacterSnapshot> GetAll()
        {
            var result = new List<CharacterSnapshot>(_characters.Values);
            result.Sort((left, right) => left.Id.CompareTo(right.Id));
            return result.AsReadOnly();
        }

        public bool TryGet(CharacterId id, out CharacterSnapshot snapshot) =>
            _characters.TryGetValue(id, out snapshot);

        public CharacterRegistryFailure Create(
            CharacterDefinition definition,
            CharacterKinematicState initialState,
            out CharacterSnapshot snapshot)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (_characters.TryGetValue(definition.Id, out snapshot))
                return CharacterRegistryFailure.DuplicateCharacterId;
            if (_retired.Contains(definition.Id))
            {
                snapshot = null;
                return CharacterRegistryFailure.RetiredCharacterId;
            }

            snapshot = new CharacterSnapshot(
                definition,
                CharacterLifecycleState.Active,
                initialState,
                revision: 1);
            _characters.Add(definition.Id, snapshot);
            _bindingsByCharacter.Add(definition.Id, new List<CharacterBinding>());
            Publish(CharacterEventKind.Created, definition.Id);
            return CharacterRegistryFailure.None;
        }

        public CharacterRegistryFailure Bind(CharacterId id, CharacterBinding binding)
        {
            if (!_characters.ContainsKey(id)) return CharacterRegistryFailure.UnknownCharacterId;

            CharacterId existing;
            if (_bindings.TryGetValue(binding, out existing))
                return existing.Equals(id)
                    ? CharacterRegistryFailure.None
                    : CharacterRegistryFailure.DuplicateBinding;

            _bindings.Add(binding, id);
            _bindingsByCharacter[id].Add(binding);
            _bindingsByCharacter[id].Sort();
            Publish(CharacterEventKind.BindingAdded, id, binding);
            return CharacterRegistryFailure.None;
        }

        public bool TryResolve(CharacterBinding binding, out CharacterId id) =>
            _bindings.TryGetValue(binding, out id);

        public CharacterRegistryFailure UpdateKinematics(
            CharacterId id,
            CharacterKinematicState state,
            out CharacterSnapshot snapshot)
        {
            CharacterSnapshot current;
            if (!_characters.TryGetValue(id, out current))
            {
                snapshot = null;
                return CharacterRegistryFailure.UnknownCharacterId;
            }

            if (current.Kinematics.Equals(state))
            {
                snapshot = current;
                return CharacterRegistryFailure.None;
            }

            snapshot = new CharacterSnapshot(
                current.Definition,
                current.Lifecycle,
                state,
                current.Revision + 1);
            _characters[id] = snapshot;
            Publish(CharacterEventKind.KinematicsChanged, id);
            return CharacterRegistryFailure.None;
        }

        public CharacterRegistryFailure MarkDefeated(CharacterId id, out CharacterSnapshot snapshot)
        {
            CharacterSnapshot current;
            if (!_characters.TryGetValue(id, out current))
            {
                snapshot = null;
                return CharacterRegistryFailure.UnknownCharacterId;
            }
            if (current.Lifecycle == CharacterLifecycleState.Defeated)
            {
                snapshot = current;
                return CharacterRegistryFailure.CharacterAlreadyDefeated;
            }

            snapshot = new CharacterSnapshot(
                current.Definition,
                CharacterLifecycleState.Defeated,
                current.Kinematics,
                current.Revision + 1);
            _characters[id] = snapshot;
            Publish(CharacterEventKind.Defeated, id);
            return CharacterRegistryFailure.None;
        }

        public CharacterRegistryFailure Remove(CharacterId id)
        {
            if (!_characters.Remove(id)) return CharacterRegistryFailure.UnknownCharacterId;

            List<CharacterBinding> bindings;
            if (_bindingsByCharacter.TryGetValue(id, out bindings))
            {
                for (int i = 0; i < bindings.Count; i++)
                    _bindings.Remove(bindings[i]);
                _bindingsByCharacter.Remove(id);
            }

            _retired.Add(id);
            Publish(CharacterEventKind.Removed, id);
            return CharacterRegistryFailure.None;
        }

        public CharacterRegistryState CaptureState()
        {
            IReadOnlyList<CharacterSnapshot> snapshots = GetAll();
            var records = new List<CharacterRecord>(snapshots.Count);
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterSnapshot snapshot = snapshots[i];
                records.Add(new CharacterRecord(
                    snapshot.Definition,
                    snapshot.Lifecycle,
                    snapshot.Kinematics,
                    snapshot.Revision,
                    BindingsFor(snapshot.Id)));
            }

            var retired = new List<CharacterId>(_retired);
            retired.Sort();
            return new CharacterRegistryState(records, retired);
        }

        public CharacterRegistryFailure RestoreState(CharacterRegistryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (_characters.Count != 0 || _bindings.Count != 0 || _retired.Count != 0)
                return CharacterRegistryFailure.RegistryNotEmpty;

            var characters = new Dictionary<CharacterId, CharacterSnapshot>();
            var bindings = new Dictionary<CharacterBinding, CharacterId>();
            var bindingsByCharacter = new Dictionary<CharacterId, List<CharacterBinding>>();
            var retired = new HashSet<CharacterId>();

            for (int i = 0; i < state.RetiredIds.Count; i++)
            {
                CharacterId id = state.RetiredIds[i];
                if (!id.IsValid || !retired.Add(id)) return CharacterRegistryFailure.InvalidState;
            }

            for (int i = 0; i < state.Characters.Count; i++)
            {
                CharacterRecord record = state.Characters[i];
                CharacterId id = record.Definition.Id;
                if (!id.IsValid || record.Revision == 0 || retired.Contains(id) || characters.ContainsKey(id))
                    return CharacterRegistryFailure.InvalidState;
                if (record.Lifecycle != CharacterLifecycleState.Active && record.Lifecycle != CharacterLifecycleState.Defeated)
                    return CharacterRegistryFailure.InvalidState;

                var snapshot = new CharacterSnapshot(
                    record.Definition,
                    record.Lifecycle,
                    record.Kinematics,
                    record.Revision);
                characters.Add(id, snapshot);
                var ownedBindings = new List<CharacterBinding>(record.Bindings.Count);
                for (int b = 0; b < record.Bindings.Count; b++)
                {
                    CharacterBinding binding = record.Bindings[b];
                    if (!binding.IsValid || bindings.ContainsKey(binding))
                        return CharacterRegistryFailure.InvalidState;
                    bindings.Add(binding, id);
                    ownedBindings.Add(binding);
                }
                ownedBindings.Sort();
                bindingsByCharacter.Add(id, ownedBindings);
            }

            foreach (KeyValuePair<CharacterId, CharacterSnapshot> pair in characters)
                _characters.Add(pair.Key, pair.Value);
            foreach (KeyValuePair<CharacterBinding, CharacterId> pair in bindings)
                _bindings.Add(pair.Key, pair.Value);
            foreach (KeyValuePair<CharacterId, List<CharacterBinding>> pair in bindingsByCharacter)
                _bindingsByCharacter.Add(pair.Key, pair.Value);
            foreach (CharacterId id in retired)
                _retired.Add(id);

            return CharacterRegistryFailure.None;
        }

        internal IReadOnlyList<CharacterBinding> BindingsFor(CharacterId id)
        {
            List<CharacterBinding> bindings;
            if (!_bindingsByCharacter.TryGetValue(id, out bindings))
                return Array.Empty<CharacterBinding>();
            return bindings.AsReadOnly();
        }

        internal IReadOnlyCollection<CharacterId> RetiredIds => _retired;

        private void Publish(CharacterEventKind kind, CharacterId id, CharacterBinding binding = default)
        {
            _eventSequence++;
            Changed?.Invoke(new CharacterEvent(_eventSequence, kind, id, binding));
        }
    }
}
