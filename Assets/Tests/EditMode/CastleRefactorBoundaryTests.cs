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
        public void SpatialPlanningStaysOutsideRuntimeWhilePipelineConsumesValidatedGeometry()
        {
            string planning = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api", "CastleSpatialPlanner.cs"));
            StringAssert.DoesNotContain("VoxelBrush", planning);
            StringAssert.DoesNotContain("Structures.Runtime", planning);

            string composition = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Composition", "StructuresComposition.cs"));
            StringAssert.Contains("CastleLayoutPlanner.Create(plan.Seed)", composition);
            StringAssert.Contains("CastleSpatialPlanner.Create(in plan, in topology)", composition);
            StringAssert.Contains("CastleSpatialPlanValidator.TryValidate(", composition);

            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            StringAssert.Contains("CastleSpatialPlan spatialPlan", pipeline);
            StringAssert.Contains(
                "CastleBuildPreflight.EvaluateRuntimeReady(",
                pipeline);
            StringAssert.Contains("CastlePerimeterRealizer.Walls(", pipeline);
            StringAssert.Contains("CastlePlannedTowerRealizer.BuildAll(", pipeline);
            StringAssert.Contains("spatialPlan.Towers.Clone()", pipeline);
            StringAssert.Contains("CastlePerimeterRealizer.Gatehouse(", pipeline);
            StringAssert.Contains("CastleCourtyardRealizer.BuildPlanned(", pipeline);
            StringAssert.Contains("spatialPlan.CourtyardBuildings.Clone()", pipeline);
            StringAssert.Contains("CastleSpatialProjection.Create(", pipeline);
            StringAssert.DoesNotContain("CastleSpatialPlanner.Create(", pipeline);
            StringAssert.DoesNotContain("CastleLayoutPlanner.Create(", pipeline);

            string plannedTowers = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedTowerRealizer.cs"));
            StringAssert.Contains("tower.HeightVariation", plannedTowers);
            StringAssert.Contains("tower.HasRoof", plannedTowers);
            StringAssert.DoesNotContain("CastleSeedPartition", plannedTowers,
                "Runtime must consume planned tower variation rather than choose it from a seed.");

            string courtyard = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleCourtyardRealizer.cs"));
            StringAssert.Contains("CastleCourtyardBuildingRealizer.BuildAll(", courtyard);

            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string runtimeSource = File.ReadAllText(file);
                StringAssert.DoesNotContain("CastleSpatialPlanner.Create(", runtimeSource,
                    $"{Path.GetFileName(file)} must consume planned geometry rather than re-plan it.");
                StringAssert.DoesNotContain("CastleLayoutPlanner.Create(", runtimeSource,
                    $"{Path.GetFileName(file)} must not choose semantic topology during realization.");
                StringAssert.DoesNotContain("CastleCourtyardBuildingPlacementGeometry.Plan(", runtimeSource,
                    $"{Path.GetFileName(file)} must consume planned courtyard buildings rather than place them.");
                StringAssert.DoesNotContain("CastleCourtyardBuildingPlanner.Create(", runtimeSource,
                    $"{Path.GetFileName(file)} must not invoke courtyard planning during realization.");
                StringAssert.DoesNotContain("CastleCavePlanning.Create(", runtimeSource,
                    $"{Path.GetFileName(file)} must consume CavePlan rather than plan natural space.");
            }
        }

        [Test]
        public void SpatialProjectionFeedsBothKeepAndDungeonStages()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string projection = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleSpatialProjection.cs"));
            string layout = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Api",
                "CastlePlan.cs"));

            StringAssert.Contains("CastleSpatialProjection.Create(", pipeline);
            StringAssert.Contains("CastleKeepRealizer.TryStep(ref _brush, in keepPlan", pipeline);
            StringAssert.Contains("CastleKeepAnnexRealizer.Build(ref _brush, in keepPlan)", pipeline);
            StringAssert.Contains("CastleDungeonRealizer.Build(ref _brush, in dungeonPlan)", pipeline);
            StringAssert.Contains("LegacyKeepCentreZOffset = 60", layout);
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);
            StringAssert.DoesNotContain("LegacyKeepCentreZOffset = 60", projection,
                "The migration offset must have one API-owned declaration, not a projection copy.");
            StringAssert.DoesNotContain("Structures.Runtime", projection);
        }

        [Test]
        public void SpatialDungeonConsumesPlannedNaturalCave()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string plannedDungeon = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedDungeonRealizer.cs"));

            StringAssert.Contains("CavePlan _spatialCavePlan", pipeline);
            StringAssert.Contains("CavePlanSnapshot.CloneValidated(spatialPlan.Cave)", pipeline);
            StringAssert.Contains(
                "CastlePlannedDungeonRealizer.Build(\n                            ref _brush, _spatialDungeonPlan, _spatialCavePlan)",
                pipeline);
            StringAssert.Contains("CaveRealizer.Build(ref brush, cavePlan)", plannedDungeon);
            StringAssert.DoesNotContain("CastleCavePlanning.Create(", plannedDungeon);
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
