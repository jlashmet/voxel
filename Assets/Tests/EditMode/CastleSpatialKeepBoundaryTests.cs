using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialKeepBoundaryTests
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
        public void SpatialKeepUsesDedicatedCoordinatorWithoutCompatibilityDispatch()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepRealizer.cs"));

            StringAssert.Contains("CastlePlannedKeepRealizer.Step(", pipeline);
            StringAssert.Contains("CastleKeepRealizer.TryStep(", pipeline,
                "Compatibility builds should retain the historical staged dispatcher.");

            StringAssert.Contains("CastleKeepShellRealizer.Build(", planned);
            StringAssert.Contains("CastlePlannedKeepTurretRealizer.BuildAll(", planned);
            StringAssert.Contains("CastleKeepFloorRealizer.Build(", planned);
            StringAssert.Contains("CastleKeepCirculationRealizer.Build(", planned);
            StringAssert.Contains("CastlePlannedKeepWindowRealizer.BuildAll(", planned);
            StringAssert.Contains("CastlePlannedKeepExteriorRealizer.Build(", planned);
            StringAssert.Contains("CastlePlannedKeepAnnexRealizer.Build(", planned);
            StringAssert.DoesNotContain("CastleKeepRealizer", planned,
                "Spatial keep sequencing must not route through the compatibility coordinator.");
        }
    }
}
