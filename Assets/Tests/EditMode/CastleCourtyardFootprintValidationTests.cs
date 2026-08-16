using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardFootprintValidationTests
    {
        [Test]
        public void CanonicalSpatialBuildingsHaveExactWardSafeFootprints()
        {
            int plannedBuildings = 0;

            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);

                for (int i = 0; i < spatial.CourtyardBuildings.Length; i++)
                {
                    CastleCourtyardBuildingSpec building = spatial.CourtyardBuildings[i];
                    int2[] footprint =
                    {
                        building.FootprintCorner(0),
                        building.FootprintCorner(1),
                        building.FootprintCorner(2),
                        building.FootprintCorner(3),
                    };

                    Assert.AreEqual(i, building.Id,
                        $"seed {seed}: filtered courtyard building IDs must remain stable and compact");
                    Assert.IsTrue(
                        CastlePolygonGeometry.ContainsPolygon(spatial.OuterWardVertices, footprint),
                        $"seed {seed}, building {i}: rotated footprint crosses outer ward boundary");

                    if (spatial.InnerWardVertices != null && spatial.InnerWardVertices.Length >= 3)
                    {
                        Assert.IsFalse(
                            CastlePolygonGeometry.PolygonsOverlapOrTouch(
                                spatial.InnerWardVertices, footprint),
                            $"seed {seed}, building {i}: rotated footprint intersects inner ward");
                    }

                    plannedBuildings++;
                }
            }

            Assert.Greater(plannedBuildings, 0,
                "Seed corpus produced no canonical courtyard buildings to validate.");
        }

        [Test]
        public void CanonicalAdapterUsesExactPolygonPredicates()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                dir = dir.Parent;

            Assert.NotNull(dir, "Could not locate project root containing Assets/.");
            string source = File.ReadAllText(Path.Combine(
                dir.FullName,
                "Assets", "VoxelEngine", "Structures", "Api",
                "CastleCourtyardBuildingPlacementGeometry.cs"));

            StringAssert.Contains("CastlePolygonGeometry.ContainsPolygon", source);
            StringAssert.Contains("CastlePolygonGeometry.PolygonsOverlapOrTouch", source);
        }
    }
}
