using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepRoomPlanningHandoffTests
    {
        [Test]
        public void CompletedSpatialPlanCarriesOneSemanticPlanPerKeepFloor()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 271u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(dimensions.Seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                in dimensions, spatial);

            Assert.AreEqual(dimensions.Floors, completed.KeepFloors.Length);
            for (int i = 0; i < completed.KeepFloors.Length; i++)
                Assert.AreEqual(i, completed.KeepFloors[i].FloorIndex);
        }

        [Test]
        public void RuntimeReadyPreflightRejectsMissingKeepFloorPlan()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 273u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(dimensions.Seed);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan incomplete = CastleSpatialPlanner.Create(in dimensions, in topology);

            CastleBuildPreflightResult result = CastleBuildPreflight.EvaluateRuntimeReady(
                in dimensions, incomplete, long.MaxValue);

            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, result.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.MissingKeepFloorPlan,
                result.ReadinessIssue);
        }

        [Test]
        public void RuntimeConsumesPlannedKeepFloorsWithoutCallingRoomPlanner()
        {
            string root = FindRepoRoot();
            string pipeline = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleBuildPipeline.cs"));
            string keep = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleKeepRealizer.cs"));

            StringAssert.Contains("spatialPlan.KeepFloors", pipeline);
            StringAssert.Contains("CastleKeepFloorPlan[]", keep);
            StringAssert.Contains("roomPlan.Purpose", keep);
            StringAssert.DoesNotContain("CastleKeepRoomPlanner.Create", pipeline);
            StringAssert.DoesNotContain("CastleKeepRoomPlanner.Create", keep);
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
                directory = directory.Parent;
            Assert.NotNull(directory, "Could not locate project root containing Assets/.");
            return directory.FullName;
        }
    }
}
