using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleShowcaseSpatialBoundaryTests
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
        public void ShowcaseBuildLifecycleUsesRuntimeReadySpatialBundle()
        {
            string world = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.cs"));
            string spatialWorld = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string spatialState = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatialState.cs"));

            StringAssert.Contains("PreparePendingCastleSpatialPlan();", world);
            StringAssert.Contains("BeginPendingSpatialCastleBuild(", world);
            StringAssert.Contains("CommitPendingCastleSpatialPlan();", world);

            StringAssert.Contains("StructuresComposition.PlanCastleBuild(", spatialWorld);
            StringAssert.Contains("CastleBuildBoundsResolver.Resolve(", spatialWorld);
            StringAssert.Contains("ShowcaseCastleDependencyRegionRange.FromCastleBounds", spatialWorld);
            StringAssert.Contains("StructuresComposition.BeginCastleBuild(", spatialWorld);
            StringAssert.Contains("in _world._pendingPlannedCastle", spatialWorld);

            StringAssert.Contains("PlannedCastleBuild _pendingPlannedCastle", spatialState);
            StringAssert.Contains("PlannedCastleBuild _plannedCastle", spatialState);
            StringAssert.Contains("CastleSpatialProjection _castleSpatialProjection", spatialState);
        }

        [Test]
        public void ShowcaseInteractionAndPresentationUseRealizedSpatialProjection()
        {
            string spatialWorld = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpatial.cs"));
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleSpatialLayout.cs"));
            string spawn = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseWorld.CastleSpawn.cs"));

            StringAssert.Contains("_plannedCastle.Projection", spatialWorld);
            StringAssert.Contains("PrimaryGateLeafVoxels", spatialWorld);
            StringAssert.Contains("TrapdoorCentre", spatialWorld);
            StringAssert.Contains("_plannedCastle.Spatial.Dungeon", spatialWorld);

            StringAssert.Contains("projection.PrimaryGateGeometry", layout);
            StringAssert.Contains("projection.TrapdoorCentre", layout);
            StringAssert.Contains("DungeonPlan dungeon", layout);
            StringAssert.DoesNotContain("CastleLayout.FrontGateMinimum", layout,
                "Spatial interaction must not fall back to the historical axis-aligned gate.");

            StringAssert.Contains("ShowcaseCastleSpawnPlanner.PlanColumn(", spawn);
            StringAssert.Contains("projection.KeepCentreWorld", spawn);
            StringAssert.DoesNotContain("const int cz = -220", spawn,
                "Spatial spawn must follow the planned approach rather than the old -Z axis.");
        }
    }
}
