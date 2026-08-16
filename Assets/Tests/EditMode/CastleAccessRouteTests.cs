using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleAccessRouteTests
    {
        [Test]
        public void RouteConnectsPrimaryGateThroughInnerGateToPhysicalKeepEntrance()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                if (topology.KeepPlacement == CastleKeepPlacement.HighestGround)
                    topology.KeepPlacement = CastleKeepPlacement.Central;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(
                    in dimensions, in topology);
                CastleAccessRoute route = CastleAccessRoute.Create(in dimensions, spatial);

                Assert.AreEqual(spatial.HasInnerGate ? 3 : 2, route.WaypointCount,
                    $"seed {seed}: unexpected route waypoint count");
                Assert.AreEqual(spatial.PrimaryGate.Centre, route.Waypoint(0),
                    $"seed {seed}: route did not start at primary gate");

                if (spatial.HasInnerGate)
                {
                    Assert.AreEqual(spatial.InnerGate.Centre, route.Waypoint(1),
                        $"seed {seed}: nested ward route skipped inner gate");
                }

                Assert.AreEqual(
                    CastleAccessRoute.KeepEntrance(in dimensions, spatial.KeepCentre),
                    route.Waypoint(route.WaypointCount - 1),
                    $"seed {seed}: route did not end at physical keep entrance");

                Assert.IsFalse(route.ClearsPoint(route.PrimaryGateCentre, 0f),
                    $"seed {seed}: route did not reserve its own gate corridor");
                Assert.IsTrue(route.ClearsPoint(new int2(10_000, 10_000), 0f),
                    $"seed {seed}: distant point was incorrectly treated as route obstruction");
            }
        }

        [Test]
        public void BuildingClearanceRejectsFootprintsCrossingRouteCorridor()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 331u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(331u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.Wards = CastleWardPattern.SingleWard;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleAccessRoute route = CastleAccessRoute.Create(in dimensions, spatial);

            int2 a = route.Waypoint(0);
            int2 b = route.Waypoint(1);
            var crossing = new CastleCourtyardBuildingSpec
            {
                Centre = new int2((a.x + b.x) / 2, (a.y + b.y) / 2),
                Tangent = new float2(1f, 0f),
                Inward = new float2(0f, 1f),
                Width = 40,
                Depth = 40,
                Height = 30,
            };
            Assert.IsFalse(route.ClearsBuilding(in crossing),
                "Building centred on the gate-to-keep route should be rejected.");

            var distant = crossing;
            distant.Centre = new int2(10_000, 10_000);
            Assert.IsTrue(route.ClearsBuilding(in distant),
                "Distant building should not be rejected by access-route clearance.");
        }

        [Test]
        public void PlannedCourtyardObstaclesLeaveAccessCorridorClear()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                if (topology.KeepPlacement == CastleKeepPlacement.HighestGround)
                    topology.KeepPlacement = CastleKeepPlacement.Central;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(
                    in dimensions, in topology);
                CastleAccessRoute route = CastleAccessRoute.Create(in dimensions, spatial);

                Assert.IsTrue(spatial.HasWell,
                    $"seed {seed}: resolved castle unexpectedly omitted its well");
                Assert.IsTrue(route.ClearsPoint(spatial.WellCentre, 20f),
                    $"seed {seed}: planned well blocks primary access");

                CastleCourtyardBuildingSpec[] buildings = spatial.CourtyardBuildings;
                Assert.NotNull(buildings, $"seed {seed}: courtyard building plan is null");
                for (int i = 0; i < buildings.Length; i++)
                {
                    Assert.IsTrue(route.ClearsBuilding(in buildings[i]),
                        $"seed {seed}: {buildings[i].Purpose} blocks primary access");
                }
            }
        }
    }
}
