using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Event-driven player knowledge for planned secrets. World generation may know every target while
    /// this ledger remains empty until gameplay reports clue observation or final discovery.
    /// </summary>
    public sealed class SecretDiscoveryLedger
    {
        private readonly HashSet<string> _observedClues = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _discoveredSecrets = new HashSet<string>(StringComparer.Ordinal);

        public bool Observe(ResolvedSecretCluePlan clue)
        {
            if (clue == null) throw new ArgumentNullException(nameof(clue));
            return _observedClues.Add(clue.Id.Id);
        }

        public bool HasObserved(SecretClueId clue) => _observedClues.Contains(clue.Id);

        public bool Discover(SecretRef secret) => _discoveredSecrets.Add(secret.Id);

        public bool IsDiscovered(SecretRef secret) => _discoveredSecrets.Contains(secret.Id);

        public SecretDiscoverySnapshot Capture()
        {
            string[] clues = CopySorted(_observedClues);
            string[] secrets = CopySorted(_discoveredSecrets);
            return new SecretDiscoverySnapshot(clues, secrets);
        }

        public void Restore(SecretDiscoverySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _observedClues.Clear();
            _discoveredSecrets.Clear();

            for (var i = 0; i < snapshot.ObservedClueIds.Count; i++)
                if (!string.IsNullOrWhiteSpace(snapshot.ObservedClueIds[i]))
                    _observedClues.Add(snapshot.ObservedClueIds[i]);
            for (var i = 0; i < snapshot.DiscoveredSecretIds.Count; i++)
                if (!string.IsNullOrWhiteSpace(snapshot.DiscoveredSecretIds[i]))
                    _discoveredSecrets.Add(snapshot.DiscoveredSecretIds[i]);
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
