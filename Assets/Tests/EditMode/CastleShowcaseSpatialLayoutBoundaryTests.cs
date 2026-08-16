using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleShowcaseSpatialLayoutBoundaryTests
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
        public void ShowcaseSpatialLayoutConsumesSharedProjectionForInteractionAndPresentation()
        {
            string helper = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "Showcase",
                "ShowcaseCastleSpatialLayout.cs"));

            StringAssert.Contains("CastleSpatialProjection", helper);
            StringAssert.Contains("projection.PrimaryGateGeometry.InteractionPointVoxels", helper);
            StringAssert.Contains("geometry.ContainsArchVoxel", helper);
            StringAssert.Contains("geometry.WorldVoxel", helper);
            StringAssert.Contains("projection.TrapdoorCentre", helper);
            StringAssert.Contains("CastlePlan plan = projection.KeepPlan", helper);
            StringAssert.Contains("projection.ChapelBellTowerCentre", helper);

            StringAssert.DoesNotContain("CastleLayout.FrontGateMinimum", helper);
            StringAssert.DoesNotContain("CastleSpatialPlanner.Create", helper);
            StringAssert.DoesNotContain("CastleLayoutPlanner.Create", helper);
        }
    }
}
