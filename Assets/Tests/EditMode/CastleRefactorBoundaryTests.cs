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
        public void LegacyCastleBuilderIsOnlyCompatibilityFacade()
        {
            string builder = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", "CastleBuilder.cs"));

            StringAssert.Contains("CastlePlanner.Create(centre, seed)", builder);
            StringAssert.Contains("CastleBuildPreflight.EstimateWrites(in plan)", builder);
            StringAssert.Contains("new CastleBuildPipeline(", builder);
            StringAssert.Contains("return build.Pipeline.Step();", builder);

            StringAssert.DoesNotContain("TerrainQuery", builder);
            StringAssert.DoesNotContain("TerrainSampler", builder);
            StringAssert.DoesNotContain("private static void CurtainWalls", builder);
            StringAssert.DoesNotContain("private static void Dungeon", builder);
            StringAssert.DoesNotContain("private static void LandscapeDetails", builder);
        }

        [Test]
        public void PipelineOwnsEveryCastleRealizationStage()
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
            StringAssert.Contains("CastleKeepAnnexRealizer.Build(", pipeline);
            StringAssert.Contains("CastleDungeonRealizer.Build(", pipeline);
            StringAssert.Contains("CastleLandscapeRealizer.Build(", pipeline);

            StringAssert.DoesNotContain("CastleBuilder.StepBuild", pipeline);
            StringAssert.DoesNotContain("CastleBuilder.IncrementalBuild", pipeline);
            StringAssert.DoesNotContain("CastleBuilder.BeginBuild", pipeline);
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

        [Test]
        public void KeepAnnexesAreSeparatedFromCoreKeepRealization()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string annex = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepAnnexRealizer.cs"));

            StringAssert.DoesNotContain("GreatHallWing", keep);
            StringAssert.DoesNotContain("ChapelWing", keep);
            StringAssert.Contains("BuildGreatHallWing", annex);
            StringAssert.Contains("BuildChapelWing", annex);
            StringAssert.Contains("BuildChapelBellTower", annex);
        }

        [Test]
        public void DungeonDelegatesNaturalGeometryToCaveRealizer()
        {
            string dungeon = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleDungeonRealizer.cs"));
            string cave = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleCaveRealizer.cs"));

            StringAssert.Contains("CastleCaveRealizer.Build(", dungeon);
            StringAssert.DoesNotContain("CarveCavernEllipsoid", dungeon);
            StringAssert.Contains("CarveCavernEllipsoid", cave);
        }
    }
}
