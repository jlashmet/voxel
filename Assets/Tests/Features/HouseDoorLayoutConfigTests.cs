using NUnit.Framework;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features
{
    public sealed class HouseDoorLayoutConfigTests
    {
        [Test]
        public void CottagePresetExposesIndependentDoorCountsByFacade()
        {
            HouseConfig house = HousePresets.CottageCompatibility(stoneMaterial: 1, woodMaterial: 2);

            Assert.AreEqual(HouseFacade.Front, house.FrontDoors.Facade);
            Assert.AreEqual(HouseFacadePlacementMode.Centered, house.FrontDoors.Placement);
            Assert.AreEqual(1, house.FrontDoors.Count);
            Assert.AreEqual(StructureOpeningKind.Door, house.FrontDoors.Opening.Kind);
            Assert.AreEqual(12, house.FrontDoors.Opening.Width);
            Assert.AreEqual(20, house.FrontDoors.Opening.Height);

            Assert.AreEqual(HouseFacade.Rear, house.RearDoors.Facade);
            Assert.AreEqual(0, house.RearDoors.Count);
            Assert.AreEqual(HouseFacade.Left, house.LeftDoors.Facade);
            Assert.AreEqual(0, house.LeftDoors.Count);
            Assert.AreEqual(HouseFacade.Right, house.RightDoors.Facade);
            Assert.AreEqual(0, house.RightDoors.Count);
        }

        [Test]
        public void ExplicitDoorLayoutSupportsFramesAndStepTreatment()
        {
            var layout = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Rear,
                Placement = HouseFacadePlacementMode.ExplicitOffsets,
                Count = 2,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 10,
                    Height = 22,
                    BottomOffset = 1,
                    FrameThickness = 2,
                    LintelThickness = 3,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                StepsEnabled = true,
                StepDepth = 4,
                StepHeight = 2,
                StepMaterialRole = StructureMaterialRole.Foundation,
            };
            layout.ExplicitOffsets.Add(-14);
            layout.ExplicitOffsets.Add(14);

            Assert.IsTrue(layout.IsWellFormed);
            Assert.AreEqual(2, layout.Opening.FrameThickness);
            Assert.AreEqual(3, layout.Opening.LintelThickness);
            Assert.IsTrue(layout.StepsEnabled);
            Assert.AreEqual(4, layout.StepDepth);
            Assert.AreEqual(2, layout.StepHeight);
            Assert.AreEqual(2, layout.ExplicitOffsets.Length);
        }

        [Test]
        public void ExplicitDoorLayoutRejectsOffsetCountMismatch()
        {
            var layout = new HouseDoorLayoutConfig
            {
                Facade = HouseFacade.Left,
                Placement = HouseFacadePlacementMode.ExplicitOffsets,
                Count = 2,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = 8,
                    Height = 20,
                },
            };
            layout.ExplicitOffsets.Add(0);

            Assert.IsFalse(layout.IsWellFormed);
        }
    }
}
