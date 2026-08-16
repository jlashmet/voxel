using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepInteriorPlannerTests
    {
        [Test]
        public void PlannerKeepsAnchorFloorsAndUsesSupportedUpperFloorSemantics()
        {
            CastlePlan plan = CreatePlan(41u, 6);

            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in plan);

            Assert.AreEqual(6, interior.FloorCount);
            Assert.AreEqual(CastleKeepFloorPurpose.GreatHall, interior.Floor(0).Purpose);
            Assert.AreEqual(CastleKeepFloorPurpose.Bedchamber, interior.Floor(1).Purpose);
            Assert.IsFalse(interior.Floor(0).HasPartition);
            Assert.IsFalse(interior.Floor(1).HasPartition);

            for (int floor = 2; floor < interior.FloorCount - 1; floor++)
            {
                CastleKeepFloorPlan planned = interior.Floor(floor);
                Assert.AreEqual(floor, planned.FloorIndex);
                Assert.IsTrue(
                    planned.Purpose == CastleKeepFloorPurpose.Bedchamber ||
                    planned.Purpose == CastleKeepFloorPurpose.LibraryAndStores,
                    $"floor {floor}: unsupported purpose {planned.Purpose}");
                Assert.AreEqual(
                    planned.Purpose == CastleKeepFloorPurpose.LibraryAndStores,
                    planned.HasPartition,
                    $"floor {floor}: partition does not match purpose {planned.Purpose}");
            }

            CastleKeepFloorPlan top = interior.Floor(interior.FloorCount - 1);
            Assert.AreEqual(CastleKeepFloorPurpose.LibraryAndStores, top.Purpose);
            Assert.IsTrue(top.HasPartition);
            Assert.IsTrue(
                CastleKeepFloorPlanValidator.TryValidate(
                    in plan,
                    interior.SnapshotFloors(),
                    out CastleKeepFloorPlanIssue issue),
                issue.ToString());
        }

        [Test]
        public void IntermediateUpperFloorsVaryDeterministicallyAcrossSeeds()
        {
            bool sawBedchamber = false;
            bool sawLibrary = false;

            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan plan = CreatePlan(seed, 6);
                CastleKeepInteriorPlan first = CastleKeepInteriorPlanner.Create(in plan);
                CastleKeepInteriorPlan second = CastleKeepInteriorPlanner.Create(in plan);

                for (int floor = 2; floor < plan.Floors - 1; floor++)
                {
                    CastleKeepFloorPlan a = first.Floor(floor);
                    CastleKeepFloorPlan b = second.Floor(floor);
                    Assert.AreEqual(a.Purpose, b.Purpose,
                        $"seed {seed}, floor {floor}: room purpose was not deterministic");
                    Assert.AreEqual(a.HasPartition, b.HasPartition,
                        $"seed {seed}, floor {floor}: partition was not deterministic");

                    sawBedchamber |= a.Purpose == CastleKeepFloorPurpose.Bedchamber;
                    sawLibrary |= a.Purpose == CastleKeepFloorPurpose.LibraryAndStores;
                }
            }

            Assert.IsTrue(sawBedchamber,
                "Intermediate upper floors never selected the bedchamber recipe.");
            Assert.IsTrue(sawLibrary,
                "Intermediate upper floors never selected the library/store recipe.");
        }

        [Test]
        public void RoomSemanticSeedsAreDeterministicAndPartitionedPerFloor()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CreatePlan(seed, 6);

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
        public void ValidatorRejectsPurposePartitionMismatchOnVariableUpperFloor()
        {
            CastlePlan plan = CreatePlan(73u, 6);
            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in plan);
            CastleKeepFloorPlan[] floors = interior.SnapshotFloors();
            CastleKeepFloorPlan original = floors[2];
            floors[2] = new CastleKeepFloorPlan(
                original.FloorIndex,
                original.Purpose,
                !original.HasPartition,
                original.SemanticSeed,
                original.Accents);

            Assert.IsFalse(
                CastleKeepFloorPlanValidator.TryValidate(
                    in plan, floors, out CastleKeepFloorPlanIssue issue));
            Assert.AreEqual(CastleKeepFloorPlanIssue.PartitionMismatch, issue);
        }

        [Test]
        public void SnapshotCannotMutateInteriorPlan()
        {
            CastlePlan plan = CreatePlan(9u, 5);
            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in plan);
            CastleKeepFloorPlan[] snapshot = interior.SnapshotFloors();

            snapshot[0] = new CastleKeepFloorPlan(
                0, CastleKeepFloorPurpose.LibraryAndStores, true, 1u);

            Assert.AreEqual(CastleKeepFloorPurpose.GreatHall, interior.Floor(0).Purpose);
            Assert.IsFalse(interior.Floor(0).HasPartition);
        }

        private static CastlePlan CreatePlan(uint seed, int floors)
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
            plan.Floors = floors;
            plan.KeepHeight = floors * plan.FloorHeight;
            return plan;
        }
    }
}
