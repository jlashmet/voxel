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
        public void RuntimeReadyBundleCarriesFrozenGatehouseRecipe()
        {
            PlannedCastleBuild planned = StructuresComposition.PlanCastleBuild(
                new int3(240, 32, 420), 37u, 91u);
            CastlePlan dimensions = planned.Dimensions;
            CastleGatehousePlan expected = CastleGatehousePlanner.Create(in dimensions);
            CastleGatehousePlan actual = planned.Gatehouse;

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
        }

        [Test]
        public void ProductionCompositionHandsGatehousePlanToPlannedRealizer()
        {
            string composition = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "StructuresComposition.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("public CastleGatehousePlan Gatehouse { get; }", composition);
            StringAssert.Contains("Gatehouse = CastleGatehousePlanner.Create(in dimensions);", composition);
            StringAssert.Contains("CastleGatehousePlan gatehouse = planned.Gatehouse;", composition);
            StringAssert.Contains("in CastleGatehousePlan gatehousePlan", composition);

            StringAssert.Contains("in CastleGatehousePlan gatehousePlan", pipeline);
            StringAssert.Contains("CastleGatehousePlanValidator.RequireValid(in gatehousePlan);", pipeline);
            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", pipeline);
            StringAssert.Contains("in _gatehousePlan", pipeline);
        }
    }
}
