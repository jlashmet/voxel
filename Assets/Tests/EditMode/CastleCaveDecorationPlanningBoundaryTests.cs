using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveDecorationPlanningBoundaryTests
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
        public void AdmissionAndDependenciesConsumeExplicitDecorationPlan()
        {
            string preflight = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleBuildPreflight.cs"));
            string bounds = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleBuildBounds.cs"));

            StringAssert.Contains("spatialPlan.CaveDecoration", preflight);
            StringAssert.Contains(
                "CastleCaveDecorationEstimate.Estimate(cave, caveDecoration)", preflight);
            StringAssert.Contains("spatial.CaveDecoration", bounds);
            StringAssert.Contains(
                "CastleCaveDecorationBuildBoundsResolver.Resolve(cave, caveDecoration)", bounds);

            StringAssert.DoesNotContain("caveDecorationPadding", bounds,
                "Dependency sizing must use planned decoration coordinates instead of a fixed cave halo.");
        }
    }
}
