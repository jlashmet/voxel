using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuildBoundsTests
    {
        [Test]
        public void SpatialBoundsCoverUpperStructureApproachAndDungeonEnvelope()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                if (spatial.KeepRequiresTerrainResolution)
                {
                    spatial = CastleSpatialPlanner.ResolveHighestGroundKeep(
                        in plan, spatial, int2.zero);
                }

                CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
                CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
                int baseY = plan.Centre.y + plan.PlateauHeight;

                Assert.Greater(bounds.MaxExclusive.y, 512,
                    $"seed {seed}: upper castle must reserve the Y=1 voxel region");
                Assert.IsTrue(bounds.Contains(projection.TrapdoorCentre),
                    $"seed {seed}: trapdoor escaped build bounds");

                int currentSiteReach = plan.PlateauRadius + plan.CliffDrop - 8;
                int currentGorgeOutward = plan.WallThickness + 92 + 11 + 90;
                int2 farApproachLocal = projection.Approach.LocalPoint(
                    currentSiteReach, currentGorgeOutward);
                var farApproach = new int3(
                    plan.Centre.x + farApproachLocal.x,
                    baseY - CastleLayout.LowerRiverDepth,
                    plan.Centre.z + farApproachLocal.y);
                Assert.IsTrue(bounds.Contains(farApproach),
                    $"seed {seed}: gate-oriented gorge escaped build bounds");

                // Keep compatibility details retain a broad historical envelope. Planned dungeon
                // rooms/caves are additionally included from their actual semantic coordinates.
                var farDungeon = new int3(
                    projection.KeepCentreWorld.x + 276,
                    baseY - 178,
                    projection.KeepCentreWorld.y - 505);
                Assert.IsTrue(bounds.Contains(farDungeon),
                    $"seed {seed}: far dungeon/cave escaped build bounds");

                for (int vertex = 0; vertex < spatial.OuterWardVertices.Length; vertex++)
                {
                    int2 local = spatial.OuterWardVertices[vertex];
                    var world = new int3(
                        plan.Centre.x + local.x,
                        baseY,
                        plan.Centre.z + local.y);
                    Assert.IsTrue(bounds.Contains(world),
                        $"seed {seed}: perimeter vertex {vertex} escaped build bounds");
                }
            }
        }

        [Test]
        public void PlannedForwardCaveExitExtendsDependencyEnvelopePastThreshold()
        {
            bool foundForwardExit = false;

            for (uint seed = 1; seed <= 512 && !foundForwardExit; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                if (spatial.KeepRequiresTerrainResolution)
                {
                    spatial = CastleSpatialPlanner.ResolveHighestGroundKeep(
                        in plan, spatial, int2.zero);
                }

                DungeonPlan dungeon = spatial.Dungeon ?? CastleDungeonPlanning.Create(in plan, spatial);
                if (!dungeon.HasCaveExit) continue;

                DungeonRoomPlan threshold = dungeon.Rooms[dungeon.CaveThresholdRoomId];
                DungeonRoomPlan hall = default;
                bool foundHall = false;
                for (int room = 0; room < dungeon.Rooms.Length; room++)
                {
                    if (dungeon.Rooms[room].Purpose != DungeonRoomPurpose.GreatHall) continue;
                    hall = dungeon.Rooms[room];
                    foundHall = true;
                    break;
                }

                Assert.IsTrue(foundHall, $"seed {seed}: dungeon has no great hall");
                if (threshold.Centre.z <= hall.Centre.z) continue;

                foundForwardExit = true;
                CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
                int caveFloorY = threshold.Centre.y - threshold.Size.y / 2;
                var forwardCaveDetail = new int3(
                    threshold.Centre.x + 240,
                    caveFloorY + 100,
                    threshold.Centre.z + 240);

                Assert.IsTrue(bounds.Contains(forwardCaveDetail),
                    $"seed {seed}: +Z cave detail escaped planned dungeon dependency bounds");
                Assert.Greater(bounds.MaxExclusive.z, threshold.Centre.z + 256,
                    $"seed {seed}: dependency envelope did not reserve cave margin past threshold");
            }

            Assert.IsTrue(foundForwardExit,
                "Expected the dungeon seed stream to produce at least one +Z cave exit.");
        }

        [Test]
        public void SpatialBoundsRemainConservativeBelowWorldZero()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(-180, -140, 95), 131u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(131u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
            int baseY = plan.Centre.y + plan.PlateauHeight;

            var caveFloor = new int3(
                projection.KeepCentreWorld.x,
                baseY - 178,
                projection.KeepCentreWorld.y - 371);

            Assert.Less(bounds.Min.y, 0,
                "Signed voxel worlds must not clamp castle dependency bounds to Y=0.");
            Assert.IsTrue(bounds.Contains(caveFloor),
                "The underground castle envelope must remain valid at negative world Y.");
        }
    }
}
