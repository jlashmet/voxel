using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePolygonGeometryTests
    {
        [Test]
        public void ExactContainmentRejectsConcaveNotchMissedByLegacySamples()
        {
            int2[] ward =
            {
                new int2(-20, -20),
                new int2( 20, -20),
                new int2( 20,  20),
                new int2(  6,  20),
                new int2(  6,   8),
                new int2(  4,   8),
                new int2(  4,  20),
                new int2(-20,  20),
            };
            int2[] building =
            {
                new int2(-10, -10),
                new int2( 10, -10),
                new int2( 10,  10),
                new int2(-10,  10),
            };

            // Reproduce the old planner's four corners, four edge midpoints, and centre. Every
            // sample is valid even though the narrow x=4..6 notch cuts through the top edge.
            int2[] legacySamples =
            {
                int2.zero,
                building[0], building[1], building[2], building[3],
                new int2(0, -10), new int2(10, 0),
                new int2(0, 10), new int2(-10, 0),
            };
            for (int i = 0; i < legacySamples.Length; i++)
            {
                Assert.IsTrue(CastlePolygonGeometry.ContainsPoint(legacySamples[i], ward),
                    $"legacy sample {i} should intentionally miss the notch");
            }

            Assert.IsFalse(CastlePolygonGeometry.ContainsPolygon(ward, building),
                "Exact containment must reject an edge that leaves the concave ward between samples.");
        }

        [Test]
        public void PolygonOverlapDetectsContainmentCrossingAndBoundaryTouch()
        {
            int2[] building =
            {
                new int2(-10, -10), new int2(10, -10),
                new int2(10, 10), new int2(-10, 10),
            };
            int2[] contained =
            {
                new int2(3, 3), new int2(7, 3),
                new int2(7, 7), new int2(3, 7),
            };
            int2[] crossing =
            {
                new int2(8, -14), new int2(12, -14),
                new int2(12, 14), new int2(8, 14),
            };
            int2[] touching =
            {
                new int2(10, -3), new int2(14, -3),
                new int2(14, 3), new int2(10, 3),
            };
            int2[] separate =
            {
                new int2(20, 20), new int2(24, 20),
                new int2(24, 24), new int2(20, 24),
            };

            Assert.IsTrue(CastlePolygonGeometry.PolygonsOverlapOrTouch(building, contained));
            Assert.IsTrue(CastlePolygonGeometry.PolygonsOverlapOrTouch(building, crossing));
            Assert.IsTrue(CastlePolygonGeometry.PolygonsOverlapOrTouch(building, touching));
            Assert.IsFalse(CastlePolygonGeometry.PolygonsOverlapOrTouch(building, separate));
        }
    }
}
