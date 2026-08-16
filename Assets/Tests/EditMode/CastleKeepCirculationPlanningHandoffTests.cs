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
                "CastleKeepCirculationRealizer.cs"));

            StringAssert.Contains("spatialPlan.KeepCirculation", pipeline);
            StringAssert.Contains("CastleKeepCirculationPlanner.TryValidate(", pipeline);
            StringAssert.Contains("projection.KeepCentreWorld", pipeline,
                "Runtime should snapshot the semantic world keep centre instead of reapplying the legacy +60 anchor.");
            StringAssert.Contains("CastleKeepCirculationRealizer.Build(", pipeline);
            StringAssert.Contains("worldKeepCentre", realizer);
            StringAssert.Contains("circulation.EntranceCentre", realizer);
            StringAssert.Contains("circulation.GrandStairOrigin", realizer);
            StringAssert.Contains("circulation.SpiralStairCentre", realizer);
            StringAssert.Contains("circulation.VerticalReach", realizer);
            StringAssert.DoesNotContain("LegacyKeepCentreZOffset", realizer,
                "The planned circulation realizer must stay independent of compatibility projection offsets.");
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
