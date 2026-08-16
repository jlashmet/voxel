using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCirculationPlannerTests
    {
        [Test]
        public void PlannerPreservesCurrentKeepCirculationAnchorsAcrossSeeds()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleKeepCirculationPlan circulation = CastleKeepCirculationPlanner.Create(in plan);

                Assert.AreEqual(new int2(0, -plan.KeepHalfZ), circulation.EntranceCentre,
                    $"seed {seed}: entrance drifted");
                Assert.AreEqual(new int2(-68, -plan.KeepHalfZ + 28), circulation.GrandStairOrigin,
                    $"seed {seed}: grand stair drifted");
                Assert.AreEqual(
                    new int2(-plan.KeepHalfX + 34, -plan.KeepHalfZ + 34),
                    circulation.SpiralStairCentre,
                    $"seed {seed}: spiral stair drifted");
                Assert.AreEqual(plan.Floors * plan.FloorHeight, circulation.VerticalReach,
                    $"seed {seed}: vertical reach drifted");
                Assert.IsTrue(
                    CastleKeepCirculationPlanner.TryValidate(
                        in plan, in circulation, out CastleKeepCirculationPlanIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void CompletedSpatialPlanCarriesValidatedKeepCirculation()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in plan, spatial);

                CastleKeepCirculationPlan circulation = completed.KeepCirculation;
                Assert.IsTrue(
                    CastleKeepCirculationPlanner.TryValidate(
                        in plan, in circulation, out CastleKeepCirculationPlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.AreEqual(
                    CastleAccessRoute.KeepEntrance(in plan, completed.KeepCentre),
                    completed.KeepCentre + circulation.EntranceCentre,
                    $"seed {seed}: internal entrance drifted from the gate-to-keep access route");
            }
        }

        [Test]
        public void ValidatorRejectsGrandStairOutsideKeepInterior()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 41u);
            CastleKeepCirculationPlan valid = CastleKeepCirculationPlanner.Create(in plan);
            var invalid = new CastleKeepCirculationPlan(
                valid.EntranceCentre,
                new int2(plan.KeepHalfX, valid.GrandStairOrigin.y),
                valid.GrandStairWidth,
                valid.GrandStairRise,
                valid.GrandStairRun,
                valid.SpiralStairCentre,
                valid.SpiralStairRadius,
                valid.VerticalReach);

            Assert.IsFalse(
                CastleKeepCirculationPlanner.TryValidate(
                    in plan, in invalid, out CastleKeepCirculationPlanIssue issue));
            Assert.AreEqual(CastleKeepCirculationPlanIssue.InvalidGrandStair, issue);
        }

        [Test]
        public void ValidatorRejectsSpiralStairOutsideKeepInterior()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 43u);
            CastleKeepCirculationPlan valid = CastleKeepCirculationPlanner.Create(in plan);
            var invalid = new CastleKeepCirculationPlan(
                valid.EntranceCentre,
                valid.GrandStairOrigin,
                valid.GrandStairWidth,
                valid.GrandStairRise,
                valid.GrandStairRun,
                new int2(plan.KeepHalfX, 0),
                valid.SpiralStairRadius,
                valid.VerticalReach);

            Assert.IsFalse(
                CastleKeepCirculationPlanner.TryValidate(
                    in plan, in invalid, out CastleKeepCirculationPlanIssue issue));
            Assert.AreEqual(CastleKeepCirculationPlanIssue.InvalidSpiralStair, issue);
        }
    }
}
