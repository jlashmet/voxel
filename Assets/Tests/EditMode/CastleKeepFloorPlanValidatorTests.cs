using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepFloorPlanValidatorTests
    {
        [Test]
        public void PlannerProducesValidKeepFloorSemanticsAcrossSeeds()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleKeepFloorPlan[] floors = CastleKeepRoomPlanner.Create(in plan);

                Assert.IsTrue(
                    CastleKeepFloorPlanValidator.TryValidate(
                        in plan, floors, out CastleKeepFloorPlanIssue issue),
                    $"seed {seed}: {issue}");
                Assert.AreEqual(plan.Floors, floors.Length);
                Assert.AreEqual(CastleKeepFloorPurpose.GreatHall, floors[0].Purpose);
                Assert.AreEqual(CastleKeepFloorPurpose.Bedchamber, floors[1].Purpose);
                for (int floor = 2; floor < floors.Length; floor++)
                {
                    Assert.AreEqual(CastleKeepFloorPurpose.LibraryAndStores, floors[floor].Purpose);
                    Assert.IsTrue(floors[floor].HasPartition);
                }
            }
        }

        [Test]
        public void ValidatorRejectsIncompleteFloorStack()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 41u);
            CastleKeepFloorPlan[] planned = CastleKeepRoomPlanner.Create(in plan);
            var truncated = new CastleKeepFloorPlan[planned.Length - 1];
            for (int i = 0; i < truncated.Length; i++)
                truncated[i] = planned[i];

            Assert.IsFalse(
                CastleKeepFloorPlanValidator.TryValidate(
                    in plan, truncated, out CastleKeepFloorPlanIssue issue));
            Assert.AreEqual(CastleKeepFloorPlanIssue.FloorCountMismatch, issue);
        }

        [Test]
        public void ValidatorRejectsMissingFloorPlan()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 57u);

            Assert.IsFalse(
                CastleKeepFloorPlanValidator.TryValidate(
                    in plan, null, out CastleKeepFloorPlanIssue issue));
            Assert.AreEqual(CastleKeepFloorPlanIssue.MissingFloors, issue);
        }

        [Test]
        public void ValidatorRejectsMissingPlannedRoomAccents()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 63u);
            CastleKeepFloorPlan[] planned = CastleKeepRoomPlanner.Create(in plan);
            CastleKeepFloorPlan first = planned[0];
            planned[0] = new CastleKeepFloorPlan(
                first.FloorIndex,
                first.Purpose,
                first.HasPartition,
                first.SemanticSeed);

            Assert.IsFalse(
                CastleKeepFloorPlanValidator.TryValidate(
                    in plan, planned, out CastleKeepFloorPlanIssue issue));
            Assert.AreEqual(CastleKeepFloorPlanIssue.MissingAccentPlan, issue);
        }
    }
}
