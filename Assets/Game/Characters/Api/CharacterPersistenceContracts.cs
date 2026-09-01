using System;
using System.Collections.Generic;

namespace Game.Characters.Api
{
    public sealed class CharacterRecord
    {
        private readonly CharacterBinding[] _bindings;

        public CharacterDefinition Definition { get; }
        public CharacterLifecycleState Lifecycle { get; }
        public CharacterKinematicState Kinematics { get; }
        public ulong Revision { get; }
        public IReadOnlyList<CharacterBinding> Bindings => _bindings;

        public CharacterRecord(
            CharacterDefinition definition,
            CharacterLifecycleState lifecycle,
            CharacterKinematicState kinematics,
            ulong revision,
            IReadOnlyList<CharacterBinding> bindings)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            Lifecycle = lifecycle;
            Kinematics = kinematics;
            Revision = revision;
            _bindings = new CharacterBinding[bindings.Count];
            for (int i = 0; i < bindings.Count; i++) _bindings[i] = bindings[i];
            Array.Sort(_bindings);
        }
    }

    public sealed class CharacterRegistryState
    {
        private readonly CharacterRecord[] _characters;
        private readonly CharacterId[] _retiredIds;

        public IReadOnlyList<CharacterRecord> Characters => _characters;
        public IReadOnlyList<CharacterId> RetiredIds => _retiredIds;

        public CharacterRegistryState(
            IReadOnlyList<CharacterRecord> characters,
            IReadOnlyList<CharacterId> retiredIds)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (retiredIds == null) throw new ArgumentNullException(nameof(retiredIds));

            _characters = new CharacterRecord[characters.Count];
            for (int i = 0; i < characters.Count; i++)
                _characters[i] = characters[i] ?? throw new ArgumentException("Character state contains a null record.", nameof(characters));
            Array.Sort(_characters, (left, right) => left.Definition.Id.CompareTo(right.Definition.Id));

            _retiredIds = new CharacterId[retiredIds.Count];
            for (int i = 0; i < retiredIds.Count; i++) _retiredIds[i] = retiredIds[i];
            Array.Sort(_retiredIds);
        }
    }

    public interface ICharacterRegistryPersistence
    {
        CharacterRegistryState CaptureState();
        CharacterRegistryFailure RestoreState(CharacterRegistryState state);
    }
}
