using System;
using System.Collections.Generic;
using System.Threading;
using Game.Residency.Api;
using Unity.Mathematics;
using VoxelEngine.Streaming.Api;

namespace Game.Residency.Runtime
{
    public sealed class GameplayResidencyCoordinator : IGameplayResidencyCoordinator, IDisposable
    {
        private sealed class TargetRuntime
        {
            public ResidencyFidelity Current;
            public ResidencyFidelity Desired;
            public ResidencyTransitionPhase Phase;
            public string Diagnostic = string.Empty;
            public ulong Revision;
            public IRegionResidencyLease WorldLease;
        }

        private sealed class DemandLease : IResidencyDemandLease
        {
            private GameplayResidencyCoordinator _owner;
            public DemandLease(GameplayResidencyCoordinator owner, ulong leaseId, ResidencyDemandRequest demand) { _owner = owner; LeaseId = leaseId; Demand = demand; }
            public ulong LeaseId { get; }
            public ResidencyDemandRequest Demand { get; }
            public void Dispose() { GameplayResidencyCoordinator owner = Interlocked.Exchange(ref _owner, null); owner?.Release(LeaseId); }
        }

        private readonly Dictionary<ulong, ResidencyDemandRequest> _demands = new Dictionary<ulong, ResidencyDemandRequest>();
        private readonly Dictionary<ResidencyTarget, TargetRuntime> _targets = new Dictionary<ResidencyTarget, TargetRuntime>();
        private readonly Dictionary<ResidencyTargetKind, IResidencyTargetAdapter> _adapters = new Dictionary<ResidencyTargetKind, IResidencyTargetAdapter>();
        private readonly List<ResidencyTransitionRecord> _history = new List<ResidencyTransitionRecord>();
        private readonly IRegionResidencyPins _worldPins;
        private ulong _nextLeaseId = 1;
        private ulong _transitionSequence;
        private bool _disposed;

        public GameplayResidencyCoordinator(IRegionResidencyPins worldPins, IEnumerable<IResidencyTargetAdapter> adapters = null)
        {
            _worldPins = worldPins;
            if (adapters == null) return;
            foreach (IResidencyTargetAdapter adapter in adapters)
            {
                if (adapter == null) throw new ArgumentException("Residency adapter cannot be null.", nameof(adapters));
                if (_adapters.ContainsKey(adapter.Kind)) throw new ArgumentException("Duplicate residency adapter for " + adapter.Kind + ".", nameof(adapters));
                _adapters.Add(adapter.Kind, adapter);
            }
        }

        public IResidencyDemandLease Acquire(ResidencyDemandRequest demand)
        {
            ThrowIfDisposed();
            ulong id = _nextLeaseId++;
            _demands.Add(id, demand);
            EnsureTarget(demand.Target);
            RecomputeDesired(demand.Target);
            return new DemandLease(this, id, demand);
        }

