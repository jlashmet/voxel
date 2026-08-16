using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepRoomAccentPlanTests
    {
        [Test]
        public void AggregateMatchesPerFloorAccentPlannerAcrossSeeds()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in dimensions);
                CastleKeepRoomAccentPlan aggregate = CastleKeepRoomAccentPlanner.Create(
                    in dimensions, interior);

                Assert.AreEqual(dimensions.Floors, aggregate.FloorCount,
                    $"seed {seed}: accent stack did not cover every keep floor");
                Assert.IsTrue(
                    CastleKeepRoomAccentPlanValidator.TryValidate(
                        in dimensions, aggregate, out CastleKeepRoomAccentPlanIssue issue),
                    $"seed {seed}: aggregate invalid: {issue}");

                for (int floor = 0; floor < aggregate.FloorCount; floor++)
                {
                    CastleKeepFloorPlan floorPlan = interior.Floor(floor);
                    CastleRoomAccentPlan direct = CastleRoomAccentPlanner.Create(
                        in dimensions, in floorPlan);
                    CastleRoomAccentPlan planned = aggregate.Floor(floor);
                    Assert.AreEqual(direct.Count, planned.Count,
                        $"seed {seed}, floor {floor}: accent count drifted");

                    for (int accent = 0; accent < direct.Count; accent++)
                    {
                        CastleRoomAccentSpec expected = direct.AccentAt(accent);
                        CastleRoomAccentSpec actual = planned.AccentAt(accent);
                        Assert.AreEqual(expected.Id, actual.Id);
                        Assert.AreEqual(expected.LocalX, actual.LocalX);
                        Assert.AreEqual(expected.LocalZ, actual.LocalZ);
                        Assert.AreEqual(expected.Radius, actual.Radius);
                        Assert.AreEqual(expected.Height, actual.Height);
                    }
                }
            }
        }

        [Test]
        public void SnapshotCannotReplaceAggregateFloorPlans()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 41u);
            CastleKeepInteriorPlan interior = CastleKeepInteriorPlanner.Create(in dimensions);
            CastleKeepRoomAccentPlan aggregate = CastleKeepRoomAccentPlanner.Create(
                in dimensions, interior);

            CastleRoomAccentPlan first = aggregate.Floor(0);
            CastleRoomAccentPlan[] snapshot = aggregate.SnapshotFloors();
            snapshot[0] = null;

            Assert.AreSame(first, aggregate.Floor(0));
            Assert.IsTrue(
                CastleKeepRoomAccentPlanValidator.TryValidate(
                    in dimensions, aggregate, out CastleKeepRoomAccentPlanIssue issue),
                issue.ToString());
        }
    }
}
