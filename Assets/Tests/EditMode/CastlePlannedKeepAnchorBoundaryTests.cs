using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedKeepAnchorBoundaryTests
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
        public void PlannedKeepComponentsUseSharedSpatialProjectionForWorldCentre()
        {
            string runtime = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string turrets = File.ReadAllText(Path.Combine(
                runtime, "CastlePlannedKeepTurretRealizer.cs"));
            string exterior = File.ReadAllText(Path.Combine(
                runtime, "CastlePlannedKeepExteriorRealizer.cs"));

            StringAssert.Contains("CastleSpatialProjection.ActualKeepCentre(", turrets);
            StringAssert.Contains("CastleSpatialProjection.ActualKeepCentre(", exterior);
            StringAssert.DoesNotContain("LegacyKeepCentreZOffset", turrets);
            StringAssert.DoesNotContain("LegacyKeepCentreZOffset", exterior);
        }
    }
}
