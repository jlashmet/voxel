using System;
using System.Collections.Generic;
using Game.Residency.Api;

namespace Game.Residency.Runtime
{
    /// <summary>
    /// Policy-free-of-world-position proximity producer. Composition measures semantic distance;
    /// this class applies configured hysteresis and owns only its own demand leases.
    /// </summary>
    public sealed class ProximityResidencyDemandSource : IDisposable
    {
        private sealed class Entry
        {
            public ResidencyFidelity Fidelity;
            public IResidencyDemandLease Lease;
        }

        private readonly IGameplayResidencyCoordinator _coordinator;
        private readonly ResidencyProximityPolicy _policy;
        private readonly string _requesterId;
        private readonly Dictionary<ResidencyTarget, Entry> _entries = new Dictionary<ResidencyTarget, Entry>();
        private bool _disposed;

        public ProximityResidencyDemandSource(
            IGameplayResidencyCoordinator coordinator,
            ResidencyProximityPolicy policy,
            string sourceId)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("Proximity source id is required.", nameof(sourceId));
            _policy = policy;
            _requesterId = "proximity:" + sourceId.Trim();
        }

        public ResidencyFidelity Update(ResidencyTarget target, int distanceMetres)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(target, out Entry entry))
            {
                entry = new Entry { Fidelity = ResidencyFidelity.Dormant };
                _entries.Add(target, entry);
            }

            ResidencyFidelity next = _policy.Select(entry.Fidelity, distanceMetres);
            if (next == entry.Fidelity) return next;

            entry.Lease?.Dispose();
            entry.Lease = null;
            entry.Fidelity = next;
            if (next != ResidencyFidelity.Dormant)
            {
                entry.Lease = _coordinator.Acquire(new ResidencyDemandRequest(
                    target,
                    next,
                    _requesterId,
                    "Proximity",
                    "semantic distance band"));
            }
            return next;
        }

        public bool Remove(ResidencyTarget target)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(target, out Entry entry)) return false;
            entry.Lease?.Dispose();
            _entries.Remove(target);
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var targets = new List<ResidencyTarget>(_entries.Keys);
            targets.Sort();
            for (int i = 0; i < targets.Count; i++)
                _entries[targets[i]].Lease?.Dispose();
            _entries.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ProximityResidencyDemandSource));
        }
    }
}
