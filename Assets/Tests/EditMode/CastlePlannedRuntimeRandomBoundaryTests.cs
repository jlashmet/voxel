using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedRuntimeRandomBoundaryTests
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
        public void PlannedCastleRealizersNeverMakeSeededChoices()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] files = Directory.GetFiles(
                runtimeDirectory,
                "CastlePlanned*.cs",
                SearchOption.TopDirectoryOnly);

            Assert.IsNotEmpty(files,
                "Expected dedicated CastlePlanned* Runtime realizers for spatial castle builds.");

            foreach (string file in files)
            {
                string source = File.ReadAllText(file);
                string name = Path.GetFileName(file);

                StringAssert.DoesNotContain("new Random(", source,
                    $"{name} must realize frozen planning data instead of drawing Runtime RNG.");
                StringAssert.DoesNotContain("NextInt(", source,
                    $"{name} must not choose integer variation during realization.");
                StringAssert.DoesNotContain("NextFloat(", source,
                    $"{name} must not choose floating variation during realization.");
                StringAssert.DoesNotContain("NextBool(", source,
                    $"{name} must not choose boolean variation during realization.");
                StringAssert.DoesNotContain("CastleSeedPartition.Derive(", source,
                    $"{name} must not derive new semantic/decor seeds during realization.");
            }
        }

        [Test]
        public void PlannedSiteRealizerUsesFrozenGrassMaskWhileLegacySiteRetainsRng()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string legacy = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleSiteRealizer.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedSiteRealizer.cs"));

            StringAssert.Contains("sitePlan.ShouldGrassCap(x, z)", planned,
                "Spatial site realization must consume the planner-owned grass mask.");
            StringAssert.DoesNotContain("new Random(", planned,
                "Spatial site realization must not own Runtime RNG state.");
            StringAssert.DoesNotContain("NextInt(", planned,
                "Spatial site realization must not draw authored variation during mutation.");
            StringAssert.DoesNotContain("CastleSeedPartition.Derive(", planned,
                "Spatial site realization must not derive authored seeds during mutation.");
            StringAssert.DoesNotContain("plan.Seed", planned,
                "Spatial site realization must be driven only by frozen site data and terrain queries.");

            StringAssert.Contains("new Random(siteSeed)", legacy,
                "The dimension-only compatibility site path retains the historical RNG recipe.");
            StringAssert.Contains("state.Random.NextInt(0, 100) < 92", legacy,
                "Legacy grass variation remains explicitly isolated in the compatibility realizer.");

            StringAssert.Contains("CastlePlannedSiteRealizer", pipeline,
                "The spatial pipeline must name the dedicated no-RNG site realizer.");
        }

        [Test]
        public void SpatialCourtyardDelegatesToDedicatedNoRngRealizer()
        {
            string compatibility = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleCourtyardRealizer.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedCourtyardRealizer.cs"));

            StringAssert.Contains("CastlePlannedCourtyardRealizer.Build(", compatibility,
                "The spatial courtyard entry point must delegate to the dedicated planned realizer.");
            StringAssert.Contains("sitePlan.ShouldUseCourtyardStone(x, z)", planned,
                "Planned courtyard paving must consume the frozen site surface mask.");
            StringAssert.Contains("CastleCourtyardBuildingRealizer.BuildAll", planned,
                "Planned courtyard buildings must be realized from planner-owned building specs.");
            StringAssert.DoesNotContain("new Random(", planned);
            StringAssert.DoesNotContain("NextInt(", planned);
            StringAssert.DoesNotContain("NextFloat(", planned);
            StringAssert.DoesNotContain("NextBool(", planned);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive(", planned);
            StringAssert.DoesNotContain("plan.Seed", planned);
        }
    }
}
