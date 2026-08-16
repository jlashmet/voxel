using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleTowerSlitCompletionTests
    {
        [Test]
        public void RuntimeReadyCastleFreezesValidSlitPhasesForEveryPlannedTower()
        {
            PlannedCastleBuild build = StructuresComposition.PlanCastleBuild(
                int3.zero, 137u, 0x51A7u);
            CastlePlan plan = build.Dimensions;
            CastleSpatialPlan spatial = build.Spatial;

            AssertTowerSlits(in plan, spatial.Towers, plan.TowerHeight, "outer");
            AssertTowerSlits(
                in plan,
                spatial.InnerTowers,
                CastleInnerWardTowerPlanner.Height(in plan),
                "inner");
            Assert.IsTrue(CastleTowerSlitPlanCompletion.TryValidate(
                in plan, spatial, out CastleTowerSlitBuildReadinessIssue issue),
                issue.ToString());
            Assert.IsTrue(CastleSpatialBuildReadiness.TryValidate(
                in plan, spatial, out CastleSpatialBuildReadinessIssue readinessIssue),
                readinessIssue.ToString());
        }

        [Test]
        public void TowerSlitCompletionIsDeterministicAcrossRepeatedPlanning()
        {
            PlannedCastleBuild first = StructuresComposition.PlanCastleBuild(
                int3.zero, 241u, 0x71C3u);
            PlannedCastleBuild second = StructuresComposition.PlanCastleBuild(
                int3.zero, 241u, 0x71C3u);

            AssertSameSlits(first.Spatial.Towers, second.Spatial.Towers);
            AssertSameSlits(first.Spatial.InnerTowers, second.Spatial.InnerTowers);
        }

        [Test]
        public void RuntimeReadinessRejectsMissingOuterTowerSlitPlan()
        {
            PlannedCastleBuild build = StructuresComposition.PlanCastleBuild(
                int3.zero, 313u, 0x91D5u);
            CastlePlan plan = build.Dimensions;
            CastleSpatialPlan spatial = build.Spatial;
            Assert.Greater(spatial.Towers.Length, 0);

            spatial.Towers[0].Slits = null;

            Assert.IsFalse(CastleTowerSlitPlanCompletion.TryValidate(
                in plan, spatial, out CastleTowerSlitBuildReadinessIssue slitIssue));
            Assert.AreEqual(CastleTowerSlitBuildReadinessIssue.MissingSlitPlan, slitIssue);

            Assert.IsFalse(CastleSpatialBuildReadiness.TryValidate(
                in plan, spatial, out CastleSpatialBuildReadinessIssue readinessIssue));
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.MissingTowerSlitPlan, readinessIssue);

            CastleBuildPreflightResult preflight = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, spatial, long.MaxValue);
            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, preflight.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.MissingTowerSlitPlan,
                preflight.ReadinessIssue,
                "Direct runtime callers must be rejected before stage 1, not during tower realization.");
        }

        [Test]
        public void PlannedTowerRealizersConsumeFrozenSlitPlansWithoutPlanning()
        {
            string root = RepoRoot;
            string outer = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedTowerRealizer.cs"));
            string inner = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleInnerWardTowerRealizer.cs"));

            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", outer);
            StringAssert.Contains("tower.Slits", outer);
            StringAssert.DoesNotContain("CastleTowerSlitPlanner", outer);
            StringAssert.DoesNotContain("new Random", outer);

            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", inner);
            StringAssert.Contains("tower.Slits", inner);
            StringAssert.DoesNotContain("CastleTowerSlitPlanner", inner);
            StringAssert.DoesNotContain("new Random", inner);
        }

        private static void AssertTowerSlits(
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers,
            int baseHeight,
            string label)
        {
            Assert.NotNull(towers);
            for (int i = 0; i < towers.Length; i++)
            {
                int height = baseHeight + math.max(0, towers[i].HeightVariation);
                Assert.NotNull(towers[i].Slits, $"{label} tower {i} has no slit plan");
                Assert.IsTrue(CastleTowerSlitPlanValidator.TryValidate(
                    towers[i].Slits, height, plan.FloorHeight, out CastleTowerSlitPlanIssue issue),
                    $"{label} tower {i}: {issue}");
            }
        }

        private static void AssertSameSlits(
            CastleTowerPlacementSpec[] first,
            CastleTowerPlacementSpec[] second)
        {
            Assert.AreEqual(first.Length, second.Length);
            for (int tower = 0; tower < first.Length; tower++)
            {
                Assert.NotNull(first[tower].Slits);
                Assert.NotNull(second[tower].Slits);
                Assert.AreEqual(first[tower].Slits.FloorCount, second[tower].Slits.FloorCount);
                for (int floor = 0; floor < first[tower].Slits.FloorCount; floor++)
                {
                    Assert.AreEqual(
                        first[tower].Slits.PhaseRadiansAt(floor),
                        second[tower].Slits.PhaseRadiansAt(floor),
                        $"tower {tower}, floor {floor}: slit phase changed");
                }
            }
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