        public int ReleaseRequester(string requesterId)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(requesterId)) throw new ArgumentException("Requester id is required.", nameof(requesterId));
            var ids = new List<ulong>();
            foreach (KeyValuePair<ulong, ResidencyDemandRequest> pair in _demands)
                if (string.Equals(pair.Value.RequesterId, requesterId, StringComparison.Ordinal)) ids.Add(pair.Key);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++) Release(ids[i]);
            return ids.Count;
        }

        public void Reconcile()
        {
            ThrowIfDisposed();
            var keys = new List<ResidencyTarget>(_targets.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++) Reconcile(keys[i], _targets[keys[i]]);
        }

        public bool TryGetState(ResidencyTarget target, out ResidencyTargetSnapshot snapshot)
        {
            ThrowIfDisposed();
            if (!_targets.TryGetValue(target, out TargetRuntime state)) { snapshot = default; return false; }
            snapshot = Snapshot(target, state);
            return true;
        }

        public IReadOnlyList<ResidencyTargetSnapshot> GetStates()
        {
            ThrowIfDisposed();
            var keys = new List<ResidencyTarget>(_targets.Keys);
            keys.Sort();
            var result = new ResidencyTargetSnapshot[keys.Count];
            for (int i = 0; i < keys.Count; i++) result[i] = Snapshot(keys[i], _targets[keys[i]]);
            return Array.AsReadOnly(result);
        }

        public ResidencyDiagnosticsSnapshot GetDiagnostics()
        {
            ThrowIfDisposed();
            int dormant = 0, coarse = 0, detailed = 0, pending = 0;
            foreach (TargetRuntime state in _targets.Values)
            {
                if (state.Current == ResidencyFidelity.Dormant) dormant++;
                else if (state.Current == ResidencyFidelity.Coarse) coarse++;
                else detailed++;
                if (state.Phase != ResidencyTransitionPhase.Stable) pending++;
            }

            var ids = new List<ulong>(_demands.Keys); ids.Sort();
            var demands = new ResidencyDemandSnapshot[ids.Count];
            for (int i = 0; i < ids.Count; i++) demands[i] = new ResidencyDemandSnapshot(ids[i], _demands[ids[i]]);
            ResidencyTransitionRecord[] history = _history.ToArray();
            return new ResidencyDiagnosticsSnapshot(dormant, coarse, detailed, pending, Array.AsReadOnly(demands), Array.AsReadOnly(history));
        }

        public void Dispose()
        {
            if (_disposed) return;

            // Teardown is a real residency transition, not permission to drop the physical substrate
            // out from under Detailed consumers. Clear policy demands, drive every target toward
            // Dormant through its owner adapter, and only let normal demotion release the world pin.
            _demands.Clear();
            var keys = new List<ResidencyTarget>(_targets.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                TargetRuntime state = _targets[keys[i]];
                if (state.Desired != ResidencyFidelity.Dormant)
                {
                    state.Desired = ResidencyFidelity.Dormant;
                    state.Revision++;
                }
                if (state.Phase == ResidencyTransitionPhase.Failed)
                {
                    state.Phase = ResidencyTransitionPhase.Stable;
                    state.Diagnostic = string.Empty;
                    state.Revision++;
                }
            }

            for (int i = 0; i < keys.Count; i++)
                Reconcile(keys[i], _targets[keys[i]]);

            for (int i = 0; i < keys.Count; i++)
            {
                TargetRuntime state = _targets[keys[i]];
                if (state.Current != ResidencyFidelity.Dormant || state.WorldLease != null)
                    throw new InvalidOperationException(
                        "Cannot dispose residency coordinator before target quiesces: " + keys[i] +
                        " current=" + state.Current + " phase=" + state.Phase +
                        " diagnostic=" + state.Diagnostic);
            }

            _disposed = true;
            _targets.Clear();
            _adapters.Clear();
        }

        private void Release(ulong leaseId)
        {
            if (_disposed || !_demands.TryGetValue(leaseId, out ResidencyDemandRequest demand)) return;
            _demands.Remove(leaseId);
            RecomputeDesired(demand.Target);
        }

        private TargetRuntime EnsureTarget(ResidencyTarget target)
        {
            if (_targets.TryGetValue(target, out TargetRuntime state)) return state;
            state = new TargetRuntime { Current = ResidencyFidelity.Dormant, Desired = ResidencyFidelity.Dormant, Phase = ResidencyTransitionPhase.Stable, Revision = 1 };
            _targets.Add(target, state);
            return state;
        }

        private void RecomputeDesired(ResidencyTarget target)
        {
            TargetRuntime state = EnsureTarget(target);
            ResidencyFidelity desired = ResidencyFidelity.Dormant;
            foreach (ResidencyDemandRequest demand in _demands.Values)
                if (demand.Target == target && demand.MinimumFidelity > desired) desired = demand.MinimumFidelity;
            if (state.Desired == desired) return;
            state.Desired = desired;
            state.Revision++;
            if (state.Phase == ResidencyTransitionPhase.Failed)
            {
                state.Phase = ResidencyTransitionPhase.Stable;
                state.Diagnostic = string.Empty;
            }
        }

        private void Reconcile(ResidencyTarget target, TargetRuntime state)
        {
            // A failed transition remains quiescent until the effective demand changes. Re-running the
            // same failing adapter or physical prerequisite every frame creates retry storms and can
            // repeatedly allocate/release realization resources without any new semantic instruction.
            if (state.Phase == ResidencyTransitionPhase.Failed) return;

            if (state.WorldLease != null && state.Current < ResidencyFidelity.Detailed && state.Desired < ResidencyFidelity.Detailed)
            {
                state.WorldLease.Dispose(); state.WorldLease = null;
            }

            while (state.Current != state.Desired)
            {
                if (state.Current < state.Desired)
                {
                    ResidencyFidelity next = (ResidencyFidelity)((byte)state.Current + 1);
                    if (next == ResidencyFidelity.Detailed && !EnsureWorldReady(target, state)) return;
                    if (!ApplyAdapter(target, state, true, state.Current, next)) return;
                    ResidencyFidelity from = state.Current;
                    state.Current = next; state.Phase = ResidencyTransitionPhase.Stable; state.Diagnostic = string.Empty; state.Revision++;
                    Record(target, from, next, ResidencyTransitionPhase.Stable, "promoted");
                }
                else
                {
                    ResidencyFidelity next = (ResidencyFidelity)((byte)state.Current - 1);
                    ResidencyFidelity from = state.Current;
                    if (!ApplyAdapter(target, state, false, from, next)) return;
                    state.Current = next; state.Phase = ResidencyTransitionPhase.Stable; state.Diagnostic = string.Empty; state.Revision++;
                    Record(target, from, next, ResidencyTransitionPhase.Stable, "demoted");
                    if (from == ResidencyFidelity.Detailed && state.WorldLease != null) { state.WorldLease.Dispose(); state.WorldLease = null; }
                }
            }
            state.Phase = ResidencyTransitionPhase.Stable;
            state.Diagnostic = string.Empty;
        }

        private bool EnsureWorldReady(ResidencyTarget target, TargetRuntime state)
        {
            if (!_adapters.TryGetValue(target.Kind, out IResidencyTargetAdapter adapter) || !adapter.TryGetDetailedRegion(target, out ResidencyRegion region)) return true;
            if (_worldPins == null) { Fail(target, state, state.Current, ResidencyFidelity.Detailed, "physical residency pins unavailable"); return false; }
            if (state.WorldLease == null)
            {
                state.WorldLease = _worldPins.AcquireResidency(new RegionLoadRequest(new int3(region.X, region.Y, region.Z), region.TerrainSeed, region.RequestedMipLevel));
                state.Revision++;
            }
            if (state.WorldLease.IsReady) return true;
            state.Phase = ResidencyTransitionPhase.WaitingForWorld;
            state.Diagnostic = "waiting for physical region " + region;
            state.Revision++;
            return false;
        }

        private bool ApplyAdapter(ResidencyTarget target, TargetRuntime state, bool promotion, ResidencyFidelity from, ResidencyFidelity to)
        {
            if (!_adapters.TryGetValue(target.Kind, out IResidencyTargetAdapter adapter)) return true;
            state.Phase = promotion ? ResidencyTransitionPhase.Promoting : ResidencyTransitionPhase.Demoting;
            ResidencyAdapterResult result = promotion ? adapter.Promote(target, from, to) : adapter.Demote(target, from, to);
            state.Revision++;
            if (result.Status == ResidencyAdapterStatus.Completed) return true;
            if (result.Status == ResidencyAdapterStatus.Pending) { state.Diagnostic = result.Diagnostic; return false; }
            if (promotion && to == ResidencyFidelity.Detailed && state.WorldLease != null) { state.WorldLease.Dispose(); state.WorldLease = null; }
            Fail(target, state, from, to, result.Diagnostic);
            return false;
        }

        private void Fail(ResidencyTarget target, TargetRuntime state, ResidencyFidelity from, ResidencyFidelity to, string diagnostic)
        {
            state.Phase = ResidencyTransitionPhase.Failed;
            state.Diagnostic = string.IsNullOrWhiteSpace(diagnostic) ? "residency transition failed" : diagnostic;
            state.Revision++;
            Record(target, from, to, ResidencyTransitionPhase.Failed, state.Diagnostic);
        }

        private void Record(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to, ResidencyTransitionPhase phase, string diagnostic)
        {
            _history.Add(new ResidencyTransitionRecord(++_transitionSequence, target, from, to, phase, diagnostic));
        }

        private static ResidencyTargetSnapshot Snapshot(ResidencyTarget target, TargetRuntime state) => new ResidencyTargetSnapshot(target, state.Current, state.Desired, state.Phase, state.Diagnostic, state.Revision);
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(GameplayResidencyCoordinator)); }
    }
}
