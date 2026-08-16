using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepPlanTests
    {
        [Test]
        public void KeepPlannerAggregatesFloorsCirculationAndTopologyAnnexesAcrossSeeds()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleKeepPlan keep = CastleKeepPlanner.Create(in dimensions, in topology);

                Assert.AreEqual(dimensions.Floors, keep.Floors.Length, $"seed {seed}: floor count");
                Assert.AreEqual(topology.KeepAnnexes.HasGreatHallWing, keep.Annexes.HasGreatHallWing,
                    $"seed {seed}: Great Hall wing drifted from topology");
                Assert.AreEqual(topology.KeepAnnexes.HasChapelWing, keep.Annexes.HasChapelWing,
                    $"seed {seed}: chapel wing drifted from topology");
                Assert.AreEqual(topology.KeepAnnexes.HasBellTower, keep.Annexes.HasBellTower,
                    $"seed {seed}: bell tower drifted from topology");
                Assert.AreEqual(
                    dimensions.Floors * dimensions.FloorHeight,
                    keep.Circulation.VerticalReach,
                    $"seed {seed}: vertical circulation reach");
                Assert.IsTrue(
                    CastleKeepPlanValidator.TryValidate(
                        in dimensions, keep, out CastleKeepPlanIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void KeepPlannerMatchesCanonicalInteriorCirculationAndTopologySemantics()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 311u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(311u);
            CastleKeepPlan aggregate = CastleKeepPlanner.Create(in dimensions, in topology);
            CastleKeepFloorPlan[] floors =
                CastleKeepInteriorPlanner.Create(in dimensions).SnapshotFloors();
            CastleKeepCirculationPlan circulation = CastleKeepCirculationPlanner.Create(in dimensions);
            CastleKeepAnnexPlan annexes = topology.KeepAnnexes;

            Assert.AreEqual(floors.Length, aggregate.Floors.Length);
            for (int i = 0; i < floors.Length; i++)
            {
                Assert.AreEqual(floors[i].FloorIndex, aggregate.Floors[i].FloorIndex);
                Assert.AreEqual(floors[i].Purpose, aggregate.Floors[i].Purpose);
                Assert.AreEqual(floors[i].HasPartition, aggregate.Floors[i].HasPartition);
                Assert.AreEqual(floors[i].SemanticSeed, aggregate.Floors[i].SemanticSeed);
            }

            Assert.AreEqual(circulation.EntranceCentre, aggregate.Circulation.EntranceCentre);
            Assert.AreEqual(circulation.GrandStairOrigin, aggregate.Circulation.GrandStairOrigin);
            Assert.AreEqual(circulation.SpiralStairCentre, aggregate.Circulation.SpiralStairCentre);
            Assert.AreEqual(annexes.HasGreatHallWing, aggregate.Annexes.HasGreatHallWing);
            Assert.AreEqual(annexes.HasChapelWing, aggregate.Annexes.HasChapelWing);
            Assert.AreEqual(annexes.HasBellTower, aggregate.Annexes.HasBellTower);
        }

        [Test]
        public void KeepPlannerPreservesCallerSuppliedTopologyAnnexSelection()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 401u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(401u);
            topology.HasKeepAnnexPlan = true;
            topology.KeepAnnexes = new CastleKeepAnnexPlan(
                hasGreatHallWing: true,
                hasChapelWing: false,
                hasBellTower: false);

            CastleKeepPlan keep = CastleKeepPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(keep.Annexes.HasGreatHallWing);
            Assert.IsFalse(keep.Annexes.HasChapelWing);
            Assert.IsFalse(keep.Annexes.HasBellTower);
        }

        [Test]
        public void KeepPlannerRejectsTopologyWithoutAnnexSemantics()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 409u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(409u);
            topology.HasKeepAnnexPlan = false;
            topology.KeepAnnexes = default;

            Assert.Throws<InvalidOperationException>(() =>
                CastleKeepPlanner.Create(in dimensions, in topology));
        }
    }
}
