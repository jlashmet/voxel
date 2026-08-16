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
        public void CompatibilityKeepAndSpatialProjectionOwnLegacyOffsetAtTheirBoundary()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepRealizer.cs"));
            string projection = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialProjection.cs"));
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastlePlan.cs"));

            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", keep);
            StringAssert.DoesNotContain("plan.Centre.z - hz + 60", keep,
                "The compatibility keep coordinator must not own a second copy of the anchor.");

            StringAssert.Contains("CastleSpatialProjection.KeepMinimum(", planned);
            StringAssert.Contains("CastleSpatialProjection.KeepSize(", planned);
            StringAssert.DoesNotContain("CastleLayout.LegacyKeepCentreZOffset", planned,
                "Spatial keep realization must consume projected bounds without knowing the compatibility offset.");
            StringAssert.DoesNotContain("keepPlan.Centre.z - halfZ + 60", planned,
                "The spatial keep coordinator must not reconstruct the compatibility anchor.");

            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);
            StringAssert.Contains("LegacyKeepCentreZOffset = 60", layout);
        }
    }
}
