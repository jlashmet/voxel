using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingFootprintTests
    {
        private const int BuildingClearance = 16;

        [Test]
        public void PlannedBuildingsStayFullyInsideWardAndClearPlannedTowers()
        {
            int checkedBuildings = 0;

            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan plan = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);

                for (int buildingIndex = 0;
                     buildingIndex < spatial.CourtyardBuildings.Length;
                     buildingIndex++)
                {
                    CastleCourtyardBuildingSpec building =
                        spatial.CourtyardBuildings[buildingIndex];
                    int2[] footprint =
                    {
                        building.FootprintCorner(0),
                        building.FootprintCorner(1),
                        building.FootprintCorner(2),
                        building.FootprintCorner(3),
                    };

                    Assert.IsTrue(
                        CastlePolygonGeometry.ContainsPolygon(
                            spatial.OuterWardVertices, footprint),
                        $"seed {seed}, building {buildingIndex}: footprint crosses/touches outer ward");

                    if (spatial.InnerWardVertices != null &&
                        spatial.InnerWardVertices.Length >= 3)
                    {
                        Assert.IsFalse(
                            CastlePolygonGeometry.PolygonsOverlapOrTouch(
                                footprint, spatial.InnerWardVertices),
                            $"seed {seed}, building {buildingIndex}: footprint overlaps/touches inner ward");
                    }

                    int outerClearance = plan.TowerRadius + BuildingClearance;
                    long outerClearanceSq = (long)outerClearance * outerClearance;
                    for (int towerIndex = 0; towerIndex < spatial.Towers.Length; towerIndex++)
                    {
                        long distanceSq = PointDistanceSquared(
                            in building, spatial.Towers[towerIndex].Centre);
                        Assert.GreaterOrEqual(
                            distanceSq,
                            outerClearanceSq,
                            $"seed {seed}, building {buildingIndex}: overlaps outer tower {towerIndex}");
                    }

                    int innerClearance = CastleInnerWardTowerPlanner.Radius(in plan)
                                       + BuildingClearance;
                    long innerClearanceSq = (long)innerClearance * innerClearance;
                    for (int towerIndex = 0; towerIndex < spatial.InnerTowers.Length; towerIndex++)
                    {
                        long distanceSq = PointDistanceSquared(
                            in building, spatial.InnerTowers[towerIndex].Centre);
                        Assert.GreaterOrEqual(
                            distanceSq,
                            innerClearanceSq,
                            $"seed {seed}, building {buildingIndex}: overlaps inner tower {towerIndex}");
                    }

                    checkedBuildings++;
                }
            }

            Assert.Greater(checkedBuildings, 0,
                "Seed corpus produced no courtyard buildings to validate.");
        }

        [Test]
        public void PlannerUsesExactPolygonContainmentRatherThanSampledCorners()
        {
            string planner = File.ReadAllText(Path.Combine(
                RepoRoot,
                "Assets",
                "VoxelEngine",
                "Structures",
                "Api",
                "CastleCourtyardBuildingPlanner.cs"));

            StringAssert.Contains("CastlePolygonGeometry.ContainsPolygon(", planner);
            StringAssert.Contains("CastlePolygonGeometry.PolygonsOverlapOrTouch(", planner);
            StringAssert.DoesNotContain("FootprintSamples", planner);
        }

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

        private static long PointDistanceSquared(
            in CastleCourtyardBuildingSpec building,
            int2 point)
        {
            float2 tangent = math.normalizesafe(building.Tangent, new float2(1f, 0f));
            float2 inward = math.normalizesafe(building.Inward, new float2(0f, 1f));
            float2 delta = new float2(
                point.x - building.Centre.x,
                point.y - building.Centre.y);
            float along = math.max(
                0f,
                math.abs(math.dot(delta, tangent)) - building.Width * 0.5f);
            float depth = math.max(
                0f,
                math.abs(math.dot(delta, inward)) - building.Depth * 0.5f);
            return (long)math.round(along * along + depth * depth);
        }
    }
}
