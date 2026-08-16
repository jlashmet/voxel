using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleInnerWardTowerTests
    {
        [Test]
        public void NestedWardPlansOneTowerPerInnerCorner()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.Perimeter = CastlePerimeterKind.Rectangular;
                topology.Wards = CastleWardPattern.InnerAndOuterWards;
                topology.KeepPlacement = CastleKeepPlacement.Central;
                topology.DesiredTowerCount = 4;
                topology.HasPosternGate = false;

                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
                CastleTowerPlacementSpec[] innerTowers = spatial.InnerTowers;

                Assert.AreEqual(spatial.InnerWardVertices.Length, innerTowers.Length,
                    $"seed {seed}: inner tower count");
                for (int i = 0; i < innerTowers.Length; i++)
                {
                    Assert.AreEqual(i, innerTowers[i].Id, $"seed {seed}: inner tower {i} id");
                    Assert.AreEqual(CastleTowerPlacementRole.Corner, innerTowers[i].Role,
                        $"seed {seed}: inner tower {i} role");
                    Assert.AreEqual(spatial.InnerWardVertices[i], innerTowers[i].Centre,
                        $"seed {seed}: inner tower {i} centre");
                }
            }
        }

        [Test]
        public void InnerTowerRoofChoiceIsMaterializedFromRingGeometry()
        {
            int2[] ring =
            {
                new int2(-84, -62),
                new int2(91, -58),
                new int2(86, 67),
                new int2(-79, 71),
            };

            CastleTowerPlacementSpec[] first = CastleInnerWardTowerPlanner.Create(ring);
            CastleTowerPlacementSpec[] second = CastleInnerWardTowerPlanner.Create(ring);

            Assert.AreEqual(ring.Length, first.Length);
            Assert.AreEqual(first.Length, second.Length);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(0, first[i].HeightVariation,
                    $"inner tower {i}: planning should preserve the secondary-ring height profile");
                Assert.AreEqual(first[i].HasRoof, second[i].HasRoof,
                    $"inner tower {i}: roof choice must be deterministic plan data");
            }
        }

        [Test]
        public void SingleWardDoesNotInventInnerTowers()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 91u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(91u);
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Central;

            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            Assert.AreEqual(0, spatial.InnerWardVertices.Length);
            Assert.AreEqual(0, spatial.InnerTowers.Length);
        }

        [Test]
        public void ValidatorRejectsMaterializedInnerTowerDrift()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 103u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(103u);
            topology.Perimeter = CastlePerimeterKind.Rectangular;
            topology.Wards = CastleWardPattern.InnerAndOuterWards;
            topology.KeepPlacement = CastleKeepPlacement.Central;
            topology.DesiredTowerCount = 4;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

            spatial.InnerTowers[0].Centre += new int2(1, 0);

            Assert.IsFalse(
                CastleSpatialPlanValidator.TryValidate(
                    in plan, spatial, out CastleSpatialPlanIssue issue));
            Assert.AreEqual(CastleSpatialPlanIssue.InvalidInnerTowerPlacement, issue);
        }

        [Test]
        public void InnerTowerPolicyUsesSmallerFootprintThanOuterTower()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 113u);

            int radius = CastleInnerWardTowerPlanner.Radius(in plan);
            int height = CastleInnerWardTowerPlanner.Height(in plan);

            Assert.Greater(radius, 0);
            Assert.Less(radius, plan.TowerRadius);
            Assert.Greater(height, plan.WallHeight,
                "Inner towers still need to rise above their curtain wall.");
        }

        [Test]
        public void PipelineRealizesMaterializedInnerTowersWithoutPlanningInRuntime()
        {
            string root = RepoRoot();
            string pipeline = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleInnerWardTowerRealizer.cs"));
            string plan = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlan.cs"));

            StringAssert.Contains("private readonly CastleTowerPlacementSpec[] _innerTowers;", plan);
            StringAssert.Contains("public CastleTowerPlacementSpec[] InnerTowers => _innerTowers;", plan);
            StringAssert.Contains("_innerTowers = CastleInnerWardTowerPlanner.Create(innerWardVertices);", plan);
            StringAssert.Contains("spatialPlan.InnerTowers", pipeline);
            StringAssert.Contains("CastleInnerWardTowerRealizer.BuildAll(", pipeline);
            StringAssert.Contains("_innerTowerSpecs", pipeline);
            StringAssert.Contains("tower.HasRoof", realizer);
            StringAssert.DoesNotContain("CastleSeedPartition", realizer,
                "Runtime must consume planned inner-tower appearance rather than draw seed choices.");
            StringAssert.DoesNotContain("CastleInnerWardTowerPlanner.", pipeline,
                "Runtime must consume the materialized tower plan rather than invoke planning policy.");
            StringAssert.DoesNotContain("CastleInnerWardTowerPlanner.", realizer,
                "The realizer owns only voxel profile decisions for supplied tower specs.");
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
