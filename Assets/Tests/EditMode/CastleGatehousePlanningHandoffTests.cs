using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleGatehousePlanningHandoffTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;

                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        [Test]
        public void RuntimeReadySpatialPlanCarriesGatehouseForItsActualPrimaryGate()
        {
            PlannedCastleBuild planned = StructuresComposition.PlanCastleBuild(
                new int3(240, 32, 420), 37u, 91u);
            CastlePlan dimensions = planned.Dimensions;
            CastleSpatialPlan spatial = planned.Spatial;
            CastleTopologyPlan topology = spatial.Topology;
            CastleGatePlacementSpec primaryGate = spatial.PrimaryGate;
            CastleGatehousePlan expected = CastleGatehousePlanner.Create(
                in dimensions, in primaryGate);
            CastleGatehousePlan actual = topology.Gatehouse;

            Assert.IsTrue(topology.HasGatehousePlan);
            Assert.AreEqual(expected.TowerSpacing, actual.TowerSpacing);
            Assert.AreEqual(expected.LeftTowerHeight, actual.LeftTowerHeight);
            Assert.AreEqual(expected.RightTowerHeight, actual.RightTowerHeight);
            Assert.AreEqual(expected.BlockHeight, actual.BlockHeight);
            Assert.AreEqual(expected.OpeningHeight, actual.OpeningHeight);
            Assert.AreEqual(expected.BridgeNearDistance, actual.BridgeNearDistance);
            Assert.AreEqual(expected.BridgeLength, actual.BridgeLength);
            Assert.AreEqual(expected.BridgeWidth, actual.BridgeWidth);
            Assert.AreEqual(expected.BridgeSupportOffset, actual.BridgeSupportOffset);
            Assert.AreEqual(expected.BridgeRailYOffset, actual.BridgeRailYOffset);

            Assert.AreEqual(expected.LeftTowerSlits.FloorCount, actual.LeftTowerSlits.FloorCount);
            Assert.AreEqual(expected.RightTowerSlits.FloorCount, actual.RightTowerSlits.FloorCount);
            for (int floor = 0; floor < actual.LeftTowerSlits.FloorCount; floor++)
            {
                Assert.AreEqual(
                    expected.LeftTowerSlits.PhaseRadiansAt(floor),
                    actual.LeftTowerSlits.PhaseRadiansAt(floor));
            }
            for (int floor = 0; floor < actual.RightTowerSlits.FloorCount; floor++)
            {
                Assert.AreEqual(
                    expected.RightTowerSlits.PhaseRadiansAt(floor),
                    actual.RightTowerSlits.PhaseRadiansAt(floor));
            }
        }

        [Test]
        public void ProductionPlanningHandsFrozenGatehouseThroughTopologyToRuntime()
        {
            string terrainPlanning = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "CastleTerrainPlanning.cs"));
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleGatehousePlanCompletion.cs"));
            string readiness = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialBuildReadiness.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedGatehouseRealizer.cs"));

            StringAssert.Contains("CastleGatehousePlanCompletion.Attach", terrainPlanning);
            StringAssert.Contains(
                "CastleGatehousePlanner.Create(\n                    in dimensions, in primaryGate)",
                completion);
            StringAssert.Contains("topology.HasGatehousePlan = true", completion);
            StringAssert.Contains("TryValidateTowerDetails", readiness);

            StringAssert.Contains("CastleGatehousePlan gatehouse = topology.Gatehouse", pipeline);
            StringAssert.Contains("_gatehousePlan = gatehouse", pipeline);
            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", pipeline);

            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", realizer);
            StringAssert.Contains("gatehouse.LeftTowerSlits", realizer);
            StringAssert.Contains("gatehouse.RightTowerSlits", realizer);
            StringAssert.DoesNotContain("new Random(", realizer);
        }
    }
}
