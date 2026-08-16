using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialBuildReadinessTests
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
        public void CompletedSpatialPlanSatisfiesCanonicalRuntimeReadiness()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 101u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(101u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            Assert.IsTrue(
                CastleSpatialBuildReadiness.TryValidate(
                    in plan, completed, out CastleSpatialBuildReadinessIssue issue),
                issue.ToString());
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.None, issue);
        }

        [Test]
        public void RuntimeReadyPreflightReportsIncompletePlanBeforeWriteBudget()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 103u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(103u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleLandscapeDecorationSpec[] decorations = completed.Landscape.Decorations;
            Assert.Greater(decorations.Length, 0);
            decorations[0].Radius = 0;

            CastleBuildPreflightResult result = CastleBuildPreflight.EvaluateRuntimeReady(
                in plan, completed, 0);

            Assert.AreEqual(CastleBuildPreflightIssue.IncompleteSpatialPlan, result.Issue);
            Assert.AreEqual(
                CastleSpatialBuildReadinessIssue.InvalidLandscapePlan,
                result.ReadinessIssue);
            Assert.AreEqual(0L, result.EstimatedWrites,
                "Readiness must be diagnosed before the write-budget estimate is admitted.");
        }

        [Test]
        public void CorruptedKeepWindowsAreRejectedByCanonicalRuntimeReadiness()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 107u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(107u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(in plan, spatial);

            CastleKeepWindowSpec window = completed.KeepWindows[0];
            completed.KeepWindows[0] = new CastleKeepWindowSpec(
                window.Id,
                window.FloorIndex,
                window.Face,
                window.LocalOrigin,
                window.BaseYOffset,
                0,
                window.Height,
                window.Depth,
                window.HasLitGlazing);

            Assert.IsFalse(
                CastleSpatialBuildReadiness.TryValidate(
                    in plan, completed, out CastleSpatialBuildReadinessIssue issue));
            Assert.AreEqual(CastleSpatialBuildReadinessIssue.InvalidKeepWindowPlan, issue);
        }

        [Test]
        public void RuntimePreflightDelegatesCompletenessToCanonicalReadinessContract()
        {
            string preflight = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api", "CastleBuildPreflight.cs"));

            StringAssert.Contains("CastleSpatialBuildReadiness.TryValidate(", preflight);
            StringAssert.DoesNotContain("CastleKeepAnnexBuildReadiness.TryValidate(", preflight);
            StringAssert.DoesNotContain("CastleCaveBuildReadiness.TryValidate(", preflight);
            StringAssert.DoesNotContain("CastleKeepWindowPlanner.TryValidate(", preflight);
        }
    }
}
