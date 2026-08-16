using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleRuntimePlanningBoundaryTests
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
        public void SpatialKeepFurnishingConsumesPlannedRoomAccents()
        {
            string keep = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));
            string furnishing = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleRoomFurnisher.cs"));

            StringAssert.Contains("CastleRoomFurnisher.FurnishPlanned(", keep);
            StringAssert.Contains("roomPlan.Accents", keep);
            StringAssert.DoesNotContain("RoomFurnishingPlanSeed", keep,
                "Spatial realization must consume the frozen accent plan rather than re-encode its seed.");
            StringAssert.Contains("CastleRoomFurnisher.Furnish(ref brush, in plan, min, size, y, f)", keep,
                "Compatibility builds must retain the historical per-floor RNG recipe.");

            StringAssert.Contains("FurnishPlannedAccents", furnishing);
            StringAssert.Contains("CastleRoomAccentPlan accents", furnishing);
            StringAssert.Contains("FurnishLegacyAccents", furnishing,
                "Legacy RNG furnishing remains isolated to the compatibility path.");
        }

        [Test]
        public void SpatialKeepTurretsConsumeFrozenRoofVariation()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));
            string planned = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedKeepTurretRealizer.cs"));
            string legacy = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleKeepRealizer.cs"));

            StringAssert.Contains("CastlePlannedKeepTurretRealizer.BuildAll(", pipeline);
            StringAssert.Contains("_keepTurrets = topology.KeepTurrets.Snapshot()", pipeline);
            StringAssert.DoesNotContain("CastleSeedPartition.Derive(", planned);
            StringAssert.Contains("CastleSeedPartition.Derive(", legacy,
                "Compatibility keep realization must retain the historical roof choice.");
        }

        [Test]
        public void SpatialPipelineConsumesPlannedSurfaceAndLandscapeData()
        {
            string pipeline = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastleBuildPipeline.cs"));

            StringAssert.Contains("_sitePlan = spatialPlan.Topology.Site", pipeline);
            StringAssert.Contains("CastleSiteRealizer.StepPlanned(", pipeline);
            StringAssert.Contains("in _sitePlan", pipeline);
            StringAssert.Contains("CastleCourtyardRealizer.BuildPlanned(", pipeline);
            StringAssert.Contains("CastleLandscapePlanSnapshot.CloneValidated", pipeline);
            StringAssert.Contains("CastlePlannedLandscapeRealizer.Build(", pipeline);
            StringAssert.DoesNotContain("CastleSpatialLandscapeRealizer.Build(", pipeline);
        }

        [Test]
        public void DedicatedSpatialRealizersDoNotOwnAuthoredRandomness()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] realizationFiles =
            {
                "CastleBuildPipeline.cs",
                "CastlePerimeterRealizer.cs",
                "CastlePlannedTowerRealizer.cs",
                "CastleInnerWardTowerRealizer.cs",
                "CastlePlannedGatehouseRealizer.cs",
                "CastleCourtyardBuildingRealizer.cs",
                "CastleKeepCirculationRealizer.cs",
                "CastleKeepWindowRealizer.cs",
                "CastlePlannedKeepWindowRealizer.cs",
                "CastlePlannedKeepTurretRealizer.cs",
                "CastlePlannedKeepExteriorRealizer.cs",
                "CastlePlannedKeepAnnexRealizer.cs",
                "CastlePlannedDungeonRealizer.cs",
                "CastlePlannedLandscapeRealizer.cs",
            };

            for (int i = 0; i < realizationFiles.Length; i++)
            {
                string source = File.ReadAllText(Path.Combine(runtimeDirectory, realizationFiles[i]));
                StringAssert.DoesNotContain("new Random(", source,
                    $"{realizationFiles[i]} must consume planned variation rather than create an RNG.");
                StringAssert.DoesNotContain("CastleSeedPartition.Derive(", source,
                    $"{realizationFiles[i]} must not derive authored seeds during realization.");
            }
        }

        [Test]
        public void StructuresRuntimeDoesNotInvokePlanningEntryPoints()
        {
            string runtimeDirectory = Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime");
            string[] forbiddenCalls =
            {
                "CastleLayoutPlanner.Create(",
                "CastleSpatialPlanner.Create(",
                "CastleSpatialPlanCompletion.",
                "CastleSitePlanner.Create(",
                "CastleInnerWardTowerPlanner.Create(",
                "CastleGatehousePlanner.Create(",
                "CastleKeepInteriorPlanner.Create(",
                "CastleKeepTurretPlanner.Create(",
                "CastleKeepCirculationPlanner.Create(",
                "CastleKeepAnnexPlanner.Create(",
                "CastleKeepWindowPlanner.Create(",
                "CastleRoomAccentPlanner.Create(",
                "CastleCourtyardPlanner.Create(",
                "CastleCourtyardBuildingPlanner.Create(",
                "CastleCourtyardBuildingPlacementGeometry.Plan(",
                "CastleAccessRoutePlanner.Create(",
                "CastleDungeonPlanning.Create(",
                "CastleCavePlanning.Create(",
                "CastleCaveDecorationPlanner.Create(",
                "CastleLandscapePlanner.Create(",
                "DungeonPlanner.Create(",
                "CavePlanner.Create(",
            };

            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                for (int i = 0; i < forbiddenCalls.Length; i++)
                {
                    StringAssert.DoesNotContain(
                        forbiddenCalls[i],
                        source,
                        $"{Path.GetFileName(file)} must realize completed plan data, not call {forbiddenCalls[i]}.");
                }
            }
        }
    }
}
