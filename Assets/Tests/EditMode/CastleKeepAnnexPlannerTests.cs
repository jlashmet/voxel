using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepAnnexPlannerTests
    {
        [Test]
        public void CompatibilityPlannerPreservesCurrentAnnexRecipeAcrossSeeds()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleKeepAnnexPlan annexes = CastleKeepAnnexPlanner.Create(in plan);

                Assert.IsTrue(annexes.HasGreatHallWing, $"seed {seed}: missing Great Hall wing");
                Assert.IsTrue(annexes.HasChapelWing, $"seed {seed}: missing chapel wing");
                Assert.IsTrue(annexes.HasBellTower, $"seed {seed}: missing chapel bell tower");
                Assert.IsTrue(annexes.HasRearOriel, $"seed {seed}: missing rear timber oriel");
                Assert.IsTrue(
                    CastleKeepAnnexPlanValidator.TryValidate(
                        in annexes, out CastleKeepAnnexPlanIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void SeededTopologyPlannerIsDeterministicAndVariesAnnexes()
        {
            bool sawHall = false, sawNoHall = false;
            bool sawChapel = false, sawNoChapel = false;
            bool sawOriel = false, sawNoOriel = false;
            bool sawBell = false, sawNoBell = false;

            for (uint seed = 1; seed <= 512; seed++)
            {
                CastleKeepAnnexPlan first = CastleKeepAnnexPlanner.Create(seed);
                CastleKeepAnnexPlan second = CastleKeepAnnexPlanner.Create(seed);

                Assert.AreEqual(first.HasGreatHallWing, second.HasGreatHallWing, $"seed {seed}: hall drift");
                Assert.AreEqual(first.HasChapelWing, second.HasChapelWing, $"seed {seed}: chapel drift");
                Assert.AreEqual(first.HasBellTower, second.HasBellTower, $"seed {seed}: bell drift");
                Assert.AreEqual(first.HasRearOriel, second.HasRearOriel, $"seed {seed}: oriel drift");
                Assert.IsTrue(
                    first.HasGreatHallWing || first.HasChapelWing || first.HasRearOriel,
                    $"seed {seed}: keep lost every annex");
                Assert.IsTrue(
                    CastleKeepAnnexPlanValidator.TryValidate(
                        in first, out CastleKeepAnnexPlanIssue issue),
                    $"seed {seed}: {issue}");

                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                Assert.AreEqual(first.HasGreatHallWing, topology.KeepAnnexes.HasGreatHallWing);
                Assert.AreEqual(first.HasChapelWing, topology.KeepAnnexes.HasChapelWing);
                Assert.AreEqual(first.HasBellTower, topology.KeepAnnexes.HasBellTower);
                Assert.AreEqual(first.HasRearOriel, topology.KeepAnnexes.HasRearOriel);

                sawHall |= first.HasGreatHallWing;
                sawNoHall |= !first.HasGreatHallWing;
                sawChapel |= first.HasChapelWing;
                sawNoChapel |= !first.HasChapelWing;
                sawOriel |= first.HasRearOriel;
                sawNoOriel |= !first.HasRearOriel;
                sawBell |= first.HasBellTower;
                sawNoBell |= !first.HasBellTower;
            }

            Assert.IsTrue(sawHall && sawNoHall, "Seeded planner never varied Great Hall wings.");
            Assert.IsTrue(sawChapel && sawNoChapel, "Seeded planner never varied chapel wings.");
            Assert.IsTrue(sawOriel && sawNoOriel, "Seeded planner never varied rear oriels.");
            Assert.IsTrue(sawBell && sawNoBell, "Seeded planner never varied bell towers.");
        }

        [Test]
        public void ValidatorRejectsBellTowerWithoutChapel()
        {
            var annexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: false,
                hasBellTower: true);

            Assert.IsFalse(
                CastleKeepAnnexPlanValidator.TryValidate(
                    in annexes, out CastleKeepAnnexPlanIssue issue));
            Assert.AreEqual(CastleKeepAnnexPlanIssue.BellTowerWithoutChapel, issue);
        }

        [Test]
        public void PlannerRejectsInvalidKeepDimensions()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 77u);
            plan.KeepHalfX = 0;

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                CastleKeepAnnexPlanner.Create(in plan));
        }
    }
}
