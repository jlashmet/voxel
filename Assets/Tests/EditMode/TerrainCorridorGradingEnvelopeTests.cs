using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class TerrainCorridorGradingEnvelopeTests
    {
        [Test]
        public void PackedCorridor_GradesBeyondSurfaceWithoutExtendingSurfaceCoverage()
        {
            Primitive primitive = Corridor(
                surfaceOuterRadius: 20,
                gradingOuterRadius: 40,
                packed: true);

            Assert.That(TerrainCorridorRasteriser.TrySample(
                in primitive, 40, 18, out TerrainCorridorSample shoulder), Is.True);
            Assert.That(shoulder.SurfaceCoverage31, Is.GreaterThan(0));
            Assert.That(shoulder.Coverage31, Is.EqualTo(31),
                "grading must remain fully formed through the authored surface shoulder");

            Assert.That(TerrainCorridorRasteriser.TrySample(
                in primitive, 40, 24, out TerrainCorridorSample gradeOnly), Is.True);
            Assert.That(gradeOnly.SurfaceCoverage31, Is.EqualTo(0),
                "material/detail coverage must stop at the authored surface envelope");
            Assert.That(gradeOnly.Coverage31, Is.GreaterThan(0),
                "terrain shaping must continue through the independent grading envelope");
            Assert.That(gradeOnly.InCore, Is.False);

            Assert.That(TerrainCorridorRasteriser.TrySample(
                in primitive, 40, 35, out TerrainCorridorSample outerGrade), Is.True);
            Assert.That(outerGrade.SurfaceCoverage31, Is.EqualTo(0));
            Assert.That(outerGrade.Coverage31, Is.InRange(1, 30));
            Assert.That(TerrainCorridorRasteriser.TrySample(
                in primitive, 40, 41, out _), Is.False);
        }

        [Test]
        public void RoadCatalogue_EncodesGradeRadiusWithoutWideningSurfaceRadius()
        {
            var profile = new WorldRoadProfile(
                "grading-fixture",
                "road-surface",
                carriagewayWidthDm: 20,
                transitionWidthDm: 30,
                maximumGradePermille: 200,
                maximumCutFillDm: 20,
                edgeVariationDm: 2);
            var intent = new WorldRoadIntent(
                "fixture-road",
                "fixture-a",
                "fixture-b",
                123u,
                profile,
                "grading-envelope regression fixture",
                new[]
                {
                    new WorldRoadPlanPoint(0, 0),
                    new WorldRoadPlanPoint(80, 0),
                });
            var resolved = new ResolvedWorldRoad(
                intent,
                WorldRoadResolutionStatus.Resolved,
                string.Empty,
                new[]
                {
                    new ResolvedWorldRoadPoint(0, 10, 0),
                    new ResolvedWorldRoadPoint(80, 10, 0),
                });
            var route = new WorldRoadNetworkRoute(
                resolved,
                WorldRoadSemanticClass.Vehicle,
                shoulderWidthDm: 5,
                clearanceWidthDm: 5);
            var network = new WorldRoadNetwork(new[] { route });
            var materials = new VoxelMaterialMap(
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
            var settings = new VoxelWorldGenSettings(1, materials);

            FeatureCatalogue catalogue = WorldRoadNetworkVoxelCatalogue.Build(
                network,
                settings,
                Allocator.Temp);
            try
            {
                Assert.That(catalogue.Definitions.Length, Is.EqualTo(1));
                Assert.That(catalogue.Program[9], Is.EqualTo(44),
                    "terrain-corridor radius must include the wider grade radius plus edge variation");

                int packed = catalogue.Program[16];
                Assert.That(ShapeOps.HasPackedTerrainCorridorSurfaceOuter(packed), Is.True);
                Assert.That(ShapeOps.TerrainCorridorScale(packed), Is.EqualTo(1));
                Assert.That(
                    ShapeOps.TerrainCorridorSurfaceOuterRadius(packed, catalogue.Program[9]),
                    Is.EqualTo(17),
                    "visible material coverage must remain at core + authored shoulder + edge variation");
                Assert.That(catalogue.Definitions[0].Footprint.z, Is.EqualTo(89),
                    "feature bounds must include both sides of the physical grading envelope");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        [Test]
        public void LegacyCorridor_RetainsSingleCoverageEnvelope()
        {
            Primitive primitive = Corridor(
                surfaceOuterRadius: 24,
                gradingOuterRadius: 24,
                packed: false);

            Assert.That(TerrainCorridorRasteriser.TrySample(
                in primitive, 40, 18, out TerrainCorridorSample sample), Is.True);
            Assert.That(sample.Coverage31, Is.InRange(1, 30));
            Assert.That(sample.SurfaceCoverage31, Is.EqualTo(sample.Coverage31),
                "plain-scale bytecode must preserve the original single-envelope contract");
        }

        [Test]
        public void TerrainCorridorPacking_RoundTripsSurfaceRadiusAndScale()
        {
            int packed = ShapeOps.PackTerrainCorridorSurfaceOuterAndScale(37, 3);

            Assert.That(ShapeOps.HasPackedTerrainCorridorSurfaceOuter(packed), Is.True);
            Assert.That(ShapeOps.TerrainCorridorScale(packed), Is.EqualTo(3));
            Assert.That(ShapeOps.TerrainCorridorSurfaceOuterRadius(packed, 99), Is.EqualTo(37));

            Assert.That(ShapeOps.HasPackedTerrainCorridorSurfaceOuter(3), Is.False);
            Assert.That(ShapeOps.TerrainCorridorScale(3), Is.EqualTo(3));
            Assert.That(ShapeOps.TerrainCorridorSurfaceOuterRadius(3, 99), Is.EqualTo(99));
        }

        private static Primitive Corridor(
            int surfaceOuterRadius,
            int gradingOuterRadius,
            bool packed)
        {
            return new Primitive
            {
                Shape = PrimitiveShape.TerrainCorridor,
                Mode = PrimitiveMode.TerrainCorridor,
                Material = 7,
                A = new int3(0, 10, 0),
                B = new int3(80, 10, 0),
                InnerRadius = 12,
                Radius = gradingOuterRadius,
                C = new int3(12, 4, 8),
                D = new int3(
                    0,
                    12345,
                    packed
                        ? ShapeOps.PackTerrainCorridorSurfaceOuterAndScale(
                            surfaceOuterRadius, 1)
                        : 1),
            };
        }
    }
}
