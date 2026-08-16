using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialSiteBoundaryTests
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
        public void SpatialPipelineOrientsSiteApproachFromPrimaryGate()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string site = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleSiteRealizer.cs"));

            StringAssert.Contains("CastleApproachFrame.FromGate", pipeline);
            StringAssert.Contains("CastleSiteRealizer.StepPlanned(", pipeline);
            StringAssert.Contains("LowerRiverGorgePlanned(", site);
            StringAssert.Contains("approach.LocalPoint(", site);
            StringAssert.Contains("LowerRiverGorgeLegacy(", site,
                "The compatibility build must retain its historical site path.");
        }
    }
}
