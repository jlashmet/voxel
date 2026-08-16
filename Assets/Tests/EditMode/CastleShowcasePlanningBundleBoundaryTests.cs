using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleShowcasePlanningBundleBoundaryTests
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
        public void ShowcaseKeepsResolvedCastlePlanSeedAndProjectionInOneBundle()
        {
            string bridge = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string state = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatialState.cs"));
            string world = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.cs"));

            StringAssert.Contains("StructuresComposition.PlanCastleBuild(", bridge);
            StringAssert.Contains("in _world._pendingPlannedCastle", bridge);
            StringAssert.Contains("_plannedCastle = _pendingPlannedCastle", bridge);
            StringAssert.Contains("_castleSpatialProjection = _plannedCastle.Projection", bridge);
            StringAssert.DoesNotContain("PlanCastleSpatial(", bridge,
                "Showcase should consume the Composition-owned runtime bundle rather than split planning again.");

            StringAssert.Contains("PlannedCastleBuild _pendingPlannedCastle", state);
            StringAssert.Contains("PlannedCastleBuild _plannedCastle", state);
            StringAssert.DoesNotContain("CastleSpatialPlan _pendingCastleSpatialPlan", state);

            StringAssert.Contains("PreparePendingCastleSpatialPlan();", world);
            StringAssert.Contains("BeginPendingSpatialCastleBuild(", world);
            StringAssert.Contains("CommitPendingCastleSpatialPlan();", world);
            StringAssert.Contains("ActiveCastleFrontGatePosition()", world);
            StringAssert.Contains("OpenActiveCastleFrontGate();", world);
            StringAssert.Contains("ActiveCastleTrapdoorPosition()", world);
            StringAssert.Contains("OpenActiveCastleTrapdoor();", world);
        }
    }
}
