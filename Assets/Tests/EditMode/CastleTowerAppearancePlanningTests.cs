using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTowerAppearancePlanningTests
    {
        [Test]
        public void OuterTowerAppearancePreservesHistoricalSeedSequence()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

                for (int i = 0; i < spatial.Towers.Length; i++)
                {
                    CastleTowerPlacementSpec tower = spatial.Towers[i];
                    uint variationSeed = CastleSeedPartition.Derive(
                        plan.Seed, CastleSeedDomain.Walls, (uint)(0x2000 + i));
                    int expectedVariation = 8 + (int)(variationSeed % 51u);
                    bool expectedRoof = tower.Role == CastleTowerPlacementRole.Corner
                                     && ((variationSeed >> 8) & 1u) != 0u;

                    Assert.AreEqual(expectedVariation, tower.HeightVariation,
                        $"seed {seed}: tower {i} height variation");
                    Assert.AreEqual(expectedRoof, tower.HasRoof,
                        $"seed {seed}: tower {i} roof");
                }
            }
        }

        [Test]
        public void SpatialOuterTowerRealizerConsumesAppearanceWithoutSeedChoices()
        {
            string root = RepoRoot();
            string planner = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Api", "CastleSpatialPlanner.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedTowerRealizer.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("HeightVariation = 8 + (int)(variationSeed % 51u)", planner);
            StringAssert.Contains("HasRoof =", planner);
            StringAssert.Contains("tower.HeightVariation", realizer);
            StringAssert.Contains("tower.HasRoof", realizer);
            StringAssert.DoesNotContain("CastleSeedPartition", realizer,
                "Spatial tower realization must consume the plan rather than redraw randomness.");
            StringAssert.Contains("CastlePlannedTowerRealizer.BuildAll(", pipeline);
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                dir = dir.Parent;

            Assert.NotNull(dir, "Could not locate project root containing Assets/.");
            return dir.FullName;
        }
    }
}
