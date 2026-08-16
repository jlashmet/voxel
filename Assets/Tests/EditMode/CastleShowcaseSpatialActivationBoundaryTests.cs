using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleShowcaseSpatialActivationBoundaryTests
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
        public void ShowcaseBuildAndInteractionsFollowCommittedSpatialCastle()
        {
            string showcase = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.cs"));
            string spatial = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string state = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatialState.cs"));
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleSpatialLayout.cs"));

            StringAssert.Contains("PreparePendingCastleSpatialPlan();", showcase);
            StringAssert.Contains("BeginPendingSpatialCastleBuild(", showcase);
            StringAssert.Contains("CommitPendingCastleSpatialPlan();", showcase);
            StringAssert.Contains("return ActiveCastleFrontGatePosition();", showcase);
            StringAssert.Contains("OpenActiveCastleFrontGate();", showcase);
            StringAssert.Contains("return ActiveCastleTrapdoorPosition();", showcase);
            StringAssert.Contains("OpenActiveCastleTrapdoor();", showcase);

            StringAssert.Contains("StructuresComposition.PlanCastleBuild(", spatial);
            StringAssert.Contains("CastleBuildBoundsResolver.Resolve(", spatial);
            StringAssert.Contains("in _world._pendingPlannedCastle", spatial);
            StringAssert.Contains("_castleSpatialProjection = _plannedCastle.Projection;", spatial);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.BuildPresentationLights(", spatial);

            StringAssert.Contains("PlannedCastleBuild _pendingPlannedCastle", state);
            StringAssert.Contains("PlannedCastleBuild _plannedCastle", state);
            StringAssert.Contains("CastleSpatialProjection _castleSpatialProjection", state);

            StringAssert.Contains("projection.PrimaryGateGeometry", layout);
            StringAssert.Contains("projection.TrapdoorCentre", layout);
            StringAssert.DoesNotContain("CastleLayout.FrontGateMinimum", layout,
                "Showcase interaction geometry must follow the realized spatial gate.");
        }
    }
}
