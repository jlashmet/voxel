using System;
using System.Collections.Generic;
using Game.Residency.Api;
using Game.Residency.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Streaming.Api;

namespace Game.Residency.Tests
{
    public sealed class GameplayResidencyCoordinatorTests
    {
        [Test]
        public void HighestIndependentDemandWinsAndReleaseIsLeaseScoped()
        {
            ResidencyTarget target = Character("npc-01");
            using var coordinator = new GameplayResidencyCoordinator(null);
            IResidencyDemandLease coarse = coordinator.Acquire(Demand(target, ResidencyFidelity.Coarse, "proximity"));
            IResidencyDemandLease detailed = coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "encounter"));
            try
            {
                coordinator.Reconcile();
                AssertState(coordinator, target, ResidencyFidelity.Detailed, ResidencyFidelity.Detailed);
                coarse.Dispose();
                coordinator.Reconcile();
                AssertState(coordinator, target, ResidencyFidelity.Detailed, ResidencyFidelity.Detailed);
                detailed.Dispose();
                coordinator.Reconcile();
                AssertState(coordinator, target, ResidencyFidelity.Dormant, ResidencyFidelity.Dormant);
            }
            finally { coarse.Dispose(); detailed.Dispose(); }
        }

        [Test]
        public void DuplicateRequesterDemandsCanBeCleanedDeterministically()
        {
            ResidencyTarget a = Character("a"); ResidencyTarget b = Character("b");
            using var coordinator = new GameplayResidencyCoordinator(null);
            coordinator.Acquire(Demand(b, ResidencyFidelity.Coarse, "system-x"));
            coordinator.Acquire(Demand(a, ResidencyFidelity.Detailed, "system-x"));
            coordinator.Acquire(Demand(a, ResidencyFidelity.Coarse, "system-y"));
            Assert.AreEqual(2, coordinator.ReleaseRequester("system-x"));
            coordinator.Reconcile();
            AssertState(coordinator, a, ResidencyFidelity.Coarse, ResidencyFidelity.Coarse);
            AssertState(coordinator, b, ResidencyFidelity.Dormant, ResidencyFidelity.Dormant);
            IReadOnlyList<ResidencyTargetSnapshot> states = coordinator.GetStates();
            Assert.AreEqual(a, states[0].Target, "Diagnostics ordering must be stable rather than dictionary iteration order.");
            Assert.AreEqual(b, states[1].Target);
        }

        [Test]
        public void DetailedWaitsForPhysicalWorldThenQuiescesBeforePinRelease()
        {
            ResidencyTarget target = Character("npc-spatial");
            var pins = new RecordingPins();
            var adapter = new RecordingAdapter(ResidencyTargetKind.Character, new ResidencyRegion(4, 0, -2, 77u));
            using var coordinator = new GameplayResidencyCoordinator(pins, new[] { adapter });
            IResidencyDemandLease demand = coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "proximity"));
            coordinator.Reconcile();
            AssertState(coordinator, target, ResidencyFidelity.Coarse, ResidencyFidelity.Detailed, ResidencyTransitionPhase.WaitingForWorld);
            CollectionAssert.AreEqual(new[] { "promote:Dormant->Coarse" }, adapter.Calls);
            Assert.AreEqual(1, pins.Acquired.Count);

            pins.Acquired[0].Ready = true;
            coordinator.Reconcile();
            AssertState(coordinator, target, ResidencyFidelity.Detailed, ResidencyFidelity.Detailed);
            CollectionAssert.AreEqual(new[] { "promote:Dormant->Coarse", "promote:Coarse->Detailed" }, adapter.Calls);

            demand.Dispose();
            coordinator.Reconcile();
            CollectionAssert.AreEqual(new[] { "promote:Dormant->Coarse", "promote:Coarse->Detailed", "demote:Detailed->Coarse", "demote:Coarse->Dormant" }, adapter.Calls);
            Assert.IsTrue(pins.Acquired[0].Disposed, "Detailed owner must quiesce before its physical-world lease is released.");
            AssertState(coordinator, target, ResidencyFidelity.Dormant, ResidencyFidelity.Dormant);
        }

        [Test]
        public void FailedDetailedPromotionRemainsAtValidLowerFidelityAndReleasesPin()
        {
            ResidencyTarget target = Character("npc-fail");
            var pins = new RecordingPins { ReadyOnAcquire = true };
            var adapter = new RecordingAdapter(ResidencyTargetKind.Character, new ResidencyRegion(1, 2, 3, 5u)) { FailDetailed = true };
            using var coordinator = new GameplayResidencyCoordinator(pins, new[] { adapter });
            coordinator.Acquire(Demand(target, ResidencyFidelity.Detailed, "encounter"));
            coordinator.Reconcile();
            AssertState(coordinator, target, ResidencyFidelity.Coarse, ResidencyFidelity.Detailed, ResidencyTransitionPhase.Failed);
            Assert.IsTrue(pins.Acquired[0].Disposed);
        }

        [Test]
        public void SameDemandSequenceProducesSameTransitionOrder()
        {
            string[] first = Replay(); string[] second = Replay();
            CollectionAssert.AreEqual(first, second);
        }

        private static string[] Replay()
        {
            using var coordinator = new GameplayResidencyCoordinator(null);
            IResidencyDemandLease b = coordinator.Acquire(Demand(Character("b"), ResidencyFidelity.Coarse, "proximity"));
            IResidencyDemandLease a = coordinator.Acquire(Demand(Character("a"), ResidencyFidelity.Detailed, "control"));
            coordinator.Reconcile(); a.Dispose(); coordinator.Reconcile(); b.Dispose(); coordinator.Reconcile();
            IReadOnlyList<ResidencyTransitionRecord> history = coordinator.GetDiagnostics().TransitionHistory;
            var result = new string[history.Count];
            for (int i = 0; i < history.Count; i++) result[i] = history[i].Target + ":" + history[i].From + "->" + history[i].To + ":" + history[i].Phase;
            return result;
        }

        private static ResidencyTarget Character(string id) => new ResidencyTarget(ResidencyTargetKind.Character, id);
        private static ResidencyDemandRequest Demand(ResidencyTarget target, ResidencyFidelity fidelity, string requester) => new ResidencyDemandRequest(target, fidelity, requester, "test", "regression");

        private static void AssertState(IGameplayResidencyCoordinator coordinator, ResidencyTarget target, ResidencyFidelity current, ResidencyFidelity desired, ResidencyTransitionPhase phase = ResidencyTransitionPhase.Stable)
        {
            Assert.IsTrue(coordinator.TryGetState(target, out ResidencyTargetSnapshot state));
            Assert.AreEqual(current, state.Current); Assert.AreEqual(desired, state.Desired); Assert.AreEqual(phase, state.Phase);
        }

        private sealed class RecordingAdapter : IResidencyTargetAdapter
        {
            private readonly ResidencyRegion _region;
            public readonly List<string> Calls = new List<string>();
            public bool FailDetailed;
            public RecordingAdapter(ResidencyTargetKind kind, ResidencyRegion region) { Kind = kind; _region = region; }
            public ResidencyTargetKind Kind { get; }
            public bool TryGetDetailedRegion(ResidencyTarget target, out ResidencyRegion region) { region = _region; return true; }
            public ResidencyAdapterResult Promote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to)
            {
                Calls.Add("promote:" + from + "->" + to);
                return FailDetailed && to == ResidencyFidelity.Detailed ? ResidencyAdapterResult.Failed("detail realization rejected") : ResidencyAdapterResult.Completed();
            }
            public ResidencyAdapterResult Demote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to) { Calls.Add("demote:" + from + "->" + to); return ResidencyAdapterResult.Completed(); }
        }

        private sealed class RecordingPins : IRegionResidencyPins
        {
            public readonly List<Lease> Acquired = new List<Lease>();
            public bool ReadyOnAcquire;
            public IRegionResidencyLease AcquireResidency(in RegionLoadRequest request)
            {
                var lease = new Lease(request.RegionCoord) { Ready = ReadyOnAcquire };
                Acquired.Add(lease); return lease;
            }
            public sealed class Lease : IRegionResidencyLease
            {
                public Lease(int3 coord) { RegionCoord = coord; }
                public int3 RegionCoord { get; }
                public bool Ready;
                public bool Disposed;
                public bool IsReady => Ready && !Disposed;
                public void Dispose() { Disposed = true; }
            }
        }
    }
}
