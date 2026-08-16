using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTowerSlitPlanningTests
    {
        [Test]
        public void PlannerPreservesHistoricalWorldPositionRngSequence()
        {
            var centre = new int2(913, -427);
            const int towerHeight = 214;
            const int floorHeight = 46;
            CastleTowerSlitPlan planned = CastleTowerSlitPlanner.Create(
                centre, towerHeight, floorHeight);

            uint historicalSeed = unchecked(
                (uint)(centre.x * 8191 + centre.y * 131071) | 1u);
            var rng = new Random(historicalSeed);
            int expectedFloors = 0;
            for (int floor = 0; floor * floorHeight < towerHeight - 40; floor++)
            {
                float expected = rng.NextFloat(0f, 6.28f);
                Assert.AreEqual(expected, planned.PhaseRadiansAt(floor),
                    $"floor {floor} changed the historical arrow-slit phase");
                expectedFloors++;
            }

            Assert.AreEqual(expectedFloors, planned.FloorCount);
            Assert.IsTrue(CastleTowerSlitPlanValidator.TryValidate(
                planned, towerHeight, floorHeight, out CastleTowerSlitPlanIssue issue),
                issue.ToString());
        }

        [Test]
        public void GatehousePlannerBindsSlitsToActualPlannedTowerCoordinates()
        {
            CastlePlan castle = CastlePlanner.Create(new int3(300, 35, 700), 43u);
            var gate = new CastleGatePlacementSpec
            {
                EdgeIndex = 1,
                Centre = new int2(castle.BaileyHalfX, 27),
                Outward = new float2(1f, 0f),
            };

            CastleGatehousePlan gatehouse = CastleGatehousePlanner.Create(in castle, in gate);
            CastleGateGeometry geometry = CastleGateGeometryResolver.Resolve(in castle, in gate);
            float2 leftF = geometry.PerimeterCentre - geometry.Tangent * gatehouse.TowerSpacing;
            float2 rightF = geometry.PerimeterCentre + geometry.Tangent * gatehouse.TowerSpacing;
            var left = new int2((int)math.round(leftF.x), (int)math.round(leftF.y));
            var right = new int2((int)math.round(rightF.x), (int)math.round(rightF.y));

            CastleTowerSlitPlan expectedLeft = CastleTowerSlitPlanner.Create(
                left, gatehouse.LeftTowerHeight, castle.FloorHeight);
            CastleTowerSlitPlan expectedRight = CastleTowerSlitPlanner.Create(
                right, gatehouse.RightTowerHeight, castle.FloorHeight);

            Assert.AreEqual(expectedLeft.FloorCount, gatehouse.LeftTowerSlits.FloorCount);
            Assert.AreEqual(expectedRight.FloorCount, gatehouse.RightTowerSlits.FloorCount);
            for (int floor = 0; floor < expectedLeft.FloorCount; floor++)
                Assert.AreEqual(expectedLeft.PhaseRadiansAt(floor),
                                gatehouse.LeftTowerSlits.PhaseRadiansAt(floor));
            for (int floor = 0; floor < expectedRight.FloorCount; floor++)
                Assert.AreEqual(expectedRight.PhaseRadiansAt(floor),
                                gatehouse.RightTowerSlits.PhaseRadiansAt(floor));
        }
    }
}
