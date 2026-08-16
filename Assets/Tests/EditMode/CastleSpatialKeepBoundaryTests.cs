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
        public void SpatialKeepUsesDedicatedRealizersWithoutCompatibilityDispatch()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("CastleKeepShellRealizer.Build(", pipeline);
            StringAssert.Contains("CastlePlannedKeepTurretRealizer.BuildAll(", pipeline);
            StringAssert.Contains("CastleKeepFloorRealizer.Build(", pipeline);
            StringAssert.Contains("CastleKeepCirculationRealizer.Build(", pipeline);
            StringAssert.Contains("CastleKeepWindowRealizer.Build(", pipeline);
            StringAssert.Contains("CastlePlannedKeepExteriorRealizer.Build(", pipeline);
            StringAssert.Contains("CastlePlannedKeepAnnexRealizer.Build(", pipeline);

            StringAssert.DoesNotContain("? CastleKeepRealizer.TryStep(", pipeline,
                "Spatial keeps must not route through the compatibility dispatcher.");
            StringAssert.Contains("CastleKeepRealizer.TryStep(", pipeline,
                "Compatibility builds should retain the historical staged dispatcher.");
        }
    }
}
