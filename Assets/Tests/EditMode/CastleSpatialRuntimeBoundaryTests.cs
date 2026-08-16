using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleSpatialRuntimeBoundaryTests
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
        public void SpatialPipelineSnapshotsMutablePlanningPayloadsBeforeVoxelMutation()
        {
            string pipeline = ReadRuntime("CastleBuildPipeline.cs");

            StringAssert.Contains("(int2[])spatialPlan.OuterWardVertices.Clone()", pipeline);
            StringAssert.Contains("(int2[])spatialPlan.InnerWardVertices.Clone()", pipeline);
            StringAssert.Contains("(CastleTowerPlacementSpec[])spatialPlan.Towers.Clone()", pipeline);
            StringAssert.Contains("(CastleTowerPlacementSpec[])spatialPlan.InnerTowers.Clone()", pipeline);
            StringAssert.Contains("(CastleCourtyardBuildingSpec[])spatialPlan.CourtyardBuildings.Clone()", pipeline);
            StringAssert.Contains("(CastleKeepFloorPlan[])spatialPlan.KeepFloors.Clone()", pipeline);
            StringAssert.Contains("(CastleKeepWindowSpec[])windows.Clone()", pipeline);
            StringAssert.Contains("DungeonPlanSnapshot.CloneValidated(spatialPlan.Dungeon)", pipeline);
            StringAssert.Contains("CavePlanSnapshot.CloneValidated(spatialPlan.Cave)", pipeline);
            StringAssert.Contains("spatialPlan.CaveDecoration.Snapshot()", pipeline);
            StringAssert.Contains("CastleLandscapePlanSnapshot.CloneValidated(spatialPlan.Landscape)", pipeline);
        }

        [Test]
        public void DedicatedSpatialRealizersDoNotRerollPlannerChoices()
        {
            string[] files =
            {
                "CastlePlannedTowerRealizer.cs",
                "CastleInnerWardTowerRealizer.cs",
                "CastleCourtyardBuildingRealizer.cs",
                "CastleKeepCirculationRealizer.cs",
                "CastleKeepWindowRealizer.cs",
                "CastlePlannedKeepAnnexRealizer.cs",
                "CastlePlannedDungeonRealizer.cs",
                "CastlePlannedCaveDecorator.cs",
                "CastlePlannedLandscapeRealizer.cs",
            };

            foreach (string file in files)
            {
                string source = ReadRuntime(file);
                StringAssert.DoesNotContain("Unity.Mathematics.Random", source,
                    $"{file} must consume planner-owned variation rather than create an RNG.");
                StringAssert.DoesNotContain("new Random(", source,
                    $"{file} must not reroll planner-owned choices during realization.");
                StringAssert.DoesNotContain("CastleSeedPartition", source,
                    $"{file} must not derive new semantic seeds during realization.");
            }
        }

        private static string ReadRuntime(string file) =>
            File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", file));
    }
}
