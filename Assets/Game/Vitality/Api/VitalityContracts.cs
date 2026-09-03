using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.Vitality.Api
{
    /// <summary>Immutable actor vitality truth shared by mutation, replication, and persistence consumers.</summary>
    public readonly struct VitalitySnapshot : IEquatable<VitalitySnapshot>
    {
        public CharacterId CharacterId { get; }
        public int Current { get; }
        public int Maximum { get; }
        public bool Defeated { get; }
        public bool IsDefeated => Defeated;
        public bool IsAlive => !Defeated && Current > 0;
        public ulong Revision { get; }

        public VitalitySnapshot(CharacterId characterId, int current, int maximum, bool defeated, ulong revision)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character id is required.", nameof(characterId));
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (current < 0 || current > maximum) throw new ArgumentOutOfRangeException(nameof(current));
            if (defeated != (current == 0)) throw new ArgumentException("Defeated state must match zero current vitality.", nameof(defeated));
            CharacterId = characterId;
            Current = current;
            Maximum = maximum;
            Defeated = defeated;
            Revision = revision;
        }

        public VitalitySnapshot(CharacterId characterId, int current, int maximum, bool defeated)
            : this(characterId, current, maximum, defeated, 0UL) { }

        public VitalitySnapshot(CharacterId characterId, int current, int maximum)
            : this(characterId, current, maximum, current == 0, 0UL) { }

        public static VitalitySnapshot Alive(CharacterId characterId, int maximum) =>
            new VitalitySnapshot(characterId, maximum, maximum, false, 0UL);

        public bool Equals(VitalitySnapshot other) =>
            CharacterId == other.CharacterId && Current == other.Current && Maximum == other.Maximum &&
            Defeated == other.Defeated && Revision == other.Revision;
        public override bool Equals(object obj) => obj is VitalitySnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CharacterId, Current, Maximum, Defeated, Revision);
        public static bool operator ==(VitalitySnapshot left, VitalitySnapshot right) => left.Equals(right);
        public static bool operator !=(VitalitySnapshot left, VitalitySnapshot right) => !left.Equals(right);
    }

    public interface IVitalityQuery
    {
        IReadOnlyList<VitalitySnapshot> GetAll();
        bool TryGet(CharacterId characterId, out VitalitySnapshot snapshot);
    }

    public readonly struct DamageRequest
    {
        public CharacterId Target { get; }
        public int Amount { get; }
        public DamageRequest(CharacterId target, int amount) { Target = target; Amount = amount; }
    }

    public enum DamageRejectionReason { None = 0, UnknownCharacter = 1, InvalidAmount = 2, AlreadyDefeated = 3 }

    public readonly struct DamageResult
    {
        public bool Accepted { get; }
        public DamageRejectionReason RejectionReason { get; }
        public int AppliedAmount { get; }
        public VitalitySnapshot State { get; }
        public bool DefeatOccurred { get; }
        public DamageResult(bool accepted, DamageRejectionReason rejectionReason, int appliedAmount, VitalitySnapshot state, bool defeatOccurred)
        { Accepted = accepted; RejectionReason = rejectionReason; AppliedAmount = appliedAmount; State = state; DefeatOccurred = defeatOccurred; }
    }

    public readonly struct DefeatEvent
    {
        public CharacterId CharacterId { get; }
        public VitalitySnapshot State { get; }
        public DefeatEvent(CharacterId characterId, VitalitySnapshot state) { CharacterId = characterId; State = state; }
    }

    public enum VitalityRestoreRejectionReason { None = 0, NullSnapshotSet = 1, DuplicateCharacter = 2 }

    public readonly struct VitalityRestoreResult
    {
        public bool Accepted { get; }
        public VitalityRestoreRejectionReason RejectionReason { get; }
        public VitalityRestoreResult(bool accepted, VitalityRestoreRejectionReason rejectionReason)
        { Accepted = accepted; RejectionReason = rejectionReason; }
    }

    public interface IVitalityService : IVitalityQuery
    {
        event Action<DefeatEvent> Defeated;
        bool Register(VitalitySnapshot initialState);
        bool Remove(CharacterId characterId);
        DamageResult ApplyDamage(DamageRequest request);
        VitalitySnapshot[] Capture();
        VitalityRestoreResult Restore(VitalitySnapshot[] snapshots);
    }
}
