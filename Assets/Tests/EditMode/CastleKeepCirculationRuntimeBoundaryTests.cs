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
        public void SpatialKeepRealizationConsumesPlannedCirculationInDedicatedRealizer()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string circulation = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepCirculationRealizer.cs"));
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("_keepCirculation = circulation", pipeline);
            StringAssert.Contains("CastleKeepCirculationRealizer.Build(", pipeline);
            StringAssert.Contains("_worldKeepCentre, in _keepCirculation", pipeline);

            StringAssert.Contains("CastleKeepCirculationPlan circulation", circulation);
            StringAssert.Contains("CastleKeepCirculationPlanner.TryValidate", circulation);
            StringAssert.Contains("circulation.EntranceCentre", circulation);
            StringAssert.Contains("circulation.GrandStairOrigin", circulation);
            StringAssert.Contains("circulation.SpiralStairCentre", circulation);
            StringAssert.DoesNotContain("CastleKeepCirculationPlanner.Create(", circulation,
                "Runtime may validate supplied circulation, but it must never plan circulation itself.");

            StringAssert.DoesNotContain("CastleKeepCirculationRealizer", keep,
                "Core keep shell/room realization should stay separate from planned circulation.");
        }
    }
}
