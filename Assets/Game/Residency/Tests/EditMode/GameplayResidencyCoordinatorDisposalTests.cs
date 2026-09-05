using System;
using System.Collections.Generic;
using Game.Residency.Api;
using Game.Residency.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Streaming.Api;

namespace Game.Residency.Tests
{
    public sealed class GameplayResidencyCoordinatorDisposalTests
    {
        [Test]
        public void DisposeQuiescesDetailedConsumerBeforeReleasingPhysicalLease()
        {
            var events = new List<string>();
            var pins = new RecordingPins(events);
            var adapter = new RecordingAdapter(events);
            var target = new ResidencyTarget(ResidencyTargetKind.Character, "npc-dispose-order");
            var coordinator = new GameplayResidencyCoordinator(pins, new[] { adapter });
            coordinator.Acquire(new ResidencyDemandRequest(
                target, ResidencyFidelity.Detailed, "dispose-test", "Test", "exercise teardown ordering"));
            coordinator.Reconcile();
            events.Clear();

            coordinator.Dispose();

            CollectionAssert.AreEqual(
                new[] { "demote:Detailed->Coarse", "pin-disposed", "demote:Coarse->Dormant" },
                events);
        }

        [Test]
        public void DisposeDoesNotReleasePhysicalLeaseWhileDetailedDemotionIsPending()
        {
            var events = new List<string>();
            var pins = new RecordingPins(events);
            var adapter = new RecordingAdapter(events) { PendingDetailedDemotion = true };
            var target = new ResidencyTarget(ResidencyTargetKind.Character, "npc-dispose-pending");
            var coordinator = new GameplayResidencyCoordinator(pins, new[] { adapter });
            coordinator.Acquire(new ResidencyDemandRequest(
                target, ResidencyFidelity.Detailed, "dispose-test", "Test", "exercise pending teardown"));
            coordinator.Reconcile();
            events.Clear();

            Assert.Throws<InvalidOperationException>(() => coordinator.Dispose());
            Assert.IsFalse(pins.AcquiredLease.Disposed, "Detailed substrate must remain pinned until the adapter quiesces.");

            adapter.PendingDetailedDemotion = false;
            coordinator.Dispose();
            Assert.IsTrue(pins.AcquiredLease.Disposed);
        }

        private sealed class RecordingAdapter : IResidencyTargetAdapter
        {
            private readonly List<string> _events;

            public RecordingAdapter(List<string> events)
            {
                _events = events;
            }

            public ResidencyTargetKind Kind => ResidencyTargetKind.Character;
            public bool PendingDetailedDemotion { get; set; }

            public bool TryGetDetailedRegion(ResidencyTarget target, out ResidencyRegion region)
            {
                region = new ResidencyRegion(3, 0, 7, 19u);
                return true;
            }

            public ResidencyAdapterResult Promote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to)
            {
                _events.Add("promote:" + from + "->" + to);
                return ResidencyAdapterResult.Completed();
            }

            public ResidencyAdapterResult Demote(ResidencyTarget target, ResidencyFidelity from, ResidencyFidelity to)
            {
                _events.Add("demote:" + from + "->" + to);
                if (from == ResidencyFidelity.Detailed && to == ResidencyFidelity.Coarse && PendingDetailedDemotion)
                    return ResidencyAdapterResult.Pending("owner still quiescing");
                return ResidencyAdapterResult.Completed();
            }
        }

        private sealed class RecordingPins : IRegionResidencyPins
        {
            private readonly List<string> _events;

            public RecordingPins(List<string> events)
            {
                _events = events;
            }

            public RecordingLease AcquiredLease { get; private set; }

            public IRegionResidencyLease AcquireResidency(in RegionLoadRequest request)
            {
                AcquiredLease = new RecordingLease(request.RegionCoord, _events);
                return AcquiredLease;
            }

            public sealed class RecordingLease : IRegionResidencyLease
            {
                private readonly List<string> _events;

                public RecordingLease(int3 regionCoord, List<string> events)
                {
                    RegionCoord = regionCoord;
                    _events = events;
                }

                public int3 RegionCoord { get; }
                public bool IsReady => !Disposed;
                public bool Disposed { get; private set; }

                public void Dispose()
                {
                    if (Disposed) return;
                    Disposed = true;
                    _events.Add("pin-disposed");
                }
            }
        }
    }
}
