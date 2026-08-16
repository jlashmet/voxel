using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleInnerWardTowerPlanTests
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
        public void InnerTowersAreSnapshotPlanDataRatherThanAComputedPlannerView()
        {
            CastlePlan dimensions = CastlePlanner.Create(int3.zero, 173u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(173u);
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            topology.KeepPlacement = CastleKeepPlacement.Central;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
            Assert.Greater(spatial.InnerWardVertices.Length, 0);
            Assert.AreEqual(spatial.InnerWardVertices.Length, spatial.InnerTowers.Length);

            int2 plannedTower = spatial.InnerTowers[0].Centre;
            spatial.InnerWardVertices[0] += new int2(17, 9);

            Assert.AreEqual(plannedTower, spatial.InnerTowers[0].Centre,
                "Reading InnerTowers must not re-run placement after plan construction.");
            Assert.AreNotEqual(spatial.InnerWardVertices[0], spatial.InnerTowers[0].Centre);
        }

        [Test]
        public void RuntimeNeverInvokesInnerWardTowerPlanner()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                StringAssert.DoesNotContain(
                    "CastleInnerWardTowerPlanner.Create(",
                    source,
                    $"{Path.GetFileName(file)} must consume stored inner-tower plan data.");
            }
        }
    }
}
