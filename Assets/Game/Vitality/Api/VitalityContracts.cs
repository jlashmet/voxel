using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.Vitality.Api
{
    /// <summary>Immutable current vitality truth required by replication and persistence consumers.</summary>
    public readonly struct VitalitySnapshot
    {
        public CharacterId CharacterId { get; }
        public int Current { get; }
        public int Maximum { get; }
        public bool Defeated { get; }
        public ulong Revision { get; }

        public VitalitySnapshot(CharacterId characterId, int current, int maximum, bool defeated, ulong revision)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character id is required.", nameof(characterId));
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (current < 0 || current > maximum) throw new ArgumentOutOfRangeException(nameof(current));
            CharacterId = characterId;
            Current = current;
            Maximum = maximum;
            Defeated = defeated;
            Revision = revision;
        }
    }

    public interface IVitalityQuery
    {
        IReadOnlyList<VitalitySnapshot> GetAll();
        bool TryGet(CharacterId characterId, out VitalitySnapshot snapshot);
    }
}
