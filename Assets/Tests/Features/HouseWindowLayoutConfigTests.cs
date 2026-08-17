using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseWindowLayoutConfigTests
    {
        [Test]
        public void HouseConfigExposesIndependentWindowLayoutsForEveryFacade()
        {
            HouseConfig house = HousePresets.CottageCompatibility(stoneMaterial: 1, woodMaterial: 2);

            Assert.AreEqual(HouseFacade.Front, house.FrontWindows.Facade);
            Assert.AreEqual(HouseFacade.Rear, house.RearWindows.Facade);
            Assert.AreEqual(HouseFacade.Left, house.LeftWindows.Facade);
            Assert.AreEqual(HouseFacade.Right, house.RightWindows.Facade);
        }

        [Test]
        public void WindowLayoutExposesSpacingSillHeadFrameAndDeterministicVariationRanges()
        {
            var layout = new HouseWindowLayoutConfig
            {
                Facade = HouseFacade.Front,
                Placement = HouseFacadePlacementMode.EvenlySpaced,
                Count = 4,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 8,
                    Height = 12,
                    BottomOffset = 10,
                    Spacing = 14,
                    StartMargin = 6,
                    EndMargin = 6,
                    FrameThickness = 2,
                    LintelThickness = 1,
                    WidthVariation = 2,
                    HeightVariation = 3,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
                ShuttersEnabled = true,
                ShutterThickness = 1,
                ShutterMaterialRole = StructureMaterialRole.SecondaryWall,
            };

            Assert.IsTrue(layout.IsWellFormed);
            Assert.AreEqual(HouseFacadePlacementMode.EvenlySpaced, layout.Placement);
            Assert.AreEqual(4, layout.Count);
            Assert.AreEqual(14, layout.Opening.Spacing);
            Assert.AreEqual(10, layout.Opening.BottomOffset, "bottom offset is the window sill height");
            Assert.AreEqual(22, layout.Opening.BottomOffset + layout.Opening.Height, "sill plus height is the window head height");
            Assert.AreEqual(2, layout.Opening.FrameThickness);
            Assert.AreEqual(1, layout.Opening.LintelThickness);
            Assert.AreEqual(2, layout.Opening.WidthVariation);
            Assert.AreEqual(3, layout.Opening.HeightVariation);
            Assert.AreEqual(StructureMaterialRole.Glass, layout.Opening.FillMaterialRole);
            Assert.IsTrue(layout.ShuttersEnabled);
        }

        [Test]
        public void ExplicitWindowLayoutRequiresOneOffsetPerWindow()
        {
            var layout = new HouseWindowLayoutConfig
            {
                Facade = HouseFacade.Right,
                Placement = HouseFacadePlacementMode.ExplicitOffsets,
                Count = 2,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 6,
                    Height = 10,
                },
            };
            layout.ExplicitOffsets.Add(0);

            Assert.IsFalse(layout.IsWellFormed);

            layout.ExplicitOffsets.Add(12);
            Assert.IsTrue(layout.IsWellFormed);
        }
    }
}
