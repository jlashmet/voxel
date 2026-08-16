using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCirculationHandoffTests
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
        public void RuntimeConsumesCompletedKeepCirculationWithoutReplanning()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepCirculationRealizer.cs"));
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));

            StringAssert.Contains("AttachKeepCirculation(in plan", completion);
            StringAssert.Contains("CastleKeepCirculationPlanner.Create(in plan)", completion);
            StringAssert.Contains("spatialPlan.KeepCirculation", pipeline);
            StringAssert.Contains("CastlePlannedKeepCirculationRealizer.Build(", pipeline);
            StringAssert.Contains("_keepStage == 3", pipeline,
                "The planned circulation realizer must preserve the historical keep substage cadence.");
            StringAssert.Contains("CastleKeepCirculationPlan", realizer);

            StringAssert.DoesNotContain("CastleKeepCirculationPlanner.Create(", pipeline,
                "Runtime must consume the completed circulation plan rather than planning it.");
            StringAssert.DoesNotContain("CastleKeepCirculationPlanner.Create(", realizer,
                "The circulation realizer must not choose its own anchors.");
        }
    }
}
