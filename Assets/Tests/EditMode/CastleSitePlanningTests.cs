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
                Assert.AreNotEqual(0u, first.Site.CourtyardPatternSeed, $"seed {seed}");
                Assert.AreEqual(82, first.Site.CourtyardStonePercent, $"seed {seed}");
                Assert.AreEqual(first.Site.GrassPatternSeed, second.Site.GrassPatternSeed, $"seed {seed}");
                Assert.AreEqual(first.Site.CourtyardPatternSeed, second.Site.CourtyardPatternSeed, $"seed {seed}");

                for (int z = -12; z <= 12; z += 3)
                for (int x = -12; x <= 12; x += 3)
                {
                    Assert.AreEqual(
                        first.Site.ShouldGrassCap(x, z),
                        second.Site.ShouldGrassCap(x, z),
                        $"seed {seed}, grass column ({x},{z})");
                    Assert.AreEqual(
                        first.Site.ShouldUseCourtyardStone(x, z),
                        second.Site.ShouldUseCourtyardStone(x, z),
                        $"seed {seed}, courtyard column ({x},{z})");
                }
            }
        }

        [Test]
        public void SiteSurfaceDecisionsDoNotDependOnEvaluationOrder()
        {
            CastleSitePlan site = CastleSitePlanner.Create(0xC45A71Eu);
            bool[] grassForward = new bool[81];
            bool[] grassReverse = new bool[81];
            bool[] courtyardForward = new bool[81];
            bool[] courtyardReverse = new bool[81];

            int cursor = 0;
            for (int z = -4; z <= 4; z++)
            for (int x = -4; x <= 4; x++)
            {
                grassForward[cursor] = site.ShouldGrassCap(x, z);
                courtyardForward[cursor] = site.ShouldUseCourtyardStone(x, z);
                cursor++;
            }

            cursor = 80;
            for (int z = 4; z >= -4; z--)
            for (int x = 4; x >= -4; x--)
            {
                grassReverse[cursor] = site.ShouldGrassCap(x, z);
                courtyardReverse[cursor] = site.ShouldUseCourtyardStone(x, z);
                cursor--;
            }

            CollectionAssert.AreEqual(grassForward, grassReverse);
            CollectionAssert.AreEqual(courtyardForward, courtyardReverse);
        }

        [Test]
        public void SpatialSurfaceRuntimeConsumesPlanInsteadOfMutableRandomStreams()
        {
            string site = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleSiteRealizer.cs"));
            string courtyard = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleCourtyardRealizer.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleBuildPipeline.cs"));

            StringAssert.Contains("in CastleSitePlan sitePlan", site);
            StringAssert.Contains("sitePlan.ShouldGrassCap(x, z)", site);
            StringAssert.Contains("state.Cursor == 0 && !hasPlannedApproach", site,
                "Spatial builds must not initialize the compatibility site RNG.");

            StringAssert.Contains("sitePlan.ShouldUseCourtyardStone(x, z)", courtyard);
            StringAssert.Contains("bool hasPlannedSurface", courtyard);

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
