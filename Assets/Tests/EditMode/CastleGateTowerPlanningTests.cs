using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGateTowerPlanningTests
    {
        [Test]
        public void GeneratedWallTowersStayOffReservedGateEdges()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = CastlePerimeterKind.Rectangular;
                topology.DesiredTowerCount = 6;
                topology.Wards = CastleWardPattern.SingleWard;
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.HasPosternGate = (seed & 1u) != 0u;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.IsTrue(
                    CastleSpatialPlanValidator.TryValidate(
                        in dimensions, spatial, out CastleSpatialPlanIssue issue),
                    $"seed {seed}: {issue}");
            }
        }

        [Test]
        public void ValidatorRejectsWallTowerMovedOntoPrimaryGateEdge()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 701u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(701u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.DesiredTowerCount = 6;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            int wallTower = FindWallTower(spatial.Towers);
            CastleTowerPlacementSpec moved = spatial.Towers[wallTower];
            moved.Centre = spatial.PrimaryGate.Centre;
            spatial.Towers[wallTower] = moved;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.WallTowerOnGateEdge, issue);
        }

        [Test]
        public void ValidatorRejectsWallTowerMovedOntoPosternEdge()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 709u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(709u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.DesiredTowerCount = 6;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.HasPosternGate = true;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

            Assert.IsTrue(spatial.HasPosternGate);
            int wallTower = FindWallTower(spatial.Towers);
            CastleTowerPlacementSpec moved = spatial.Towers[wallTower];
            moved.Centre = spatial.PosternGate.Centre;
            spatial.Towers[wallTower] = moved;

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in dimensions, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.WallTowerOnGateEdge, issue);
        }

        private static int FindWallTower(CastleTowerPlacementSpec[] towers)
        {
            for (int i = 0; i < towers.Length; i++)
                if (towers[i].Role == CastleTowerPlacementRole.Wall) return i;
            Assert.Fail("Expected at least one optional wall tower.");
            return -1;
        }
    }
}
