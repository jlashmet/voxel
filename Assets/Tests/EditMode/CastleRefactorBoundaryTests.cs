using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRefactorBoundaryTests
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
        public void CompositionRoutesCastleBuildsThroughIncrementalPipeline()
        {
            string composition = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "StructuresComposition.cs"));

            StringAssert.Contains("new CastleBuildPipeline(", composition);
            StringAssert.DoesNotContain("CastleBuilder.BeginBuild(", composition);
        }

        [Test]
        public void PipelineOwnsMigratedStagesBeforeLegacyFallback()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("CastleSiteRealizer.Step(", pipeline);
            StringAssert.Contains("CastleFortificationRealizer.CurtainWalls(", pipeline);
            StringAssert.Contains("CastleFortificationRealizer.CornerTowers(", pipeline);
            StringAssert.Contains("CastleFortificationRealizer.Gatehouse(", pipeline);
            StringAssert.Contains("CastleCourtyardRealizer.Build(", pipeline);
            StringAssert.Contains("CastleKeepRealizer.TryStep(", pipeline);
            StringAssert.Contains("CastleBuilder.StepBuild(ref _legacy)", pipeline,
                "The final keep roof/annex substage, dungeon, and landscape remain on the migration fallback.");
        }

        [Test]
        public void KeepRealizerUsesReusableTowerAndRoomComponents()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("CastleTowerRealizer.Build(", keep);
            StringAssert.Contains("CastleRoomFurnisher.Furnish(", keep);
            StringAssert.DoesNotContain("BedroomBuilder", keep);
            StringAssert.DoesNotContain("LibraryBuilder", keep);
        }
    }
}
