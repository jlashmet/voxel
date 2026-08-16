using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepPlanTests
    {
        [Test]
        public void KeepPlannerAggregatesFloorsCirculationAndAnnexesAcrossSeeds()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleKeepPlan keep = CastleKeepPlanner.Create(in dimensions);

                Assert.AreEqual(dimensions.Floors, keep.Floors.Length, $"seed {seed}: floor count");
                Assert.IsTrue(keep.Annexes.HasGreatHallWing, $"seed {seed}: Great Hall wing");
                Assert.IsTrue(keep.Annexes.HasChapelWing, $"seed {seed}: chapel wing");
                Assert.IsTrue(keep.Annexes.HasBellTower, $"seed {seed}: bell tower");
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
        public void KeepPlannerMatchesStandaloneSemanticPlanners()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 311u);
            CastleKeepPlan aggregate = CastleKeepPlanner.Create(in dimensions);
            CastleKeepFloorPlan[] floors = CastleKeepRoomPlanner.Create(in dimensions);
            CastleKeepCirculationPlan circulation = CastleKeepCirculationPlanner.Create(in dimensions);
            CastleKeepAnnexPlan annexes = CastleKeepAnnexPlanner.Create(in dimensions);

            Assert.AreEqual(floors.Length, aggregate.Floors.Length);
            for (int i = 0; i < floors.Length; i++)
            {
                Assert.AreEqual(floors[i].FloorIndex, aggregate.Floors[i].FloorIndex);
                Assert.AreEqual(floors[i].Purpose, aggregate.Floors[i].Purpose);
                Assert.AreEqual(floors[i].HasPartition, aggregate.Floors[i].HasPartition);
            }

            Assert.AreEqual(circulation.EntranceCentre, aggregate.Circulation.EntranceCentre);
            Assert.AreEqual(circulation.GrandStairOrigin, aggregate.Circulation.GrandStairOrigin);
            Assert.AreEqual(circulation.SpiralStairCentre, aggregate.Circulation.SpiralStairCentre);
            Assert.AreEqual(annexes.HasGreatHallWing, aggregate.Annexes.HasGreatHallWing);
            Assert.AreEqual(annexes.HasChapelWing, aggregate.Annexes.HasChapelWing);
            Assert.AreEqual(annexes.HasBellTower, aggregate.Annexes.HasBellTower);
        }
    }
}
