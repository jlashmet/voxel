using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepInteriorPlannerTests
    {
        [Test]
        public void PlannerPreservesExistingKeepFloorSemantics()
        {
            var plan = new CastlePlan
            {
                Floors = 6,
                Seed = 41u,
            };

            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in plan);

            Assert.AreEqual(6, interior.FloorCount);
            Assert.AreEqual(CastleKeepFloorPurpose.GreatHall, interior.Floor(0).Purpose);
            Assert.AreEqual(CastleKeepFloorPurpose.Bedchamber, interior.Floor(1).Purpose);
            Assert.IsFalse(interior.Floor(0).HasPartition);
            Assert.IsFalse(interior.Floor(1).HasPartition);

            for (int floor = 2; floor < interior.FloorCount; floor++)
            {
                Assert.AreEqual(floor, interior.Floor(floor).FloorIndex);
                Assert.AreEqual(
                    CastleKeepFloorPurpose.LibraryAndStores,
                    interior.Floor(floor).Purpose);
                Assert.IsTrue(interior.Floor(floor).HasPartition);
            }
        }

        [Test]
        public void RoomSemanticSeedsAreDeterministicAndPartitionedPerFloor()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                var plan = new CastlePlan
                {
                    Floors = 6,
                    Seed = seed,
                };

                CastleKeepInteriorPlan first = CastleKeepInteriorPlanner.Create(in plan);
                CastleKeepInteriorPlan second = CastleKeepInteriorPlanner.Create(in plan);

                for (int floor = 0; floor < plan.Floors; floor++)
                {
                    uint expected = CastleSeedPartition.Derive(
                        seed, CastleSeedDomain.Rooms, (uint)floor);
                    Assert.AreEqual(expected, first.Floor(floor).SemanticSeed);
                    Assert.AreEqual(first.Floor(floor).SemanticSeed, second.Floor(floor).SemanticSeed);

                    for (int previous = 0; previous < floor; previous++)
                    {
                        Assert.AreNotEqual(
                            first.Floor(previous).SemanticSeed,
                            first.Floor(floor).SemanticSeed,
                            $"seed {seed}: floors {previous} and {floor} share a room seed");
                    }
                }
            }
        }

        [Test]
        public void SnapshotCannotMutateInteriorPlan()
        {
            var plan = new CastlePlan { Floors = 5, Seed = 9u };
            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in plan);
            CastleKeepFloorPlan[] snapshot = interior.SnapshotFloors();

            snapshot[0] = new CastleKeepFloorPlan(
                0, CastleKeepFloorPurpose.LibraryAndStores, true, 1u);

            Assert.AreEqual(CastleKeepFloorPurpose.GreatHall, interior.Floor(0).Purpose);
            Assert.IsFalse(interior.Floor(0).HasPartition);
        }
    }
}
