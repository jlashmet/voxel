using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardTowerClearanceTests
    {
        private const int BuildingClearance = 16;

        [Test]
        public void CourtyardBuildingsClearOuterWallTowers()
        {
            int checkedBuildings = 0;
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = CastlePerimeterKind.Rectangular;
                topology.Wards = CastleWardPattern.SingleWard;
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.DesiredTowerCount = 6;
                topology.HasPosternGate = false;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

                float clearance = plan.TowerRadius + BuildingClearance;
                float clearanceSquared = clearance * clearance;
                for (int buildingIndex = 0;
                     buildingIndex < spatial.CourtyardBuildings.Length;
                     buildingIndex++)
                {
                    CastleCourtyardBuildingSpec building = spatial.CourtyardBuildings[buildingIndex];
                    checkedBuildings++;
                    for (int towerIndex = 0; towerIndex < spatial.Towers.Length; towerIndex++)
                    {
                        Assert.GreaterOrEqual(
                            DistanceSquared(in building, spatial.Towers[towerIndex].Centre),
                            clearanceSquared,
                            $"seed {seed}: building {buildingIndex} overlaps outer tower {towerIndex}");
                    }
                }
            }

            Assert.Greater(checkedBuildings, 0, "No courtyard building was available to test.");
        }

        [Test]
        public void CourtyardBuildingsClearInnerWardTowers()
        {
            int checkedBuildings = 0;
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = CastlePerimeterKind.Rectangular;
                topology.Wards = CastleWardPattern.InnerAndOuterWards;
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.DesiredTowerCount = 4;
                topology.HasPosternGate = false;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

                float clearance = CastleInnerWardTowerPlanner.Radius(in plan) + BuildingClearance;
                float clearanceSquared = clearance * clearance;
                CastleTowerPlacementSpec[] innerTowers = spatial.InnerTowers;
                for (int buildingIndex = 0;
                     buildingIndex < spatial.CourtyardBuildings.Length;
                     buildingIndex++)
                {
                    CastleCourtyardBuildingSpec building = spatial.CourtyardBuildings[buildingIndex];
                    checkedBuildings++;
                    for (int towerIndex = 0; towerIndex < innerTowers.Length; towerIndex++)
                    {
                        Assert.GreaterOrEqual(
                            DistanceSquared(in building, innerTowers[towerIndex].Centre),
                            clearanceSquared,
                            $"seed {seed}: building {buildingIndex} overlaps inner tower {towerIndex}");
                    }
                }
            }

            Assert.Greater(checkedBuildings, 0, "No nested-ward courtyard building was available to test.");
        }

        private static float DistanceSquared(
            in CastleCourtyardBuildingSpec building,
            int2 point)
        {
            float2 delta = new float2(
                point.x - building.Centre.x,
                point.y - building.Centre.y);
            float along = math.max(
                0f,
                math.abs(math.dot(delta, building.Tangent)) - building.Width * 0.5f);
            float inward = math.max(
                0f,
                math.abs(math.dot(delta, building.Inward)) - building.Depth * 0.5f);
            return along * along + inward * inward;
        }
    }
}
