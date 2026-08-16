using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCirculationPlanningHandoffTests
    {
        [Test]
        public void SpatialPipelineConsumesPlannedKeepCirculationWithoutReplanning()
        {
            string root = FindRepoRoot();
            string pipeline = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepCirculationRealizer.cs"));

            StringAssert.Contains("spatialPlan.KeepCirculation", pipeline);
            StringAssert.Contains("CastleKeepCirculationPlanner.TryValidate(", pipeline);
            StringAssert.Contains("CastlePlannedKeepCirculationRealizer.Build(", pipeline);
            StringAssert.Contains("circulation.EntranceCentre", realizer);
            StringAssert.Contains("circulation.GrandStairOrigin", realizer);
            StringAssert.Contains("circulation.SpiralStairCentre", realizer);
            StringAssert.Contains("circulation.VerticalReach", realizer);
            StringAssert.DoesNotContain("CastleKeepCirculationPlanner.Create(", pipeline);
            StringAssert.DoesNotContain("CastleKeepCirculationPlanner.Create(", realizer);
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
