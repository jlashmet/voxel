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
            StringAssert.Contains("CastlePlannedPerimeterRealizer.Walls(", pipeline);
            StringAssert.DoesNotContain("CastlePerimeterRealizer.Walls(", pipeline,
                "Production spatial walls must not route through the compatibility perimeter facade.");
            StringAssert.Contains("CastlePlannedTowerRealizer.BuildAll(", pipeline);
            StringAssert.Contains("spatialPlan.Towers.Clone()", pipeline);
            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", pipeline);
            StringAssert.Contains("CastlePlannedCourtyardRealizer.Build(", pipeline);
            StringAssert.Contains("spatialPlan.CourtyardBuildings.Clone()", pipeline);
            StringAssert.Contains("CastleSpatialProjection.Create(", pipeline);
            StringAssert.DoesNotContain("CastleSpatialPlanner.Create(", pipeline);
            StringAssert.DoesNotContain("CastleLayoutPlanner.Create(", pipeline);

            string plannedWalls = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedPerimeterRealizer.cs"));
            StringAssert.Contains("CastleWallPlan walls", plannedWalls);
            StringAssert.Contains("VoxelWallRasterizer.FillSegment(", plannedWalls);
            StringAssert.DoesNotContain("plan.Seed", plannedWalls,
                "Planned wall realization must not choose authored variation from the castle seed.");
            StringAssert.DoesNotContain("CastleWallRecipe.Historical(", plannedWalls,
                "Production wall realization must consume the frozen wall plan.");

            string plannedTowers = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedTowerRealizer.cs"));
            StringAssert.Contains("tower.HeightVariation", plannedTowers);
            StringAssert.Contains("tower.HasRoof", plannedTowers);
            StringAssert.Contains("tower.Slits", plannedTowers);
            StringAssert.Contains("CastleTowerRealizer.BuildPlanned(", plannedTowers);
            StringAssert.DoesNotContain("CastleTowerSlitPlanner", plannedTowers,
                "Runtime must consume frozen tower slit phases rather than plan them.");
            StringAssert.DoesNotContain("CastleSeedPartition", plannedTowers,
                "Runtime must consume planned tower variation rather than choose it from a seed.");

            string courtyard = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedCourtyardRealizer.cs"));
            StringAssert.Contains("CastleCourtyardBuildingRealizer.BuildAll(", courtyard);
            StringAssert.DoesNotContain("new Random(", courtyard,
                "Planned courtyard realization must consume frozen paving/building choices.");

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
            StringAssert.Contains("CastleKeepRealizer.TryStep(", pipeline);
            StringAssert.Contains("CastleKeepAnnexRealizer.Build(ref _brush, in keepPlan)", pipeline);
            StringAssert.Contains("CastleDungeonRealizer.Build(ref _brush, in dungeonPlan)", pipeline);
            StringAssert.Contains("LegacyKeepCentreZOffset = 60", layout);
            StringAssert.Contains("CastleLayout.LegacyKeepCentreZOffset", projection);
            StringAssert.DoesNotContain("LegacyKeepCentreZOffset = 60", projection,
                "The migration offset must have one API-owned declaration, not a projection copy.");
            StringAssert.DoesNotContain("Structures.Runtime", projection);
        }

        [Test]
        public void SpatialDungeonConsumesPlannedNaturalCaveAndDecoration()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string plannedDungeon = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedDungeonRealizer.cs"));

            StringAssert.Contains("CavePlan _spatialCavePlan", pipeline);
            StringAssert.Contains("CastleCaveDecorationPlan _spatialCaveDecorationPlan", pipeline);
            StringAssert.Contains("CavePlanSnapshot.CloneValidated(spatialPlan.Cave)", pipeline);
            StringAssert.Contains("spatialPlan.CaveDecoration.Snapshot()", pipeline);
            StringAssert.Contains("CastlePlannedDungeonRealizer.Build(", pipeline);
            StringAssert.Contains("_spatialCaveDecorationPlan);", pipeline);
            StringAssert.Contains("CaveRealizer.Build(ref brush, cavePlan)", plannedDungeon);
            StringAssert.Contains(
                "CastlePlannedCaveDecorator.Build(ref brush, cavePlan, caveDecoration)",
                plannedDungeon);
            StringAssert.DoesNotContain("CastleCavePlanning.Create(", plannedDungeon);
            StringAssert.DoesNotContain("CastleCaveDecorationPlanner.Create(", plannedDungeon,
                "Runtime must consume the planned cave decoration instead of choosing it.");
        }

        [Test]
        public void KeepRealizerDelegatesExtractedKeepGeometry()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("CastleKeepShellRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepTurretRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepFloorRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepFenestrationRealizer.Build(", keep);
            StringAssert.Contains("CastleKeepFacadeRealizer.Build(", keep);
            StringAssert.DoesNotContain("private static void BuildShell", keep);
            StringAssert.DoesNotContain("private static void BuildCornerTurrets", keep);
            StringAssert.DoesNotContain("private static void BuildFloorsAndRooms", keep);
            StringAssert.DoesNotContain("private static void BuildWindows", keep);
            StringAssert.DoesNotContain("private static void BuildFacade", keep);
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
