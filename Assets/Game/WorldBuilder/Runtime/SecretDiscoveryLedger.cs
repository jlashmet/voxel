using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// WorldBuilder-owned clue observation memory composed with the canonical runtime secret discovery authority.
    /// SecretDiscoveryState owns physical discovery identity/reward events; this type must not create a second
    /// SecretRef-keyed discovery store.
    /// </summary>
    public sealed class SecretDiscoveryLedger
    {
        private readonly HashSet<string> _observedClues = new HashSet<string>(StringComparer.Ordinal);
        private readonly SecretDiscoveryState _discoveries;

        public SecretDiscoveryLedger(SecretDiscoveryState discoveries = null)
        {
            _discoveries = discoveries ?? new SecretDiscoveryState();
        }

        public SecretDiscoveryState Discoveries => _discoveries;

        public bool Observe(ResolvedSecretCluePlan clue)
        {
            if (clue == null) throw new ArgumentNullException(nameof(clue));
            return _observedClues.Add(clue.Id.Id);
        }

        public bool HasObserved(SecretClueId clue) => _observedClues.Contains(clue.Id);

        public bool Discover(ResolvedSecretPlan secret)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            return _discoveries.TryDiscover(secret.Candidate);
        }

        public bool Discover(ResolvedSecretDiscoveryPlan secret)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            return _discoveries.TryDiscover(secret.Candidate);
        }

        public bool IsDiscovered(ResolvedSecretPlan secret)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            return _discoveries.IsDiscovered(secret.Candidate);
        }

        public bool IsDiscovered(ResolvedSecretDiscoveryPlan secret)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            return _discoveries.IsDiscovered(secret.Candidate);
        }

        public SecretDiscoverySnapshot Capture()
        {
            string[] clues = CopySorted(_observedClues);
            return new SecretDiscoverySnapshot(clues, _discoveries.Snapshot());
        }

        public void Restore(SecretDiscoverySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _observedClues.Clear();

            for (var i = 0; i < snapshot.ObservedClueIds.Count; i++)
                if (!string.IsNullOrWhiteSpace(snapshot.ObservedClueIds[i]))
                    _observedClues.Add(snapshot.ObservedClueIds[i]);

            var candidates = new SecretCandidateId[snapshot.DiscoveredCandidates.Count];
            for (var i = 0; i < candidates.Length; i++)
                candidates[i] = snapshot.DiscoveredCandidates[i];
            _discoveries.Restore(candidates);
        }

        private static string[] CopySorted(HashSet<string> source)
        {
            var result = new string[source.Count];
            source.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
    }
}
