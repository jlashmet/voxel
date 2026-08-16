using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRoomClutterPlannerTests
    {
        [Test]
        public void PlannerPreservesLegacyPedestalDrawOrder()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleKeepFloorPlan[] floors =
                    CastleKeepInteriorPlanner.Create(in plan).SnapshotFloors();
                CastleRoomClutterPlan clutter = CastleRoomClutterPlanner.Create(in plan, floors);

                Assert.IsTrue(
                    CastleRoomClutterPlanner.TryValidate(
                        in plan, floors, clutter, out CastleRoomClutterPlanIssue issue),
                    $"seed {seed}: {issue}");

                int cursor = 0;
                for (int floor = 0; floor < floors.Length; floor++)
                {
                    var rng = new Random(floors[floor].SemanticSeed);
                    int floorItems = 0;

                    // This intentionally mirrors the legacy for-condition RNG draw. The migration
                    // must preserve that unusual order rather than 'fixing' it and changing seeds.
                    for (int item = 0; item < rng.NextInt(2, 5); item++)
                    {
                        bool leftWall = rng.NextBool();
                        int expectedX = leftWall
                            ? -plan.KeepHalfX + 30
                            : plan.KeepHalfX - 38;
                        int expectedZ = rng.NextInt(
                            -plan.KeepHalfZ + 16,
                            plan.KeepHalfZ - 20);
                        int expectedRadius = rng.NextInt(4, 7);
                        int expectedHeight = rng.NextInt(8, 14);

                        Assert.Less(cursor, clutter.Count, $"seed {seed}, floor {floor}");
                        CastleRoomClutterSpec planned = clutter.Item(cursor);
                        Assert.AreEqual(cursor, planned.Id);
                        Assert.AreEqual(floor, planned.FloorIndex);
                        Assert.AreEqual(new int2(expectedX, expectedZ), planned.LocalCentre);
                        Assert.AreEqual(expectedRadius, planned.Radius);
                        Assert.AreEqual(expectedHeight, planned.Height);

                        cursor++;
                        floorItems++;
                    }

                    Assert.That(floorItems, Is.InRange(2, 4),
                        $"seed {seed}, floor {floor}: legacy loop should produce 2-4 items");
                }

                Assert.AreEqual(cursor, clutter.Count, $"seed {seed}: unexpected extra clutter");
            }
        }

        [Test]
        public void PlannerIsDeterministicForSameFloorSeeds()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 0xC1A77E2u);
            CastleKeepFloorPlan[] floors =
                CastleKeepInteriorPlanner.Create(in plan).SnapshotFloors();

            CastleRoomClutterPlan first = CastleRoomClutterPlanner.Create(in plan, floors);
            CastleRoomClutterPlan second = CastleRoomClutterPlanner.Create(in plan, floors);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                CastleRoomClutterSpec a = first.Item(i);
                CastleRoomClutterSpec b = second.Item(i);
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.FloorIndex, b.FloorIndex);
                Assert.AreEqual(a.LocalCentre, b.LocalCentre);
                Assert.AreEqual(a.Radius, b.Radius);
                Assert.AreEqual(a.Height, b.Height);
            }
        }
    }
}
