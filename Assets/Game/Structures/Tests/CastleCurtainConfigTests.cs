using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleCurtainConfigTests
    {
        [Test]
        public void CompatibilityPresetPreservesRectangularCurtainDimensions()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(80, 20, -120), 0x1234u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            CastleCurtainConfig curtain = CastleCurtainPresets.Compatibility(in components);

            StructureWallRunConfig wallX = curtain.RectangularWallX();
            StructureWallRunConfig wallZ = curtain.RectangularWallZ();

            Assert.Multiple(() =>
            {
                Assert.IsTrue(curtain.IsWellFormed);
                Assert.AreEqual(CastleCurtainLayoutKind.Rectangular, curtain.Layout);
                Assert.AreEqual(plan.BaileyHalfX, curtain.RectangularHalfExtents.x);
                Assert.AreEqual(plan.BaileyHalfZ, curtain.RectangularHalfExtents.y);
                Assert.AreEqual(components.CurtainWallX.Length, wallX.Length);
                Assert.AreEqual(components.CurtainWallZ.Length, wallZ.Length);
                Assert.AreEqual(plan.WallHeight, curtain.Height);
                Assert.AreEqual(plan.WallThickness, curtain.Thickness);
                Assert.AreEqual(components.CurtainBattlements.MerlonWidth, curtain.Battlements.MerlonWidth);
                Assert.AreEqual(math.max(wallX.Length, wallZ.Length), curtain.MaximumSegmentLength);
            });
        }

        [Test]
        public void PolygonCurtainRequiresBoundedOrthogonalSegments()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x99u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            CastleCurtainConfig curtain = CastleCurtainPresets.Compatibility(in components);
            curtain.Layout = CastleCurtainLayoutKind.Polygon;
            curtain.PolygonVertices.Clear();

            curtain.PolygonVertices.Add(new int2(-80, -60));
            curtain.PolygonVertices.Add(new int2(90, -60));
            curtain.PolygonVertices.Add(new int2(90, 85));
            Assert.IsFalse(curtain.IsWellFormed, "A polygon loop needs at least four bounded vertices.");

            curtain.PolygonVertices.Add(new int2(-80, 85));
            Assert.IsTrue(curtain.IsWellFormed);

            curtain.PolygonVertices[2] = new int2(70, 85);
            Assert.IsFalse(curtain.IsWellFormed, "Diagonal wall edges are not supported by axis-aligned wall runs.");

            curtain.PolygonVertices[2] = curtain.PolygonVertices[1];
            Assert.IsFalse(curtain.IsWellFormed, "Zero-length wall edges must be rejected.");
        }

        [Test]
        public void WallSegmentationAndBattlementsAreIndependentlyOverrideable()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x55u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            CastleCurtainConfig curtain = CastleCurtainPresets.Compatibility(in components);

            curtain.RectangularHalfExtents = new int2(420, 360);
            curtain.Wall.Height = 96;
            curtain.Wall.Thickness = 12;
            curtain.MaximumSegmentLength = 128;
            curtain.Battlements.MerlonWidth = 22;
            curtain.Battlements.GapWidth = 14;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(curtain.IsWellFormed);
                Assert.AreEqual(840, curtain.RectangularWallX().Length);
                Assert.AreEqual(720, curtain.RectangularWallZ().Length);
                Assert.AreEqual(96, curtain.Height);
                Assert.AreEqual(12, curtain.Thickness);
                Assert.AreEqual(128, curtain.MaximumSegmentLength);
                Assert.AreEqual(22, curtain.Battlements.MerlonWidth);
                Assert.AreEqual(14, curtain.Battlements.GapWidth);
            });
        }
    }
}
