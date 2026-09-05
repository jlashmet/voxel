using Game.Residency.Api;
using Game.Residency.Runtime;
using NUnit.Framework;

namespace Game.Residency.Tests
{
    public sealed class ProximityResidencyDemandSourceTests
    {
        [Test]
        public void BoundaryOscillationWithinHysteresisDoesNotThrashDetailedFidelity()
        {
            using var coordinator = new GameplayResidencyCoordinator(null);
            using var proximity = new ProximityResidencyDemandSource(
                coordinator,
                new ResidencyProximityPolicy(100, 125, 250, 300),
                "player-1");
            ResidencyTarget target = new ResidencyTarget(ResidencyTargetKind.Character, "npc:boundary");

            Assert.AreEqual(ResidencyFidelity.Detailed, proximity.Update(target, 99));
            coordinator.Reconcile();
            int initialTransitions = coordinator.GetDiagnostics().TransitionHistory.Count;
            Assert.AreEqual(2, initialTransitions);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(ResidencyFidelity.Detailed, proximity.Update(target, (i & 1) == 0 ? 101 : 99));
                coordinator.Reconcile();
            }

            Assert.AreEqual(initialTransitions, coordinator.GetDiagnostics().TransitionHistory.Count,
                "Crossing the enter threshold repeatedly while still inside the exit band must not promote/demote churn.");
            Assert.AreEqual(ResidencyFidelity.Coarse, proximity.Update(target, 126));
            coordinator.Reconcile();
            Assert.IsTrue(coordinator.TryGetState(target, out ResidencyTargetSnapshot state));
            Assert.AreEqual(ResidencyFidelity.Coarse, state.Current);
        }

        [Test]
        public void ExplicitDetailedPinBypassesProximityHysteresisAndReleasesIndependently()
        {
            using var coordinator = new GameplayResidencyCoordinator(null);
            using var proximity = new ProximityResidencyDemandSource(
                coordinator,
                new ResidencyProximityPolicy(100, 125, 250, 300),
                "player-1");
            ResidencyTarget target = new ResidencyTarget(ResidencyTargetKind.Character, "npc:important");

            proximity.Update(target, 220);
            coordinator.Reconcile();
            AssertCurrent(coordinator, target, ResidencyFidelity.Coarse);

            IResidencyDemandLease control = coordinator.Acquire(new ResidencyDemandRequest(
                target, ResidencyFidelity.Detailed, "control:player-1", "Control", "explicit control"));
            coordinator.Reconcile();
            AssertCurrent(coordinator, target, ResidencyFidelity.Detailed);

            proximity.Update(target, 400);
            coordinator.Reconcile();
            AssertCurrent(coordinator, target, ResidencyFidelity.Detailed);

            control.Dispose();
            coordinator.Reconcile();
            AssertCurrent(coordinator, target, ResidencyFidelity.Dormant);
        }

        private static void AssertCurrent(
            IGameplayResidencyCoordinator coordinator,
            ResidencyTarget target,
            ResidencyFidelity expected)
        {
            Assert.IsTrue(coordinator.TryGetState(target, out ResidencyTargetSnapshot state));
            Assert.AreEqual(expected, state.Current);
        }
    }
}
