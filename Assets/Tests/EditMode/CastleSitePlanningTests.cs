using System.IO;
using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSitePlanningTests
    {
        [Test]
        public void GeneratedTopologyCarriesDeterministicSiteSurfacePlan()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastleTopologyPlan first = CastleLayoutPlanner.Create(seed);
                CastleTopologyPlan second = CastleLayoutPlanner.Create(seed);

                Assert.AreNotEqual(0u, first.Site.GrassPatternSeed, $"seed {seed}");
                Assert.AreEqual(92, first.Site.GrassCoveragePercent, $"seed {seed}");
                Assert.AreEqual(first.Site.GrassPatternSeed, second.Site.GrassPatternSeed, $"seed {seed}");

                for (int z = -12; z <= 12; z += 3)
                for (int x = -12; x <= 12; x += 3)
                {
                    Assert.AreEqual(
                        first.Site.ShouldGrassCap(x, z),
                        second.Site.ShouldGrassCap(x, z),
                        $"seed {seed}, column ({x},{z})");
                }
            }
        }

        [Test]
        public void SiteSurfaceDecisionDoesNotDependOnEvaluationOrder()
        {
            CastleSitePlan site = CastleSitePlanner.Create(0xC45A71Eu);
            bool[] forward = new bool[81];
            bool[] reverse = new bool[81];

            int cursor = 0;
            for (int z = -4; z <= 4; z++)
            for (int x = -4; x <= 4; x++)
                forward[cursor++] = site.ShouldGrassCap(x, z);

            cursor = 80;
            for (int z = 4; z >= -4; z--)
            for (int x = 4; x >= -4; x--)
                reverse[cursor--] = site.ShouldGrassCap(x, z);

            CollectionAssert.AreEqual(forward, reverse);
        }

        [Test]
        public void SpatialSiteRuntimeConsumesPlanInsteadOfMutableRandomStream()
        {
            string site = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleSiteRealizer.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleBuildPipeline.cs"));

            StringAssert.Contains("in CastleSitePlan sitePlan", site);
            StringAssert.Contains("sitePlan.ShouldGrassCap(x, z)", site);
            StringAssert.Contains("state.Cursor == 0 && !hasPlannedApproach", site,
                "Spatial builds must not initialize the compatibility RNG.");
            StringAssert.Contains("_sitePlan = spatialPlan.Topology.Site;", pipeline);
            StringAssert.Contains("in _sitePlan", pipeline);
        }

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
    }
}
