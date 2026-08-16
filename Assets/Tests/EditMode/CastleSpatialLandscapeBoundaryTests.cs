using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialLandscapeBoundaryTests
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
        public void SpatialLandscapeIsPlannedBeforeRuntimeAndSnapshottedAtHandoff()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedLandscapeRealizer.cs"));
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));

            StringAssert.Contains("CastleSpatialPlan completed = AttachLandscape(", completion);
            StringAssert.Contains("CastleLandscapePlanner.Create(", completion);
            StringAssert.Contains("CastleLandscapePlanSnapshot.CloneValidated(spatialPlan.Landscape)", pipeline);
            StringAssert.Contains("CastlePlannedLandscapeRealizer.Build(", pipeline);
            StringAssert.DoesNotContain("CastleSpatialLandscapeRealizer.Build(", pipeline);

            StringAssert.Contains("CastleLandscapePlan landscape", realizer);
            StringAssert.Contains("landscape.Decorations", realizer);
            StringAssert.DoesNotContain("Random", realizer);
            StringAssert.DoesNotContain("CastleSeedPartition", realizer);
            StringAssert.DoesNotContain("Waterfall", realizer);
        }
    }
}
