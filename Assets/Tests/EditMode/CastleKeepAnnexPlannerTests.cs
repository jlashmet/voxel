using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepAnnexPlannerTests
    {
        [Test]
        public void PlannerPreservesCurrentAnnexRecipeAcrossSeeds()
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
