using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Persistent-friendly runtime authority for secret discovery. SecretCandidateId is the canonical physical
    /// identity selected by SecretPlanner, so aliasing entrances or replaying interactions cannot duplicate a reward.
    /// Consumers grant rewards from Discovered; the event is raised only for the first discovery of an identity.
    /// </summary>
    public sealed class SecretDiscoveryState
    {
        private readonly HashSet<SecretCandidateId> _discovered = new HashSet<SecretCandidateId>();

        public event Action<SecretCandidateId> Discovered;

        public int Count => _discovered.Count;

        public bool IsDiscovered(SecretCandidateId id) => _discovered.Contains(id);

        public bool TryDiscover(SecretCandidateId id)
        {
            if (string.IsNullOrWhiteSpace(id.Id))
                throw new ArgumentException("Secret candidate id must be non-empty.", nameof(id));
            if (!_discovered.Add(id)) return false;
            Discovered?.Invoke(id);
            return true;
        }

        public SecretCandidateId[] Snapshot()
        {
            var result = new SecretCandidateId[_discovered.Count];
            _discovered.CopyTo(result);
            Array.Sort(result, (left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            return result;
        }

        public void Restore(SecretCandidateId[] discovered)
        {
            _discovered.Clear();
            if (discovered == null) return;
            for (int i = 0; i < discovered.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(discovered[i].Id))
                    throw new ArgumentException("Secret discovery snapshot contains an empty identity.", nameof(discovered));
                _discovered.Add(discovered[i]);
            }
        }

        public void Reset() => _discovered.Clear();
    }
}
