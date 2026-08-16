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
        public void SpatialPipelineDelegatesSiteToDedicatedPlannedRealizer()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string legacy = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleSiteRealizer.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedSiteRealizer.cs"));

            StringAssert.Contains("CastleApproachFrame.FromGate", pipeline);
            StringAssert.Contains("CastleSiteRealizer.StepPlanned(", pipeline);
            StringAssert.Contains("CastlePlannedSiteRealizer.Step(", legacy,
                "The stable pipeline entry point should bridge directly to the dedicated planned realizer.");

            StringAssert.Contains("sitePlan.ShouldGrassCap", planned);
            StringAssert.Contains("CastleSiteGeometryPlan geometry", planned);
            StringAssert.Contains("CastleRiverCrossSectionPlan crossSection", planned);
            StringAssert.Contains("approach.LocalPoint(", planned);
            StringAssert.Contains("crossSection.OutsideTerraceDrop", planned);
            StringAssert.Contains("crossSection.BedDepth", planned);
            StringAssert.DoesNotContain("new Random(", planned);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive(", planned);
            StringAssert.DoesNotContain("plan.Seed", planned);

            StringAssert.Contains("CastleSeedPartition.Derive(", legacy,
                "The compatibility site path must retain its historical authored-randomness recipe.");
            StringAssert.DoesNotContain("CastleRiverCrossSectionPlan crossSection", legacy,
                "Planned river cross-section policy must not remain mixed into the compatibility realizer.");
        }
    }
}
