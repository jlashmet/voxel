using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepWindowPlanningHandoffTests
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
        public void SpatialPipelineConsumesPlannedKeepWindowsWithoutReplanning()
        {
            string completion = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialPlanCompletion.cs"));
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string realizer = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepWindowRealizer.cs"));

            StringAssert.Contains("AttachKeepWindows(in plan", completion);
            StringAssert.Contains("CastleKeepWindowPlanner.Create(in plan)", completion);
            StringAssert.Contains("spatialPlan.KeepWindows", pipeline);
            StringAssert.Contains("_keepStage == 4", pipeline,
                "Planned windows must retain the historical keep substage cadence.");
            StringAssert.Contains("CastleKeepWindowRealizer.Build(", pipeline);
            StringAssert.Contains("worldKeepCentre", realizer);
            StringAssert.Contains("window.LocalOrigin", realizer);
            StringAssert.Contains("window.HasLitGlazing", realizer);

            StringAssert.DoesNotContain("CastleKeepWindowPlanner.Create(", pipeline,
                "Runtime must consume the completed aperture list instead of planning it.");
            StringAssert.DoesNotContain("CastleKeepWindowPlanner.Create(", realizer,
                "The window realizer must not decide which apertures exist.");
            StringAssert.DoesNotContain("LegacyKeepCentreZOffset", realizer,
                "Planned window realization must stay in semantic keep-centre coordinates.");
        }
    }
}
