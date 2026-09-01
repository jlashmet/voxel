using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Vitality.Api;

namespace Game.Vitality.Runtime
{
    public sealed class VitalityRegistry : IVitalityService
    {
        private readonly Dictionary<CharacterId, VitalitySnapshot> _states = new Dictionary<CharacterId, VitalitySnapshot>();

        public event Action<DefeatEvent> Defeated;

        public bool Register(VitalitySnapshot initialState)
        {
            if (_states.ContainsKey(initialState.CharacterId))
                return false;

            _states.Add(initialState.CharacterId, initialState);
            return true;
        }

        public bool Remove(CharacterId characterId) => _states.Remove(characterId);

        public bool TryGet(CharacterId characterId, out VitalitySnapshot snapshot) =>
            _states.TryGetValue(characterId, out snapshot);

        public DamageResult ApplyDamage(DamageRequest request)
        {
            if (!request.Target.IsValid || !_states.TryGetValue(request.Target, out var current))
            {
                return new DamageResult(
                    false,
                    DamageRejectionReason.UnknownCharacter,
                    0,
                    default,
                    false);
            }

            if (request.Amount <= 0)
            {
                return new DamageResult(
                    false,
                    DamageRejectionReason.InvalidAmount,
                    0,
                    current,
                    false);
            }

            if (current.IsDefeated)
            {
                return new DamageResult(
                    false,
                    DamageRejectionReason.AlreadyDefeated,
                    0,
                    current,
                    false);
            }

            var applied = Math.Min(request.Amount, current.Current);
            var remaining = current.Current - applied;
            var next = new VitalitySnapshot(
                current.CharacterId,
                remaining,
                current.Maximum,
                remaining == 0);
            var defeatOccurred = !current.IsDefeated && next.IsDefeated;

            _states[request.Target] = next;

            var result = new DamageResult(
                true,
                DamageRejectionReason.None,
                applied,
                next,
                defeatOccurred);

            if (defeatOccurred)
                Defeated?.Invoke(new DefeatEvent(request.Target, next));

            return result;
        }

        public VitalitySnapshot[] Capture()
        {
            var snapshots = new VitalitySnapshot[_states.Count];
            var index = 0;
            foreach (var snapshot in _states.Values)
                snapshots[index++] = snapshot;

            Array.Sort(snapshots, CompareSnapshots);
            return snapshots;
        }

        public VitalityRestoreResult Restore(VitalitySnapshot[] snapshots)
        {
            if (snapshots == null)
                return new VitalityRestoreResult(false, VitalityRestoreRejectionReason.NullSnapshotSet);

            var replacement = new Dictionary<CharacterId, VitalitySnapshot>(snapshots.Length);
            for (var i = 0; i < snapshots.Length; i++)
            {
                var snapshot = snapshots[i];
                if (replacement.ContainsKey(snapshot.CharacterId))
                    return new VitalityRestoreResult(false, VitalityRestoreRejectionReason.DuplicateCharacter);
                replacement.Add(snapshot.CharacterId, snapshot);
            }

            _states.Clear();
            foreach (var pair in replacement)
                _states.Add(pair.Key, pair.Value);

            return new VitalityRestoreResult(true, VitalityRestoreRejectionReason.None);
        }

        private static int CompareSnapshots(VitalitySnapshot left, VitalitySnapshot right) =>
            left.CharacterId.CompareTo(right.CharacterId);
    }
}
