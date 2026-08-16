using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleDungeonPlannerTests
    {
        [Test]
        public void CastleAdapterPreservesDesignedDungeonEnvelope()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialProjection projection = CastleSpatialProjection.Create(
                    in dimensions, spatial);

                DungeonPlan first = CastleDungeonPlanner.Create(in dimensions, spatial);
                DungeonPlan second = CastleDungeonPlanner.Create(in dimensions, spatial);

                Assert.AreEqual(projection.TrapdoorCentre, first.Entrance,
                    $"seed {seed}: dungeon entrance drifted from trapdoor");
                Assert.AreEqual(first.Seed, second.Seed, $"seed {seed}: dungeon seed drifted");
                Assert.AreEqual(first.Rooms.Length, second.Rooms.Length,
                    $"seed {seed}: room count changed between identical planning passes");
                Assert.AreEqual(first.Connections.Length, second.Connections.Length,
                    $"seed {seed}: graph changed between identical planning passes");
                Assert.IsTrue(DungeonPlanValidator.TryValidate(first, out DungeonPlanIssue issue),
                    $"seed {seed}: {issue}");

                DungeonRoomPlan archive = Find(first, DungeonRoomPurpose.Archive);
                DungeonRoomPlan hall = Find(first, DungeonRoomPurpose.GreatHall);
                DungeonRoomPlan puzzle = Find(first, DungeonRoomPurpose.Puzzle);
                DungeonRoomPlan treasury = Find(first, DungeonRoomPurpose.Treasury);
                DungeonRoomPlan cave = Find(first, DungeonRoomPurpose.CaveThreshold);

                Assert.AreEqual(first.Entrance.x, archive.Centre.x);
                Assert.AreEqual(first.Entrance.z, archive.Centre.z);
                Assert.AreEqual(first.Entrance.y - 26, archive.Centre.y,
                    $"seed {seed}: archive level no longer matches the legacy cellar envelope");

                Assert.AreEqual(first.Entrance.x, hall.Centre.x);
                Assert.AreEqual(first.Entrance.z, hall.Centre.z);
                Assert.AreEqual(first.Entrance.y - 146, hall.Centre.y,
                    $"seed {seed}: main hall level no longer matches the legacy dungeon envelope");
                Assert.AreEqual(new int3(260, 40, 180), hall.Size);

                Assert.AreEqual(226, math.abs(puzzle.Centre.x - hall.Centre.x),
                    $"seed {seed}: puzzle chamber lateral offset drifted");
                Assert.AreEqual(226, math.abs(treasury.Centre.x - hall.Centre.x),
                    $"seed {seed}: treasury chamber lateral offset drifted");
                Assert.AreEqual(-(puzzle.Centre.x - hall.Centre.x),
                                treasury.Centre.x - hall.Centre.x,
                    $"seed {seed}: side chambers are no longer opposite the hall");
                Assert.AreEqual(411, math.abs(cave.Centre.z - hall.Centre.z),
                    $"seed {seed}: cave threshold distance drifted");
            }
        }

        [Test]
        public void CastleAdapterRejectsUnresolvedHighestGroundKeep()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 701u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(701u);
            topology.KeepPlacement = CastleKeepPlacement.HighestGround;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(spatial.KeepRequiresTerrainResolution);
            Assert.Throws<InvalidOperationException>(() =>
                CastleDungeonPlanner.Create(dimensions, spatial));
        }

        private static DungeonRoomPlan Find(DungeonPlan plan, DungeonRoomPurpose purpose)
        {
            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                if (plan.Rooms[i].Purpose == purpose)
                    return plan.Rooms[i];
            }

            Assert.Fail($"Dungeon plan did not contain {purpose}.");
            return default;
        }
    }
}
