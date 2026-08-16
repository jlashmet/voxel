using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleKeepCirculationRuntimeBoundaryTests
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
        public void SpatialKeepRealizationConsumesPlannedCirculationAnchors()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("_keepCirculation = spatialPlan.KeepCirculation", pipeline);
            StringAssert.Contains("in _keepCirculation, ref _keepStage", pipeline);

            StringAssert.Contains("CastleKeepCirculationPlan circulation", keep);
            StringAssert.Contains("CastleKeepCirculationPlanner.TryValidate", keep);
            StringAssert.Contains("circulation.EntranceCentre", keep);
            StringAssert.Contains("circulation.GrandStairOrigin", keep);
            StringAssert.Contains("circulation.SpiralStairCentre", keep);
            StringAssert.DoesNotContain("CastleKeepCirculationPlanner.Create(", keep,
                "Runtime may validate supplied circulation, but it must never plan circulation itself.");
        }
    }
}
