using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepProjectionBoundaryTests
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
        public void RuntimeKeepAdapterDelegatesLegacyOffsetToApiProjection()
        {
            string adapter = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepPlacementAdapter.cs"));
            string projection = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialProjection.cs"));
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastlePlan.cs"));

            StringAssert.Contains("CastleSpatialProjection.ProjectKeepPlan", adapter);
            StringAssert.Contains("CastleSpatialProjection.ActualKeepCentre", adapter);
            StringAssert.DoesNotContain("LegacyKeepCentreZOffset = 60", adapter,
                "Runtime must not own a second copy of the legacy keep anchor.");
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);
            StringAssert.Contains("LegacyKeepCentreZOffset = 60", layout);
        }
    }
}
