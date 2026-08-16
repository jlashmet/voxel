using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepRoomDecorationPlanningTests
    {
        [Test]
        public void PlannerIsDeterministicValidAndBoundedAcrossSeeds()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleKeepFloorPlan[] floors =
                    CastleKeepInteriorPlanner.Create(in plan).SnapshotFloors();

                for (int floorIndex = 0; floorIndex < floors.Length; floorIndex++)
                {
                    CastleKeepFloorPlan floor = floors[floorIndex];
                    CastleKeepRoomDecorationPlan first =
                        CastleKeepRoomDecorationPlanner.Create(in plan, in floor);
                    CastleKeepRoomDecorationPlan second =
                        CastleKeepRoomDecorationPlanner.Create(in plan, in floor);

                    Assert.IsTrue(
                        CastleKeepRoomDecorationPlanner.TryValidate(
                            in plan, in floor, in first, out string error),
                        $"seed {seed}, floor {floorIndex}: {error}");
                    Assert.AreEqual(first.Count, second.Count);
                    Assert.That(first.Count, Is.InRange(2, 4));

                    for (int i = 0; i < first.Count; i++)
                    {
                        CastleKeepRoomDecorationSpec a = first.Decoration(i);
                        CastleKeepRoomDecorationSpec b = second.Decoration(i);
                        Assert.AreEqual(a.Id, b.Id);
                        Assert.AreEqual(a.LocalCentre, b.LocalCentre);
                        Assert.AreEqual(a.Radius, b.Radius);
                        Assert.AreEqual(a.Height, b.Height);
                    }
                }
            }
        }

        [Test]
        public void PlannerPreservesLegacyRandomDrawOrder()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 67u);
            CastleKeepFloorPlan[] floors =
                CastleKeepInteriorPlanner.Create(in plan).SnapshotFloors();

            for (int floorIndex = 0; floorIndex < floors.Length; floorIndex++)
            {
                CastleKeepFloorPlan floor = floors[floorIndex];
                CastleKeepRoomDecorationPlan planned =
                    CastleKeepRoomDecorationPlanner.Create(in plan, in floor);
                var rng = new Random(floor.SemanticSeed);
                int expectedCount = 0;

                for (int i = 0; i < rng.NextInt(2, 5); i++)
                {
                    bool leftWall = rng.NextBool();
                    int expectedX = leftWall
                        ? -plan.KeepHalfX + 30
                        : plan.KeepHalfX - 38;
                    int expectedZ = rng.NextInt(-plan.KeepHalfZ + 16, plan.KeepHalfZ - 20);
                    int expectedRadius = rng.NextInt(4, 7);
                    int expectedHeight = rng.NextInt(8, 14);

                    CastleKeepRoomDecorationSpec decoration = planned.Decoration(expectedCount);
                    Assert.AreEqual(new int2(expectedX, expectedZ), decoration.LocalCentre,
                        $"floor {floorIndex}, decoration {expectedCount}: position");
                    Assert.AreEqual(expectedRadius, decoration.Radius,
                        $"floor {floorIndex}, decoration {expectedCount}: radius");
                    Assert.AreEqual(expectedHeight, decoration.Height,
                        $"floor {floorIndex}, decoration {expectedCount}: height");
                    expectedCount++;
                }

                Assert.AreEqual(expectedCount, planned.Count,
                    $"floor {floorIndex}: legacy loop count changed");
            }
        }
    }
}
