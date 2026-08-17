using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class CastleCurtainConfigTests
    {
        [Test]
        public void RectangleLayoutComposesWithCompatibilityWallDimensions()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(80, 20, -120), 0x1234u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            var layout = new CastleCurtainLayoutConfig
            {
                Kind = CastleCurtainLayoutKind.Rectangle,
                SegmentLength = 0,
            };

            Assert.Multiple(() =>
            {
                Assert.IsTrue(layout.IsWellFormed);
                Assert.IsTrue(components.CurtainWallX.IsWellFormed);
                Assert.IsTrue(components.CurtainWallZ.IsWellFormed);
                Assert.AreEqual(plan.BaileyHalfX * 2, components.CurtainWallX.Length);
                Assert.AreEqual(plan.BaileyHalfZ * 2, components.CurtainWallZ.Length);
                Assert.AreEqual(plan.WallHeight, components.CurtainWallX.Height);
                Assert.AreEqual(plan.WallThickness, components.CurtainWallX.Thickness);
                Assert.IsTrue(components.CurtainBattlements.IsWellFormed);
            });
        }

        [Test]
        public void RectilinearPolygonRequiresBoundedOrthogonalPerimeter()
        {
            var layout = new CastleCurtainLayoutConfig
            {
                Kind = CastleCurtainLayoutKind.RectilinearPolygon,
                SegmentLength = 96,
            };

            layout.PolygonVertices.Add(new int2(-80, -60));
            layout.PolygonVertices.Add(new int2(90, -60));
            layout.PolygonVertices.Add(new int2(90, 85));
            Assert.IsFalse(layout.IsWellFormed);

            layout.PolygonVertices.Add(new int2(-80, 85));
            Assert.IsTrue(layout.IsWellFormed);

            layout.PolygonVertices[2] = new int2(70, 85);
            Assert.IsFalse(layout.IsWellFormed);
        }

        [Test]
        public void SegmentationWallDimensionsAndBattlementsAreIndependentlyOverrideable()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x55u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            var layout = new CastleCurtainLayoutConfig
            {
                Kind = CastleCurtainLayoutKind.Rectangle,
                SegmentLength = 128,
            };

            components.CurtainWallX.Length = 840;
            components.CurtainWallZ.Length = 720;
            components.CurtainWallX.Height = 96;
            components.CurtainWallZ.Height = 96;
            components.CurtainWallX.Thickness = 12;
            components.CurtainWallZ.Thickness = 12;
            components.CurtainBattlements.MerlonWidth = 22;
            components.CurtainBattlements.GapWidth = 14;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(layout.IsWellFormed);
                Assert.AreEqual(128, layout.SegmentLength);
                Assert.AreEqual(840, components.CurtainWallX.Length);
                Assert.AreEqual(720, components.CurtainWallZ.Length);
                Assert.AreEqual(96, components.CurtainWallX.Height);
                Assert.AreEqual(12, components.CurtainWallZ.Thickness);
                Assert.AreEqual(22, components.CurtainBattlements.MerlonWidth);
                Assert.AreEqual(14, components.CurtainBattlements.GapWidth);
            });
        }
    }
}
