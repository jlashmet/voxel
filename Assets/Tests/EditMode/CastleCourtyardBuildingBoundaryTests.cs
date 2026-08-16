using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingBoundaryTests
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
        public void RuntimeConsumesPlannedBuildingsWithoutChoosingTheirPlacement()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string courtyard = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleCourtyardRealizer.cs"));

            StringAssert.Contains(
                "_courtyardBuildings = (CastleCourtyardBuildingSpec[])spatialPlan.CourtyardBuildings.Clone()",
                pipeline);
            StringAssert.Contains("_courtyardBuildings", pipeline);
            StringAssert.Contains("CastleCourtyardBuildingRealizer.BuildAll(", courtyard);

            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                StringAssert.DoesNotContain(
                    "CastleCourtyardBuildingPlanner.",
                    source,
                    $"{Path.GetFileName(file)} must realize supplied courtyard buildings rather than plan them.");
            }
        }

        [Test]
        public void SpatialPreflightPricesPlannedCourtyardBuildingDimensions()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 131u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(131u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.Greater(spatial.CourtyardBuildings.Length, 0,
                "Baseline spatial castle should contain at least one planned courtyard building.");

            long withBuildings = CastleBuildPreflight.EstimateWrites(in plan, spatial);
            for (int i = 0; i < spatial.CourtyardBuildings.Length; i++)
            {
                CastleCourtyardBuildingSpec building = spatial.CourtyardBuildings[i];
                building.Width = 0;
                building.Depth = 0;
                building.Height = 0;
                spatial.CourtyardBuildings[i] = building;
            }
            long withoutBuildingCost = CastleBuildPreflight.EstimateWrites(in plan, spatial);

            Assert.Greater(withBuildings, withoutBuildingCost,
                "Spatial admission budget must account for planner-owned courtyard structures.");
        }
    }
}
