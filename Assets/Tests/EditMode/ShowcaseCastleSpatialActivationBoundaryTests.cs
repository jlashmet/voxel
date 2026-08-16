using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastleSpatialActivationBoundaryTests
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
        public void ShowcaseCarriesRuntimeReadySpatialPlanThroughBuildAndInteraction()
        {
            string showcaseDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase");
            string world = File.ReadAllText(Path.Combine(showcaseDirectory, "ShowcaseWorld.cs"));
            string spatial = File.ReadAllText(Path.Combine(
                showcaseDirectory, "ShowcaseWorld.CastleSpatial.cs"));
            string state = File.ReadAllText(Path.Combine(
                showcaseDirectory, "ShowcaseWorld.CastleSpatialState.cs"));
            string layout = File.ReadAllText(Path.Combine(
                showcaseDirectory, "ShowcaseCastleSpatialLayout.cs"));

            StringAssert.Contains("PreparePendingCastleSpatialPlan();", world);
            StringAssert.Contains("BeginPendingSpatialCastleBuild(", world);
            StringAssert.Contains("CommitPendingCastleSpatialPlan();", world);
            StringAssert.Contains("ActiveCastleFrontGatePosition();", world);
            StringAssert.Contains("OpenActiveCastleFrontGate();", world);
            StringAssert.Contains("ActiveCastleTrapdoorPosition();", world);
            StringAssert.Contains("OpenActiveCastleTrapdoor();", world);
            StringAssert.DoesNotContain("CastleLayout.FrontGateMinimum(in _castlePlan)", world);
            StringAssert.DoesNotContain("CastleLayout.TrapdoorCentre(in _castlePlan)", world);
            StringAssert.DoesNotContain("BuildCastlePresentationLights(in plan);", world);

            StringAssert.Contains("StructuresComposition.PlanCastleBuild(", spatial);
            StringAssert.Contains("in _world._pendingPlannedCastle", spatial);
            StringAssert.Contains("_castleSpatialProjection = _plannedCastle.Projection", spatial);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.PrimaryGateLeafVoxels(", spatial);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.TrapdoorCentre(", spatial);
            StringAssert.Contains("ShowcaseCastleSpatialLayout.BuildPresentationLights(", spatial);

            StringAssert.Contains("PlannedCastleBuild _pendingPlannedCastle", state);
            StringAssert.Contains("PlannedCastleBuild _plannedCastle", state);
            StringAssert.Contains("CastleSpatialProjection _castleSpatialProjection", state);

            StringAssert.Contains("projection.PrimaryGateGeometry", layout);
            StringAssert.Contains("projection.TrapdoorCentre", layout);
            StringAssert.Contains("DungeonRoomPurpose", layout);
            StringAssert.DoesNotContain("CastleLayout.FrontGateMinimum", layout);
        }
    }
}
