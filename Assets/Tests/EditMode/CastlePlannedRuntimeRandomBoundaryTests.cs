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
        public void SpatialSitePathUsesFrozenGrassMaskWhileLegacyPathMayRetainRng()
        {
            string site = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleSiteRealizer.cs"));

            StringAssert.Contains("sitePlan.ShouldGrassCap(x, z)", site,
                "Spatial site realization must consume the planned grass mask.");
            StringAssert.Contains("if (state.Cursor == 0 && !hasPlannedApproach)", site,
                "Site RNG initialization must remain gated to the compatibility path.");
            StringAssert.Contains(": state.Random.NextInt(0, 100) < 92", site,
                "The historical random grass cap may remain only as the legacy fallback.");
        }
    }
}
